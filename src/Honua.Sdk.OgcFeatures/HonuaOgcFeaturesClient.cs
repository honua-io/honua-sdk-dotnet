// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Geometry.Vector;
using Honua.Sdk.OgcFeatures.Exceptions;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures;

/// <summary>
/// HTTP client implementation for the Honua OGC API Features read/query API.
/// </summary>
public sealed class HonuaOgcFeaturesClient :
    IHonuaOgcFeaturesClient,
    IHonuaOgcFeaturesEditClient,
    IHonuaOgcFeaturesPatchClient,
    IHonuaFeatureQueryClient,
    IHonuaFeatureEditClient,
    IHonuaFeatureDescriptorClient,
    IHonuaFeatureAttachmentClient
{
    private const string BasePath = "/ogc/features";

    /// <summary>
    /// Safety cap on the number of pages auto-pagination will fetch before throwing,
    /// guarding against servers that emit self-referential or cyclic next-links.
    /// </summary>
    private const int MaxAutoPages = 100;
    private const string RollbackUnsupportedReason =
        "OGC API Features create, update, and delete endpoints do not support rollback-on-failure edit batches.";
    private const string UnsupportedAttachmentReason =
        "OGC API Features does not define provider-neutral attachment operations in this SDK.";

    private static readonly FeatureEditCapabilities OgcEditCapabilities = new()
    {
        SupportsAdds = true,
        SupportsUpdates = true,
        SupportsPatches = true,
        SupportsDeletes = true,
        NativeSurface = "OGC API Features create/update/patch/delete"
    };
    private static readonly FeatureAttachmentCapabilities OgcAttachmentCapabilities = new()
    {
        NativeSurface = "OGC API Features attachments",
        UnsupportedReason = UnsupportedAttachmentReason
    };
    private static readonly FeatureQueryCapabilities OgcQueryCapabilities = new()
    {
        SupportsTimeFilter = true,
        SupportsStatistics = false,
        SupportsGroupBy = false,
        SupportsHaving = false,
        NativeSurface = "OGC API Features items query",
        UnsupportedReason = "OGC API Features does not expose server-side statistics, group-by, or having facets.",
    };

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcFeaturesClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth handlers.</param>
    public HonuaOgcFeaturesClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public string ProviderName => "ogc-features";

    /// <inheritdoc />
    public FeatureEditCapabilities EditCapabilities => OgcEditCapabilities;

    /// <inheritdoc />
    public FeatureAttachmentCapabilities AttachmentCapabilities => OgcAttachmentCapabilities;

    /// <inheritdoc />
    public FeatureQueryCapabilities QueryCapabilities => OgcQueryCapabilities;

    /// <inheritdoc />
    public async Task<OgcLandingPage> GetLandingPageAsync(CancellationToken cancellationToken = default)
    {
        var body = await GetStringAsync($"{BasePath}?f=json", cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcLandingPage)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize landing page.", body);
    }

    /// <inheritdoc />
    public async Task<OgcConformance> GetConformanceAsync(CancellationToken cancellationToken = default)
    {
        var body = await GetStringAsync($"{BasePath}/conformance?f=json", cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcConformance)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize conformance.", body);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OgcCollection>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var body = await GetStringAsync($"{BasePath}/collections?f=json", cancellationToken).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcCollectionsResponse)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize collections.", body);
        return response.Collections?.AsReadOnly() ?? (IReadOnlyList<OgcCollection>)[];
    }

    /// <inheritdoc />
    public async Task<OgcCollection> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}?f=json";

        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcCollection)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize collection.", body);
    }

    /// <inheritdoc />
    public async Task<OgcQueryables> GetQueryablesAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/queryables?f=json";

        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcQueryables)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize queryables.", body);
    }

    /// <inheritdoc />
    public async Task<SourceDescriptor> GetDescriptorAsync(SourceDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        if (string.IsNullOrWhiteSpace(descriptor.Locator.CollectionId))
        {
            throw new ArgumentException(
                "OGC API Features descriptor discovery requires SourceDescriptor.Locator.CollectionId.",
                nameof(descriptor));
        }

        var collection = await GetCollectionAsync(descriptor.Locator.CollectionId, cancellationToken).ConfigureAwait(false);
        var queryables = await TryGetQueryablesAsync(descriptor.Locator.CollectionId, cancellationToken).ConfigureAwait(false);
        return descriptor with
        {
            Capabilities = BuildDiscoveredCapabilities(),
            Schema = BuildSourceSchema(collection, queryables),
        };
    }

    /// <inheritdoc />
    public async Task<OgcFeatureCollection> GetItemsAsync(
        string collectionId, OgcItemsParams? query = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(query)}";

        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcFeatureCollection)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize items.", body);
    }

    /// <inheritdoc />
    public async Task<FeatureQueryResult> QueryAsync(
        FeatureQueryRequest request, CancellationToken cancellationToken = default)
    {
        var (collectionId, query) = BuildOgcQuery(request);
        var response = await GetItemsAsync(collectionId, query, cancellationToken).ConfigureAwait(false);
        return ToFeatureQueryResult(response);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
        FeatureQueryRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (collectionId, query) = BuildOgcQuery(request);
        await foreach (var page in GetItemsPagesAsync(collectionId, query, cancellationToken).ConfigureAwait(false))
        {
            yield return ToFeatureQueryResult(page);
        }
    }

    /// <inheritdoc />
    public async Task<FeatureEditResponse> ApplyEditsAsync(FeatureEditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var collectionId = GetEditCollectionId(request);
        EnsureSupportedEditRequest(request);

        var addResults = new List<FeatureEditResult>();
        foreach (var feature in request.Adds)
        {
            addResults.Add(await ApplyAddAsync(collectionId, feature, cancellationToken).ConfigureAwait(false));
        }

        var updateResults = new List<FeatureEditResult>();
        foreach (var feature in request.Updates)
        {
            updateResults.Add(await ApplyUpdateAsync(collectionId, feature, cancellationToken).ConfigureAwait(false));
        }

        var patchResults = new List<FeatureEditResult>();
        foreach (var patch in request.Patches)
        {
            patchResults.Add(await ApplyPatchAsync(collectionId, patch, cancellationToken).ConfigureAwait(false));
        }

        var deleteResults = new List<FeatureEditResult>();
        foreach (var featureId in ResolveDeleteIds(request))
        {
            deleteResults.Add(await ApplyDeleteAsync(collectionId, featureId, cancellationToken).ConfigureAwait(false));
        }

        return new FeatureEditResponse
        {
            ProviderName = ProviderName,
            AddResults = addResults,
            UpdateResults = updateResults,
            PatchResults = patchResults,
            DeleteResults = deleteResults
        };
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
    public async Task<OgcFeature> GetItemAsync(string collectionId, string featureId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(featureId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(featureId)}?f=json";

        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcFeature)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize feature.", body);
    }

    /// <inheritdoc />
    public Task<OgcFeature> CreateItemAsync(string collectionId, OgcFeature feature, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(feature);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items?f=json";
        return SendFeatureAsync(HttpMethod.Post, url, feature, cancellationToken);
    }

    /// <inheritdoc />
    public Task<OgcFeature> UpdateItemAsync(string collectionId, string featureId, OgcFeature feature, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(featureId);
        ArgumentNullException.ThrowIfNull(feature);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(featureId)}?f=json";
        return SendFeatureAsync(HttpMethod.Put, url, feature, cancellationToken);
    }

    /// <inheritdoc />
    public Task<OgcFeature> PatchItemAsync(string collectionId, string featureId, JsonElement patch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(featureId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(featureId)}?f=json";
        return SendMergePatchAsync(url, featureId, patch, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteItemAsync(string collectionId, string featureId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(featureId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(featureId)}?f=json";

        using var response = await _http.DeleteAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<OgcFeatureCollection> GetItemsPagesAsync(
        string collectionId, OgcItemsParams? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);

        var page = await GetItemsAsync(collectionId, query, cancellationToken).ConfigureAwait(false);
        yield return page;

        var visitedHrefs = new HashSet<string>(StringComparer.Ordinal);
        var pageCount = 0;
        while (pageCount < MaxAutoPages)
        {
            var nextLink = page.Links?.FirstOrDefault(l =>
                string.Equals(l.Rel, "next", StringComparison.OrdinalIgnoreCase));

            if (nextLink is null || string.IsNullOrEmpty(nextLink.Href))
            {
                yield break;
            }

            ValidateNextLinkOrigin(nextLink.Href);

            // A next-link that points back to a page already fetched (or to itself) would
            // loop forever. Track visited hrefs and stop on a repeat.
            if (!visitedHrefs.Add(nextLink.Href))
            {
                yield break;
            }

            var body = await GetStringAsync(nextLink.Href, cancellationToken).ConfigureAwait(false);
            page = JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcFeatureCollection)
                ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize paged items.", body);

            if (page.Features is null or { Count: 0 })
            {
                yield break;
            }

            yield return page;
            pageCount++;
        }

        throw new InvalidOperationException(
            $"Auto-pagination safety limit reached ({MaxAutoPages} pages). " +
            "Use manual paging with GetItemsAsync for larger result sets.");
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetItemsRawAsync(
        string collectionId, OgcItemsParams? query = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(query)}";
        return await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<VectorPayloadFeatureSet> GetItemsVectorAsync(
        string collectionId,
        OgcItemsParams? query = null,
        VectorPayloadFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);

        var vectorFormat = format ?? OgcFeaturesVectorFormats.FromOgcFeaturesFormat(query?.Format);
        var protocolFormat = OgcFeaturesVectorFormats.ToOgcFeaturesFormat(vectorFormat);
        var vectorQuery = (query ?? new OgcItemsParams()) with { Format = protocolFormat };
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(vectorQuery)}";
        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return await VectorPayloadReaders.ReadAsync(stream, vectorFormat, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    private async Task<OgcFeature> SendFeatureAsync(HttpMethod method, string url, OgcFeature feature, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = CreateGeoJsonContent(feature)
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);

        if (string.IsNullOrWhiteSpace(body))
        {
            return feature;
        }

        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcFeature)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize edited feature.", body);
    }

    private async Task<OgcFeature> SendMergePatchAsync(string url, string featureId, JsonElement patch, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = CreateMergePatchContent(patch)
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);

        if (string.IsNullOrWhiteSpace(body))
        {
            return new OgcFeature
            {
                Id = JsonSerializer.SerializeToElement(featureId)
            };
        }

        try
        {
            return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcFeature)
                ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize patched feature.", body);
        }
        catch (JsonException ex)
        {
            throw new HonuaOgcFeaturesException(
                HttpStatusCode.OK,
                "Failed to deserialize patched feature.",
                body,
                ex);
        }
    }

    private static StringContent CreateGeoJsonContent(OgcFeature feature)
    {
        var json = JsonSerializer.Serialize(feature, OgcFeaturesJsonContext.Default.OgcFeature);
        return new StringContent(json, Encoding.UTF8, "application/geo+json");
    }

    private static StringContent CreateMergePatchContent(JsonElement patch)
    {
        var json = JsonSerializer.Serialize(patch, OgcFeaturesJsonContext.Default.JsonElement);
        return new StringContent(json, Encoding.UTF8, "application/merge-patch+json");
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // Attempt to parse RFC 7807 Problem Details
        string? problemType = null;
        string? problemTitle = null;
        string? problemDetail = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                var problem = JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcProblemDetails);
                if (problem is not null)
                {
                    problemType = problem.Type;
                    problemTitle = problem.Title;
                    problemDetail = problem.Detail;
                }
            }
            catch (JsonException)
            {
                // Not valid Problem Details JSON
            }
        }

        var message = problemDetail ?? problemTitle ?? response.ReasonPhrase ?? "OGC Features request failed";
        throw new HonuaOgcFeaturesException(
            response.StatusCode, message, body, problemType, problemTitle, problemDetail);
    }

    private void ValidateNextLinkOrigin(string nextUrl)
    {
        if (!Uri.TryCreate(nextUrl, UriKind.Absolute, out var nextUri))
        {
            return; // Relative URLs are safe — they use the same base
        }

        var baseAddress = _http.BaseAddress;
        if (baseAddress is null)
        {
            return;
        }

        if (!string.Equals(nextUri.Scheme, baseAddress.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(nextUri.Authority, baseAddress.Authority, StringComparison.OrdinalIgnoreCase))
        {
            throw new HonuaOgcFeaturesException(
                HttpStatusCode.BadGateway,
                $"Server returned a next-page link to a different origin ({nextUri.Authority}), which may indicate an open-redirect attack. Paging stopped.",
                nextUrl);
        }
    }

    private static string BuildQueryString(OgcItemsParams? query)
    {
        if (query is null)
        {
            return "?f=json";
        }

        var parameters = new List<(string Key, string? Value)>
        {
            ("f", FormatToString(query.Format)),
        };

        if (query.Limit.HasValue)
            parameters.Add(("limit", query.Limit.Value.ToString(CultureInfo.InvariantCulture)));

        if (query.Offset.HasValue)
            parameters.Add(("offset", query.Offset.Value.ToString(CultureInfo.InvariantCulture)));

        if (query.Bbox is { Count: >= 4 })
            parameters.Add(("bbox", string.Join(",", query.Bbox.Select(d => d.ToString(CultureInfo.InvariantCulture)))));

        if (query.BboxCrs is not null)
            parameters.Add(("bbox-crs", query.BboxCrs));

        if (query.Crs is not null)
            parameters.Add(("crs", query.Crs));

        if (query.Datetime is not null)
            parameters.Add(("datetime", query.Datetime));

        if (query.Filter is not null)
            parameters.Add(("filter", query.Filter));

        if (query.FilterLang is not null)
            parameters.Add(("filter-lang", query.FilterLang));

        if (query.FilterCrs is not null)
            parameters.Add(("filter-crs", query.FilterCrs));

        if (query.Ids is { Count: > 0 })
            parameters.Add(("ids", string.Join(",", query.Ids)));

        if (query.Properties is not null)
            parameters.Add(("properties", query.Properties));

        if (query.Sortby is not null)
            parameters.Add(("sortby", query.Sortby));

        var parts = new List<string>();
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrEmpty(value))
            {
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }

        return parts.Count > 0 ? $"?{string.Join("&", parts)}" : string.Empty;
    }

    private static string FormatToString(OgcFeaturesFormat? format) => format switch
    {
        OgcFeaturesFormat.GeoJson => "json",
        OgcFeaturesFormat.Json => "json",
        OgcFeaturesFormat.Html => "html",
        OgcFeaturesFormat.Gml => "gml",
        OgcFeaturesFormat.Csv => "csv",
        OgcFeaturesFormat.FlatGeobuf => "flatgeobuf",
        OgcFeaturesFormat.Parquet => "parquet",
        _ => "json",
    };

    private async Task<OgcQueryables?> TryGetQueryablesAsync(string collectionId, CancellationToken cancellationToken)
    {
        try
        {
            return await GetQueryablesAsync(collectionId, cancellationToken).ConfigureAwait(false);
        }
        catch (HonuaOgcFeaturesException ex) when (
            ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NotImplemented)
        {
            return null;
        }
    }

    private static List<string> BuildDiscoveredCapabilities()
    {
        var capabilities = new HashSet<string>(
            FeatureProtocolCapabilities.DefaultsFor(FeatureProtocolIds.OgcFeatures),
            StringComparer.Ordinal);

        if (OgcEditCapabilities.SupportsAdds || OgcEditCapabilities.SupportsUpdates || OgcEditCapabilities.SupportsDeletes)
        {
            capabilities.Add(FeatureCapabilities.ApplyEdits);
        }

        return FeatureCapabilities.All.Where(capabilities.Contains).ToList();
    }

    private static SourceSchema BuildSourceSchema(OgcCollection collection, OgcQueryables? queryables)
    {
        var fields = BuildQueryableFields(queryables);
        var spatialReference =
            collection.StorageCrs ??
            collection.Extent?.Spatial?.Crs ??
            (collection.Crs is { Count: > 0 } ? collection.Crs[0] : null);
        var primaryKey = fields.FirstOrDefault(
            field => string.Equals(field.Name, "id", StringComparison.OrdinalIgnoreCase))?.Name;

        return new SourceSchema
        {
            Fields = fields,
            PrimaryKey = primaryKey,
            GeometryType = FeatureSpatialGeometryType.Unspecified,
            Extent = ToFeatureBoundingBox(collection.Extent?.Spatial, spatialReference),
            SpatialReference = spatialReference,
            EditCapabilities = OgcEditCapabilities,
            AttachmentCapabilities = OgcAttachmentCapabilities,
        };
    }

    private static Task<TResult> ThrowUnsupportedAttachmentAsync<TResult>(object request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(UnsupportedAttachmentReason);
    }

    private static List<SourceField> BuildQueryableFields(OgcQueryables? queryables)
    {
        if (queryables?.Properties is not { Count: > 0 } properties)
        {
            return [];
        }

        var required = new HashSet<string>(queryables.Required ?? [], StringComparer.Ordinal);
        return properties.Select(property => ToSourceField(property.Key, property.Value, required)).ToList();
    }

    private static SourceField ToSourceField(string name, JsonElement schema, HashSet<string> required)
        => new()
        {
            Name = name,
            Alias = TryGetStringProperty(schema, "title"),
            Type = GetJsonSchemaType(schema),
            Nullable = GetJsonSchemaNullable(name, schema, required),
            Length = TryGetIntProperty(schema, "maxLength"),
            Required = required.Contains(name),
            DefaultValue = TryGetPropertyClone(schema, "default"),
            Domain = TryGetPropertyClone(schema, "enum"),
        };

    private static string? GetJsonSchemaType(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return null;
        }

        return type.ValueKind switch
        {
            JsonValueKind.String => type.GetString(),
            JsonValueKind.Array => string.Join(
                "|",
                type.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())),
            _ => type.GetRawText(),
        };
    }

    private static bool? GetJsonSchemaNullable(string name, JsonElement schema, HashSet<string> required)
    {
        if (schema.TryGetProperty("nullable", out var nullable) &&
            nullable.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return nullable.GetBoolean();
        }

        return !required.Contains(name);
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? TryGetIntProperty(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;

    private static JsonElement? TryGetPropertyClone(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) &&
           property.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null
            ? property.Clone()
            : null;

    private static FeatureBoundingBox? ToFeatureBoundingBox(OgcSpatialExtent? spatialExtent, string? fallbackCrs)
    {
        var bbox = spatialExtent?.Bbox is { Count: > 0 } bboxes ? bboxes[0] : null;
        if (bbox is not { Count: >= 4 })
        {
            return null;
        }

        return new FeatureBoundingBox
        {
            MinX = bbox[0],
            MinY = bbox[1],
            MaxX = bbox[2],
            MaxY = bbox[3],
            Crs = spatialExtent?.Crs ?? fallbackCrs,
        };
    }

    private static (string CollectionId, OgcItemsParams Query) BuildOgcQuery(FeatureQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureSupportedFilterLanguage(request.FilterLanguage);
        EnsureSupportedSharedQueryModes(request);

        if (string.IsNullOrWhiteSpace(request.Source.CollectionId))
        {
            throw new ArgumentException("A collection ID is required for OGC API Features queries.", nameof(request));
        }

        var query = new OgcItemsParams
        {
            Limit = request.Limit,
            Offset = request.Offset,
            Bbox = request.Bbox is null
                ? null
                : [request.Bbox.MinX, request.Bbox.MinY, request.Bbox.MaxX, request.Bbox.MaxY],
            BboxCrs = request.Bbox?.Crs,
            Crs = request.OutputCrs,
            Filter = request.Filter,
            FilterLang = !string.IsNullOrWhiteSpace(request.Filter) &&
                request.FilterLanguage == FeatureFilterLanguage.Cql2Text
                    ? "cql2-text"
                    : null,
            Datetime = BuildOgcDatetime(request.TimeFilter),
            Ids = BuildIds(request),
            Properties = request.OutFields is { Count: > 0 } ? string.Join(",", request.OutFields) : null,
            Sortby = request.OrderBy,
        };

        return (request.Source.CollectionId, query);
    }

    private static void EnsureSupportedFilterLanguage(FeatureFilterLanguage language)
    {
        if (language is not FeatureFilterLanguage.ProviderDefault and not FeatureFilterLanguage.Cql2Text)
        {
            throw new NotSupportedException("OGC API Features queries support provider-default or CQL2 text filters.");
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
                "OGC API Features shared queries do not support distinct, count-only, IDs-only, or extent-only modes yet.");
        }

        if (request.OutStatistics is { Count: > 0 } ||
            request.GroupBy is { Count: > 0 } ||
            !string.IsNullOrWhiteSpace(request.Having))
        {
            throw new NotSupportedException(
                "OGC API Features shared queries do not support provider-neutral statistics, group-by, or having clauses yet.");
        }

        if (request.SpatialFilter is not null)
        {
            throw new NotSupportedException(
                "OGC API Features shared queries do not support explicit geometry spatial filters yet. Use Bbox for envelope filters.");
        }

        if (request.TimeFilter?.Relation is FeatureTimeRelation.AfterStartWithinEnd or FeatureTimeRelation.Within)
        {
            throw new NotSupportedException(
                "OGC API Features shared time filters support only provider-default or overlap datetime intervals.");
        }
    }

    private static string? BuildOgcDatetime(FeatureTimeFilter? timeFilter)
    {
        if (timeFilter is null)
        {
            return null;
        }

        if (timeFilter.End is not { } end)
        {
            return timeFilter.Start.ToString("O", CultureInfo.InvariantCulture);
        }

        if (end < timeFilter.Start)
        {
            throw new ArgumentException("Feature query time filter end must be greater than or equal to start.", nameof(timeFilter));
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{timeFilter.Start:O}/{end:O}");
    }

    private static IReadOnlyList<string>? BuildIds(FeatureQueryRequest request)
    {
        if (request.FeatureIds is { Count: > 0 })
        {
            return request.FeatureIds;
        }

        return request.ObjectIds is { Count: > 0 }
            ? request.ObjectIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList()
            : null;
    }

    private static string GetEditCollectionId(FeatureEditRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Source.CollectionId))
        {
            throw new ArgumentException("A collection ID is required for OGC API Features edits.", nameof(request));
        }

        return request.Source.CollectionId;
    }

    private static void EnsureSupportedEditRequest(FeatureEditRequest request)
    {
        if (request.ForceWrite)
        {
            throw new NotSupportedException("OGC API Features edits do not support ForceWrite.");
        }

        var operationCount = request.Adds.Count +
            request.Updates.Count +
            request.Patches.Count +
            request.DeleteIds.Count +
            request.DeleteObjectIds.Count;
        if (operationCount == 0)
        {
            throw new ArgumentException("At least one OGC API Features edit operation is required.", nameof(request));
        }

        foreach (var update in request.Updates)
        {
            _ = ResolveFeatureId(update);
        }

        foreach (var patch in request.Patches)
        {
            _ = ResolveFeatureId(patch);
            if (patch.Patch.ValueKind == JsonValueKind.Undefined)
            {
                throw new ArgumentException("OGC API Features patches require a JSON Merge Patch payload.", nameof(request));
            }
        }

        foreach (var deleteId in request.DeleteIds)
        {
            if (string.IsNullOrWhiteSpace(deleteId))
            {
                throw new ArgumentException("OGC API Features deletes require non-empty feature IDs.", nameof(request));
            }
        }

        if (request.RollbackOnFailure && operationCount > 1)
        {
            throw new NotSupportedException(RollbackUnsupportedReason);
        }
    }

    private async Task<FeatureEditResult> ApplyAddAsync(string collectionId, FeatureEditFeature feature, CancellationToken cancellationToken)
    {
        try
        {
            var response = await CreateItemAsync(collectionId, ToOgcFeature(feature), cancellationToken).ConfigureAwait(false);
            return ToFeatureEditResult(response, feature);
        }
        catch (HonuaOgcFeaturesException ex)
        {
            return ToFeatureEditResult(feature, ex);
        }
    }

    private async Task<FeatureEditResult> ApplyUpdateAsync(string collectionId, FeatureEditFeature feature, CancellationToken cancellationToken)
    {
        var featureId = ResolveFeatureId(feature);

        try
        {
            var response = await UpdateItemAsync(collectionId, featureId, ToOgcFeature(feature, featureId), cancellationToken).ConfigureAwait(false);
            return ToFeatureEditResult(response, feature);
        }
        catch (HonuaOgcFeaturesException ex)
        {
            return ToFeatureEditResult(feature, ex);
        }
    }

    private async Task<FeatureEditResult> ApplyPatchAsync(string collectionId, FeatureEditPatch patch, CancellationToken cancellationToken)
    {
        var featureId = ResolveFeatureId(patch);

        try
        {
            var response = await PatchItemAsync(collectionId, featureId, patch.Patch, cancellationToken).ConfigureAwait(false);
            return ToFeatureEditResult(response, patch);
        }
        catch (HonuaOgcFeaturesException ex)
        {
            return ToFeatureEditResult(patch, ex);
        }
    }

    private async Task<FeatureEditResult> ApplyDeleteAsync(string collectionId, string featureId, CancellationToken cancellationToken)
    {
        try
        {
            await DeleteItemAsync(collectionId, featureId, cancellationToken).ConfigureAwait(false);
            return new FeatureEditResult
            {
                Id = featureId,
                Succeeded = true
            };
        }
        catch (HonuaOgcFeaturesException ex)
        {
            return new FeatureEditResult
            {
                Id = featureId,
                Succeeded = false,
                Error = ToFeatureEditError(ex)
            };
        }
    }

    private static OgcFeature ToOgcFeature(FeatureEditFeature feature, string? featureId = null)
    {
        var id = featureId ?? ResolveOptionalFeatureId(feature);

        return new OgcFeature
        {
            Id = id is null ? null : JsonSerializer.SerializeToElement(id),
            Geometry = feature.Geometry.HasValue ? feature.Geometry.Value.Clone() : null,
            Properties = feature.Attributes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone())
        };
    }

    private static string ResolveFeatureId(FeatureEditFeature feature)
        => ResolveOptionalFeatureId(feature) ??
           throw new ArgumentException("OGC API Features updates require FeatureEditFeature.Id or ObjectId.");

    private static string ResolveFeatureId(FeatureEditPatch patch)
        => ResolveOptionalFeatureId(patch) ??
           throw new ArgumentException("OGC API Features patches require FeatureEditPatch.Id or ObjectId.");

    private static string? ResolveOptionalFeatureId(FeatureEditFeature feature)
    {
        if (!string.IsNullOrWhiteSpace(feature.Id))
        {
            return feature.Id;
        }

        return feature.ObjectId?.ToString(CultureInfo.InvariantCulture);
    }

    private static string? ResolveOptionalFeatureId(FeatureEditPatch patch)
    {
        if (!string.IsNullOrWhiteSpace(patch.Id))
        {
            return patch.Id;
        }

        return patch.ObjectId?.ToString(CultureInfo.InvariantCulture);
    }

    private static List<string> ResolveDeleteIds(FeatureEditRequest request)
    {
        var ids = new List<string>(request.DeleteIds);
        ids.AddRange(request.DeleteObjectIds.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        return ids;
    }

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);

    private static FeatureEditResult ToFeatureEditResult(OgcFeature response, FeatureEditFeature fallback)
    {
        var id = response.Id.HasValue ? JsonElementToString(response.Id.Value) : ResolveOptionalFeatureId(fallback);

        return new FeatureEditResult
        {
            Id = id,
            ObjectId = long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId) ? objectId : null,
            Succeeded = true
        };
    }

    private static FeatureEditResult ToFeatureEditResult(OgcFeature response, FeatureEditPatch fallback)
    {
        var id = response.Id.HasValue ? JsonElementToString(response.Id.Value) : ResolveOptionalFeatureId(fallback);

        return new FeatureEditResult
        {
            Id = id,
            ObjectId = long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId) ? objectId : null,
            Succeeded = true
        };
    }

    private static FeatureEditResult ToFeatureEditResult(FeatureEditFeature feature, HonuaOgcFeaturesException exception)
        => new()
        {
            Id = ResolveOptionalFeatureId(feature),
            ObjectId = feature.ObjectId,
            Succeeded = false,
            Error = ToFeatureEditError(exception)
        };

    private static FeatureEditResult ToFeatureEditResult(FeatureEditPatch patch, HonuaOgcFeaturesException exception)
        => new()
        {
            Id = ResolveOptionalFeatureId(patch),
            ObjectId = patch.ObjectId,
            Succeeded = false,
            Error = ToFeatureEditError(exception)
        };

    private static FeatureEditError ToFeatureEditError(HonuaOgcFeaturesException exception)
        => new()
        {
            Code = (int)exception.StatusCode,
            Message = exception.Message,
            Details = exception.ResponseBody
        };

    private FeatureQueryResult ToFeatureQueryResult(OgcFeatureCollection response)
    {
        var features = response.Features?.Select(ToFeatureRecord).ToList() ?? [];

        return new FeatureQueryResult
        {
            ProviderName = ProviderName,
            Features = features,
            NumberMatched = response.NumberMatched,
            NumberReturned = response.NumberReturned ?? features.Count,
            HasMoreResults = response.Links?.Any(link =>
                string.Equals(link.Rel, "next", StringComparison.OrdinalIgnoreCase)) ?? false,
        };
    }

    private static FeatureRecord ToFeatureRecord(OgcFeature feature)
    {
        return new FeatureRecord
        {
            Id = feature.Id.HasValue ? JsonElementToString(feature.Id.Value) : null,
            Attributes = feature.Properties?.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Clone()) ?? new Dictionary<string, JsonElement>(),
            Geometry = feature.Geometry.HasValue ? feature.Geometry.Value.Clone() : null,
        };
    }

    private static string? JsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null,
        };
    }
}
