// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Features.FeatureServer;
using Honua.Sdk.Features.FeatureServer.Exceptions;
using Honua.Sdk.Features.FeatureServer.Models;
using Honua.Sdk.Features.Tests.Fixtures;

namespace Honua.Sdk.Features.Tests.FeatureServer;

public class HonuaFeatureServerClientTests
{
    // ── GetServiceInfoAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetServiceInfoAsync_ReturnsServiceInfo()
    {
        var json = """
        {
            "serviceDescription": "Test Service",
            "maxRecordCount": 1000,
            "capabilities": "Query",
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 },
            "layers": [{ "id": 0, "name": "TestLayer" }]
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetServiceInfoAsync("myService");

        Assert.Equal("Test Service", result.ServiceDescription);
        Assert.Equal(1000, result.MaxRecordCount);
        Assert.Equal("Query", result.Capabilities);
        Assert.NotNull(result.Layers);
        Assert.Single(result.Layers);
        Assert.Equal("TestLayer", result.Layers[0].Name);
    }

    // ── GetLayerInfoAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetLayerInfoAsync_ReturnsLayerInfo()
    {
        var json = """
        {
            "id": 0,
            "name": "Points",
            "geometryType": "esriGeometryPoint",
            "objectIdField": "OBJECTID",
            "maxRecordCount": 2000,
            "supportsStatistics": true,
            "fields": [
                { "name": "OBJECTID", "type": "esriFieldTypeOID", "nullable": false },
                { "name": "NAME", "type": "esriFieldTypeString", "nullable": true, "length": 255 }
            ]
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.GetLayerInfoAsync("myService", 0);

        Assert.Equal("Points", result.Name);
        Assert.Equal("esriGeometryPoint", result.GeometryType);
        Assert.Equal("OBJECTID", result.ObjectIdField);
        Assert.True(result.SupportsStatistics);
        Assert.NotNull(result.Fields);
        Assert.Equal(2, result.Fields.Count);
    }

    // ── QueryAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_ReturnsFeatures()
    {
        var json = """
        {
            "objectIdFieldName": "OBJECTID",
            "features": [
                { "attributes": { "OBJECTID": 1, "NAME": "Test" }, "geometry": { "x": -118.0, "y": 34.0 } },
                { "attributes": { "OBJECTID": 2, "NAME": "Test2" }, "geometry": { "x": -117.0, "y": 33.0 } }
            ],
            "exceededTransferLimit": false
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.QueryAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" });

        Assert.NotNull(result.Features);
        Assert.Equal(2, result.Features.Count);
        Assert.False(result.ExceededTransferLimit);
    }

    [Fact]
    public async Task QueryAsync_SerializesQueryParams()
    {
        string? capturedUrl = null;
        var json = """{ "features": [], "exceededTransferLimit": false }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var query = new FeatureServerQueryParams
        {
            Where = "POP > 100",
            OutFields = "NAME,POP",
            ReturnGeometry = false,
            ResultOffset = 10,
            ResultRecordCount = 5,
            OrderByFields = "POP DESC",
            OutSR = 3857,
        };

        await client.QueryAsync("svc", 0, query);

        Assert.NotNull(capturedUrl);
        Assert.Contains("where=POP", capturedUrl);
        Assert.Contains("outFields=NAME%2CPOP", capturedUrl);
        Assert.Contains("returnGeometry=false", capturedUrl);
        Assert.Contains("resultOffset=10", capturedUrl);
        Assert.Contains("resultRecordCount=5", capturedUrl);
        Assert.Contains("orderByFields=POP", capturedUrl);
        Assert.Contains("outSR=3857", capturedUrl);
    }

    [Fact]
    public async Task QueryAsync_SpatialFilter_IncludesParams()
    {
        string? capturedUrl = null;
        var json = """{ "features": [], "exceededTransferLimit": false }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedUrl = req.RequestUri?.ToString();
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var query = new FeatureServerQueryParams
        {
            SpatialFilter = new FeatureServerSpatialFilter
            {
                Geometry = """{"xmin":-118,"ymin":33,"xmax":-117,"ymax":34}""",
                GeometryType = "esriGeometryEnvelope",
                SpatialRel = SpatialRelationship.Contains,
            }
        };

        await client.QueryAsync("svc", 0, query);

        Assert.NotNull(capturedUrl);
        Assert.Contains("geometry=", capturedUrl);
        Assert.Contains("geometryType=esriGeometryEnvelope", capturedUrl);
        Assert.Contains("spatialRel=esriSpatialRelContains", capturedUrl);
    }

    // ── QueryCountAsync ─────────────────────────────────────────────

    [Fact]
    public async Task QueryCountAsync_ReturnsCount()
    {
        var json = """{ "count": 42 }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Contains("returnCountOnly=true", req.RequestUri?.ToString() ?? "");
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var count = await client.QueryCountAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" });

        Assert.Equal(42, count);
    }

    // ── QueryIdsAsync ───────────────────────────────────────────────

    [Fact]
    public async Task QueryIdsAsync_ReturnsIds()
    {
        var json = """{ "objectIds": [1, 2, 3, 10] }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Contains("returnIdsOnly=true", req.RequestUri?.ToString() ?? "");
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var ids = await client.QueryIdsAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" });

        Assert.Equal(4, ids.Count);
        Assert.Equal(1, ids[0]);
        Assert.Equal(10, ids[3]);
    }

    // ── QueryExtentAsync ────────────────────────────────────────────

    [Fact]
    public async Task QueryExtentAsync_ReturnsExtent()
    {
        var json = """
        {
            "extent": {
                "xmin": -118.5, "ymin": 33.5, "xmax": -117.5, "ymax": 34.5,
                "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
            }
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Contains("returnExtentOnly=true", req.RequestUri?.ToString() ?? "");
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var extent = await client.QueryExtentAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" });

        Assert.Equal(-118.5, extent.Xmin);
        Assert.Equal(34.5, extent.Ymax);
        Assert.NotNull(extent.SpatialReference);
        Assert.Equal(4326, extent.SpatialReference.Wkid);
    }

    // ── QueryPagesAsync ─────────────────────────────────────────────

    [Fact]
    public async Task QueryPagesAsync_AutoPages()
    {
        var callCount = 0;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            callCount++;
            var json = callCount switch
            {
                1 => """
                {
                    "features": [{ "attributes": { "ID": 1 } }, { "attributes": { "ID": 2 } }],
                    "exceededTransferLimit": true
                }
                """,
                2 => """
                {
                    "features": [{ "attributes": { "ID": 3 } }],
                    "exceededTransferLimit": false
                }
                """,
                _ => """{ "features": [], "exceededTransferLimit": false }"""
            };
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<FeatureServerQueryResponse>();
        await foreach (var page in client.QueryPagesAsync("svc", 0, new FeatureServerQueryParams { Where = "1=1" }))
        {
            pages.Add(page);
        }

        Assert.Equal(2, pages.Count);
        Assert.Equal(2, pages[0].Features?.Count);
        Assert.Single(pages[1].Features!);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task QueryPagesAsync_StopsOnEmptyFeatures()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
        {
            var json = """{ "features": [], "exceededTransferLimit": false }""";
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var pages = new List<FeatureServerQueryResponse>();
        await foreach (var page in client.QueryPagesAsync("svc", 0, new FeatureServerQueryParams()))
        {
            pages.Add(page);
        }

        Assert.Single(pages);
    }

    // ── QueryStatisticsAsync ────────────────────────────────────────

    [Fact]
    public async Task QueryStatisticsAsync_ReturnsStatistics()
    {
        var json = """
        {
            "features": [
                { "attributes": { "COUNT_OBJECTID": 100, "STATE": "CA" } },
                { "attributes": { "COUNT_OBJECTID": 50, "STATE": "NY" } }
            ]
        }
        """;
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(json)));

        var result = await client.QueryStatisticsAsync("svc", 0, new FeatureServerStatisticsParams
        {
            OutStatistics = """[{"statisticType":"count","onStatisticField":"OBJECTID","outStatisticFieldName":"COUNT_OBJECTID"}]""",
            GroupByFieldsForStatistics = "STATE"
        });

        Assert.NotNull(result.Features);
        Assert.Equal(2, result.Features.Count);
    }

    // ── ValidateSqlAsync ────────────────────────────────────────────

    [Fact]
    public async Task ValidateSqlAsync_ReturnsValidation()
    {
        var json = """{ "isValidSQL": true }""";
        HttpMethod? capturedMethod = null;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedMethod = req.Method;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var result = await client.ValidateSqlAsync("svc", 0, "NAME = 'Test'");

        Assert.True(result.IsValidSql);
        Assert.Equal(HttpMethod.Post, capturedMethod);
    }

    // ── GeoServices 200-with-error ──────────────────────────────────

    [Fact]
    public async Task QueryAsync_GeoServicesError_ThrowsWithDetails()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoServicesErrorResponse(
                400, "Invalid WHERE clause", ["Syntax error at position 5"])));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => client.QueryAsync("svc", 0, new FeatureServerQueryParams { Where = "BAD SQL" }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(400, ex.GeoServicesErrorCode);
        Assert.Equal("Invalid WHERE clause", ex.Message);
        Assert.NotNull(ex.Details);
        Assert.Single(ex.Details);
        Assert.Contains("Syntax error", ex.Details[0]);
    }

    // ── HTTP error ──────────────────────────────────────────────────

    [Fact]
    public async Task QueryAsync_HttpError_Throws()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(HttpStatusCode.Forbidden, "Access denied")));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => client.QueryAsync("svc", 0, new FeatureServerQueryParams()));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    [Fact]
    public async Task GetServiceInfoAsync_NotFound_Throws()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("Not Found")
            }));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => client.GetServiceInfoAsync("noSuchService"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    // ── POST fallback for long queries ──────────────────────────────

    [Fact]
    public async Task QueryAsync_LongQuery_UsesPOST()
    {
        HttpMethod? capturedMethod = null;
        var json = """{ "features": [], "exceededTransferLimit": false }""";
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            capturedMethod = req.Method;
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        // Generate a long WHERE clause to exceed the POST fallback threshold
        var longWhere = "OBJECTID IN (" + string.Join(",", Enumerable.Range(1, 500)) + ")";
        await client.QueryAsync("svc", 0, new FeatureServerQueryParams { Where = longWhere });

        Assert.Equal(HttpMethod.Post, capturedMethod);
    }

    // ── QueryRawAsync ───────────────────────────────────────────────

    [Fact]
    public async Task QueryRawAsync_ReturnsRawResponse()
    {
        var client = TestHelpers.CreateFeatureServerClient(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([0x01, 0x02, 0x03])
            }));

        using var response = await client.QueryRawAsync("svc", 0,
            new FeatureServerQueryParams { Format = FeatureServerFormat.GeoJson });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
