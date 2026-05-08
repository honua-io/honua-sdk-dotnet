// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.OgcRecords.Models;

/// <summary>
/// A link in an OGC API Records response.
/// </summary>
public sealed class OgcRecordsLink
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

    /// <summary>The language of the linked resource, when advertised.</summary>
    [JsonPropertyName("hreflang")]
    public string? HrefLang { get; init; }
}

/// <summary>
/// Spatial and temporal extent for a Records collection.
/// </summary>
public sealed class OgcRecordsExtent
{
    /// <summary>Spatial extent advertised by the collection.</summary>
    [JsonPropertyName("spatial")]
    public OgcRecordsSpatialExtent? Spatial { get; init; }

    /// <summary>Temporal extent advertised by the collection.</summary>
    [JsonPropertyName("temporal")]
    public OgcRecordsTemporalExtent? Temporal { get; init; }
}

/// <summary>
/// Spatial extent metadata.
/// </summary>
public sealed class OgcRecordsSpatialExtent
{
    /// <summary>Bounding boxes advertised by the collection.</summary>
    [JsonPropertyName("bbox")]
    public IReadOnlyList<IReadOnlyList<double>>? Bbox { get; init; }

    /// <summary>Coordinate reference system of the bounding boxes.</summary>
    [JsonPropertyName("crs")]
    public string? Crs { get; init; }
}

/// <summary>
/// Temporal extent metadata.
/// </summary>
public sealed class OgcRecordsTemporalExtent
{
    /// <summary>Temporal intervals advertised by the collection.</summary>
    [JsonPropertyName("interval")]
    public IReadOnlyList<IReadOnlyList<string?>>? Interval { get; init; }

    /// <summary>Temporal reference system identifier.</summary>
    [JsonPropertyName("trs")]
    public string? Trs { get; init; }
}

/// <summary>
/// Output format for OGC API Records requests.
/// </summary>
public enum OgcRecordsFormat
{
    /// <summary>JSON or GeoJSON representation.</summary>
    Json,

    /// <summary>GeoJSON representation.</summary>
    GeoJson,

    /// <summary>HTML representation.</summary>
    Html
}

/// <summary>
/// RFC 7807 Problem Details wire model for OGC error responses.
/// </summary>
internal sealed class OgcRecordsProblemDetails
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
