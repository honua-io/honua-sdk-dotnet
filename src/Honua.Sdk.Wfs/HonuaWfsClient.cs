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
using Honua.Sdk.Wfs.Exceptions;
using Honua.Sdk.Wfs.Formats;
using Honua.Sdk.Wfs.Models;
using Honua.Sdk.Wfs.Parsing;

namespace Honua.Sdk.Wfs;

/// <summary>
/// WFS 2.0 read/query client for Honua Server.
/// </summary>
public sealed class HonuaWfsClient : IHonuaWfsClient, IHonuaFeatureQueryClient, IHonuaFeatureEditClient
{
    private static readonly ActivitySource ActivitySource = new("Honua.Sdk.Wfs");
    private static readonly GeoJsonFeatureCollectionHandler DefaultGeoJsonHandler = new();
    private const string UnsupportedEditReason = "Honua.Sdk.Wfs does not currently implement WFS-T transactions.";
    private static readonly FeatureEditCapabilities UnsupportedEditCapabilities = new()
    {
        NativeSurface = "WFS-T Transaction",
        UnsupportedReason = UnsupportedEditReason
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
    public async Task<WfsCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        using var activity = ActivitySource.StartActivity("WFS GetCapabilities");
        activity?.SetTag("wfs.operation", "GetCapabilities");

        var url = BuildWfsUrl("GetCapabilities");
        using var response = await _httpClient.GetAsync(CreateRequestUri(url), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return WfsCapabilitiesParser.Parse(body);
    }

    /// <inheritdoc />
    public async Task<WfsFeatureTypeSchema> DescribeFeatureTypeAsync(string typeName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        using var activity = ActivitySource.StartActivity("WFS DescribeFeatureType");
        activity?.SetTag("wfs.operation", "DescribeFeatureType");
        activity?.SetTag("wfs.type_name", typeName);

        var url = BuildWfsUrl("DescribeFeatureType", ("TYPENAMES", typeName));
        using var response = await _httpClient.GetAsync(CreateRequestUri(url), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return WfsDescribeFeatureTypeParser.Parse(body);
    }

    /// <inheritdoc />
    public async Task<WfsFeatureCollection> GetFeaturesAsync(GetFeaturesRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("WFS GetFeature");
        activity?.SetTag("wfs.operation", "GetFeature");
        activity?.SetTag("wfs.type_name", request.TypeNames);
        activity?.SetTag("wfs.output_format", "application/geo+json");

        var url = BuildGetFeatureUrl(request, DefaultGeoJsonHandler.MediaType);
        using var response = await _httpClient.GetAsync(CreateRequestUri(url), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        await EnsureGetFeatureSuccessAsync(response, DefaultGeoJsonHandler.MediaType, ct).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var result = await DefaultGeoJsonHandler.ReadAsync(stream, ct).ConfigureAwait(false);

        activity?.SetTag("wfs.number_returned", result.NumberReturned);
        return result;
    }

    /// <inheritdoc />
    public async Task<FeatureQueryResult> QueryAsync(
        FeatureQueryRequest request, CancellationToken ct = default)
    {
        var response = await GetFeaturesAsync(BuildWfsQuery(request), ct).ConfigureAwait(false);
        return ToFeatureQueryResult(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
        FeatureQueryRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = request;
        var startIndex = request.Offset ?? 0;
        var pageCount = 0;

        while (pageCount < MaxAutoPages)
        {
            ct.ThrowIfCancellationRequested();

            var page = await GetFeaturesAsync(BuildWfsQuery(current), ct).ConfigureAwait(false);
            yield return ToFeatureQueryResult(page);

            if (page.NumberReturned == 0)
            {
                yield break;
            }

            startIndex += page.NumberReturned;
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
    public Task<FeatureEditResponse> ApplyEditsAsync(FeatureEditRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();
        throw new NotSupportedException(UnsupportedEditReason);
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Response ownership is transferred to ResponseOwningStream when the handler owns the response stream.")]
    public async Task<TResult> GetFeaturesAsync<TResult>(
        GetFeaturesRequest request,
        IWfsOutputFormatHandler<TResult> handler,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handler);

        using var activity = ActivitySource.StartActivity("WFS GetFeature");
        activity?.SetTag("wfs.operation", "GetFeature");
        activity?.SetTag("wfs.type_name", request.TypeNames);
        activity?.SetTag("wfs.output_format", handler.MediaType);

        var url = BuildGetFeatureUrl(request, handler.MediaType);
        var response = await _httpClient.GetAsync(CreateRequestUri(url), HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        try
        {
            await EnsureGetFeatureSuccessAsync(response, handler.MediaType, ct).ConfigureAwait(false);

            Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

            if (handler.OwnsResponseStream)
            {
                // Wrap so disposing the returned stream also disposes the HTTP response,
                // which is required when using ResponseHeadersRead.
                stream = new ResponseOwningStream(stream, response);
            }

            var result = await handler.ReadAsync(stream, ct).ConfigureAwait(false);

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
    public async Task<long?> GetFeatureCountAsync(string typeName, string? filter = null, CancellationToken ct = default)
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
        using var response = await _httpClient.GetAsync(CreateRequestUri(url), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        return ParseHitsNumberMatched(body);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<WfsFeature> GetFeaturesAsyncEnumerable(
        GetFeaturesRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var activity = ActivitySource.StartActivity("WFS GetFeaturesAsyncEnumerable");
        activity?.SetTag("wfs.operation", "GetFeature");
        activity?.SetTag("wfs.type_name", request.TypeNames);

        var startIndex = request.StartIndex ?? 0;
        var pageCount = 0;

        while (pageCount < MaxAutoPages)
        {
            ct.ThrowIfCancellationRequested();

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

            var page = await GetFeaturesAsync(pageRequest, ct).ConfigureAwait(false);

            foreach (var feature in page.Features)
            {
                yield return feature;
            }

            if (page.NumberReturned == 0)
            {
                yield break;
            }

            startIndex += page.NumberReturned;

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
        HttpResponseMessage response, string requestedMediaType, CancellationToken ct)
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
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

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
