// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Features.OgcFeatures.Models;

/// <summary>
/// An OGC API collection (feature type).
/// </summary>
public sealed class OgcCollection
{
    /// <summary>The collection identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>A human-readable title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>A description of the collection.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>The spatial and temporal extent of the collection.</summary>
    [JsonPropertyName("extent")]
    public OgcCollectionExtent? Extent { get; init; }

    /// <summary>Coordinate reference systems supported by the collection.</summary>
    [JsonPropertyName("crs")]
    public IReadOnlyList<string>? Crs { get; init; }

    /// <summary>The CRS used for data storage.</summary>
    [JsonPropertyName("storageCrs")]
    public string? StorageCrs { get; init; }

    /// <summary>Navigation links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcLink>? Links { get; init; }
}

/// <summary>
/// Spatial and temporal extent of an OGC collection.
/// </summary>
public sealed class OgcCollectionExtent
{
    /// <summary>Spatial extent.</summary>
    [JsonPropertyName("spatial")]
    public OgcSpatialExtent? Spatial { get; init; }

    /// <summary>Temporal extent.</summary>
    [JsonPropertyName("temporal")]
    public OgcTemporalExtent? Temporal { get; init; }
}

/// <summary>
/// Spatial extent of an OGC collection.
/// </summary>
public sealed class OgcSpatialExtent
{
    /// <summary>Bounding boxes (each is [minLon, minLat, maxLon, maxLat]).</summary>
    [JsonPropertyName("bbox")]
    public IReadOnlyList<IReadOnlyList<double>>? Bbox { get; init; }

    /// <summary>CRS of the bounding boxes.</summary>
    [JsonPropertyName("crs")]
    public string? Crs { get; init; }
}

/// <summary>
/// Temporal extent of an OGC collection.
/// </summary>
public sealed class OgcTemporalExtent
{
    /// <summary>Time intervals (each is [start, end] as ISO 8601 strings or null for open-ended).</summary>
    [JsonPropertyName("interval")]
    public IReadOnlyList<IReadOnlyList<string?>>? Interval { get; init; }

    /// <summary>Temporal reference system.</summary>
    [JsonPropertyName("trs")]
    public string? Trs { get; init; }
}

/// <summary>
/// Response wrapper for the collections list endpoint.
/// </summary>
internal sealed class OgcCollectionsResponse
{
    [JsonPropertyName("collections")]
    public List<OgcCollection>? Collections { get; set; }

    [JsonPropertyName("links")]
    public List<OgcLink>? Links { get; set; }
}
