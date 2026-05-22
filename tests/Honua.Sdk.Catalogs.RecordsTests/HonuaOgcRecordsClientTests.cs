// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Catalogs.Records.Exceptions;
using Honua.Sdk.Catalogs.Records.Models;
using Honua.Sdk.Catalogs.RecordsTests.Fixtures;

namespace Honua.Sdk.Catalogs.RecordsTests;

public sealed class HonuaOgcRecordsClientTests
{
    [Fact]
    public async Task GetLandingPageAsync_ReturnsLandingPage()
    {
        var json = """
        {
            "title": "Honua Records",
            "description": "Public metadata catalog",
            "links": [
                { "href": "/ogc/records/collections", "rel": "data", "type": "application/json" }
            ],
            "serverProfile": "honua"
        }
        """;
        var client = TestHelpers.CreateOgcRecordsClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetLandingPageAsync();

        Assert.Equal("Honua Records", result.Title);
        Assert.Equal("Public metadata catalog", result.Description);
        Assert.NotNull(result.Links);
        Assert.Single(result.Links);
        Assert.Equal("honua", result.AdditionalProperties?["serverProfile"].GetString());
    }

    [Fact]
    public async Task GetConformanceAsync_ReturnsConformanceClasses()
    {
        var json = """
        {
            "conformsTo": [
                "http://www.opengis.net/spec/ogcapi-records-1/1.0/conf/core",
                "http://www.opengis.net/spec/ogcapi-records-1/1.0/conf/record-core"
            ]
        }
        """;
        var client = TestHelpers.CreateOgcRecordsClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetConformanceAsync();

        Assert.Equal(2, result.ConformsTo.Count);
        Assert.Contains(result.ConformsTo, value => value.Contains("record-core", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListCollectionsAsync_ReturnsCollections()
    {
        var json = """
        {
            "collections": [
                { "id": "default", "title": "Default catalog", "itemType": "record" },
                { "id": "migration", "title": "Migration inventory", "recordType": "inventory" }
            ]
        }
        """;
        var client = TestHelpers.CreateOgcRecordsClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.ListCollectionsAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("default", result[0].Id);
        Assert.Equal("Migration inventory", result[1].Title);
        Assert.Equal("inventory", result[1].RecordType);
    }

    [Fact]
    public async Task GetCollectionAsync_ReturnsCollectionExtent()
    {
        var json = """
        {
            "id": "default",
            "title": "Default catalog",
            "extent": {
                "spatial": {
                    "bbox": [[-158.4, 21.2, -157.6, 21.9]],
                    "crs": "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
                },
                "temporal": {
                    "interval": [["2026-05-01T00:00:00Z", null]]
                }
            },
            "links": [
                { "href": "/ogc/records/collections/default/items", "rel": "items" }
            ]
        }
        """;
        var client = TestHelpers.CreateOgcRecordsClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetCollectionAsync("default");

        Assert.Equal("default", result.Id);
        Assert.Equal(-158.4, result.Extent?.Spatial?.Bbox?[0][0]);
        Assert.Null(result.Extent?.Temporal?.Interval?[0][1]);
        Assert.Equal("items", Assert.Single(result.Links!).Rel);
    }

    [Fact]
    public async Task GetRecordsAsync_SerializesRecordsQueryParameters()
    {
        string? capturedUrl = null;
        var json = """{ "type": "FeatureCollection", "features": [] }""";
        var client = TestHelpers.CreateOgcRecordsClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var query = new OgcRecordsQuery
        {
            Limit = 25,
            Offset = 50,
            Bbox = [-158.4, 21.2, -157.6, 21.9],
            Datetime = "2026-05-01T00:00:00Z/..",
            Query = "parks",
            Ids = ["svc-parks", "layer-parks-0"],
            Types = ["service", "layer"],
            ExternalIds = ["arcgis:parks"],
            Filter = "properties.owner = 'gis'",
            FilterLang = "cql2-text",
            SortBy = "-updated",
            AdditionalParameters = new Dictionary<string, string?>
            {
                ["language"] = "en",
                ["limit"] = "999"
            }
        };

        await client.GetRecordsAsync("default", query);

        Assert.NotNull(capturedUrl);
        var decodedUrl = WebUtility.UrlDecode(capturedUrl);
        Assert.Contains("/ogc/records/collections/default/items", capturedUrl);
        Assert.Contains("f=json", capturedUrl);
        Assert.Contains("limit=25", capturedUrl);
        Assert.DoesNotContain("limit=999", capturedUrl);
        Assert.Contains("offset=50", capturedUrl);
        Assert.Contains("bbox=", capturedUrl);
        Assert.Contains("datetime=2026-05-01T00:00:00Z/..", decodedUrl);
        Assert.Contains("q=parks", capturedUrl);
        Assert.Contains("ids=svc-parks%2Clayer-parks-0", capturedUrl);
        Assert.Contains("type=service%2Clayer", capturedUrl);
        Assert.Contains("externalIds=arcgis%3Aparks", capturedUrl);
        Assert.Contains("filter=", capturedUrl);
        Assert.Contains("filter-lang=cql2-text", capturedUrl);
        Assert.Contains("sortby=-updated", capturedUrl);
        Assert.Contains("language=en", capturedUrl);
    }

    [Fact]
    public async Task SearchAsync_ReturnsRecordCollection()
    {
        var json = """
        {
            "type": "FeatureCollection",
            "numberMatched": 10,
            "numberReturned": 1,
            "features": [
                {
                    "type": "Feature",
                    "id": "svc-parks",
                    "geometry": { "type": "Point", "coordinates": [-158.0, 21.3] },
                    "properties": {
                        "title": "Parks service",
                        "resourceType": "FeatureServer",
                        "updated": "2026-05-07T00:00:00Z"
                    },
                    "links": [
                        { "href": "/rest/services/parks/FeatureServer", "rel": "describedby" }
                    ]
                }
            ]
        }
        """;
        var client = TestHelpers.CreateOgcRecordsClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.SearchAsync("default", new OgcRecordsQuery { Query = "parks" });

        Assert.Equal("FeatureCollection", result.Type);
        Assert.Equal(10, result.NumberMatched);
        var record = Assert.Single(result.Records!);
        Assert.Equal("svc-parks", record.Id?.GetString());
        Assert.Equal("Parks service", record.Properties?["title"].GetString());
        Assert.Equal("FeatureServer", record.Properties?["resourceType"].GetString());
        Assert.Equal("describedby", Assert.Single(record.Links!).Rel);
    }

    [Fact]
    public async Task GetRecordAsync_ReturnsRecordDetail()
    {
        string? capturedUrl = null;
        var json = """
        {
            "type": "Feature",
            "id": "svc-parks",
            "properties": {
                "title": "Parks service",
                "kind": "service"
            }
        }
        """;
        var client = TestHelpers.CreateOgcRecordsClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var result = await client.GetRecordAsync("default", "svc-parks");

        Assert.Contains("/ogc/records/collections/default/items/svc-parks?f=json", capturedUrl);
        Assert.Equal("svc-parks", result.Id?.GetString());
        Assert.Equal("service", result.Properties?["kind"].GetString());
    }

    [Fact]
    public async Task GetRecordsPagesAsync_FollowsSameOriginNextLinks()
    {
        var calls = 0;
        var client = TestHelpers.CreateOgcRecordsClient(req =>
        {
            calls++;
            var json = calls == 1
                ? """
                  {
                      "type": "FeatureCollection",
                      "numberMatched": 2,
                      "numberReturned": 1,
                      "features": [{ "type": "Feature", "id": "first", "properties": { "title": "First" } }],
                      "links": [
                          { "href": "https://honua.example.test/ogc/records/collections/default/items?offset=1&f=json", "rel": "next" }
                      ]
                  }
                  """
                : """
                  {
                      "type": "FeatureCollection",
                      "numberMatched": 2,
                      "numberReturned": 1,
                      "features": [{ "type": "Feature", "id": "second", "properties": { "title": "Second" } }]
                  }
                  """;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<OgcRecordCollection>();
        await foreach (var page in client.GetRecordsPagesAsync("default", new OgcRecordsQuery { Limit = 1 }))
        {
            pages.Add(page);
        }

        Assert.Equal(2, pages.Count);
        Assert.Equal("first", pages[0].Records?[0].Id?.GetString());
        Assert.Equal("second", pages[1].Records?[0].Id?.GetString());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetRecordsPagesAsync_RejectsCrossOriginNextLink()
    {
        var json = """
        {
            "type": "FeatureCollection",
            "features": [{ "type": "Feature", "id": "first" }],
            "links": [
                { "href": "https://attacker.example/records?page=2", "rel": "next" }
            ]
        }
        """;
        var client = TestHelpers.CreateOgcRecordsClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var ex = await Assert.ThrowsAsync<HonuaOgcRecordsException>(async () =>
        {
            await foreach (var _ in client.GetRecordsPagesAsync("default"))
            {
            }
        });

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Contains("different origin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetRecordsJsonAsync_ReturnsRawJsonDocument()
    {
        var json = """
        {
            "type": "FeatureCollection",
            "features": [],
            "facets": {
                "type": [{ "value": "service", "count": 1 }]
            }
        }
        """;
        var client = TestHelpers.CreateOgcRecordsClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        using var document = await client.GetRecordsJsonAsync("default");

        Assert.True(document.RootElement.TryGetProperty("facets", out var facets));
        Assert.Equal("service", facets.GetProperty("type")[0].GetProperty("value").GetString());
    }

    [Fact]
    public async Task GetRecordsRawAsync_ReturnsUndisposedResponse()
    {
        var client = TestHelpers.CreateOgcRecordsClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse("""{ "ok": true }""")));

        using var response = await client.GetRecordsRawAsync("default");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"ok\": true", body);
    }

    [Fact]
    public async Task ErrorResponse_ParsesProblemDetails()
    {
        var json = """
        {
            "type": "https://honua.example.test/problems/invalid-records-query",
            "title": "Invalid Records query",
            "status": 400,
            "detail": "The bbox parameter is invalid."
        }
        """;
        var client = TestHelpers.CreateOgcRecordsClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(json)
            }));

        var ex = await Assert.ThrowsAsync<HonuaOgcRecordsException>(() =>
            client.GetRecordsAsync("default", new OgcRecordsQuery { Bbox = [1, 2, 3, 4] }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("https://honua.example.test/problems/invalid-records-query", ex.ProblemType);
        Assert.Equal("Invalid Records query", ex.ProblemTitle);
        Assert.Equal("The bbox parameter is invalid.", ex.ProblemDetail);
        Assert.Contains("bbox parameter", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(json, ex.ResponseBody);
    }
}
