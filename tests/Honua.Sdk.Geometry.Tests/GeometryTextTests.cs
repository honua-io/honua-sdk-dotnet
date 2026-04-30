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
}
