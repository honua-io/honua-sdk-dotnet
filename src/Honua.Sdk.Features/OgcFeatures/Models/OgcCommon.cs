// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Features.OgcFeatures.Models;

/// <summary>
/// A link in an OGC API response (RFC 8288).
/// </summary>
public sealed class OgcLink
{
    /// <summary>The link target URI.</summary>
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;

    /// <summary>The link relation type.</summary>
    [JsonPropertyName("rel")]
    public string? Rel { get; init; }

    /// <summary>The media type of the linked resource.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>A human-readable title for the link.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

/// <summary>
/// RFC 7807 Problem Details wire model for OGC error responses (internal).
/// </summary>
internal sealed class OgcProblemDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}

/// <summary>
/// Output format for OGC Features queries.
/// </summary>
public enum OgcFeaturesFormat
{
    /// <summary>GeoJSON (default).</summary>
    GeoJson,

    /// <summary>Plain JSON.</summary>
    Json
}
