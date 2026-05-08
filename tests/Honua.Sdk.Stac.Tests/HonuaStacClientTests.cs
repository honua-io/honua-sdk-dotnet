// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.Stac.Exceptions;
using Honua.Sdk.Stac.Models;
using Honua.Sdk.Stac.Tests.Fixtures;

namespace Honua.Sdk.Stac.Tests;

public sealed class HonuaStacClientTests
{
    [Fact]
    public async Task GetCatalogAsync_ReturnsLandingPage()
    {
        string? capturedUrl = null;
        var json = """
        {
            "type": "Catalog",
            "id": "honua",
            "stac_version": "1.0.0",
            "title": "Honua STAC",
            "description": "Public asset catalog",
            "links": [
                { "href": "/stac/collections", "rel": "data", "type": "application/json" }
            ],
            "serverProfile": "honua"
        }
        """;
        var client = TestHelpers.CreateStacClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var result = await client.GetCatalogAsync();

        Assert.EndsWith("/stac", capturedUrl, StringComparison.Ordinal);
        Assert.Equal("Catalog", result.Type);
        Assert.Equal("honua", result.Id);
        Assert.Equal("1.0.0", result.StacVersion);
        Assert.Equal("Honua STAC", result.Title);
        Assert.Equal("Public asset catalog", result.Description);
        Assert.Equal("data", Assert.Single(result.Links!).Rel);
        Assert.Equal("honua", result.AdditionalProperties?["serverProfile"].GetString());
    }

    [Fact]
    public async Task ListCollectionsAsync_ReturnsCollections()
    {
        var json = """
        {
            "collections": [
                { "type": "Collection", "id": "sentinel-2", "title": "Sentinel-2", "license": "proprietary" },
                { "type": "Collection", "id": "landsat", "title": "Landsat", "stac_version": "1.0.0" }
            ]
        }
        """;
        var client = TestHelpers.CreateStacClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.ListCollectionsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("sentinel-2", result[0].Id);
        Assert.Equal("Sentinel-2", result[0].Title);
        Assert.Equal("proprietary", result[0].License);
        Assert.Equal("1.0.0", result[1].StacVersion);
    }

    [Fact]
    public async Task GetCollectionAsync_ReturnsCollectionExtentAndAssets()
    {
        var json = """
        {
            "type": "Collection",
            "id": "imagery",
            "title": "Imagery",
            "description": "Imagery collection",
            "license": "proprietary",
            "extent": {
                "spatial": {
                    "bbox": [[-158.4, 21.2, -157.6, 21.9]]
                },
                "temporal": {
                    "interval": [["2026-05-01T00:00:00Z", null]]
                }
            },
            "item_assets": {
                "thumbnail": {
                    "href": "https://cdn.example.test/thumb.png",
                    "type": "image/png",
                    "roles": ["thumbnail"]
                }
            },
            "summaries": {
                "platform": ["sentinel-2a"]
            },
            "links": [
                { "href": "/stac/collections/imagery/items", "rel": "items" }
            ]
        }
        """;
        var client = TestHelpers.CreateStacClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetCollectionAsync("imagery");

        Assert.Equal("imagery", result.Id);
        Assert.Equal(-158.4, result.Extent?.Spatial?.Bbox?[0][0]);
        Assert.Null(result.Extent?.Temporal?.Interval?[0][1]);
        Assert.Equal("thumbnail", Assert.Single(result.ItemAssets!).Key);
        Assert.Equal("sentinel-2a", result.Summaries?["platform"][0].GetString());
        Assert.Equal("items", Assert.Single(result.Links!).Rel);
    }

    [Fact]
    public async Task GetItemsAsync_SerializesItemQueryParameters()
    {
        string? capturedUrl = null;
        var json = """{ "type": "FeatureCollection", "features": [] }""";
        var client = TestHelpers.CreateStacClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var query = new StacItemsQuery
        {
            Limit = 25,
            Offset = 50,
            Next = "opaque",
            Bbox = [-158.4, 21.2, -157.6, 21.9],
            Datetime = "2026-05-01T00:00:00Z/..",
            Ids = ["scene-1", "scene-2"],
            Filter = "cloud_cover < 10",
            FilterLang = "cql2-text",
            SortBy = "-properties.datetime",
            Fields = new StacFields
            {
                Include = ["id", "properties.datetime"],
                Exclude = ["assets.thumbnail"]
            },
            AdditionalParameters = new Dictionary<string, string?>
            {
                ["language"] = "en",
                ["limit"] = "999"
            }
        };

        await client.GetItemsAsync("imagery", query);

        Assert.NotNull(capturedUrl);
        var decodedUrl = WebUtility.UrlDecode(capturedUrl);
        Assert.Contains("/stac/collections/imagery/items", capturedUrl);
        Assert.Contains("limit=25", capturedUrl);
        Assert.DoesNotContain("limit=999", capturedUrl);
        Assert.Contains("offset=50", capturedUrl);
        Assert.Contains("next=opaque", capturedUrl);
        Assert.Contains("bbox=-158.4%2C21.2%2C-157.6%2C21.9", capturedUrl);
        Assert.Contains("datetime=2026-05-01T00:00:00Z/..", decodedUrl);
        Assert.Contains("ids=scene-1%2Cscene-2", capturedUrl);
        Assert.Contains("filter=cloud_cover < 10", decodedUrl);
        Assert.Contains("filter-lang=cql2-text", capturedUrl);
        Assert.Contains("sortby=-properties.datetime", capturedUrl);
        Assert.Contains("fields=id%2Cproperties.datetime%2C-assets.thumbnail", capturedUrl);
        Assert.Contains("language=en", capturedUrl);
    }

    [Fact]
    public async Task GetItemAsync_ReturnsItemWithAssets()
    {
        string? capturedUrl = null;
        var json = """
        {
            "type": "Feature",
            "id": "scene-001",
            "collection": "imagery",
            "stac_version": "1.0.0",
            "geometry": { "type": "Point", "coordinates": [-158.0, 21.3] },
            "bbox": [-158.0, 21.3, -158.0, 21.3],
            "properties": {
                "datetime": "2026-05-01T00:00:00Z",
                "cloud_cover": 7
            },
            "assets": {
                "thumbnail": {
                    "href": "https://cdn.example.test/scene-001.png",
                    "type": "image/png",
                    "title": "Preview",
                    "roles": ["thumbnail"]
                }
            },
            "links": [
                { "href": "/stac/collections/imagery/items/scene-001", "rel": "self" }
            ]
        }
        """;
        var client = TestHelpers.CreateStacClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var result = await client.GetItemAsync("imagery", "scene-001");

        Assert.EndsWith("/stac/collections/imagery/items/scene-001", capturedUrl, StringComparison.Ordinal);
        Assert.Equal("scene-001", result.Id);
        Assert.Equal("imagery", result.Collection);
        Assert.Equal(7, result.Properties?["cloud_cover"].GetInt32());
        var asset = Assert.Single(result.Assets!).Value;
        Assert.Equal("image/png", asset.Type);
        Assert.Equal("thumbnail", Assert.Single(asset.Roles!));
    }

    [Fact]
    public async Task SearchAsync_GetSerializesStacSearchParameters()
    {
        string? capturedUrl = null;
        var json = """{ "type": "FeatureCollection", "features": [] }""";
        using var intersects = JsonDocument.Parse("""{ "type": "Point", "coordinates": [-158, 21] }""");
        using var queryPayload = JsonDocument.Parse("""{ "eo:cloud_cover": { "lt": 10 } }""");
        var client = TestHelpers.CreateStacClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        await client.SearchAsync(new StacSearchQuery
        {
            Collections = ["imagery"],
            Ids = ["scene-001"],
            Bbox = [-158.4, 21.2, -157.6, 21.9],
            Datetime = "2026-05-01T00:00:00Z/..",
            Intersects = intersects.RootElement,
            Query = queryPayload.RootElement,
            Filter = "cloud_cover < 10",
            FilterLang = "cql2-text",
            Limit = 10,
            Offset = 20,
            SortBy = "-properties.datetime",
            Fields = new StacFields
            {
                Include = ["id", "properties.datetime"],
                Exclude = ["assets.thumbnail"]
            }
        });

        Assert.NotNull(capturedUrl);
        var decodedUrl = WebUtility.UrlDecode(capturedUrl);
        Assert.Contains("/stac/search", capturedUrl);
        Assert.Contains("collections=imagery", capturedUrl);
        Assert.Contains("ids=scene-001", capturedUrl);
        Assert.Contains("bbox=-158.4%2C21.2%2C-157.6%2C21.9", capturedUrl);
        Assert.Contains("datetime=2026-05-01T00:00:00Z/..", decodedUrl);
        Assert.Contains("""intersects={ "type": "Point", "coordinates": [-158, 21] }""", decodedUrl);
        Assert.Contains("query={", decodedUrl);
        Assert.Contains("filter=cloud_cover < 10", decodedUrl);
        Assert.Contains("filter-lang=cql2-text", capturedUrl);
        Assert.Contains("limit=10", capturedUrl);
        Assert.Contains("offset=20", capturedUrl);
        Assert.Contains("sortby=-properties.datetime", capturedUrl);
        Assert.Contains("fields=id%2Cproperties.datetime%2C-assets.thumbnail", capturedUrl);
    }

    [Fact]
    public async Task SearchAsync_PostsSearchRequestBody()
    {
        HttpMethod? capturedMethod = null;
        string? capturedPath = null;
        string? capturedContentType = null;
        string? capturedBody = null;
        var json = """{ "type": "FeatureCollection", "features": [] }""";
        using var intersects = JsonDocument.Parse("""{ "type": "Point", "coordinates": [-158, 21] }""");
        var client = TestHelpers.CreateStacClient(async req =>
        {
            capturedMethod = req.Method;
            capturedPath = req.RequestUri?.AbsolutePath;
            capturedContentType = req.Content?.Headers.ContentType?.MediaType;
            capturedBody = req.Content is null ? null : await req.Content.ReadAsStringAsync();
            return TestHelpers.CreateRawJsonResponse(json);
        });

        await client.SearchAsync(new StacSearchRequest
        {
            Collections = ["imagery"],
            Bbox = [-158.4, 21.2, -157.6, 21.9],
            Intersects = intersects.RootElement,
            Filter = "cloud_cover < 10",
            FilterLang = "cql2-text",
            Limit = 25,
            Offset = 50,
            Fields = new StacFields { Include = ["id"], Exclude = ["assets.thumbnail"] }
        });

        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Equal("/stac/search", capturedPath);
        Assert.Equal("application/json", capturedContentType);
        Assert.NotNull(capturedBody);
        using var document = JsonDocument.Parse(capturedBody);
        var root = document.RootElement;
        Assert.Equal("imagery", root.GetProperty("collections")[0].GetString());
        Assert.Equal(-158.4, root.GetProperty("bbox")[0].GetDouble());
        Assert.Equal("Point", root.GetProperty("intersects").GetProperty("type").GetString());
        Assert.Equal("cloud_cover < 10", root.GetProperty("filter").GetString());
        Assert.Equal("cql2-text", root.GetProperty("filter-lang").GetString());
        Assert.Equal(25, root.GetProperty("limit").GetInt32());
        Assert.Equal(50, root.GetProperty("offset").GetInt32());
        Assert.Equal("id", root.GetProperty("fields").GetProperty("include")[0].GetString());
        Assert.Equal("assets.thumbnail", root.GetProperty("fields").GetProperty("exclude")[0].GetString());
    }

    [Fact]
    public async Task GetItemsPagesAsync_FollowsSameOriginNextLinks()
    {
        var calls = 0;
        var client = TestHelpers.CreateStacClient(req =>
        {
            calls++;
            var json = calls == 1
                ? """
                  {
                      "type": "FeatureCollection",
                      "numberMatched": 2,
                      "numberReturned": 1,
                      "features": [{ "type": "Feature", "id": "scene-001", "properties": { "datetime": "2026-05-01T00:00:00Z" } }],
                      "links": [
                          { "href": "https://honua.example.test/stac/collections/imagery/items?offset=1&limit=1", "rel": "next" }
                      ]
                  }
                  """
                : """
                  {
                      "type": "FeatureCollection",
                      "numberMatched": 2,
                      "numberReturned": 1,
                      "features": [{ "type": "Feature", "id": "scene-002", "properties": { "datetime": "2026-05-02T00:00:00Z" } }]
                  }
                  """;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<StacItemCollection>();
        await foreach (var page in client.GetItemsPagesAsync("imagery", new StacItemsQuery { Limit = 1 }))
        {
            pages.Add(page);
        }

        Assert.Equal(2, pages.Count);
        Assert.Equal("scene-001", pages[0].Features?[0].Id);
        Assert.Equal("scene-002", pages[1].Features?[0].Id);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task SearchPagesAsync_PostThenFollowsNextLinkWithGet()
    {
        var calls = 0;
        var methods = new List<HttpMethod>();
        var client = TestHelpers.CreateStacClient(req =>
        {
            calls++;
            methods.Add(req.Method);
            var json = calls == 1
                ? """
                  {
                      "type": "FeatureCollection",
                      "features": [{ "type": "Feature", "id": "scene-001" }],
                      "links": [
                          { "href": "https://honua.example.test/stac/search?limit=1&offset=1", "rel": "next" }
                      ]
                  }
                  """
                : """
                  {
                      "type": "FeatureCollection",
                      "features": [{ "type": "Feature", "id": "scene-002" }]
                  }
                  """;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<StacItemCollection>();
        await foreach (var page in client.SearchPagesAsync(new StacSearchRequest
        {
            Collections = ["imagery"],
            Limit = 1
        }))
        {
            pages.Add(page);
        }

        Assert.Equal(2, pages.Count);
        Assert.Equal([HttpMethod.Post, HttpMethod.Get], methods);
    }

    [Fact]
    public async Task SearchPagesAsync_RejectsCrossOriginNextLink()
    {
        var json = """
        {
            "type": "FeatureCollection",
            "features": [{ "type": "Feature", "id": "scene-001" }],
            "links": [
                { "href": "https://attacker.example/stac/search?offset=1", "rel": "next" }
            ]
        }
        """;
        var client = TestHelpers.CreateStacClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var ex = await Assert.ThrowsAsync<HonuaStacException>(async () =>
        {
            await foreach (var _ in client.SearchPagesAsync(new StacSearchQuery { Limit = 1 }))
            {
            }
        });

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Contains("different origin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchJsonAsync_ReturnsRawJsonDocument()
    {
        var json = """
        {
            "type": "FeatureCollection",
            "features": [],
            "context": {
                "returned": 0,
                "matched": 10
            }
        }
        """;
        var client = TestHelpers.CreateStacClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        using var document = await client.SearchJsonAsync(new StacSearchQuery { Collections = ["imagery"] });

        Assert.True(document.RootElement.TryGetProperty("context", out var context));
        Assert.Equal(10, context.GetProperty("matched").GetInt32());
    }

    [Fact]
    public async Task GetItemsRawAsync_ReturnsUndisposedResponse()
    {
        var client = TestHelpers.CreateStacClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse("""{ "ok": true }""")));

        using var response = await client.GetItemsRawAsync("imagery");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"ok\": true", body);
    }

    [Fact]
    public async Task ErrorResponse_ParsesProblemDetails()
    {
        var json = """
        {
            "type": "https://honua.example.test/problems/invalid-stac-query",
            "title": "Invalid STAC query",
            "status": 400,
            "detail": "The bbox parameter is invalid."
        }
        """;
        var client = TestHelpers.CreateStacClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(json)
            }));

        var ex = await Assert.ThrowsAsync<HonuaStacException>(() =>
            client.SearchAsync(new StacSearchQuery { Bbox = [1, 2, 3, 4] }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("https://honua.example.test/problems/invalid-stac-query", ex.ProblemType);
        Assert.Equal("Invalid STAC query", ex.ProblemTitle);
        Assert.Equal("The bbox parameter is invalid.", ex.ProblemDetail);
        Assert.Contains("bbox parameter", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(json, ex.ResponseBody);
    }
}
