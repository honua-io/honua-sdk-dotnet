// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text;
using Honua.Sdk.Geometry.Vector;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Tests;

public sealed class VectorPayloadReaderTests
{
    [Fact]
    public async Task EsriJsonReader_ParsesFeaturesIntoNtsGeometry()
    {
        const string json = """
            {
              "objectIdFieldName": "OBJECTID",
              "spatialReference": { "wkid": 4326 },
              "features": [
                {
                  "attributes": { "OBJECTID": 42, "name": "Ala Moana" },
                  "geometry": { "x": -157.842, "y": 21.291 }
                }
              ],
              "exceededTransferLimit": true
            }
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(json), VectorPayloadFormat.EsriJson);

        Assert.Equal(VectorPayloadFormat.EsriJson, result.Format);
        Assert.Equal("OBJECTID", result.ObjectIdFieldName);
        Assert.True(result.HasMoreResults);
        var feature = Assert.Single(result.Features);
        Assert.Equal("42", feature.Id);
        Assert.Equal("Ala Moana", feature.Attributes["name"].GetString());
        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(4326, point.SRID);
        Assert.Equal(-157.842, point.X, 3);
        Assert.Equal(21.291, point.Y, 3);
    }

    [Fact]
    public async Task GeoJsonReader_ParsesFeatureCollectionIntoNtsGeometry()
    {
        const string json = """
            {
              "type": "FeatureCollection",
              "numberMatched": 2,
              "numberReturned": 1,
              "bbox": [-158, 21, -157, 22],
              "links": [
                { "rel": "next", "href": "http://localhost/next", "type": "application/geo+json" }
              ],
              "features": [
                {
                  "type": "Feature",
                  "id": "parks.1",
                  "properties": { "name": "Kapiolani" },
                  "geometry": {
                    "type": "LineString",
                    "coordinates": [[-157.82, 21.27], [-157.81, 21.28]]
                  }
                }
              ]
            }
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(json), VectorPayloadFormat.GeoJson);

        Assert.Equal(VectorPayloadFormat.GeoJson, result.Format);
        Assert.Equal(2, result.NumberMatched);
        Assert.Equal(1, result.NumberReturned);
        Assert.True(result.HasMoreResults);
        Assert.NotNull(result.Extent);
        var feature = Assert.Single(result.Features);
        Assert.Equal("parks.1", feature.Id);
        Assert.Equal("Kapiolani", feature.Attributes["name"].GetString());
        var line = Assert.IsType<LineString>(feature.Geometry);
        Assert.Equal(2, line.NumPoints);
    }

    [Fact]
    public async Task GmlReader_ParsesWfsMembersIntoNtsGeometry()
    {
        const string gml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <wfs:FeatureCollection
                xmlns:wfs="http://www.opengis.net/wfs/2.0"
                xmlns:gml="http://www.opengis.net/gml/3.2"
                xmlns:honua="https://honua.io/schemas/test"
                numberMatched="1"
                numberReturned="1">
              <wfs:member>
                <honua:park gml:id="parks.1">
                  <honua:name>Kapiolani</honua:name>
                  <honua:rating>5</honua:rating>
                  <honua:shape>
                    <gml:Point srsName="http://www.opengis.net/def/crs/EPSG/0/4326">
                      <gml:pos>-157.819 21.267</gml:pos>
                    </gml:Point>
                  </honua:shape>
                </honua:park>
              </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(gml), VectorPayloadFormat.Gml);

        Assert.Equal(VectorPayloadFormat.Gml, result.Format);
        Assert.Equal(1, result.NumberMatched);
        Assert.Equal(1, result.NumberReturned);
        var feature = Assert.Single(result.Features);
        Assert.Equal("parks.1", feature.Id);
        Assert.Equal("park", feature.NativeTypeName);
        Assert.Equal("Kapiolani", feature.Attributes["name"].GetString());
        Assert.Equal(5, feature.Attributes["rating"].GetInt32());
        var point = Assert.IsType<Point>(feature.Geometry);
        Assert.Equal(4326, point.SRID);
        Assert.Equal(-157.819, point.X, 3);
        Assert.Equal(21.267, point.Y, 3);
    }

    private static Stream ToStream(string payload)
        => new MemoryStream(Encoding.UTF8.GetBytes(payload));
}
