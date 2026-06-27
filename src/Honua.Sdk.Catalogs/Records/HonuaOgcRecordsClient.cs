// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Sdk.Abstractions.Http;
using Honua.Sdk.Catalogs.Records.Exceptions;
using Honua.Sdk.Catalogs.Records.Models;

namespace Honua.Sdk.Catalogs.Records;

/// <summary>
/// HTTP client implementation for the Honua OGC API Records public catalog API.
/// </summary>
public sealed class HonuaOgcRecordsClient : IHonuaOgcRecordsClient
{
    private const string BasePath = "/ogc/records";

    /// <summary>
    /// Safety cap on the number of pages auto-pagination will fetch before throwing,
    /// guarding against servers that emit self-referential or cyclic next-links.
    /// </summary>
    private const int MaxAutoPages = 100;

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcRecordsClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth handlers.</param>
    public HonuaOgcRecordsClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<OgcRecordsLandingPage> GetLandingPageAsync(CancellationToken cancellationToken = default)
    {
        var body = await GetStringAsync($"{BasePath}?f=json", cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcRecordsJsonContext.Default.OgcRecordsLandingPage)
            ?? throw new HonuaOgcRecordsException(HttpStatusCode.OK, "Failed to deserialize Records landing page.", body);
    }

    /// <inheritdoc />
    public async Task<OgcRecordsConformance> GetConformanceAsync(CancellationToken cancellationToken = default)
    {
        var body = await GetStringAsync($"{BasePath}/conformance?f=json", cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcRecordsJsonContext.Default.OgcRecordsConformance)
            ?? throw new HonuaOgcRecordsException(HttpStatusCode.OK, "Failed to deserialize Records conformance.", body);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OgcRecordsCollection>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var body = await GetStringAsync($"{BasePath}/collections?f=json", cancellationToken).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize(body, OgcRecordsJsonContext.Default.OgcRecordsCollectionsResponse)
            ?? throw new HonuaOgcRecordsException(HttpStatusCode.OK, "Failed to deserialize Records collections.", body);
        return response.Collections?.AsReadOnly() ?? (IReadOnlyList<OgcRecordsCollection>)[];
    }

    /// <inheritdoc />
    public async Task<OgcRecordsCollection> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}?f=json";

        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcRecordsJsonContext.Default.OgcRecordsCollection)
            ?? throw new HonuaOgcRecordsException(HttpStatusCode.OK, "Failed to deserialize Records collection.", body);
    }

    /// <inheritdoc />
    public async Task<OgcRecordCollection> GetRecordsAsync(
        string collectionId,
        OgcRecordsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(query)}";

        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcRecordsJsonContext.Default.OgcRecordCollection)
            ?? throw new HonuaOgcRecordsException(HttpStatusCode.OK, "Failed to deserialize Records response.", body);
    }

    /// <inheritdoc />
    public Task<OgcRecordCollection> SearchAsync(
        string collectionId,
        OgcRecordsQuery? query = null,
        CancellationToken cancellationToken = default)
        => GetRecordsAsync(collectionId, query, cancellationToken);

    /// <inheritdoc />
    public async Task<OgcRecord> GetRecordAsync(
        string collectionId,
        string recordId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(recordId)}?f=json";

        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcRecordsJsonContext.Default.OgcRecord)
            ?? throw new HonuaOgcRecordsException(HttpStatusCode.OK, "Failed to deserialize Record detail.", body);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<OgcRecordCollection> GetRecordsPagesAsync(
        string collectionId,
        OgcRecordsQuery? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);

        var page = await GetRecordsAsync(collectionId, query, cancellationToken).ConfigureAwait(false);
        yield return page;

        var visitedHrefs = new HashSet<string>(StringComparer.Ordinal);
        var pageCount = 0;
        while (pageCount < MaxAutoPages)
        {
            var nextLink = page.Links?.FirstOrDefault(static link =>
                string.Equals(link.Rel, "next", StringComparison.OrdinalIgnoreCase));

            if (nextLink is null || string.IsNullOrWhiteSpace(nextLink.Href))
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
            page = JsonSerializer.Deserialize(body, OgcRecordsJsonContext.Default.OgcRecordCollection)
                ?? throw new HonuaOgcRecordsException(HttpStatusCode.OK, "Failed to deserialize paged Records response.", body);

            if (page.Records is null or { Count: 0 })
            {
                yield break;
            }

            yield return page;
            pageCount++;
        }

        throw new InvalidOperationException(
            $"Auto-pagination safety limit reached ({MaxAutoPages} pages). " +
            "Use manual paging with GetRecordsAsync for larger result sets.");
    }

    /// <inheritdoc />
    public async Task<JsonDocument> GetRecordsJsonAsync(
        string collectionId,
        OgcRecordsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(query)}";
        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    /// <inheritdoc />
    public async Task<JsonDocument> GetRecordJsonAsync(
        string collectionId,
        string recordId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(recordId)}?f=json";
        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetRecordsRawAsync(
        string collectionId,
        OgcRecordsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(query)}";
        return await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? problemType = null;
        string? problemTitle = null;
        string? problemDetail = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                var problem = JsonSerializer.Deserialize(body, OgcRecordsJsonContext.Default.OgcRecordsProblemDetails);
                if (problem is not null)
                {
                    problemType = problem.Type;
                    problemTitle = problem.Title;
                    problemDetail = problem.Detail;
                }
            }
            catch (JsonException)
            {
                // Non-problem JSON responses still preserve the raw body below.
            }
        }

        var message = problemDetail ?? problemTitle ?? response.ReasonPhrase ?? "OGC Records request failed";
        throw new HonuaOgcRecordsException(
            response.StatusCode,
            message,
            body,
            problemType,
            problemTitle,
            problemDetail);
    }

    private void ValidateNextLinkOrigin(string nextUrl)
    {
        if (!NextLinkOriginValidator.IsSameOrigin(nextUrl, _http.BaseAddress))
        {
            throw new HonuaOgcRecordsException(
                HttpStatusCode.BadGateway,
                NextLinkOriginValidator.CrossOriginMessage(nextUrl),
                nextUrl);
        }
    }

    private static string BuildQueryString(OgcRecordsQuery? query)
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
            parameters.Add(("bbox", string.Join(",", query.Bbox.Select(static d => d.ToString(CultureInfo.InvariantCulture)))));

        if (!string.IsNullOrWhiteSpace(query.Datetime))
            parameters.Add(("datetime", query.Datetime));

        if (!string.IsNullOrWhiteSpace(query.Query))
            parameters.Add(("q", query.Query));

        if (query.Ids is { Count: > 0 })
            parameters.Add(("ids", string.Join(",", query.Ids)));

        if (query.Types is { Count: > 0 })
            parameters.Add(("type", string.Join(",", query.Types)));

        if (query.ExternalIds is { Count: > 0 })
            parameters.Add(("externalIds", string.Join(",", query.ExternalIds)));

        if (!string.IsNullOrWhiteSpace(query.Filter))
            parameters.Add(("filter", query.Filter));

        if (!string.IsNullOrWhiteSpace(query.FilterLang))
            parameters.Add(("filter-lang", query.FilterLang));

        if (!string.IsNullOrWhiteSpace(query.SortBy))
            parameters.Add(("sortby", query.SortBy));

        if (query.AdditionalParameters is { Count: > 0 })
        {
            foreach (var (key, value) in query.AdditionalParameters)
            {
                if (!string.IsNullOrWhiteSpace(key) &&
                    !parameters.Any(parameter => string.Equals(parameter.Key, key, StringComparison.OrdinalIgnoreCase)))
                {
                    parameters.Add((key, value));
                }
            }
        }

        var parts = new List<string>();
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }

        return parts.Count > 0 ? $"?{string.Join("&", parts)}" : string.Empty;
    }

    private static string FormatToString(OgcRecordsFormat? format) => format switch
    {
        OgcRecordsFormat.Json => "json",
        OgcRecordsFormat.GeoJson => "json",
        OgcRecordsFormat.Html => "html",
        _ => "json",
    };

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);
}
