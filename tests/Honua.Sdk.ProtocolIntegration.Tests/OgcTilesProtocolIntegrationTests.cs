// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.OgcFeatures.Tiles.Models;

namespace Honua.Sdk.ProtocolIntegration.Tests;

[Collection(ProtocolIntegrationCollection.Name)]
[Trait("Category", "ProtocolIntegration")]
public sealed class OgcTilesProtocolIntegrationTests(ProtocolIntegrationFixture fixture)
{
    private readonly ProtocolIntegrationFixture _fixture = fixture;

    [ProtocolIntegrationFact]
    public async Task CollectionTiles_ListsTilesetsAndFetchesVectorPngAndEmpty()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var collectionId = _fixture.Options.OgcCollectionId;

        var tilesets = await _fixture.OgcTilesClient.ListCollectionTilesetsAsync(collectionId, timeout.Token)
            .ConfigureAwait(false);
        Assert.NotEmpty(tilesets.Tilesets);
        var tileMatrixSetId = tilesets.Tilesets
            .Select(item => item.TileMatrixSetId)
            .FirstOrDefault(id => string.Equals(id, "WebMercatorQuad", StringComparison.Ordinal))
            ?? tilesets.Tilesets[0].TileMatrixSetId;
        Assert.False(string.IsNullOrWhiteSpace(tileMatrixSetId));

        var vector = await _fixture.OgcTilesClient.GetTileAsync(
            collectionId, tileMatrixSetId, 0, 0, 0, OgcTileFormat.Vector, timeout.Token).ConfigureAwait(false);
        var png = await _fixture.OgcTilesClient.GetTileAsync(
            collectionId, tileMatrixSetId, 0, 0, 0, OgcTileFormat.Png, timeout.Token).ConfigureAwait(false);
        Assert.Equal(vector.IsEmpty, png.IsEmpty);
        if (!vector.IsEmpty)
        {
            Assert.False(vector.Content.IsEmpty);
            Assert.False(png.Content.IsEmpty);
            Assert.Contains("mapbox-vector-tile", vector.MediaType, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("image/png", png.MediaType);
        }

        // A point collection cannot paint every corner of the world at zoom 3.
        // One of those tiles is the server's HTTP 204 empty-tile contract.
        var corners = new (int Row, int Col)[] { (0, 0), (0, 7), (7, 0), (7, 7) };
        var sawEmpty = false;
        foreach (var (row, col) in corners)
        {
            var tile = await _fixture.OgcTilesClient.GetTileAsync(
                collectionId, tileMatrixSetId, 3, row, col, OgcTileFormat.Vector, timeout.Token)
                .ConfigureAwait(false);
            if (tile.IsEmpty)
            {
                Assert.True(tile.Content.IsEmpty);
                sawEmpty = true;
                break;
            }
        }

        Assert.True(sawEmpty, "Expected at least one zoom-3 corner tile to be HTTP 204.");
    }
}
