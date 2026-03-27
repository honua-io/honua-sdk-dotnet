// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.Wfs.Exceptions;
using Honua.Sdk.Wfs.Formats;
using Honua.Sdk.Wfs.Models;
using Honua.Sdk.Wfs.Tests.Fixtures;

namespace Honua.Sdk.Wfs.Tests;

public sealed class GetFeaturesTests
{
    private const string TwoFeatureResponse = """
        {
            "type": "FeatureCollection",
            "numberMatched": 42,
            "numberReturned": 2,
            "features": [
                {
                    "type": "Feature",
                    "id": "parcels.1",
                    "geometry": {
                        "type": "Point",
                        "coordinates": [-122.4194, 37.7749]
                    },
                    "properties": {
                        "parcel_id": "APN-001",
                        "area_sqm": 500.5,
                        "owner": "Alice"
                    }
                },
                {
                    "type": "Feature",
                    "id": "parcels.2",
                    "geometry": {
                        "type": "Polygon",
                        "coordinates": [[[-122.42, 37.77], [-122.41, 37.77], [-122.41, 37.78], [-122.42, 37.78], [-122.42, 37.77]]]
                    },
                    "properties": {
                        "parcel_id": "APN-002",
                        "area_sqm": 1200.0,
                        "owner": "Bob"
                    }
                }
            ]
        }
        """;

    [Fact]
    public async Task GetFeatures_Success_ReturnsFeatureCollection()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("REQUEST=GetFeature", req.RequestUri!.Query);
            Assert.Contains("TYPENAMES=parcels", req.RequestUri.Query);
            Assert.Contains("OUTPUTFORMAT=application%2Fgeo%2Bjson", req.RequestUri.Query);
            return Task.FromResult(TestHelpers.CreateGeoJsonResponse(TwoFeatureResponse));
        });

        var result = await client.GetFeaturesAsync(new GetFeaturesRequest { TypeNames = "parcels" });

        Assert.Equal(42, result.NumberMatched);
        Assert.Equal(2, result.NumberReturned);
        Assert.True(result.HasMoreResults);
        Assert.Equal(2, result.Features.Count);
    }

    [Fact]
    public async Task GetFeatures_ParsesFeatureProperties()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoJsonResponse(TwoFeatureResponse)));

        var result = await client.GetFeaturesAsync(new GetFeaturesRequest { TypeNames = "parcels" });

        var f1 = result.Features[0];
        Assert.Equal("parcels.1", f1.Id);
        Assert.NotNull(f1.Geometry);
        Assert.Equal("Point", f1.Geometry.Type);
        Assert.Equal("APN-001", f1.Properties["parcel_id"].GetString());
        Assert.Equal(500.5, f1.Properties["area_sqm"].GetDouble());
    }

    [Fact]
    public async Task GetFeatures_ParsesGeometry()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoJsonResponse(TwoFeatureResponse)));

        var result = await client.GetFeaturesAsync(new GetFeaturesRequest { TypeNames = "parcels" });

        var f2 = result.Features[1];
        Assert.Equal("Polygon", f2.Geometry!.Type);
        Assert.NotNull(f2.Geometry.Coordinates);
        Assert.Equal(JsonValueKind.Array, f2.Geometry.Coordinates.Value.ValueKind);
    }

    [Fact]
    public async Task GetFeatures_WithQueryParameters_BuildsCorrectUrl()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            var query = req.RequestUri!.Query;
            Assert.Contains("COUNT=10", query);
            Assert.Contains("STARTINDEX=20", query);
            Assert.Contains("SORTBY=name%20ASC", query);
            Assert.Contains("SRSNAME=EPSG%3A3857", query);
            Assert.Contains("PROPERTYNAME=name%2Cgeometry", query);
            return Task.FromResult(TestHelpers.CreateGeoJsonResponse(
                """{ "type": "FeatureCollection", "numberMatched": 0, "numberReturned": 0, "features": [] }"""));
        });

        await client.GetFeaturesAsync(new GetFeaturesRequest
        {
            TypeNames = "parcels",
            Count = 10,
            StartIndex = 20,
            SortBy = "name ASC",
            SrsName = "EPSG:3857",
            PropertyName = "name,geometry",
        });
    }

    [Fact]
    public async Task GetFeatures_WithBbox_BuildsCorrectUrl()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("BBOX=", req.RequestUri!.Query);
            return Task.FromResult(TestHelpers.CreateGeoJsonResponse(
                """{ "type": "FeatureCollection", "numberMatched": 0, "numberReturned": 0, "features": [] }"""));
        });

        await client.GetFeaturesAsync(new GetFeaturesRequest
        {
            TypeNames = "parcels",
            Bbox = new WfsBoundingBox { MinX = -122.5, MinY = 37.5, MaxX = -122.0, MaxY = 38.0 },
        });
    }

    [Fact]
    public async Task GetFeatures_NullRequest_ThrowsArgumentNullException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoJsonResponse("{}")));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.GetFeaturesAsync((GetFeaturesRequest)null!));
    }

    [Fact]
    public async Task GetFeatures_XmlExceptionOnGeoJsonRequest_ThrowsHonuaWfsException()
    {
        var exceptionXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
              <ows:Exception exceptionCode="InvalidParameterValue" locator="TYPENAMES">
                <ows:ExceptionText>Type not found</ows:ExceptionText>
              </ows:Exception>
            </ows:ExceptionReport>
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateXmlResponse(exceptionXml, HttpStatusCode.BadRequest)));

        var ex = await Assert.ThrowsAsync<HonuaWfsException>(
            () => client.GetFeaturesAsync(new GetFeaturesRequest { TypeNames = "bad" }));

        Assert.Equal("InvalidParameterValue", ex.ExceptionCode);
    }

    [Fact]
    public async Task GetFeatures_HttpError_ThrowsHonuaWfsException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(
                HttpStatusCode.ServiceUnavailable, "Service down", "text/plain")));

        var ex = await Assert.ThrowsAsync<HonuaWfsException>(
            () => client.GetFeaturesAsync(new GetFeaturesRequest { TypeNames = "parcels" }));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }

    [Fact]
    public async Task GetFeatures_NumberMatchedUnknown_ReturnsNull()
    {
        var json = """
            {
                "type": "FeatureCollection",
                "numberMatched": "unknown",
                "numberReturned": 1,
                "features": [
                    { "type": "Feature", "id": "1", "geometry": null, "properties": {} }
                ]
            }
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoJsonResponse(json)));

        var result = await client.GetFeaturesAsync(new GetFeaturesRequest { TypeNames = "parcels" });

        Assert.Null(result.NumberMatched);
        Assert.Equal(1, result.NumberReturned);
        Assert.True(result.HasMoreResults);
    }

    [Fact]
    public async Task GetFeatures_NumericId_ConvertedToString()
    {
        var json = """
            {
                "type": "FeatureCollection",
                "numberMatched": 1,
                "numberReturned": 1,
                "features": [
                    { "type": "Feature", "id": 42, "geometry": null, "properties": {} }
                ]
            }
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoJsonResponse(json)));

        var result = await client.GetFeaturesAsync(new GetFeaturesRequest { TypeNames = "parcels" });

        Assert.Equal("42", result.Features[0].Id);
    }

    [Fact]
    public async Task GetFeatures_WithCustomHandler_UsesHandlerMediaType()
    {
        var handler = new RawStreamHandler("text/csv");

        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("OUTPUTFORMAT=text%2Fcsv", req.RequestUri!.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("id,name\n1,test", System.Text.Encoding.UTF8, "text/csv")
            });
        });

        var stream = await client.GetFeaturesAsync(
            new GetFeaturesRequest { TypeNames = "parcels" },
            handler);

        Assert.NotNull(stream);
    }

    // ── GetFeatureCountAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetFeatureCount_Success_ParsesXmlHitsResponse()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("RESULTTYPE=hits", req.RequestUri!.Query);
            Assert.Contains("TYPENAMES=parcels", req.RequestUri.Query);
            Assert.DoesNotContain("OUTPUTFORMAT", req.RequestUri.Query);
            return Task.FromResult(TestHelpers.CreateWfsHitsXmlResponse(150));
        });

        var count = await client.GetFeatureCountAsync("parcels");

        Assert.Equal(150, count);
    }

    [Fact]
    public async Task GetFeatureCount_WithFilter_PassesFilter()
    {
        const string fesFilter =
            """<fes:Filter xmlns:fes="http://www.opengis.net/fes/2.0"><fes:PropertyIsEqualTo><fes:ValueReference>owner</fes:ValueReference><fes:Literal>Alice</fes:Literal></fes:PropertyIsEqualTo></fes:Filter>""";

        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("FILTER=", req.RequestUri!.Query);
            return Task.FromResult(TestHelpers.CreateWfsHitsXmlResponse(5));
        });

        var count = await client.GetFeatureCountAsync("parcels", fesFilter);

        Assert.Equal(5, count);
    }

    [Fact]
    public async Task GetFeatureCount_NullTypeName_ThrowsArgumentNullException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoJsonResponse("{}")));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.GetFeatureCountAsync(null!));
    }

    // ── GeometryCollection ───────────────────────────────────────────────

    [Fact]
    public async Task GetFeatures_GeometryCollection_PreservesChildGeometries()
    {
        var json = """
            {
                "type": "FeatureCollection",
                "numberMatched": 1,
                "numberReturned": 1,
                "features": [
                    {
                        "type": "Feature",
                        "id": "gc.1",
                        "geometry": {
                            "type": "GeometryCollection",
                            "geometries": [
                                { "type": "Point", "coordinates": [100.0, 0.0] },
                                { "type": "LineString", "coordinates": [[101.0, 0.0], [102.0, 1.0]] }
                            ]
                        },
                        "properties": { "name": "multi" }
                    }
                ]
            }
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateGeoJsonResponse(json)));

        var result = await client.GetFeaturesAsync(new GetFeaturesRequest { TypeNames = "mixed" });

        var geometry = result.Features[0].Geometry;
        Assert.NotNull(geometry);
        Assert.Equal("GeometryCollection", geometry.Type);
        Assert.Null(geometry.Coordinates);
        Assert.NotNull(geometry.Geometries);
        Assert.Equal(2, geometry.Geometries.Count);
        Assert.Equal("Point", geometry.Geometries[0].Type);
        Assert.Equal("LineString", geometry.Geometries[1].Type);
        Assert.NotNull(geometry.Geometries[0].Coordinates);
    }

    [Fact]
    public async Task GetFeatureCount_ExceptionReport_ThrowsHonuaWfsException()
    {
        var exceptionXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
              <ows:Exception exceptionCode="InvalidParameterValue" locator="TYPENAMES">
                <ows:ExceptionText>Type not found</ows:ExceptionText>
              </ows:Exception>
            </ows:ExceptionReport>
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateXmlResponse(exceptionXml, HttpStatusCode.BadRequest)));

        var ex = await Assert.ThrowsAsync<HonuaWfsException>(
            () => client.GetFeatureCountAsync("bad"));

        Assert.Equal("InvalidParameterValue", ex.ExceptionCode);
    }
}
