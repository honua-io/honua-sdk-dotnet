// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Abstractions.Http;
using Honua.Sdk.Geometry.Vector;
using Honua.Sdk.OgcFeatures.Wfs.Exceptions;
using Honua.Sdk.OgcFeatures.Wfs.Formats;
using Honua.Sdk.OgcFeatures.Wfs.Models;
using Honua.Sdk.OgcFeatures.Wfs.Parsing;

namespace Honua.Sdk.OgcFeatures.Wfs;

/// <summary>
/// WFS 2.0 read/query client for Honua Server.
/// </summary>
public sealed class HonuaWfsClient :
    IHonuaWfsClient,
    IHonuaFeatureQueryClient,
    IHonuaFeatureEditClient,
    IHonuaFeatureDescriptorClient,
    IHonuaFeatureAttachmentClient
{
    private static readonly ActivitySource ActivitySource = new("Honua.Sdk.OgcFeatures.Wfs");
    private static readonly GeoJsonFeatureCollectionHandler DefaultGeoJsonHandler = new();
    private const string UnsupportedEditReason = "Honua.Sdk.OgcFeatures.Wfs does not currently implement WFS-T transactions.";
    private const string UnsupportedAttachmentReason = "WFS does not expose attachment operations.";
    private static readonly FeatureEditCapabilities UnsupportedEditCapabilities = new()
    {
        NativeSurface = "WFS-T Transaction",
        UnsupportedReason = UnsupportedEditReason
    };
    private static readonly FeatureAttachmentCapabilities UnsupportedAttachmentCapabilities = new()
    {
        NativeSurface = "WFS attachments",
        UnsupportedReason = UnsupportedAttachmentReason
    };
    private static readonly FeatureQueryCapabilities WfsQueryCapabilities = new()
    {
        SupportsTimeFilter = false,
        SupportsStatistics = false,
        SupportsGroupBy = false,
        SupportsHaving = false,
        NativeSurface = "WFS GetFeature",
        UnsupportedReason = "WFS 2.0 GetFeature does not expose time-filter, statistics, group-by, or having facets.",
    };

    private static readonly JsonSerializerOptions FeatureJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const int MaxAutoPages = 100;

    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaWfsClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth.</param>
    public HonuaWfsClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public string ProviderName => "wfs";

    /// <inheritdoc />
    public FeatureEditCapabilities EditCapabilities => UnsupportedEditCapabilities;

    /// <inheritdoc />
    public FeatureAttachmentCapabilities AttachmentCapabilities => UnsupportedAttachmentCapabilities;

    /// <inheritdoc />
    public FeatureQueryCapabilities QueryCapabilities => WfsQueryCapabilities;

    /// <inheritdoc />
    public async Task<WfsCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("WFS GetCapabilities");
        activity?.SetTag("wfs.operation", "GetCapabilities");

        var url = BuildWfsUrl("GetCapabilities");
        using var response = await _httpClient.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return WfsCapabilitiesParser.Parse(body);
    }

    /// <inheritdoc />
    public async Task<WfsFeatureTypeSchema> DescribeFeatureTypeAsync(string typeName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        using var activity = ActivitySource.StartActivity("WFS DescribeFeatureType");
        activity?.SetTag("wfs.operation", "DescribeFeatureType");
        activity?.SetTag("wfs.type_name", typeName);

        var url = BuildWfsUrl("DescribeFeatureType", ("TYPENAMES", typeName));
        using var response = await _httpClient.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return WfsDescribeFeatureTypeParser.Parse(body);
    }

    /// <inheritdoc />
    public async Task<SourceDescriptor> GetDescriptorAsync(SourceDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.Locator.TypeName))
        {
            throw new ArgumentException(
                "WFS descriptor discovery requires SourceDescriptor.Locator.TypeName.",
                nameof(descriptor));
        }

        var capabilities = await GetCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        var featureType = capabilities.FeatureTypes.FirstOrDefault(
            candidate => string.Equals(candidate.Name, descriptor.Locator.TypeName, StringComparison.Ordinal));
        var schema = await DescribeFeatureTypeAsync(descriptor.Locator.TypeName, cancellationToken).ConfigureAwait(false);

        return descriptor with
        {
            Capabilities = BuildDiscoveredCapabilities(),
            Schema = BuildSourceSchema(featureType, schema),
        };
    }

    /// <inheritdoc />
    public async Task<WfsFeatureCollection> GetFeaturesAsync(GetFeaturesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("WFS GetFeature");
        activity?.SetTag("wfs.operation", "GetFeature");
        activity?.SetTag("wfs.type_name", request.TypeNames);
        activity?.SetTag("wfs.output_format", "application/geo+json");

        var url = BuildGetFeatureUrl(request, DefaultGeoJsonHandler.MediaType);
        using var response = await _httpClient.GetAsync(CreateRequestUri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        await EnsureGetFeatureSuccessAsync(response, DefaultGeoJsonHandler.MediaType, cancellationToken).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await DefaultGeoJsonHandler.ReadAsync(stream, cancellationToken).ConfigureAwait(false);

        activity?.SetTag("wfs.number_returned", result.NumberReturned);
        return result;
    }

    /// <inheritdoc />
    public async Task<FeatureQueryResult> QueryAsync(
        FeatureQueryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await GetFeaturesAsync(BuildWfsQuery(request), cancellationToken).ConfigureAwait(false);
        return ToFeatureQueryResult(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
        FeatureQueryRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = request;
        var startIndex = request.Offset ?? 0;
        var pageCount = 0;

        while (pageCount < MaxAutoPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var page = await GetFeaturesAsync(BuildWfsQuery(current), cancellationToken).ConfigureAwait(false);
            yield return ToFeatureQueryResult(page);

            // Advance by the ACTUAL number of features in the page, not the server-reported
            // numberReturned (which may be 0 or larger than the body, skipping or dropping records).
            var returnedCount = page.Features.Count;
            if (returnedCount == 0)
            {
                yield break;
            }

            startIndex += returnedCount;
            if (page.NumberMatched.HasValue && startIndex >= page.NumberMatched.Value)
            {
                yield break;
            }

            pageCount++;
            current = request with { Offset = startIndex };
        }

        throw new InvalidOperationException(
            $"Auto-pagination safety limit reached ({MaxAutoPages} pages). " +
            "Use protocol-specific manual paging for larger result sets.");
    }

    /// <inheritdoc />
    public Task<FeatureEditResponse> ApplyEditsAsync(FeatureEditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(UnsupportedEditReason);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FeatureAttachmentInfo>> ListAttachmentsAsync(
        FeatureAttachmentListRequest request,
        CancellationToken cancellationToken = default)
        => ThrowUnsupportedAttachmentAsync<IReadOnlyList<FeatureAttachmentInfo>>(request, cancellationToken);

    /// <inheritdoc />
    public Task<FeatureAttachmentContent> DownloadAttachmentAsync(
        FeatureAttachmentDownloadRequest request,
        CancellationToken cancellationToken = default)
        => ThrowUnsupportedAttachmentAsync<FeatureAttachmentContent>(request, cancellationToken);

    /// <inheritdoc />
    public Task<FeatureAttachmentResult> AddAttachmentAsync(
        FeatureAttachmentAddRequest request,
        CancellationToken cancellationToken = default)
        => ThrowUnsupportedAttachmentAsync<FeatureAttachmentResult>(request, cancellationToken);

    /// <inheritdoc />
    public Task<FeatureAttachmentResult> UpdateAttachmentAsync(
        FeatureAttachmentUpdateRequest request,
        CancellationToken cancellationToken = default)
        => ThrowUnsupportedAttachmentAsync<FeatureAttachmentResult>(request, cancellationToken);

    /// <inheritdoc />
    public Task<FeatureAttachmentResult> DeleteAttachmentAsync(
        FeatureAttachmentDeleteRequest request,
        CancellationToken cancellationToken = default)
        => ThrowUnsupportedAttachmentAsync<FeatureAttachmentResult>(request, cancellationToken);

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Response ownership is transferred to ResponseOwningStream when the handler owns the response stream.")]
    public async Task<TResult> GetFeaturesAsync<TResult>(
        GetFeaturesRequest request,
        IWfsOutputFormatHandler<TResult> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handler);

        using var activity = ActivitySource.StartActivity("WFS GetFeature");
        activity?.SetTag("wfs.operation", "GetFeature");
        activity?.SetTag("wfs.type_name", request.TypeNames);
        activity?.SetTag("wfs.output_format", handler.MediaType);

        var url = BuildGetFeatureUrl(request, handler.MediaType);
        var response = await _httpClient.GetAsync(CreateRequestUri(url), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureGetFeatureSuccessAsync(response, handler.MediaType, cancellationToken).ConfigureAwait(false);

            Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            if (handler.OwnsResponseStream)
            {
                // Wrap so disposing the returned stream also disposes the HTTP response,
                // which is required when using ResponseHeadersRead.
                stream = new ResponseOwningStream(stream, response);
            }

            var result = await handler.ReadAsync(stream, cancellationToken).ConfigureAwait(false);

            if (!handler.OwnsResponseStream)
            {
                response.Dispose();
            }

            return result;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public Task<VectorPayloadFeatureSet> GetFeaturesVectorAsync(
        GetFeaturesRequest request,
        VectorPayloadFormat format = VectorPayloadFormat.GeoJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return GetFeaturesAsync(request, new VectorPayloadFeatureSetHandler(format), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long?> GetFeatureCountAsync(string typeName, string? filter = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        using var activity = ActivitySource.StartActivity("WFS GetFeature (hits)");
        activity?.SetTag("wfs.operation", "GetFeature");
        activity?.SetTag("wfs.type_name", typeName);

        // Server returns XML wfs:FeatureCollection for RESULTTYPE=hits regardless of OUTPUTFORMAT.
        var parameters = new List<(string, string)>
        {
            ("TYPENAMES", typeName),
            ("RESULTTYPE", "hits"),
        };

        if (!string.IsNullOrEmpty(filter))
        {
            parameters.Add(("FILTER", filter));
        }

        var url = BuildWfsUrl("GetFeature", parameters.ToArray());
        using var response = await _httpClient.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return ParseHitsNumberMatched(body);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WfsFeature> GetFeaturesAsyncEnumerable(
        GetFeaturesRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("WFS GetFeaturesAsyncEnumerable");
        activity?.SetTag("wfs.operation", "GetFeature");
        activity?.SetTag("wfs.type_name", request.TypeNames);

        var startIndex = request.StartIndex ?? 0;
        var pageCount = 0;

        while (pageCount < MaxAutoPages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageRequest = new GetFeaturesRequest
            {
                TypeNames = request.TypeNames,
                Count = request.Count,
                StartIndex = startIndex,
                SortBy = request.SortBy,
                Filter = request.Filter,
                Bbox = request.Bbox,
                ResourceId = request.ResourceId,
                PropertyName = request.PropertyName,
                SrsName = request.SrsName,
            };

            var page = await GetFeaturesAsync(pageRequest, cancellationToken).ConfigureAwait(false);

            foreach (var feature in page.Features)
            {
                yield return feature;
            }

            // Advance by the ACTUAL number of features in the page, not the server-reported
            // numberReturned (which may be 0 or larger than the body, skipping or dropping records).
            var returnedCount = page.Features.Count;
            if (returnedCount == 0)
            {
                yield break;
            }

            startIndex += returnedCount;

            if (page.NumberMatched.HasValue && startIndex >= page.NumberMatched.Value)
            {
                yield break;
            }

            pageCount++;
        }

        throw new InvalidOperationException(
            $"Auto-pagination safety limit reached ({MaxAutoPages} pages). " +
            "Use manual paging with GetFeaturesAsync for larger result sets.");
    }

    // ── URL building ─────────────────────────────────────────────────────

    private static string BuildWfsUrl(string requestType, params (string Key, string Value)[] extra)
    {
        var sb = new StringBuilder("/wfs?SERVICE=WFS&VERSION=2.0.0&REQUEST=");
        sb.Append(Uri.EscapeDataString(requestType));

        foreach (var (key, value) in extra)
        {
            sb.Append('&');
            sb.Append(Uri.EscapeDataString(key));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value));
        }

        return sb.ToString();
    }

    private static string BuildGetFeatureUrl(GetFeaturesRequest request, string mediaType)
    {
        var parameters = new List<(string, string)>
        {
            ("TYPENAMES", request.TypeNames),
            ("OUTPUTFORMAT", mediaType),
        };

        if (request.Count.HasValue)
            parameters.Add(("COUNT", request.Count.Value.ToString(CultureInfo.InvariantCulture)));

        if (request.StartIndex.HasValue)
            parameters.Add(("STARTINDEX", request.StartIndex.Value.ToString(CultureInfo.InvariantCulture)));

        if (!string.IsNullOrEmpty(request.SortBy))
            parameters.Add(("SORTBY", request.SortBy));

        if (!string.IsNullOrEmpty(request.Filter))
            parameters.Add(("FILTER", request.Filter));

        if (request.Bbox is not null)
            parameters.Add(("BBOX", request.Bbox.ToQueryValue()));

        if (!string.IsNullOrEmpty(request.ResourceId))
            parameters.Add(("RESOURCEID", request.ResourceId));

        if (!string.IsNullOrEmpty(request.PropertyName))
            parameters.Add(("PROPERTYNAME", request.PropertyName));

        if (!string.IsNullOrEmpty(request.SrsName))
            parameters.Add(("SRSNAME", request.SrsName));

        return BuildWfsUrl("GetFeature", parameters.ToArray());
    }

    // ── Response validation ──────────────────────────────────────────────

    private static Task EnsureSuccessAsync(HttpResponseMessage response, string body)
    {
        // Check for OGC ExceptionReport even on 200 responses
        var exceptionReport = WfsExceptionParser.TryParse(body);
        if (exceptionReport is not null)
        {
            throw new HonuaWfsException(
                response.StatusCode,
                exceptionReport.ExceptionText ?? $"WFS exception: {exceptionReport.ExceptionCode}",
                body,
                exceptionReport.ExceptionCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HonuaWfsException(
                response.StatusCode,
                $"WFS request failed with status {(int)response.StatusCode}.",
                body);
        }

        return Task.CompletedTask;
    }

    private static async Task EnsureGetFeatureSuccessAsync(
        HttpResponseMessage response, string requestedMediaType, CancellationToken cancellationToken)
    {
        // WFS servers may return XML ExceptionReport even when GeoJSON was requested.
        // Check Content-Type to detect this. When the handler explicitly requested an
        // XML format (e.g. GML), skip the XML probe on success to avoid materializing
        // the entire response body as a string just to check for ExceptionReport.
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        bool requestedXml = requestedMediaType.Contains("xml", StringComparison.OrdinalIgnoreCase);
        bool unexpectedXml = !requestedXml &&
            contentType.Contains("xml", StringComparison.OrdinalIgnoreCase);

        if (!response.IsSuccessStatusCode || unexpectedXml)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            var exceptionReport = WfsExceptionParser.TryParse(body);
            if (exceptionReport is not null)
            {
                throw new HonuaWfsException(
                    response.StatusCode,
                    exceptionReport.ExceptionText ?? $"WFS exception: {exceptionReport.ExceptionCode}",
                    body,
                    exceptionReport.ExceptionCode);
            }

            if (unexpectedXml)
            {
                throw new HonuaWfsException(
                    response.StatusCode,
                    $"Expected content type '{requestedMediaType}' but received '{contentType}'.",
                    body);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new HonuaWfsException(
                    response.StatusCode,
                    $"WFS request failed with status {(int)response.StatusCode}.",
                    body);
            }
        }
    }

    // ── Hits response parsing ────────────────────────────────────────────

    private static long? ParseHitsNumberMatched(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root;
        if (root is null) return null;

        var attr = root.Attribute("numberMatched")?.Value;
        if (attr is null || string.Equals(attr, "unknown", StringComparison.OrdinalIgnoreCase))
            return null;

        return long.TryParse(attr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : null;
    }

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);

    private static List<string> BuildDiscoveredCapabilities()
    {
        var capabilities = new HashSet<string>(
            FeatureProtocolCapabilities.DefaultsFor(FeatureProtocolIds.Wfs),
            StringComparer.Ordinal);
        capabilities.Remove(FeatureCapabilities.ApplyEdits);

        return FeatureCapabilities.All
            .Where(capabilities.Contains)
            .ToList();
    }

    private static SourceSchema BuildSourceSchema(WfsFeatureType? featureType, WfsFeatureTypeSchema schema)
    {
        var fields = schema.Properties.Select(ToSourceField).ToList();
        var geometryType = schema.Properties
            .Select(property => ToSourceGeometryType(property.Type))
            .FirstOrDefault(type => type != FeatureSpatialGeometryType.Unspecified);
        var primaryKey = fields.FirstOrDefault(
            field => string.Equals(field.Name, "id", StringComparison.OrdinalIgnoreCase))?.Name;

        return new SourceSchema
        {
            Fields = fields,
            PrimaryKey = primaryKey,
            GeometryType = geometryType,
            Extent = ToFeatureBoundingBox(featureType),
            SpatialReference = featureType?.DefaultCrs,
            EditCapabilities = UnsupportedEditCapabilities,
            AttachmentCapabilities = UnsupportedAttachmentCapabilities,
        };
    }

    private static Task<TResult> ThrowUnsupportedAttachmentAsync<TResult>(object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(UnsupportedAttachmentReason);
    }

    private static SourceField ToSourceField(WfsSchemaProperty property)
        => new()
        {
            Name = property.Name,
            Type = property.Type,
            Nullable = property.Nillable || property.MinOccurs == 0,
            Required = property.MinOccurs > 0 && !property.Nillable,
        };

    private static FeatureSpatialGeometryType ToSourceGeometryType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type) ||
            !type.Contains("gml:", StringComparison.OrdinalIgnoreCase))
        {
            return FeatureSpatialGeometryType.Unspecified;
        }

        if (type.Contains("Point", StringComparison.OrdinalIgnoreCase))
        {
            return type.Contains("Multi", StringComparison.OrdinalIgnoreCase)
                ? FeatureSpatialGeometryType.MultiPoint
                : FeatureSpatialGeometryType.Point;
        }

        if (type.Contains("Line", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Curve", StringComparison.OrdinalIgnoreCase))
        {
            return FeatureSpatialGeometryType.Polyline;
        }

        if (type.Contains("Polygon", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("Surface", StringComparison.OrdinalIgnoreCase))
        {
            return FeatureSpatialGeometryType.Polygon;
        }

        if (type.Contains("Envelope", StringComparison.OrdinalIgnoreCase))
        {
            return FeatureSpatialGeometryType.Envelope;
        }

        return FeatureSpatialGeometryType.Unspecified;
    }

    private static FeatureBoundingBox? ToFeatureBoundingBox(WfsFeatureType? featureType)
    {
        if (featureType?.LowerCorner is not { } lower || featureType.UpperCorner is not { } upper)
        {
            return null;
        }

        return new FeatureBoundingBox
        {
            MinX = lower.X,
            MinY = lower.Y,
            MaxX = upper.X,
            MaxY = upper.Y,
            Crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
        };
    }

    private static GetFeaturesRequest BuildWfsQuery(FeatureQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureSupportedFilterLanguage(request.FilterLanguage);
        EnsureSupportedSharedQueryModes(request);

        if (string.IsNullOrWhiteSpace(request.Source.TypeName))
        {
            throw new ArgumentException("A WFS type name is required for WFS feature queries.", nameof(request));
        }

        return new GetFeaturesRequest
        {
            TypeNames = request.Source.TypeName,
            Count = request.Limit,
            StartIndex = request.Offset,
            SortBy = request.OrderBy,
            Filter = request.Filter,
            Bbox = request.Bbox is null
                ? null
                : new WfsBoundingBox
                {
                    MinX = request.Bbox.MinX,
                    MinY = request.Bbox.MinY,
                    MaxX = request.Bbox.MaxX,
                    MaxY = request.Bbox.MaxY,
                    Crs = request.Bbox.Crs
                },
            ResourceId = BuildResourceId(request),
            PropertyName = request.OutFields is { Count: > 0 } ? string.Join(",", request.OutFields) : null,
            SrsName = request.OutputCrs,
        };
    }

    private static void EnsureSupportedFilterLanguage(FeatureFilterLanguage language)
    {
        if (language is not FeatureFilterLanguage.ProviderDefault and not FeatureFilterLanguage.FesXml)
        {
            throw new NotSupportedException("WFS feature queries support provider-default or FES XML filters.");
        }
    }

    private static void EnsureSupportedSharedQueryModes(FeatureQueryRequest request)
    {
        if (request.ReturnDistinct is true ||
            request.ReturnCountOnly is true ||
            request.ReturnIdsOnly is true ||
            request.ReturnExtentOnly is true)
        {
            throw new NotSupportedException(
                "WFS shared queries do not support distinct, count-only, IDs-only, or extent-only modes yet.");
        }

        if (request.TimeFilter is not null ||
            request.OutStatistics is { Count: > 0 } ||
            request.GroupBy is { Count: > 0 } ||
            !string.IsNullOrWhiteSpace(request.Having))
        {
            throw new NotSupportedException(
                "WFS shared queries do not support provider-neutral time filters, statistics, group-by, or having clauses yet.");
        }

        if (request.SpatialFilter is not null)
        {
            throw new NotSupportedException(
                "WFS shared queries do not support explicit geometry spatial filters yet. Use Bbox for envelope filters.");
        }
    }

    private static string? BuildResourceId(FeatureQueryRequest request)
    {
        if (request.FeatureIds is { Count: > 0 })
        {
            return string.Join(",", request.FeatureIds);
        }

        return request.ObjectIds is { Count: > 0 }
            ? string.Join(",", request.ObjectIds.Select(id => id.ToString(CultureInfo.InvariantCulture)))
            : null;
    }

    private FeatureQueryResult ToFeatureQueryResult(WfsFeatureCollection response)
    {
        return new FeatureQueryResult
        {
            ProviderName = ProviderName,
            Features = response.Features.Select(ToFeatureRecord).ToList(),
            NumberMatched = response.NumberMatched,
            NumberReturned = response.NumberReturned,
            HasMoreResults = response.HasMoreResults,
        };
    }

    private static FeatureRecord ToFeatureRecord(WfsFeature feature)
    {
        JsonElement? geometry = feature.Geometry is not null
            ? JsonSerializer.SerializeToElement(feature.Geometry, FeatureJsonOptions)
            : null;

        return new FeatureRecord
        {
            Id = feature.Id,
            Attributes = feature.Properties.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Clone()),
            Geometry = geometry,
        };
    }
}
