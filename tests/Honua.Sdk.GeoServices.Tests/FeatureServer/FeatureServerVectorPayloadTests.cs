// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.GeoServices.Tests.Fixtures;
using Honua.Sdk.Geometry.Vector;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.GeoServices.Tests.FeatureServer;

public sealed class FeatureServerVectorPayloadTests
{
    [Fact]
    public async Task QueryVectorAsync_DefaultsToEsriJsonAndParsesNtsGeometry()
    {
        const string json = """
            {
              "objectIdFieldName": "OBJECTID",
              "spatialReference": { "wkid": 4326 },
              "features": [
                {
                  "attributes": { "OBJECTID": 7, "name": "Harbor" },
                  "geometry": { "x": -157.865, "y": 21.306 }
                }
              ]
            }
            """;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Equal("/rest/services/parks/FeatureServer/0/query", req.RequestUri!.AbsolutePath);
            Assert.Contains("f=json", req.RequestUri.Query);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(json));
        });

        var result = await client.QueryVectorAsync("parks", 0, new FeatureServerQueryParams());

        Assert.Equal(VectorPayloadFormat.EsriJson, result.Format);
        var feature = Assert.Single(result.Features);
        Assert.Equal("7", feature.Id);
        Assert.Equal("Harbor", feature.Attributes["name"].GetString());
        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(4326, point.SRID);
    }

    [Fact]
    public async Task QueryVectorAsync_GeoJson_RequestsGeoJsonAndParsesNtsGeometry()
    {
        const string geoJson = """
            {
              "type": "FeatureCollection",
              "numberReturned": 1,
              "features": [
                {
                  "type": "Feature",
                  "id": "parks.9",
                  "properties": { "name": "Lagoon" },
                  "geometry": { "type": "Point", "coordinates": [-157.84, 21.29] }
                }
              ]
            }
            """;
        var client = TestHelpers.CreateFeatureServerClient(req =>
        {
            Assert.Contains("f=geojson", req.RequestUri!.Query);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(geoJson));
        });

        var result = await client.QueryVectorAsync(
            "parks",
            0,
            new FeatureServerQueryParams(),
            VectorPayloadFormat.GeoJson);

        Assert.Equal(VectorPayloadFormat.GeoJson, result.Format);
        var point = Assert.IsType<Point>(Assert.Single(result.Features).Geometry);
        Assert.Equal(-157.84, point.X, 2);
    }
}
