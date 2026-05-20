// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Geometry;

namespace Honua.Sdk.Geometry.Tests;

public class GeographicBoundingBoxTests
{
    [Fact]
    public void Construction_WithValidCoordinates_Succeeds()
    {
        var bbox = new GeographicBoundingBox(-122.5, 37.5, -122.0, 38.0);

        Assert.Equal(-122.5, bbox.MinLongitude);
        Assert.Equal(37.5, bbox.MinLatitude);
        Assert.Equal(-122.0, bbox.MaxLongitude);
        Assert.Equal(38.0, bbox.MaxLatitude);
    }

    [Fact]
    public void Construction_WithMinLongitudeNotLessThanMax_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new GeographicBoundingBox(10.0, 0.0, 10.0, 1.0));
        Assert.Throws<ArgumentException>(
            () => new GeographicBoundingBox(11.0, 0.0, 10.0, 1.0));
    }

    [Fact]
    public void Construction_WithMinLatitudeNotLessThanMax_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new GeographicBoundingBox(0.0, 5.0, 1.0, 5.0));
        Assert.Throws<ArgumentException>(
            () => new GeographicBoundingBox(0.0, 6.0, 1.0, 5.0));
    }

    [Fact]
    public void Construction_WithNonFiniteCoordinate_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new GeographicBoundingBox(double.NaN, 0.0, 1.0, 1.0));
        Assert.Throws<ArgumentException>(
            () => new GeographicBoundingBox(0.0, 0.0, double.PositiveInfinity, 1.0));
    }

    [Fact]
    public void Contains_ReturnsTrueForInteriorPoint()
    {
        var bbox = new GeographicBoundingBox(-122.5, 37.5, -122.0, 38.0);

        Assert.True(bbox.Contains(-122.25, 37.75));
    }

    [Fact]
    public void Contains_ReturnsTrueOnBoundary()
    {
        var bbox = new GeographicBoundingBox(-122.5, 37.5, -122.0, 38.0);

        Assert.True(bbox.Contains(-122.5, 37.5));
        Assert.True(bbox.Contains(-122.0, 38.0));
    }

    [Fact]
    public void Contains_ReturnsFalseForOutsidePoints()
    {
        var bbox = new GeographicBoundingBox(-122.5, 37.5, -122.0, 38.0);

        Assert.False(bbox.Contains(-123.0, 37.75));
        Assert.False(bbox.Contains(-122.25, 39.0));
        Assert.False(bbox.Contains(-121.0, 37.75));
        Assert.False(bbox.Contains(-122.25, 36.0));
    }

    [Fact]
    public void Intersects_OverlappingBoxes_ReturnsTrue()
    {
        var a = new GeographicBoundingBox(-10.0, -10.0, 10.0, 10.0);
        var b = new GeographicBoundingBox(0.0, 0.0, 20.0, 20.0);

        Assert.True(a.Intersects(b));
        Assert.True(b.Intersects(a));
    }

    [Fact]
    public void Intersects_DisjointBoxes_ReturnsFalse()
    {
        var a = new GeographicBoundingBox(-10.0, -10.0, 0.0, 0.0);
        var b = new GeographicBoundingBox(1.0, 1.0, 5.0, 5.0);

        Assert.False(a.Intersects(b));
        Assert.False(b.Intersects(a));
    }

    [Fact]
    public void Intersects_TouchingBoxes_ReturnsTrue()
    {
        var a = new GeographicBoundingBox(0.0, 0.0, 5.0, 5.0);
        var b = new GeographicBoundingBox(5.0, 0.0, 10.0, 5.0);

        Assert.True(a.Intersects(b));
    }

    [Fact]
    public void Intersects_WithNull_Throws()
    {
        var bbox = new GeographicBoundingBox(-1.0, -1.0, 1.0, 1.0);

        Assert.Throws<ArgumentNullException>(() => bbox.Intersects(null!));
    }

    [Fact]
    public void ToFeatureBoundingBox_PopulatesWgs84Crs()
    {
        var bbox = new GeographicBoundingBox(-122.5, 37.5, -122.0, 38.0);

        var converted = bbox.ToFeatureBoundingBox();

        Assert.Equal(-122.5, converted.MinX);
        Assert.Equal(37.5, converted.MinY);
        Assert.Equal(-122.0, converted.MaxX);
        Assert.Equal(38.0, converted.MaxY);
        Assert.Equal("EPSG:4326", converted.Crs);
    }

    [Fact]
    public void RoundTrip_PreservesCoordinates()
    {
        var original = new GeographicBoundingBox(-122.5, 37.5, -122.0, 38.0);

        var roundTripped = GeographicBoundingBox.FromFeatureBoundingBox(original.ToFeatureBoundingBox());

        Assert.Equal(original, roundTripped);
    }

    [Theory]
    [InlineData("EPSG:4326")]
    [InlineData("epsg:4326")]
    [InlineData("urn:ogc:def:crs:EPSG::4326")]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/4326")]
    [InlineData("http://www.opengis.net/def/crs/OGC/1.3/CRS84")]
    [InlineData("4326")]
    [InlineData(null)]
    public void FromFeatureBoundingBox_AcceptsWgs84Crs(string? crs)
    {
        var input = new FeatureBoundingBox
        {
            MinX = -122.5,
            MinY = 37.5,
            MaxX = -122.0,
            MaxY = 38.0,
            Crs = crs,
        };

        var bbox = GeographicBoundingBox.FromFeatureBoundingBox(input);

        Assert.Equal(-122.5, bbox.MinLongitude);
        Assert.Equal(38.0, bbox.MaxLatitude);
    }

    [Fact]
    public void FromFeatureBoundingBox_WithNonWgs84Crs_Throws()
    {
        var input = new FeatureBoundingBox
        {
            MinX = -1.0,
            MinY = -1.0,
            MaxX = 1.0,
            MaxY = 1.0,
            Crs = "EPSG:3857",
        };

        Assert.Throws<ArgumentException>(
            () => GeographicBoundingBox.FromFeatureBoundingBox(input));
    }

    [Fact]
    public void FromFeatureBoundingBox_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => GeographicBoundingBox.FromFeatureBoundingBox(null!));
    }
}
