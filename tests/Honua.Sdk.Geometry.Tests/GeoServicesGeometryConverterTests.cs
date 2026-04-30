// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Tests;

public class GeoServicesGeometryConverterTests
{
    [Fact]
    public void ReadGeometry_ParsesPointWithSpatialReference()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "x": -157.8583,
                "y": 21.3069,
                "z": 12.5,
                "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
            }
            """);

        var geometry = GeoServicesGeometryConverter.ReadGeometry(document.RootElement);

        var point = Assert.IsType<Point>(geometry);
        Assert.Equal(4326, point.SRID);
        Assert.Equal(12.5, point.Coordinate.Z);
    }

    [Fact]
    public void ReadGeometry_ParsesPolygonRings()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "rings": [[
                    [-158, 21],
                    [-157, 21],
                    [-157, 22],
                    [-158, 21]
                ]],
                "spatialReference": { "wkid": 4326 }
            }
            """);

        var geometry = GeoServicesGeometryConverter.ReadGeometry(document.RootElement);

        var polygon = Assert.IsType<Polygon>(geometry);
        Assert.Equal(4326, polygon.SRID);
        Assert.Equal(4, polygon.ExteriorRing.NumPoints);
    }

    [Fact]
    public void WriteGeometry_WritesLinePathWithDimensionality()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var line = factory.CreateLineString(
            [
                new CoordinateZM(-158, 21, 10, 1),
                new CoordinateZM(-157, 22, 11, 2),
            ]);

        var json = GeoServicesGeometryConverter.WriteGeometry(line, HonuaSpatialReference.FromWkid(4326));

        Assert.True(json.GetProperty("hasZ").GetBoolean());
        Assert.True(json.GetProperty("hasM").GetBoolean());
        Assert.Equal(JsonValueKind.Array, json.GetProperty("paths").ValueKind);
        Assert.Equal(4326, json.GetProperty("spatialReference").GetProperty("wkid").GetInt32());
    }

    [Fact]
    public void WriteGeometry_WritesXymPathWithoutZPlaceholder()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var line = factory.CreateLineString(
            [
                new CoordinateM(-158, 21, 1),
                new CoordinateM(-157, 22, 2),
            ]);

        var json = GeoServicesGeometryConverter.WriteGeometry(line);
        var firstCoordinate = json.GetProperty("paths")[0][0];

        Assert.True(json.GetProperty("hasM").GetBoolean());
        Assert.False(json.TryGetProperty("hasZ", out _));
        Assert.Equal(3, firstCoordinate.GetArrayLength());
        Assert.Equal(1, firstCoordinate[2].GetDouble());
    }
}
