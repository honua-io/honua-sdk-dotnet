// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Catalogs.Stac.Models;

/// <summary>
/// A link in a STAC response.
/// </summary>
public sealed class StacLink
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

    /// <summary>HTTP method advertised by STAC link extensions.</summary>
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    /// <summary>Headers advertised by STAC link extensions.</summary>
    [JsonPropertyName("headers")]
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Request body advertised by STAC link extensions.</summary>
    [JsonPropertyName("body")]
    public JsonElement? Body { get; init; }

    /// <summary>Whether advertised body content should be merged with caller search parameters.</summary>
    [JsonPropertyName("merge")]
    public bool? Merge { get; init; }

    /// <summary>Additional server-specific or extension link members.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>
/// Spatial and temporal extent for a STAC collection.
/// </summary>
public sealed class StacExtent
{
    /// <summary>Spatial extent advertised by the collection.</summary>
    [JsonPropertyName("spatial")]
    public StacSpatialExtent? Spatial { get; init; }

    /// <summary>Temporal extent advertised by the collection.</summary>
    [JsonPropertyName("temporal")]
    public StacTemporalExtent? Temporal { get; init; }
}

/// <summary>
/// Spatial extent metadata.
/// </summary>
public sealed class StacSpatialExtent
{
    /// <summary>Bounding boxes advertised by the collection.</summary>
    [JsonPropertyName("bbox")]
    public IReadOnlyList<IReadOnlyList<double>>? Bbox { get; init; }

    /// <summary>Coordinate reference system of the bounding boxes, when advertised by an extension.</summary>
    [JsonPropertyName("crs")]
    public string? Crs { get; init; }
}

/// <summary>
/// Temporal extent metadata.
/// </summary>
public sealed class StacTemporalExtent
{
    /// <summary>Temporal intervals advertised by the collection.</summary>
    [JsonPropertyName("interval")]
    public IReadOnlyList<IReadOnlyList<string?>>? Interval { get; init; }

    /// <summary>Temporal reference system identifier, when advertised by an extension.</summary>
    [JsonPropertyName("trs")]
    public string? Trs { get; init; }
}

/// <summary>
/// RFC 7807 Problem Details wire model for STAC error responses.
/// </summary>
internal sealed class StacProblemDetails
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
