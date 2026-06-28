// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Linq;
using System.Text.Json;
using Honua.Sdk.Geometry;
using NetTopologySuite.Algorithm;
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
    public void ReadGeometry_PreservesInteriorRingsForMultipolygon()
    {
        using var document = JsonDocument.Parse(
            """
            {
                "rings": [
                    [[0,0],[0,10],[10,10],[10,0],[0,0]],
                    [[2,2],[8,2],[8,8],[2,8],[2,2]],
                    [[20,0],[20,10],[30,10],[30,0],[20,0]],
                    [[22,2],[28,2],[28,8],[22,8],[22,2]]
                ],
                "spatialReference": { "wkid": 4326 }
            }
            """);

        var geometry = GeoServicesGeometryConverter.ReadGeometry(document.RootElement);

        var multiPolygon = Assert.IsType<MultiPolygon>(geometry);
        Assert.Equal(2, multiPolygon.NumGeometries);

        var firstPolygon = Assert.IsType<Polygon>(multiPolygon.GetGeometryN(0));
        var secondPolygon = Assert.IsType<Polygon>(multiPolygon.GetGeometryN(1));
        Assert.Equal(1, firstPolygon.NumInteriorRings);
        Assert.Equal(1, secondPolygon.NumInteriorRings);
    }

    [Fact]
    public void ReadGeometry_LeadingHoleRing_ClassifiesByOrientationNotPosition()
    {
        // The CCW hole ring is listed FIRST, before the CW outer shell. Esri JSON does
        // not mandate shell-before-hole ordering; classification must be by orientation
        // (CW = shell, CCW = hole), so the hole must not be promoted to the exterior.
        using var document = JsonDocument.Parse(
            """
            {
                "rings": [
                    [[2,2],[8,2],[8,8],[2,8],[2,2]],
                    [[0,0],[0,10],[10,10],[10,0],[0,0]]
                ],
                "spatialReference": { "wkid": 4326 }
            }
            """);

        var geometry = GeoServicesGeometryConverter.ReadGeometry(document.RootElement);

        var polygon = Assert.IsType<Polygon>(geometry);
        Assert.True(polygon.IsValid);
        Assert.Equal(1, polygon.NumInteriorRings);
        // Exterior is the 10x10 outer ring; the 6x6 hole is subtracted -> area 100 - 36 = 64.
        Assert.Equal(5, polygon.ExteriorRing.NumPoints);
        Assert.Equal(64.0, polygon.Area, 6);
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

    [Fact]
    public void WriteGeometry_NormalizesPolygonRingOrientationToEsriConvention()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        // Exterior wound CCW and hole wound CW: the GeoJSON (RFC 7946) convention, which
        // is the OPPOSITE of Esri's clockwise-exterior / counter-clockwise-hole rule. The
        // writer must normalize so a strict Esri consumer (and this SDK's orientation-aware
        // reader) does not mis-demote the exterior ring of a multi-ring polygon to a hole.
        var shell = factory.CreateLinearRing(
        [
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0),
        ]);
        var hole = factory.CreateLinearRing(
        [
            new Coordinate(2, 2),
            new Coordinate(2, 8),
            new Coordinate(8, 8),
            new Coordinate(8, 2),
            new Coordinate(2, 2),
        ]);
        Assert.True(Orientation.IsCCW(shell.CoordinateSequence));
        Assert.False(Orientation.IsCCW(hole.CoordinateSequence));

        var polygon = factory.CreatePolygon(shell, [hole]);

        var json = GeoServicesGeometryConverter.WriteGeometry(polygon);

        var rings = json.GetProperty("rings");
        Assert.Equal(2, rings.GetArrayLength());

        // Esri convention: clockwise exterior (not CCW), counter-clockwise hole.
        Assert.False(Orientation.IsCCW(ReadRing(rings[0])));
        Assert.True(Orientation.IsCCW(ReadRing(rings[1])));

        // The normalized geometry round-trips back to a polygon with its hole intact.
        var roundTripped = Assert.IsType<Polygon>(GeoServicesGeometryConverter.ReadGeometry(json));
        Assert.Equal(1, roundTripped.NumInteriorRings);
    }

    private static Coordinate[] ReadRing(JsonElement ring)
        => ring.EnumerateArray()
            .Select(coordinate => new Coordinate(coordinate[0].GetDouble(), coordinate[1].GetDouble()))
            .ToArray();
}
