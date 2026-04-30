// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Collections.ObjectModel;
using System.Text.Json;
using Honua.Sdk.Admin.Exceptions;

namespace Honua.Sdk.Admin.Geocoding;

/// <summary>
/// HTTP client implementation for the Honua Geocoding REST API (GeoServices-compatible).
/// </summary>
public sealed class HonuaGeocodingClient : IHonuaBatchGeocodingClient
{
    private const string DefaultLocatorName = "World";
    private const int DefaultSpatialReferenceWkid = 4326;
    private const int DefaultMaxResults = 5;

    private readonly HttpClient _http;
    private readonly string _locatorName;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaGeocodingClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth handlers.</param>
    public HonuaGeocodingClient(HttpClient httpClient)
        : this(httpClient, DefaultLocatorName)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaGeocodingClient"/> class
    /// with a custom locator name.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth handlers.</param>
    /// <param name="locatorName">The geocode locator name. Defaults to "World".</param>
    public HonuaGeocodingClient(HttpClient httpClient, string locatorName)
    {
        _http = httpClient;
        _locatorName = locatorName;
    }

    private string ServicePath => $"/rest/services/{Uri.EscapeDataString(_locatorName)}/GeocodeServer";

    /// <inheritdoc />
    public async Task<IReadOnlyList<GeocodeResult>> ForwardGeocodeAsync(
        string address,
        ForwardGeocodeOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        var wkid = options?.SpatialReferenceWkid ?? DefaultSpatialReferenceWkid;
        var max = options?.MaxResults ?? DefaultMaxResults;

        var queryParams = new List<(string Key, string? Value)>
        {
            ("singleLine", address),
            ("maxLocations", max.ToString(CultureInfo.InvariantCulture)),
            ("outSR", wkid.ToString(CultureInfo.InvariantCulture)),
            ("f", "json"),
        };

        if (options?.MagicKey is not null)
        {
            queryParams.Add(("magicKey", options.MagicKey));
        }

        if (options?.CountryCodes is { Count: > 0 })
        {
            queryParams.Add(("countryCode", string.Join(",", options.CountryCodes)));
        }

        AddCommonSearchParameters(
            queryParams,
            options?.Location,
            options?.SearchExtent,
            options?.Categories,
            options?.OutFields);

        var url = $"{ServicePath}/findAddressCandidates{BuildQuery(queryParams)}";

        using var response = await _http.GetAsync(CreateRequestUri(url), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        var result = JsonSerializer.Deserialize(body, GeocodingJsonContext.Default.GeoServicesFindAddressCandidatesResponse);
        if (result?.Candidates is null or { Count: 0 })
        {
            return [];
        }

        return result.Candidates.Select(c => new GeocodeResult(
            Address: c.Address,
            Latitude: c.Location?.Y ?? 0,
            Longitude: c.Location?.X ?? 0,
            Score: c.Score,
            Attributes: FlattenAttributes(c.Attributes)
        )).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<ReverseGeocodeResult?> ReverseGeocodeAsync(
        double latitude,
        double longitude,
        ReverseGeocodeOptions? options = null,
        CancellationToken ct = default)
    {
        var wkid = options?.SpatialReferenceWkid ?? DefaultSpatialReferenceWkid;

        // GeoServices reverseGeocode expects location as x,y (longitude,latitude)
        var locationValue = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1}",
            longitude,
            latitude);

        var url = $"{ServicePath}/reverseGeocode{BuildQuery(
            ("location", locationValue),
            ("outSR", wkid.ToString(CultureInfo.InvariantCulture)),
            ("f", "json"))}";

        using var response = await _http.GetAsync(CreateRequestUri(url), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        var result = JsonSerializer.Deserialize(body, GeocodingJsonContext.Default.GeoServicesReverseGeocodeResponse);
        if (result?.Address is null || result.Location is null)
        {
            return null;
        }

        // Build attributes from the raw address JSON by re-parsing the address object
        var attributes = new Dictionary<string, string?>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("address", out var addrElement) &&
                addrElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in addrElement.EnumerateObject())
                {
                    attributes[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : prop.Value.ToString();
                }
            }
        }
        catch (JsonException)
        {
            // If we cannot parse additional attributes, continue with empty dictionary
        }

        return new ReverseGeocodeResult(
            Address: result.Address.MatchAddr,
            Latitude: result.Location.Y,
            Longitude: result.Location.X,
            Attributes: attributes);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GeocodeSuggestion>> SuggestAsync(
        string text,
        SuggestOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var max = options?.MaxResults ?? DefaultMaxResults;

        var queryParams = new List<(string Key, string? Value)>
        {
            ("text", text),
            ("maxSuggestions", max.ToString(CultureInfo.InvariantCulture)),
            ("f", "json"),
        };

        if (options?.CountryCodes is { Count: > 0 })
        {
            queryParams.Add(("countryCode", string.Join(",", options.CountryCodes)));
        }

        AddCommonSearchParameters(
            queryParams,
            options?.Location,
            options?.SearchExtent,
            options?.Categories,
            outFields: null);

        var url = $"{ServicePath}/suggest{BuildQuery(queryParams)}";

        using var response = await _http.GetAsync(CreateRequestUri(url), ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        var result = JsonSerializer.Deserialize(body, GeocodingJsonContext.Default.GeoServicesSuggestResponse);
        if (result?.Suggestions is null or { Count: 0 })
        {
            return [];
        }

        return result.Suggestions.Select(s => new GeocodeSuggestion(
            Text: s.Text,
            MagicKey: s.MagicKey,
            IsCollection: s.IsCollection
        )).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GeocodeResult>> BatchGeocodeAsync(
        IReadOnlyList<string> addresses,
        BatchGeocodeOptions? options = null,
        CancellationToken ct = default)
    {
        var detailedResults = await BatchGeocodeDetailedAsync(addresses, options, ct).ConfigureAwait(false);
        return detailedResults
            .Where(result => result.Result is not null)
            .Select(result => result.Result!)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BatchGeocodeResult>> BatchGeocodeDetailedAsync(
        IReadOnlyList<string> addresses,
        BatchGeocodeOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        if (addresses.Count == 0)
        {
            return [];
        }

        var payload = CreateBatchRequest(addresses);
        var queryParams = new List<KeyValuePair<string, string>>
        {
            new(
                "addresses",
                JsonSerializer.Serialize(payload, GeocodingJsonContext.Default.GeoServicesBatchGeocodeRequest)),
            new("f", "json"),
        };

        var wkid = options?.SpatialReferenceWkid ?? DefaultSpatialReferenceWkid;
        queryParams.Add(new KeyValuePair<string, string>("outSR", wkid.ToString(CultureInfo.InvariantCulture)));

        if (options?.CountryCodes is { Count: > 0 })
        {
            queryParams.Add(new KeyValuePair<string, string>("sourceCountry", string.Join(",", options.CountryCodes)));
        }

        if (options?.SearchExtent is { } searchExtent)
        {
            queryParams.Add(new KeyValuePair<string, string>("searchExtent", FormatExtent(searchExtent)));
        }

        if (JoinCsv(options?.Categories) is { } category)
        {
            queryParams.Add(new KeyValuePair<string, string>("category", category));
        }

        if (JoinCsv(options?.OutFields) is { } outFields)
        {
            queryParams.Add(new KeyValuePair<string, string>("outFields", outFields));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            CreateRequestUri($"{ServicePath}/geocodeAddresses"))
        {
            Content = new FormUrlEncodedContent(queryParams)
        };

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, body).ConfigureAwait(false);

        var result = JsonSerializer.Deserialize(body, GeocodingJsonContext.Default.GeoServicesBatchGeocodeResponse);
        return MapBatchResults(addresses, result);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            // GeoServices may return 200 with an error payload; check for "error" in body
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                        errorElement.ValueKind == JsonValueKind.Object)
                    {
                        var message = "Geocoding service returned an error.";
                        if (errorElement.TryGetProperty("message", out var msgProp) &&
                            msgProp.ValueKind == JsonValueKind.String)
                        {
                            message = msgProp.GetString() ?? message;
                        }

                        var code = response.StatusCode;
                        if (errorElement.TryGetProperty("code", out var codeProp) &&
                            codeProp.TryGetInt32(out var errorCode))
                        {
                            code = (System.Net.HttpStatusCode)errorCode;
                        }

                        throw new HonuaAdminApiException(code, message, body);
                    }
                }
                catch (JsonException)
                {
                    // Not JSON, ignore
                }
            }

            return;
        }

        var errorMessage = TryExtractErrorMessage(body) ?? response.ReasonPhrase ?? "Geocoding request failed";
        throw new HonuaAdminApiException(response.StatusCode, errorMessage, body);
    }

    private static string? TryExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);

            // GeoServices error format: { "error": { "message": "..." } }
            if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.Object &&
                errorElement.TryGetProperty("message", out var msg) &&
                msg.ValueKind == JsonValueKind.String)
            {
                return msg.GetString();
            }

            // Fallback: top-level message
            if (doc.RootElement.TryGetProperty("message", out var topMsg) &&
                topMsg.ValueKind == JsonValueKind.String)
            {
                return topMsg.GetString();
            }
        }
        catch (JsonException)
        {
            // Not JSON
        }

        return null;
    }

    private static Dictionary<string, string?> FlattenAttributes(Dictionary<string, object?>? attributes)
    {
        if (attributes is null or { Count: 0 })
        {
            return new Dictionary<string, string?>();
        }

        var result = new Dictionary<string, string?>(attributes.Count);
        foreach (var (key, value) in attributes)
        {
            result[key] = value switch
            {
                null => null,
                JsonElement je => je.ValueKind == JsonValueKind.Null ? null : je.ToString(),
                _ => value.ToString(),
            };
        }

        return result;
    }

    private static void AddCommonSearchParameters(
        List<(string Key, string? Value)> queryParams,
        GeocodePoint? location,
        GeocodeExtent? searchExtent,
        IReadOnlyList<string>? categories,
        IReadOnlyList<string>? outFields)
    {
        if (location is not null)
        {
            queryParams.Add(("location", FormatPoint(location)));
        }

        if (searchExtent is not null)
        {
            queryParams.Add(("searchExtent", FormatExtent(searchExtent)));
        }

        if (JoinCsv(categories) is { } category)
        {
            queryParams.Add(("category", category));
        }

        if (JoinCsv(outFields) is { } fields)
        {
            queryParams.Add(("outFields", fields));
        }
    }

    private static string? JoinCsv(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var normalized = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();
        return normalized.Length == 0 ? null : string.Join(",", normalized);
    }

    private static string FormatPoint(GeocodePoint point)
    {
        if (!point.SpatialReferenceWkid.HasValue)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0},{1}", point.X, point.Y);
        }

        var requestPoint = new GeoServicesRequestPoint
        {
            X = point.X,
            Y = point.Y,
            SpatialReference = new GeoServicesRequestSpatialReference { Wkid = point.SpatialReferenceWkid.Value }
        };
        return JsonSerializer.Serialize(requestPoint, GeocodingJsonContext.Default.GeoServicesRequestPoint);
    }

    private static string FormatExtent(GeocodeExtent extent)
    {
        if (!extent.SpatialReferenceWkid.HasValue)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3}",
                extent.XMin,
                extent.YMin,
                extent.XMax,
                extent.YMax);
        }

        var requestExtent = new GeoServicesRequestExtent
        {
            XMin = extent.XMin,
            YMin = extent.YMin,
            XMax = extent.XMax,
            YMax = extent.YMax,
            SpatialReference = new GeoServicesRequestSpatialReference { Wkid = extent.SpatialReferenceWkid.Value }
        };
        return JsonSerializer.Serialize(requestExtent, GeocodingJsonContext.Default.GeoServicesRequestExtent);
    }

    private static GeoServicesBatchGeocodeRequest CreateBatchRequest(IReadOnlyList<string> addresses)
    {
        var request = new GeoServicesBatchGeocodeRequest();
        for (var index = 0; index < addresses.Count; index++)
        {
            var address = addresses[index];
            if (address is null)
            {
                throw new ArgumentException("Batch geocode addresses cannot contain null values.", nameof(addresses));
            }

            request.Records.Add(new GeoServicesBatchAddressRecord
            {
                Attributes = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["OBJECTID"] = index + 1,
                    ["SingleLine"] = address
                }
            });
        }

        return request;
    }

    private static ReadOnlyCollection<BatchGeocodeResult> MapBatchResults(
        IReadOnlyList<string> addresses,
        GeoServicesBatchGeocodeResponse? response)
    {
        var byInputId = new Dictionary<int, BatchGeocodeResult>();
        if (response?.Locations is not null)
        {
            foreach (var location in response.Locations)
            {
                var attributes = FlattenAttributes(location.Attributes);
                var inputId = ReadInputId(attributes) ?? byInputId.Count + 1;
                var status = attributes.TryGetValue("Status", out var statusValue) ? statusValue : null;
                var inputAddress = inputId > 0 && inputId <= addresses.Count ? addresses[inputId - 1] : string.Empty;
                var result = CreateGeocodeResult(location, attributes, status);
                byInputId[inputId] = new BatchGeocodeResult(
                    inputId,
                    inputAddress,
                    status,
                    result,
                    result is null ? CreateBatchFailureMessage(status) : null,
                    attributes);
            }
        }

        var results = new List<BatchGeocodeResult>(addresses.Count);
        for (var index = 0; index < addresses.Count; index++)
        {
            var inputId = index + 1;
            if (byInputId.TryGetValue(inputId, out var batchResult))
            {
                results.Add(batchResult);
                continue;
            }

            results.Add(new BatchGeocodeResult(
                inputId,
                addresses[index],
                Status: null,
                Result: null,
                ErrorMessage: "No candidate returned for this input address.",
                Attributes: new Dictionary<string, string?>()));
        }

        return results.AsReadOnly();
    }

    private static GeocodeResult? CreateGeocodeResult(
        GeoServicesBatchLocation location,
        Dictionary<string, string?> attributes,
        string? status)
    {
        if (location.Location is null ||
            string.Equals(status, "U", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new GeocodeResult(
            Address: location.Address,
            Latitude: location.Location.Y,
            Longitude: location.Location.X,
            Score: location.Score,
            Attributes: attributes);
    }

    private static int? ReadInputId(Dictionary<string, string?> attributes)
    {
        if (attributes.TryGetValue("ResultID", out var resultId) &&
            int.TryParse(resultId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string CreateBatchFailureMessage(string? status)
        => string.IsNullOrWhiteSpace(status)
            ? "No candidate returned for this input address."
            : $"Geocoding service returned status '{status}' for this input address.";

    private static string BuildQuery(params ReadOnlySpan<(string Key, string? Value)> parameters)
    {
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

    private static string BuildQuery(List<(string Key, string? Value)> parameters)
    {
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

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);
}
