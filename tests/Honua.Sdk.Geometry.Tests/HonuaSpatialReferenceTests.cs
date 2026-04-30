// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry;

namespace Honua.Sdk.Geometry.Tests;

public class HonuaSpatialReferenceTests
{
    [Theory]
    [InlineData("EPSG:4326", 4326)]
    [InlineData("WKID:3857", 3857)]
    [InlineData("http://www.opengis.net/def/crs/EPSG/0/4326", 4326)]
    [InlineData("urn:ogc:def:crs:EPSG::3857", 3857)]
    public void Parse_RecognizesCommonIdentifiers(string value, int expectedWkid)
    {
        var spatialReference = HonuaSpatialReference.Parse(value);

        Assert.Equal(expectedWkid, spatialReference.Wkid);
    }

    [Fact]
    public void ToOgcCrsIdentifier_ReturnsOgcUriForAuthorityCode()
    {
        var spatialReference = HonuaSpatialReference.FromWkid(4326);

        Assert.Equal(
            "http://www.opengis.net/def/crs/EPSG/0/4326",
            spatialReference.ToOgcCrsIdentifier());
    }

    [Fact]
    public void Parse_PreservesNonEpsgAuthorityCode()
    {
        var spatialReference = HonuaSpatialReference.Parse("IAU:49900");

        Assert.Equal("IAU", spatialReference.Authority);
        Assert.Equal(49900, spatialReference.Code);
        Assert.Null(spatialReference.Wkid);
    }
}
