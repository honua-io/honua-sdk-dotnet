// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Text.Json;
using Honua.Sdk.Admin.Exceptions;

namespace Honua.Sdk.Admin.Geocoding;

/// <summary>
/// HTTP client implementation for the Honua Geocoding REST API (GeoServices-compatible).
/// </summary>
public sealed class HonuaGeocodingClient : IHonuaGeocodingClient
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

        var url = $"{ServicePath}/findAddressCandidates{BuildQuery(queryParams)}";

        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
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

        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
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

        var url = $"{ServicePath}/suggest{BuildQuery(queryParams)}";

        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
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
    public Task<IReadOnlyList<GeocodeResult>> BatchGeocodeAsync(
        IReadOnlyList<string> addresses,
        BatchGeocodeOptions? options = null,
        CancellationToken ct = default)
    {
        throw new NotSupportedException(
            "Batch geocoding (geocodeAddresses) is not yet implemented server-side. " +
            "Use ForwardGeocodeAsync for individual address geocoding.");
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

    private static IReadOnlyDictionary<string, string?> FlattenAttributes(Dictionary<string, object?>? attributes)
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
}
