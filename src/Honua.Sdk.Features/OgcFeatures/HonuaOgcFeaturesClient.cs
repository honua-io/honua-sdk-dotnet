// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Sdk.Features.OgcFeatures.Exceptions;
using Honua.Sdk.Features.OgcFeatures.Models;

namespace Honua.Sdk.Features.OgcFeatures;

/// <summary>
/// HTTP client implementation for the Honua OGC API Features read/query API.
/// </summary>
public sealed class HonuaOgcFeaturesClient : IHonuaOgcFeaturesClient
{
    private const string BasePath = "/ogc/features";
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
    public async Task<OgcLandingPage> GetLandingPageAsync(CancellationToken ct = default)
    {
        var body = await GetStringAsync($"{BasePath}?f=json", ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcLandingPage)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize landing page.", body);
    }

    /// <inheritdoc />
    public async Task<OgcConformance> GetConformanceAsync(CancellationToken ct = default)
    {
        var body = await GetStringAsync($"{BasePath}/conformance?f=json", ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcConformance)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize conformance.", body);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OgcCollection>> ListCollectionsAsync(CancellationToken ct = default)
    {
        var body = await GetStringAsync($"{BasePath}/collections?f=json", ct).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcCollectionsResponse)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize collections.", body);
        return response.Collections?.AsReadOnly() ?? (IReadOnlyList<OgcCollection>)[];
    }

    /// <inheritdoc />
    public async Task<OgcCollection> GetCollectionAsync(string collectionId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}?f=json";

        var body = await GetStringAsync(url, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcCollection)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize collection.", body);
    }

    /// <inheritdoc />
    public async Task<OgcQueryables> GetQueryablesAsync(string collectionId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/queryables?f=json";

        var body = await GetStringAsync(url, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcQueryables)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize queryables.", body);
    }

    /// <inheritdoc />
    public async Task<OgcFeatureCollection> GetItemsAsync(
        string collectionId, OgcItemsParams? query = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(query)}";

        var body = await GetStringAsync(url, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcFeatureCollection)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize items.", body);
    }

    /// <inheritdoc />
    public async Task<OgcFeature> GetItemAsync(string collectionId, string featureId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        ArgumentNullException.ThrowIfNull(featureId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(featureId)}?f=json";

        var body = await GetStringAsync(url, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcFeature)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize feature.", body);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<OgcFeatureCollection> GetItemsPagesAsync(
        string collectionId, OgcItemsParams? query = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);

        var page = await GetItemsAsync(collectionId, query, ct).ConfigureAwait(false);
        yield return page;

        while (true)
        {
            var nextLink = page.Links?.FirstOrDefault(l =>
                string.Equals(l.Rel, "next", StringComparison.OrdinalIgnoreCase));

            if (nextLink is null || string.IsNullOrEmpty(nextLink.Href))
            {
                yield break;
            }

            ValidateNextLinkOrigin(nextLink.Href);

            var body = await GetStringAsync(nextLink.Href, ct).ConfigureAwait(false);
            page = JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcFeatureCollection)
                ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize paged items.", body);

            if (page.Features is null or { Count: 0 })
            {
                yield break;
            }

            yield return page;
        }
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetItemsRawAsync(
        string collectionId, OgcItemsParams? query = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(query)}";
        return await _http.GetAsync(url, ct).ConfigureAwait(false);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
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
        _ => "json",
    };
}
