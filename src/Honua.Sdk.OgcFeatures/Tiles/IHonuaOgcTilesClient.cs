// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.OgcFeatures.Tiles.Models;

namespace Honua.Sdk.OgcFeatures.Tiles;

/// <summary>
/// Client for the Honua OGC API Tiles surface (<c>/ogc/tiles</c>).
/// </summary>
public interface IHonuaOgcTilesClient
{
    /// <summary>
    /// Lists the tilesets for a collection
    /// (<c>GET /ogc/tiles/collections/{collectionId}/tiles</c>).
    /// </summary>
    /// <param name="collectionId">The OGC collection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tileset list.</returns>
    Task<OgcTileSetsList> ListCollectionTilesetsAsync(
        string collectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches one tile
    /// (<c>GET /ogc/tiles/collections/{collectionId}/tiles/{tileMatrixSetId}/{tileMatrix}/{tileRow}/{tileCol}</c>).
    /// </summary>
    /// <param name="collectionId">The OGC collection identifier.</param>
    /// <param name="tileMatrixSetId">The tile matrix set identifier, such as <c>WebMercatorQuad</c>.</param>
    /// <param name="tileMatrix">The tile matrix (zoom) identifier.</param>
    /// <param name="tileRow">The tile row.</param>
    /// <param name="tileCol">The tile column.</param>
    /// <param name="format">
    /// Tile encoding. <see cref="OgcTileFormat.Vector"/> is the default and does not
    /// send <c>f</c>. <see cref="OgcTileFormat.Png"/> sends <c>f=png</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The tile. HTTP 204 is an empty tile (<see cref="OgcTile.IsEmpty"/>), not an error.
    /// </returns>
    Task<OgcTile> GetTileAsync(
        string collectionId,
        string tileMatrixSetId,
        int tileMatrix,
        int tileRow,
        int tileCol,
        OgcTileFormat format = OgcTileFormat.Vector,
        CancellationToken cancellationToken = default);
}
