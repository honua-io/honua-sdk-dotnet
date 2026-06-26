// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Tests;

public class FeatureRecordGeometryExtensionsTests
{
    [Fact]
    public void GetGeometry_ReadsEsriJson_Point()
    {
        var record = RecordWithGeometry(
            """{ "x": -122.4, "y": 37.8, "z": 5, "spatialReference": { "wkid": 4326 } }""");

        var point = Assert.IsType<Point>(record.GetGeometry());

        Assert.Equal(-122.4, point.X, 6);
        Assert.Equal(37.8, point.Y, 6);
        Assert.Equal(5, point.Coordinate.Z);
        Assert.Equal(4326, point.SRID);
    }

    [Fact]
    public void GetGeometry_ReadsEsriJson_PolygonRingOrientation()
    {
        var record = RecordWithGeometry(
            """{ "rings": [ [ [0,0],[10,0],[10,10],[0,10],[0,0] ] ] }""");

        var polygon = Assert.IsType<Polygon>(record.GetGeometry());

        // Esri outer rings are clockwise; NTS normalizes shells to counter-clockwise.
        Assert.True(NetTopologySuite.Algorithm.Orientation.IsCCW(polygon.ExteriorRing.Coordinates));
    }

    [Fact]
    public void GetGeometry_DetectsAndReadsGeoJson_LineString()
    {
        var record = RecordWithGeometry(
            """{ "type": "LineString", "coordinates": [ [-158,21],[-157,22] ] }""");

        var line = Assert.IsType<LineString>(record.GetGeometry());

        Assert.Equal(2, line.NumPoints);
        Assert.Equal(-158, line.GetCoordinateN(0).X);
        Assert.Equal(22, line.GetCoordinateN(1).Y);
    }

    [Fact]
    public void GetGeometry_HonorsExplicitFormat()
    {
        var record = RecordWithGeometry(
            """{ "type": "Point", "coordinates": [-122.4, 37.8] }""");

        var point = Assert.IsType<Point>(record.GetGeometry(FeatureGeometryFormat.GeoJson));

        Assert.Equal(-122.4, point.X, 6);
    }

    [Fact]
    public void GetGeometry_ReturnsNull_WhenNoGeometry()
    {
        Assert.Null(new FeatureRecord().GetGeometry());
    }

    private static FeatureRecord RecordWithGeometry(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new FeatureRecord { Geometry = document.RootElement.Clone() };
    }
}
