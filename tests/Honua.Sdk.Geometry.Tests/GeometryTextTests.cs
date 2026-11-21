// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Tests;

public class GeometryTextTests
{
    [Fact]
    public void ReadWkt_ParsesPoint()
    {
        var geometry = GeometryText.ReadWkt("POINT (-157.8583 21.3069)");

        var point = Assert.IsType<Point>(geometry);
        Assert.Equal(-157.8583, point.X, precision: 4);
        Assert.Equal(21.3069, point.Y, precision: 4);
    }

    [Fact]
    public void Wkb_RoundTripsPolygon()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var polygon = factory.CreatePolygon(
            [
                new Coordinate(-158, 21),
                new Coordinate(-157, 21),
                new Coordinate(-157, 22),
                new Coordinate(-158, 21),
            ]);

        var wkb = GeometryText.WriteWkb(polygon, includeSrid: true);
        var roundTripped = GeometryText.ReadWkb(wkb);

        Assert.True(polygon.EqualsExact(roundTripped));
    }

    [Fact]
    public void Wkt_Preserves3DAnd4DCoordinates()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var pointZ = factory.CreatePoint(new CoordinateZ(-157.8583, 21.3069, 42.5));
        var pointM = factory.CreatePoint(new CoordinateM(-157.8583, 21.3069, 7.25));
        var pointZm = factory.CreatePoint(new CoordinateZM(-157.8583, 21.3069, 42.5, 7.25));

        var wktZ = GeometryText.WriteWkt(pointZ);
        var wktM = GeometryText.WriteWkt(pointM);
        var wktZm = GeometryText.WriteWkt(pointZm);

        var parsedZ = Assert.IsType<Point>(GeometryText.ReadWkt(wktZ));
        var parsedM = Assert.IsType<Point>(GeometryText.ReadWkt(wktM));
        var parsedZm = Assert.IsType<Point>(GeometryText.ReadWkt(wktZm));

        Assert.Equal(42.5, parsedZ.Coordinate.Z);
        Assert.Equal(7.25, parsedM.Coordinate.M);
        Assert.Equal(42.5, parsedZm.Coordinate.Z);
        Assert.Equal(7.25, parsedZm.Coordinate.M);
    }

    [Fact]
    public void Wkb_Preserves3DAnd4DCoordinates()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var pointZ = factory.CreatePoint(new CoordinateZ(-157.8583, 21.3069, 42.5));
        var pointM = factory.CreatePoint(new CoordinateM(-157.8583, 21.3069, 7.25));
        var pointZm = factory.CreatePoint(new CoordinateZM(-157.8583, 21.3069, 42.5, 7.25));

        var parsedZ = Assert.IsType<Point>(GeometryText.ReadWkb(GeometryText.WriteWkb(pointZ, includeSrid: true)));
        var parsedM = Assert.IsType<Point>(GeometryText.ReadWkb(GeometryText.WriteWkb(pointM, includeSrid: true)));
        var parsedZm = Assert.IsType<Point>(GeometryText.ReadWkb(GeometryText.WriteWkb(pointZm, includeSrid: true)));

        Assert.Equal(4326, parsedZ.SRID);
        Assert.Equal(42.5, parsedZ.Coordinate.Z);
        Assert.Equal(7.25, parsedM.Coordinate.M);
        Assert.Equal(42.5, parsedZm.Coordinate.Z);
        Assert.Equal(7.25, parsedZm.Coordinate.M);
    }
}
