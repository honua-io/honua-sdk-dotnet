// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Tests;

public class HonuaFeatureReprojectorTests
{
    private static readonly GeometryFactory Factory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    [Fact]
    public void Reproject_TransformsGeometryToTargetCrs()
    {
        var reprojector = new HonuaFeatureReprojector();
        var point = Factory.CreatePoint(new Coordinate(-157.8583, 21.3069));

        var result = reprojector.Reproject(point, HonuaSpatialReference.Wgs84, HonuaSpatialReference.WebMercator);

        Assert.NotSame(point, result);
        Assert.Equal(3857, result.SRID);
        Assert.InRange(result.Coordinate.X, -17_600_000, -17_500_000);
        Assert.InRange(result.Coordinate.Y, 2_400_000, 2_500_000);
    }

    [Fact]
    public void Reproject_ReturnsInputUnchanged_WhenCrsMatches()
    {
        var reprojector = new HonuaFeatureReprojector();
        var point = Factory.CreatePoint(new Coordinate(-157.8583, 21.3069));

        var result = reprojector.Reproject(point, HonuaSpatialReference.Wgs84, HonuaSpatialReference.Wgs84);

        Assert.Same(point, result);
    }

    [Fact]
    public void Reproject_Collection_TransformsEachGeometryAndPassesNullsThrough()
    {
        var reprojector = new HonuaFeatureReprojector();
        var first = Factory.CreatePoint(new Coordinate(-157.8583, 21.3069));
        var second = Factory.CreatePoint(new Coordinate(0, 0));

        var results = reprojector.Reproject(
            [first, null, second],
            HonuaSpatialReference.Wgs84,
            HonuaSpatialReference.WebMercator);

        Assert.Equal(3, results.Count);
        Assert.NotNull(results[0]);
        Assert.Equal(3857, results[0]!.SRID);
        Assert.Null(results[1]);
        Assert.InRange(results[0]!.Coordinate.X, -17_600_000, -17_500_000);
        Assert.Equal(0, results[2]!.Coordinate.X, 3);
    }

    [Fact]
    public void AreEquivalent_MatchesByWkid()
    {
        Assert.True(HonuaFeatureReprojector.AreEquivalent(
            HonuaSpatialReference.Wgs84,
            HonuaSpatialReference.FromWkid(4326)));
        Assert.False(HonuaFeatureReprojector.AreEquivalent(
            HonuaSpatialReference.Wgs84,
            HonuaSpatialReference.WebMercator));
        Assert.False(HonuaFeatureReprojector.AreEquivalent(null, HonuaSpatialReference.Wgs84));
    }
}
