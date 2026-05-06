// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Geometry.Vector;
using Honua.Sdk.OgcFeatures.Models;
using Honua.Sdk.OgcFeatures.Tests.Fixtures;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.OgcFeatures.Tests.OgcFeatures;

public sealed class OgcFeaturesVectorPayloadTests
{
    [Fact]
    public async Task GetItemsVectorAsync_DefaultsToGeoJsonAndParsesNtsGeometry()
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
                  "properties": { "name": "Kewalo" },
                  "geometry": { "type": "Point", "coordinates": [-157.861, 21.293] }
                }
              ]
            }
            """;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            Assert.Equal("/ogc/features/collections/parks/items", req.RequestUri!.AbsolutePath);
            Assert.Contains("f=json", req.RequestUri.Query);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(geoJson));
        });

        var result = await client.GetItemsVectorAsync("parks");

        Assert.Equal(VectorPayloadFormat.GeoJson, result.Format);
        Assert.Equal(1, result.NumberMatched);
        var feature = Assert.Single(result.Features);
        Assert.Equal("Kewalo", feature.Attributes["name"].GetString());
        Assert.IsType<Point>(feature.Geometry);
    }

    [Fact]
    public async Task GetItemsVectorAsync_Gml_RequestsGmlAndParsesNtsGeometry()
    {
        const string gml = """
            <gml:FeatureCollection
                xmlns:gml="http://www.opengis.net/gml/3.2"
                xmlns:honua="https://honua.io/schemas/test"
                numberMatched="1"
                numberReturned="1">
              <gml:featureMember>
                <honua:park gml:id="parks.2">
                  <honua:name>Magic Island</honua:name>
                  <honua:shape>
                    <gml:Point srsName="EPSG:4326">
                      <gml:pos>-157.847 21.286</gml:pos>
                    </gml:Point>
                  </honua:shape>
                </honua:park>
              </gml:featureMember>
            </gml:FeatureCollection>
            """;
        var client = TestHelpers.CreateOgcFeaturesClient(req =>
        {
            Assert.Contains("f=gml", req.RequestUri!.Query);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(gml, System.Text.Encoding.UTF8, "application/gml+xml")
            });
        });

        var result = await client.GetItemsVectorAsync(
            "parks",
            new OgcItemsParams { Limit = 1 },
            VectorPayloadFormat.Gml);

        Assert.Equal(VectorPayloadFormat.Gml, result.Format);
        var feature = Assert.Single(result.Features);
        Assert.Equal("parks.2", feature.Id);
        Assert.Equal("Magic Island", feature.Attributes["name"].GetString());
        Assert.Equal(4326, Assert.IsType<Point>(feature.Geometry).SRID);
    }
}
