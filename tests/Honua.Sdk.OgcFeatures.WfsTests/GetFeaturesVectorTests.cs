// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry.Vector;
using Honua.Sdk.OgcFeatures.Wfs.Models;
using Honua.Sdk.OgcFeatures.WfsTests.Fixtures;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.OgcFeatures.WfsTests;

public sealed class GetFeaturesVectorTests
{
    [Fact]
    public async Task GetFeaturesVectorAsync_DefaultsToGeoJsonAndParsesNtsGeometry()
    {
        const string geoJson = """
            {
              "type": "FeatureCollection",
              "numberMatched": 1,
              "numberReturned": 1,
              "features": [
                {
                  "type": "Feature",
                  "id": "parks.1",
                  "properties": { "name": "Sand Island" },
                  "geometry": { "type": "Point", "coordinates": [-157.886, 21.319] }
                }
              ]
            }
            """;
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("OUTPUTFORMAT=application%2Fgeo%2Bjson", req.RequestUri!.Query);
            return Task.FromResult(TestHelpers.CreateGeoJsonResponse(geoJson));
        });

        var result = await client.GetFeaturesVectorAsync(new GetFeaturesRequest { TypeNames = "parks" });

        Assert.Equal(VectorPayloadFormat.GeoJson, result.Format);
        var feature = Assert.Single(result.Features);
        Assert.Equal("parks.1", feature.Id);
        Assert.Equal("Sand Island", feature.Attributes["name"].GetString());
        Assert.IsType<Point>(feature.Geometry);
    }

    [Fact]
    public async Task GetFeaturesVectorAsync_Gml_RequestsGmlAndParsesNtsGeometry()
    {
        const string gml = """
            <wfs:FeatureCollection
                xmlns:wfs="http://www.opengis.net/wfs/2.0"
                xmlns:gml="http://www.opengis.net/gml/3.2"
                xmlns:honua="https://honua.io/schemas/test"
                numberMatched="1"
                numberReturned="1">
              <wfs:member>
                <honua:park gml:id="parks.3">
                  <honua:name>Foster</honua:name>
                  <honua:shape>
                    <gml:Point srsName="EPSG:4326">
                      <gml:pos>-157.858 21.315</gml:pos>
                    </gml:Point>
                  </honua:shape>
                </honua:park>
              </wfs:member>
            </wfs:FeatureCollection>
            """;
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("OUTPUTFORMAT=application%2Fgml%2Bxml%3B%20version%3D3.2", req.RequestUri!.Query);
            return Task.FromResult(TestHelpers.CreateXmlResponse(gml));
        });

        var result = await client.GetFeaturesVectorAsync(
            new GetFeaturesRequest { TypeNames = "parks" },
            VectorPayloadFormat.Gml);

        Assert.Equal(VectorPayloadFormat.Gml, result.Format);
        var feature = Assert.Single(result.Features);
        Assert.Equal("parks.3", feature.Id);
        Assert.Equal("Foster", feature.Attributes["name"].GetString());
        Assert.Equal(4326, Assert.IsType<Point>(feature.Geometry).SRID);
    }
}
