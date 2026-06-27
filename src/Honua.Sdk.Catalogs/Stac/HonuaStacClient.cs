// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Sdk.Abstractions.Http;
using Honua.Sdk.Catalogs.Stac.Exceptions;
using Honua.Sdk.Catalogs.Stac.Models;

namespace Honua.Sdk.Catalogs.Stac;

/// <summary>
/// HTTP client implementation for the Honua STAC catalog and item search API.
/// </summary>
public sealed class HonuaStacClient : IHonuaStacClient
{
    private const string BasePath = "/stac";

    /// <summary>
    /// Safety cap on the number of pages auto-pagination will fetch before throwing,
    /// guarding against servers that emit self-referential or cyclic next-links.
    /// </summary>
    private const int MaxAutoPages = 100;

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStacClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth handlers.</param>
    public HonuaStacClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<StacLandingPage> GetLandingPageAsync(CancellationToken cancellationToken = default)
    {
        var body = await GetStringAsync(BasePath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, StacJsonContext.Default.StacLandingPage)
            ?? throw new HonuaStacException(HttpStatusCode.OK, "Failed to deserialize STAC landing page.", body);
    }

    /// <inheritdoc />
    public Task<StacLandingPage> GetCatalogAsync(CancellationToken cancellationToken = default)
        => GetLandingPageAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StacCollection>> ListCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var body = await GetStringAsync($"{BasePath}/collections", cancellationToken).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize(body, StacJsonContext.Default.StacCollectionsResponse)
            ?? throw new HonuaStacException(HttpStatusCode.OK, "Failed to deserialize STAC collections.", body);
        return response.Collections?.AsReadOnly() ?? (IReadOnlyList<StacCollection>)[];
    }

    /// <inheritdoc />
    public async Task<StacCollection> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}";

        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, StacJsonContext.Default.StacCollection)
            ?? throw new HonuaStacException(HttpStatusCode.OK, "Failed to deserialize STAC collection.", body);
    }

    /// <inheritdoc />
    public async Task<StacItemCollection> GetItemsAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(query)}";

        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return DeserializeItemCollection(body, "Failed to deserialize STAC items response.");
    }

    /// <inheritdoc />
    public async Task<StacItem> GetItemAsync(
        string collectionId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(itemId)}";

        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, StacJsonContext.Default.StacItem)
            ?? throw new HonuaStacException(HttpStatusCode.OK, "Failed to deserialize STAC item detail.", body);
    }

    /// <inheritdoc />
    public async Task<StacItemCollection> SearchAsync(
        StacSearchQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        // `intersects` is only valid on POST /search per the STAC API spec; a conformant server
        // may ignore it on a GET and silently return unfiltered results. Route such searches to
        // the POST path so the geometry filter is actually honored.
        if (query?.Intersects is not null)
        {
            return await PostSearchAsync(ToSearchRequest(query), cancellationToken).ConfigureAwait(false);
        }

        var body = await GetStringAsync($"{BasePath}/search{BuildQueryString(query)}", cancellationToken).ConfigureAwait(false);
        return DeserializeItemCollection(body, "Failed to deserialize STAC search response.");
    }

    private static StacSearchRequest ToSearchRequest(StacSearchQuery query)
    {
        var request = new StacSearchRequest
        {
            Collections = query.Collections,
            Ids = query.Ids,
            Bbox = query.Bbox,
            Datetime = query.Datetime,
            Intersects = query.Intersects,
            Query = query.Query,
            Filter = query.Filter,
            FilterLang = query.FilterLang,
            Limit = query.Limit,
            Offset = query.Offset,
            Next = query.Next,
            SortBy = query.SortBy,
            Fields = query.Fields,
        };

        if (query.AdditionalParameters is { Count: > 0 })
        {
            var extra = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var pair in query.AdditionalParameters)
            {
                extra[pair.Key] = JsonSerializer.SerializeToElement(pair.Value);
            }

            request = request with { AdditionalProperties = extra };
        }

        return request;
    }

    /// <inheritdoc />
    public async Task<StacItemCollection> PostSearchAsync(
        StacSearchRequest? request,
        CancellationToken cancellationToken = default)
    {
        var body = await PostJsonStringAsync($"{BasePath}/search", request ?? new StacSearchRequest(), cancellationToken)
            .ConfigureAwait(false);
        return DeserializeItemCollection(body, "Failed to deserialize STAC search response.");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StacItemCollection> GetItemsPagesAsync(
        string collectionId,
        StacItemsQuery? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);

        var page = await GetItemsAsync(collectionId, query, cancellationToken).ConfigureAwait(false);
        yield return page;

        var visitedHrefs = new HashSet<string>(StringComparer.Ordinal);
        var pageCount = 0;
        while (pageCount < MaxAutoPages)
        {
            var nextLink = FindNextLink(page);
            if (nextLink is null)
            {
                yield break;
            }

            if (!visitedHrefs.Add(nextLink.Href))
            {
                yield break;
            }

            var body = await GetNextPageStringAsync(nextLink, cancellationToken).ConfigureAwait(false);
            page = DeserializeItemCollection(body, "Failed to deserialize paged STAC items response.");

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
    public async IAsyncEnumerable<StacItemCollection> SearchPagesAsync(
        StacSearchQuery? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var page = await SearchAsync(query, cancellationToken).ConfigureAwait(false);
        yield return page;

        var visitedHrefs = new HashSet<string>(StringComparer.Ordinal);
        var pageCount = 0;
        while (pageCount < MaxAutoPages)
        {
            var nextLink = FindNextLink(page);
            if (nextLink is null)
            {
                yield break;
            }

            if (!visitedHrefs.Add(nextLink.Href))
            {
                yield break;
            }

            var body = await GetNextPageStringAsync(nextLink, cancellationToken).ConfigureAwait(false);
            page = DeserializeItemCollection(body, "Failed to deserialize paged STAC search response.");

            if (page.Features is null or { Count: 0 })
            {
                yield break;
            }

            yield return page;
            pageCount++;
        }

        throw new InvalidOperationException(
            $"Auto-pagination safety limit reached ({MaxAutoPages} pages). " +
            "Use manual paging with SearchAsync for larger result sets.");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StacItemCollection> PostSearchPagesAsync(
        StacSearchRequest? request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var searchRequest = request ?? new StacSearchRequest();

        // Serialize the original request body once so POST continuation links can merge their
        // continuation token onto it, per the STAC API pagination spec.
        var requestBody = JsonSerializer.SerializeToElement(searchRequest, StacJsonContext.Default.StacSearchRequest);

        var page = await PostSearchAsync(searchRequest, cancellationToken).ConfigureAwait(false);
        yield return page;

        var visitedHrefs = new HashSet<string>(StringComparer.Ordinal);
        var pageCount = 0;
        while (pageCount < MaxAutoPages)
        {
            var nextLink = FindNextLink(page);
            if (nextLink is null)
            {
                yield break;
            }

            if (!visitedHrefs.Add(nextLink.Href))
            {
                yield break;
            }

            var body = await FollowNextLinkAsync(nextLink, requestBody, cancellationToken).ConfigureAwait(false);
            page = DeserializeItemCollection(body, "Failed to deserialize paged STAC search response.");

            if (page.Features is null or { Count: 0 })
            {
                yield break;
            }

            yield return page;
            pageCount++;
        }

        throw new InvalidOperationException(
            $"Auto-pagination safety limit reached ({MaxAutoPages} pages). " +
            "Use manual paging with PostSearchAsync for larger result sets.");
    }

    /// <inheritdoc />
    public async Task<JsonDocument> GetItemsJsonAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(query)}";
        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    /// <inheritdoc />
    public async Task<JsonDocument> GetItemJsonAsync(
        string collectionId,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(itemId)}";
        var body = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    /// <inheritdoc />
    public async Task<JsonDocument> SearchJsonAsync(
        StacSearchQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var body = await GetStringAsync($"{BasePath}/search{BuildQueryString(query)}", cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    /// <inheritdoc />
    public async Task<JsonDocument> PostSearchJsonAsync(
        StacSearchRequest? request,
        CancellationToken cancellationToken = default)
    {
        var body = await PostJsonStringAsync($"{BasePath}/search", request ?? new StacSearchRequest(), cancellationToken)
            .ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> GetItemsRawAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        var url = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/items{BuildQueryString(query)}";
        return await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HttpResponseMessage> SearchRawAsync(
        StacSearchQuery? query = null,
        CancellationToken cancellationToken = default)
        => await _http.GetAsync(CreateRequestUri($"{BasePath}/search{BuildQueryString(query)}"), cancellationToken)
            .ConfigureAwait(false);

    private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    private async Task<string> PostJsonStringAsync(string url, StacSearchRequest request, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(request, StacJsonContext.Default.StacSearchRequest);
        using var content = new ByteArrayContent(body);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var response = await _http.PostAsync(CreateRequestUri(url), content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, responseBody);
        return responseBody;
    }

    private async Task<string> GetNextPageStringAsync(StacLink nextLink, CancellationToken cancellationToken)
    {
        ValidateNextLinkOrigin(nextLink.Href);
        return await GetStringAsync(nextLink.Href, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Follows a STAC pagination link, honoring its advertised HTTP method, headers, and body.
    /// When the link advertises <c>method:POST</c> the continuation body (typically carrying the
    /// continuation token) is merged onto the original search request body and re-POSTed; otherwise
    /// the href is fetched with a plain GET (current behavior).
    /// </summary>
    private async Task<string> FollowNextLinkAsync(
        StacLink nextLink,
        JsonElement originalBody,
        CancellationToken cancellationToken)
    {
        ValidateNextLinkOrigin(nextLink.Href);

        if (!string.Equals(nextLink.Method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return await GetStringAsync(nextLink.Href, cancellationToken).ConfigureAwait(false);
        }

        // STAC API pagination: a POST next-link's body should be merged onto the original
        // request body (link members override). The default merge behavior is to merge unless
        // the link explicitly sets merge:false.
        var mergedBody = nextLink.Merge == false
            ? nextLink.Body ?? originalBody
            : MergeJsonObjects(originalBody, nextLink.Body);

        var payload = JsonSerializer.SerializeToUtf8Bytes(mergedBody, StacJsonContext.Default.JsonElement);
        using var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, CreateRequestUri(nextLink.Href))
        {
            Content = content,
        };

        if (nextLink.Headers is not null)
        {
            foreach (var header in nextLink.Headers)
            {
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        using var response = await _http.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, responseBody);
        return responseBody;
    }

    /// <summary>
    /// Merges the members of <paramref name="overlay"/> onto <paramref name="baseline"/>, with overlay
    /// members overriding. Both are expected to be JSON objects; non-object inputs degrade gracefully.
    /// </summary>
    private static JsonElement MergeJsonObjects(JsonElement baseline, JsonElement? overlay)
    {
        if (overlay is not { ValueKind: JsonValueKind.Object } overlayElement)
        {
            return baseline;
        }

        if (baseline.ValueKind != JsonValueKind.Object)
        {
            return overlayElement;
        }

        var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in baseline.EnumerateObject())
        {
            merged[property.Name] = property.Value;
        }

        foreach (var property in overlayElement.EnumerateObject())
        {
            merged[property.Name] = property.Value;
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var entry in merged)
            {
                writer.WritePropertyName(entry.Key);
                entry.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return JsonSerializer.Deserialize(buffer.ToArray(), StacJsonContext.Default.JsonElement);
    }

    private static StacItemCollection DeserializeItemCollection(string body, string errorMessage)
        => JsonSerializer.Deserialize(body, StacJsonContext.Default.StacItemCollection)
            ?? throw new HonuaStacException(HttpStatusCode.OK, errorMessage, body);

    private static StacLink? FindNextLink(StacItemCollection page)
        => page.Links?.FirstOrDefault(static link =>
            string.Equals(link.Rel, "next", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(link.Href));

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = Honua.Sdk.Abstractions.HonuaProblemDetailsParser.ResolveMessage(
            body, response.ReasonPhrase ?? "STAC request failed", out var problem);
        throw new HonuaStacException(
            response.StatusCode,
            message,
            body,
            problem?.Type,
            problem?.Title,
            problem?.Detail);
    }

    private void ValidateNextLinkOrigin(string nextUrl)
    {
        if (!NextLinkOriginValidator.IsSameOrigin(nextUrl, _http.BaseAddress))
        {
            throw new HonuaStacException(
                HttpStatusCode.BadGateway,
                NextLinkOriginValidator.CrossOriginMessage(nextUrl),
                nextUrl);
        }
    }

    private static string BuildQueryString(StacItemsQuery? query)
    {
        if (query is null)
        {
            return string.Empty;
        }

        var parameters = CreateCommonParameters(query.Limit, query.Offset, query.Next, query.Bbox, query.Datetime);
        AddList(parameters, "ids", query.Ids);
        AddValue(parameters, "filter", query.Filter);
        AddValue(parameters, "filter-lang", query.FilterLang);
        AddValue(parameters, "sortby", query.SortBy);
        AddValue(parameters, "fields", FormatFields(query.Fields));
        AddAdditionalParameters(parameters, query.AdditionalParameters);
        return FormatQueryString(parameters);
    }

    private static string BuildQueryString(StacSearchQuery? query)
    {
        if (query is null)
        {
            return string.Empty;
        }

        var parameters = CreateCommonParameters(query.Limit, query.Offset, query.Next, query.Bbox, query.Datetime);
        AddList(parameters, "ids", query.Ids);
        AddList(parameters, "collections", query.Collections);
        AddValue(parameters, "intersects", FormatJsonElement(query.Intersects));
        AddValue(parameters, "query", FormatJsonElement(query.Query));
        AddValue(parameters, "filter", query.Filter);
        AddValue(parameters, "filter-lang", query.FilterLang);
        AddValue(parameters, "sortby", query.SortBy);
        AddValue(parameters, "fields", FormatFields(query.Fields));
        AddAdditionalParameters(parameters, query.AdditionalParameters);
        return FormatQueryString(parameters);
    }

    private static List<(string Key, string? Value)> CreateCommonParameters(
        int? limit,
        int? offset,
        string? next,
        IReadOnlyList<double>? bbox,
        string? datetime)
    {
        var parameters = new List<(string Key, string? Value)>();

        if (limit.HasValue)
            parameters.Add(("limit", limit.Value.ToString(CultureInfo.InvariantCulture)));

        if (offset.HasValue)
            parameters.Add(("offset", offset.Value.ToString(CultureInfo.InvariantCulture)));

        AddValue(parameters, "next", next);

        if (bbox is { Count: >= 4 })
            parameters.Add(("bbox", string.Join(",", bbox.Select(static d => d.ToString(CultureInfo.InvariantCulture)))));

        AddValue(parameters, "datetime", datetime);
        return parameters;
    }

    private static void AddList(
        List<(string Key, string? Value)> parameters,
        string key,
        IReadOnlyList<string>? values)
    {
        if (values is { Count: > 0 })
        {
            parameters.Add((key, string.Join(",", values)));
        }
    }

    private static void AddValue(List<(string Key, string? Value)> parameters, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add((key, value));
        }
    }

    private static void AddAdditionalParameters(
        List<(string Key, string? Value)> parameters,
        IReadOnlyDictionary<string, string?>? additionalParameters)
    {
        if (additionalParameters is not { Count: > 0 })
        {
            return;
        }

        foreach (var (key, value) in additionalParameters)
        {
            if (!string.IsNullOrWhiteSpace(key) &&
                !parameters.Any(parameter => string.Equals(parameter.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                parameters.Add((key, value));
            }
        }
    }

    private static string FormatQueryString(IReadOnlyList<(string Key, string? Value)> parameters)
    {
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

    private static string? FormatJsonElement(JsonElement? value)
    {
        if (!value.HasValue || value.Value.ValueKind is JsonValueKind.Undefined)
        {
            return null;
        }

        return value.Value.GetRawText();
    }

    private static string? FormatFields(StacFields? fields)
    {
        if (fields is null)
        {
            return null;
        }

        var parts = new List<string>();
        if (fields.Include is { Count: > 0 })
        {
            parts.AddRange(fields.Include.Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        if (fields.Exclude is { Count: > 0 })
        {
            parts.AddRange(fields.Exclude
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => $"-{value}"));
        }

        return parts.Count > 0 ? string.Join(",", parts) : null;
    }

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);
}
