// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures.Tiles.Models;

/// <summary>
/// Encoding of an OGC API Tiles tile response.
/// </summary>
public enum OgcTileFormat
{
    /// <summary>
    /// Mapbox Vector Tile. The request omits <c>f</c> and sends
    /// <c>Accept: application/vnd.mapbox-vector-tile</c>.
    /// </summary>
    Vector,

    /// <summary>
    /// PNG raster tile. The request sends <c>f=png</c> and
    /// <c>Accept: image/png</c>.
    /// </summary>
    Png,
}

/// <summary>
/// One fetched tile. <see cref="IsEmpty"/> is <c>true</c> only for HTTP 204,
/// which is the server's empty-tile contract. A 200 with an empty body is a
/// tile whose <see cref="Content"/> happens to be empty.
/// </summary>
public sealed class OgcTile
{
    /// <summary>Whether the server reported the tile as empty (HTTP 204).</summary>
    public bool IsEmpty { get; init; }

    /// <summary>Media type from the response, when the server sent one.</summary>
    public string? MediaType { get; init; }

    /// <summary>Tile bytes. Empty when <see cref="IsEmpty"/> is <c>true</c>.</summary>
    public ReadOnlyMemory<byte> Content { get; init; }
}

/// <summary>
/// OGC API Tiles tileset list for one collection
/// (<c>GET /ogc/tiles/collections/{collectionId}/tiles</c>).
/// </summary>
public sealed class OgcTileSetsList
{
    /// <summary>Tilesets advertised for the collection.</summary>
    [JsonPropertyName("tilesets")]
    public IReadOnlyList<OgcTileSetItem> Tilesets { get; init; } = [];

    /// <summary>Links to related resources.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcLink>? Links { get; init; }
}

/// <summary>
/// One tileset in an OGC API Tiles collection tileset list.
/// </summary>
public sealed class OgcTileSetItem
{
    /// <summary>Human-readable title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Tileset data type, such as <c>vector</c> or <c>map</c>.</summary>
    [JsonPropertyName("dataType")]
    public string DataType { get; init; } = string.Empty;

    /// <summary>Coordinate reference system of the tileset.</summary>
    [JsonPropertyName("crs")]
    public string Crs { get; init; } = string.Empty;

    /// <summary>Tile matrix set identifier used to request tiles.</summary>
    [JsonPropertyName("tileMatrixSetId")]
    public string TileMatrixSetId { get; init; } = string.Empty;

    /// <summary>Optional URI of the tile matrix set definition, as the OGC document carries it.</summary>
    [JsonPropertyName("tileMatrixSetURI")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Mirrors the OGC API Tiles JSON contract, which carries tileMatrixSetURI as a verbatim string.")]
    public string? TileMatrixSetUri { get; init; }

    /// <summary>Optional per-matrix limits.</summary>
    [JsonPropertyName("tileMatrixSetLimits")]
    public IReadOnlyList<OgcTileMatrixSetLimit>? TileMatrixSetLimits { get; init; }

    /// <summary>Links to the tileset and its tiles.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcLink> Links { get; init; } = [];
}

/// <summary>
/// Row and column limits for one tile matrix inside a tileset.
/// </summary>
public sealed class OgcTileMatrixSetLimit
{
    /// <summary>Tile matrix identifier (zoom level for the built-in grid sets).</summary>
    [JsonPropertyName("tileMatrix")]
    public string TileMatrix { get; init; } = string.Empty;

    /// <summary>Lowest tile row in this matrix.</summary>
    [JsonPropertyName("minTileRow")]
    public int MinTileRow { get; init; }

    /// <summary>Highest tile row in this matrix.</summary>
    [JsonPropertyName("maxTileRow")]
    public int MaxTileRow { get; init; }

    /// <summary>Lowest tile column in this matrix.</summary>
    [JsonPropertyName("minTileCol")]
    public int MinTileCol { get; init; }

    /// <summary>Highest tile column in this matrix.</summary>
    [JsonPropertyName("maxTileCol")]
    public int MaxTileCol { get; init; }
}
