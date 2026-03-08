// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Geocoding;

// ── SDK-facing models (provider-agnostic) ────────────────────────────────

/// <summary>
/// A single geocode result returned from a forward geocode operation.
/// </summary>
/// <param name="Address">The matched address string.</param>
/// <param name="Latitude">The latitude (Y) of the matched location.</param>
/// <param name="Longitude">The longitude (X) of the matched location.</param>
/// <param name="Score">A confidence score for the match (0–100).</param>
/// <param name="Attributes">Additional attributes returned by the geocoding service.</param>
public sealed record GeocodeResult(
    string Address,
    double Latitude,
    double Longitude,
    double Score,
    IReadOnlyDictionary<string, string?> Attributes);

/// <summary>
/// The result of a reverse geocode operation.
/// </summary>
/// <param name="Address">The matched address string.</param>
/// <param name="Latitude">The latitude (Y) of the matched location.</param>
/// <param name="Longitude">The longitude (X) of the matched location.</param>
/// <param name="Attributes">Additional address attributes returned by the geocoding service.</param>
public sealed record ReverseGeocodeResult(
    string Address,
    double Latitude,
    double Longitude,
    IReadOnlyDictionary<string, string?> Attributes);

/// <summary>
/// A suggestion returned from the suggest/autocomplete endpoint.
/// </summary>
/// <param name="Text">The suggestion display text.</param>
/// <param name="MagicKey">An opaque key that can be passed back to forward geocode for faster lookup.</param>
/// <param name="IsCollection">Whether the suggestion represents a collection of results.</param>
public sealed record GeocodeSuggestion(
    string Text,
    string MagicKey,
    bool IsCollection);

// ── Options records ──────────────────────────────────────────────────────

/// <summary>
/// Options for the forward geocode operation.
/// </summary>
public sealed record ForwardGeocodeOptions
{
    /// <summary>
    /// Maximum number of candidates to return. Defaults to 5.
    /// </summary>
    public int? MaxResults { get; init; }

    /// <summary>
    /// Restrict results to specific country codes (ISO 3166-1 alpha-3).
    /// </summary>
    public IReadOnlyList<string>? CountryCodes { get; init; }

    /// <summary>
    /// The spatial reference WKID for the output geometry. Defaults to 4326 (WGS 84).
    /// </summary>
    public int? SpatialReferenceWkid { get; init; }

    /// <summary>
    /// An opaque key from a prior suggest call to accelerate the lookup.
    /// </summary>
    public string? MagicKey { get; init; }
}

/// <summary>
/// Options for the reverse geocode operation.
/// </summary>
public sealed record ReverseGeocodeOptions
{
    /// <summary>
    /// The spatial reference WKID for the output geometry. Defaults to 4326 (WGS 84).
    /// </summary>
    public int? SpatialReferenceWkid { get; init; }
}

/// <summary>
/// Options for the suggest/autocomplete operation.
/// </summary>
public sealed record SuggestOptions
{
    /// <summary>
    /// Maximum number of suggestions to return. Defaults to 5.
    /// </summary>
    public int? MaxResults { get; init; }

    /// <summary>
    /// Restrict suggestions to specific country codes (ISO 3166-1 alpha-3).
    /// </summary>
    public IReadOnlyList<string>? CountryCodes { get; init; }
}

/// <summary>
/// Options for the batch geocode operation.
/// </summary>
public sealed record BatchGeocodeOptions
{
    /// <summary>
    /// The spatial reference WKID for the output geometry. Defaults to 4326 (WGS 84).
    /// </summary>
    public int? SpatialReferenceWkid { get; init; }

    /// <summary>
    /// Restrict results to specific country codes (ISO 3166-1 alpha-3).
    /// </summary>
    public IReadOnlyList<string>? CountryCodes { get; init; }
}

// ── Raw GeoServices wire models (internal) ───────────────────────────────

/// <summary>
/// Raw GeoServices spatial reference object.
/// </summary>
internal sealed class GeoServicesSpatialReference
{
    [JsonPropertyName("wkid")]
    public int Wkid { get; set; }

    [JsonPropertyName("latestWkid")]
    public int LatestWkid { get; set; }
}

/// <summary>
/// Raw GeoServices point location.
/// </summary>
internal sealed class GeoServicesLocation
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("spatialReference")]
    public GeoServicesSpatialReference? SpatialReference { get; set; }
}

/// <summary>
/// Raw GeoServices candidate from findAddressCandidates.
/// </summary>
internal sealed class GeoServicesCandidate
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public GeoServicesLocation? Location { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, object?>? Attributes { get; set; }
}

/// <summary>
/// Raw GeoServices response from findAddressCandidates.
/// </summary>
internal sealed class GeoServicesFindAddressCandidatesResponse
{
    [JsonPropertyName("spatialReference")]
    public GeoServicesSpatialReference? SpatialReference { get; set; }

    [JsonPropertyName("candidates")]
    public List<GeoServicesCandidate>? Candidates { get; set; }
}

/// <summary>
/// Raw GeoServices address from reverseGeocode.
/// </summary>
internal sealed class GeoServicesReverseAddress
{
    [JsonPropertyName("Match_addr")]
    public string MatchAddr { get; set; } = string.Empty;
}

/// <summary>
/// Raw GeoServices response from reverseGeocode.
/// </summary>
internal sealed class GeoServicesReverseGeocodeResponse
{
    [JsonPropertyName("address")]
    public GeoServicesReverseAddress? Address { get; set; }

    [JsonPropertyName("location")]
    public GeoServicesLocation? Location { get; set; }
}

/// <summary>
/// Raw GeoServices suggestion from suggest.
/// </summary>
internal sealed class GeoServicesSuggestion
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("magicKey")]
    public string MagicKey { get; set; } = string.Empty;

    [JsonPropertyName("isCollection")]
    public bool IsCollection { get; set; }
}

/// <summary>
/// Raw GeoServices response from suggest.
/// </summary>
internal sealed class GeoServicesSuggestResponse
{
    [JsonPropertyName("suggestions")]
    public List<GeoServicesSuggestion>? Suggestions { get; set; }
}
