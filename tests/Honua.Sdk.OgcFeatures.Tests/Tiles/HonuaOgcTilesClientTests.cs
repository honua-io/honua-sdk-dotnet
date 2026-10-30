// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using Honua.Sdk.OgcFeatures.Exceptions;
using Honua.Sdk.OgcFeatures.Tiles.Models;
using Honua.Sdk.OgcFeatures.Tests.Fixtures;

namespace Honua.Sdk.OgcFeatures.Tests.Tiles;

public class HonuaOgcTilesClientTests
{
    [Fact]
    public async Task ListCollectionTilesetsAsync_ReturnsTilesets()
    {
        var json = """
        {
            "tilesets": [
                {
                    "title": "WebMercatorQuad",
                    "dataType": "vector",
                    "crs": "http://www.opengis.net/def/crs/EPSG/0/3857",
                    "tileMatrixSetId": "WebMercatorQuad",
                    "tileMatrixSetURI": "http://www.opengis.net/def/tilematrixset/OGC/1.0/WebMercatorQuad",
                    "links": [
                        { "href": "/ogc/tiles/collections/segments/tiles/WebMercatorQuad", "rel": "self", "type": "application/json" }
                    ]
                }
            ],
            "links": [
                { "href": "/ogc/tiles/collections/segments/tiles", "rel": "self", "type": "application/json" }
            ]
        }
        """;
        HttpRequestMessage? captured = null;
        var client = TestHelpers.CreateOgcTilesClient(req =>
        {
            captured = req;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var result = await client.ListCollectionTilesetsAsync("segments");

        Assert.Single(result.Tilesets);
        Assert.Equal("vector", result.Tilesets[0].DataType);
        Assert.Equal("WebMercatorQuad", result.Tilesets[0].TileMatrixSetId);
        Assert.Equal("http://www.opengis.net/def/crs/EPSG/0/3857", result.Tilesets[0].Crs);
        Assert.Contains("tilematrixset", result.Tilesets[0].TileMatrixSetUri, StringComparison.Ordinal);
        Assert.Single(result.Tilesets[0].Links);
        Assert.NotNull(result.Links);
        Assert.NotNull(captured);
        Assert.Equal("/ogc/tiles/collections/segments/tiles", captured!.RequestUri!.PathAndQuery);
        Assert.Contains(captured.Headers.Accept, header => header.MediaType == "application/json");
    }

    [Fact]
    public async Task GetTileAsync_Vector_OmitsFormatAndSendsVectorAccept()
    {
        var payload = new byte[] { 0x1a, 0x04 };
        HttpRequestMessage? captured = null;
        var client = TestHelpers.CreateOgcTilesClient(req =>
        {
            captured = req;
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.mapbox-vector-tile");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });

        var result = await client.GetTileAsync("segments", "WebMercatorQuad", 2, 1, 1);

        Assert.False(result.IsEmpty);
        Assert.True(payload.AsSpan().SequenceEqual(result.Content.Span));
        Assert.Equal("application/vnd.mapbox-vector-tile", result.MediaType);
        Assert.NotNull(captured);
        Assert.Equal("/ogc/tiles/collections/segments/tiles/WebMercatorQuad/2/1/1", captured!.RequestUri!.PathAndQuery);
        Assert.DoesNotContain("f=", captured.RequestUri.Query, StringComparison.Ordinal);
        Assert.Contains(
            captured.Headers.Accept,
            header => header.MediaType == "application/vnd.mapbox-vector-tile");
    }

    [Fact]
    public async Task GetTileAsync_Png_SendsFormatAndPngAccept()
    {
        var payload = Encoding.ASCII.GetBytes("png");
        HttpRequestMessage? captured = null;
        var client = TestHelpers.CreateOgcTilesClient(req =>
        {
            captured = req;
            var content = new ByteArrayContent(payload);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });

        var result = await client.GetTileAsync("segments", "WebMercatorQuad", 2, 1, 1, OgcTileFormat.Png);

        Assert.False(result.IsEmpty);
        Assert.True(payload.AsSpan().SequenceEqual(result.Content.Span));
        Assert.Equal("image/png", result.MediaType);
        Assert.NotNull(captured);
        Assert.Equal("/ogc/tiles/collections/segments/tiles/WebMercatorQuad/2/1/1?f=png", captured!.RequestUri!.PathAndQuery);
        Assert.Contains(captured.Headers.Accept, header => header.MediaType == "image/png");
    }

    [Fact]
    public async Task GetTileAsync_NoContent_IsEmptyAndDoesNotThrow()
    {
        var client = TestHelpers.CreateOgcTilesClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        var result = await client.GetTileAsync("segments", "WebMercatorQuad", 2, 1, 3);

        Assert.True(result.IsEmpty);
        Assert.True(result.Content.IsEmpty);
    }

    [Fact]
    public async Task GetTileAsync_OkWithEmptyBody_IsNotTheEmptyContract()
    {
        var client = TestHelpers.CreateOgcTilesClient(_ =>
        {
            var content = new ByteArrayContent([]);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.mapbox-vector-tile");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });

        var result = await client.GetTileAsync("segments", "WebMercatorQuad", 0, 0, 0);

        Assert.False(result.IsEmpty);
        Assert.True(result.Content.IsEmpty);
    }

    [Fact]
    public async Task GetTileAsync_NotFound_Throws()
    {
        var client = TestHelpers.CreateOgcTilesClient(_ =>
            Task.FromResult(TestHelpers.CreateProblemDetailsResponse(
                HttpStatusCode.NotFound, "Not Found", "Collection 'missing' not found.")));

        var ex = await Assert.ThrowsAsync<HonuaOgcFeaturesException>(
            () => client.GetTileAsync("missing", "WebMercatorQuad", 0, 0, 0));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task ListCollectionTilesetsAsync_EmptyCollectionId_Throws()
    {
        var client = TestHelpers.CreateOgcTilesClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse("{}")));

        await Assert.ThrowsAsync<ArgumentException>(() => client.ListCollectionTilesetsAsync("  "));
    }
}
