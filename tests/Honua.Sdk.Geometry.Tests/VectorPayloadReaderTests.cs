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

    [Fact]
    public async Task GeoJsonReader_Parses3DBbox_MapsHorizontalExtentIgnoringElevation()
    {
        // RFC 7946 section 5: a 3D bbox is [west, south, minElev, east, north, maxElev].
        const string json = """
            {
              "type": "FeatureCollection",
              "bbox": [-158, 21, 0, -157, 22, 1500],
              "features": []
            }
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(json), VectorPayloadFormat.GeoJson);

        Assert.NotNull(result.Extent);
        Assert.Equal(-158, result.Extent!.MinX, 6); // west
        Assert.Equal(-157, result.Extent.MaxX, 6);  // east
        Assert.Equal(21, result.Extent.MinY, 6);    // south
        Assert.Equal(22, result.Extent.MaxY, 6);    // north
    }

    [Fact]
    public async Task GeoJsonReader_Parses2DBbox_Unchanged()
    {
        const string json = """
            {
              "type": "FeatureCollection",
              "bbox": [-158, 21, -157, 22],
              "features": []
            }
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(json), VectorPayloadFormat.GeoJson);

        Assert.NotNull(result.Extent);
        Assert.Equal(-158, result.Extent!.MinX, 6);
        Assert.Equal(-157, result.Extent.MaxX, 6);
        Assert.Equal(21, result.Extent.MinY, 6);
        Assert.Equal(22, result.Extent.MaxY, 6);
    }

    [Fact]
    public async Task GmlReader_UrnGeographicCrs_SwapsAxesToLonLat()
    {
        // urn:ogc:def:crs:EPSG::4326 mandates lat,lon axis order; the pos below is
        // "lat lon" (21.267 -157.819) and must come back as lon,lat (X=-157.819).
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
                  <honua:shape>
                    <gml:Point srsName="urn:ogc:def:crs:EPSG::4326">
                      <gml:pos>21.267 -157.819</gml:pos>
                    </gml:Point>
                  </honua:shape>
                </honua:park>
              </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(gml), VectorPayloadFormat.Gml);

        var point = Assert.IsType<Point>(Assert.Single(result.Features).Geometry);
        Assert.Equal(4326, point.SRID);
        Assert.Equal(-157.819, point.X, 3); // longitude, western hemisphere
        Assert.Equal(21.267, point.Y, 3);   // latitude, northern hemisphere
    }

    [Fact]
    public async Task GmlReader_NonUrnGeographicCrs_DoesNotSwapAxes()
    {
        // The short EPSG:4326 form is interpreted as lon,lat and must NOT be swapped.
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
                  <honua:shape>
                    <gml:Point srsName="EPSG:4326">
                      <gml:pos>-157.819 21.267</gml:pos>
                    </gml:Point>
                  </honua:shape>
                </honua:park>
              </wfs:member>
            </wfs:FeatureCollection>
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(gml), VectorPayloadFormat.Gml);

        var point = Assert.IsType<Point>(Assert.Single(result.Features).Geometry);
        Assert.Equal(-157.819, point.X, 3);
        Assert.Equal(21.267, point.Y, 3);
    }

    [Fact]
    public async Task EsriJsonReader_WebMercatorWkid_MapsToEpsg3857()
    {
        const string json = """
            {
              "spatialReference": { "wkid": 102100 },
              "features": [
                { "attributes": { "OBJECTID": 1 }, "geometry": { "x": -17578142.0, "y": 2428724.0 } }
              ]
            }
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(json), VectorPayloadFormat.EsriJson);

        Assert.Equal("EPSG:3857", result.Crs);
        Assert.Equal("EPSG:3857", Assert.Single(result.Features).Crs);
    }

    [Fact]
    public async Task EsriJsonReader_LatestWkidPreferred_MapsToEpsg()
    {
        const string json = """
            {
              "spatialReference": { "wkid": 102100, "latestWkid": 3857 },
              "features": [
                { "attributes": { "OBJECTID": 1 }, "geometry": { "x": -17578142.0, "y": 2428724.0 } }
              ]
            }
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(json), VectorPayloadFormat.EsriJson);

        Assert.Equal("EPSG:3857", result.Crs);
    }

    [Fact]
    public async Task EsriJsonReader_UnknownEsriProprietaryWkid_UsesEsriAuthority()
    {
        const string json = """
            {
              "spatialReference": { "wkid": 53034 },
              "features": [
                { "attributes": { "OBJECTID": 1 }, "geometry": { "x": 0.0, "y": 0.0 } }
              ]
            }
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(json), VectorPayloadFormat.EsriJson);

        Assert.Equal("ESRI:53034", result.Crs);
        Assert.Equal("ESRI:53034", Assert.Single(result.Features).Crs);
    }

    [Fact]
    public async Task EsriJsonReader_StandardEpsgWkid_UsesEpsgAuthority()
    {
        const string json = """
            {
              "spatialReference": { "wkid": 4326 },
              "features": [
                { "attributes": { "OBJECTID": 1 }, "geometry": { "x": -157.8, "y": 21.3 } }
              ]
            }
            """;

        var result = await VectorPayloadReaders.ReadAsync(ToStream(json), VectorPayloadFormat.EsriJson);

        Assert.Equal("EPSG:4326", result.Crs);
    }

    private static Stream ToStream(string payload)
        => new MemoryStream(Encoding.UTF8.GetBytes(payload));
}
