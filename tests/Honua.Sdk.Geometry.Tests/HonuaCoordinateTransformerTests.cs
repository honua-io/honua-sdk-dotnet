// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Tests;

public class HonuaCoordinateTransformerTests
{
    [Fact]
    public void TransformCoordinate_UsesProjNet()
    {
        var transformer = new HonuaCoordinateTransformer();
        var source = HonuaSpatialReference.Wgs84;
        var target = HonuaSpatialReference.WebMercator;

        var result = transformer.Transform(new Coordinate(-157.8583, 21.3069), source, target);

        Assert.InRange(result.X, -17_600_000, -17_500_000);
        Assert.InRange(result.Y, 2_400_000, 2_500_000);
    }

    [Fact]
    public void TransformGeometry_ReturnsGeometryCopyWithTargetSrid()
    {
        var transformer = new HonuaCoordinateTransformer();
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var point = factory.CreatePoint(new Coordinate(-157.8583, 21.3069));

        var result = transformer.Transform(point, HonuaSpatialReference.Wgs84, HonuaSpatialReference.WebMercator);

        Assert.NotSame(point, result);
        Assert.Equal(3857, result.SRID);
        Assert.NotEqual(point.Coordinate.X, result.Coordinate.X);
    }
}
