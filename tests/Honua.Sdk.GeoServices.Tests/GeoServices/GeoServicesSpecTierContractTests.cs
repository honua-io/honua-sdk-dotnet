// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.GeoServices.Tests.GeoServices;

/// <summary>
/// Contract tests for the GeoServices ImageServer, GeometryServer, and
/// raster <c>exportImage</c> surfaces.
///
/// These surfaces are currently spec-tier only: the canonical protocol
/// identifiers and capability sets are declared in <see cref="FeatureProtocolIds"/>
/// and <see cref="FeatureProtocolCapabilities"/>, but no .NET client implements
/// them yet (compare with the implemented FeatureServer and NAServer routing
/// clients). These tests lock down the published protocol/capability contract so
/// the wiring stays stable while the clients are built. The expected client
/// behavior for the unimplemented operations is documented in
/// <c>GeoServicesSpecTierGapTests</c> as clearly-marked skipped tests.
/// </summary>
public sealed class GeoServicesSpecTierContractTests
{
    // -- ImageServer protocol id + aliases ------------------------------------

    [Fact]
    public void ImageService_CanonicalProtocolId_IsStable()
    {
        Assert.Equal("geoservices-image-service", FeatureProtocolIds.GeoServicesImageService);
        Assert.Contains(FeatureProtocolIds.GeoServicesImageService, FeatureProtocolIds.All);
    }

    [Theory]
    [InlineData("geoservices-image-service")]
    [InlineData("image-server")]
    [InlineData("imageserver")]
    [InlineData("IMAGESERVER")]
    public void ImageService_Aliases_NormalizeToCanonicalId(string alias)
    {
        Assert.Equal(
            FeatureProtocolIds.GeoServicesImageService,
            FeatureProtocolIds.Normalize(alias));
        Assert.True(FeatureProtocolIds.Matches(alias, FeatureProtocolIds.GeoServicesImageService));
    }

    [Fact]
    public void ImageService_AliasesFor_IncludesKnownProviderNames()
    {
        var aliases = FeatureProtocolIds.AliasesFor(FeatureProtocolIds.GeoServicesImageService);

        Assert.Contains(FeatureProtocolIds.GeoServicesImageService, aliases);
        Assert.Contains("image-server", aliases);
        Assert.Contains("imageserver", aliases);
    }

    [Fact]
    public void ImageService_DefaultCapabilities_AdvertiseImageRenderAndTiles()
    {
        var capabilities = FeatureProtocolCapabilities.DefaultsFor(
            FeatureProtocolIds.GeoServicesImageService);

        Assert.Contains(FeatureCapabilities.Image, capabilities);
        Assert.Contains(FeatureCapabilities.Render, capabilities);
        Assert.Contains(FeatureCapabilities.Tiles, capabilities);
        Assert.Contains(FeatureCapabilities.Query, capabilities);
        Assert.Contains(FeatureCapabilities.QueryExtent, capabilities);
        Assert.Contains(FeatureCapabilities.QueryObjectIds, capabilities);
        Assert.Contains(FeatureCapabilities.Connect, capabilities);
    }

    // -- GeometryServer protocol id + aliases ---------------------------------

    [Fact]
    public void GeometryService_CanonicalProtocolId_IsStable()
    {
        Assert.Equal("geoservices-geometry-service", FeatureProtocolIds.GeoServicesGeometryService);
        Assert.Contains(FeatureProtocolIds.GeoServicesGeometryService, FeatureProtocolIds.All);
    }

    [Theory]
    [InlineData("geoservices-geometry-service")]
    [InlineData("geometry-server")]
    [InlineData("geometryserver")]
    [InlineData("GeometryServer")]
    public void GeometryService_Aliases_NormalizeToCanonicalId(string alias)
    {
        Assert.Equal(
            FeatureProtocolIds.GeoServicesGeometryService,
            FeatureProtocolIds.Normalize(alias));
        Assert.True(FeatureProtocolIds.Matches(alias, FeatureProtocolIds.GeoServicesGeometryService));
    }

    [Fact]
    public void GeometryService_AliasesFor_IncludesKnownProviderNames()
    {
        var aliases = FeatureProtocolIds.AliasesFor(FeatureProtocolIds.GeoServicesGeometryService);

        Assert.Contains(FeatureProtocolIds.GeoServicesGeometryService, aliases);
        Assert.Contains("geometry-server", aliases);
        Assert.Contains("geometryserver", aliases);
    }

    [Fact]
    public void GeometryService_DefaultCapabilities_AdvertiseGeometryAndConnect()
    {
        var capabilities = FeatureProtocolCapabilities.DefaultsFor(
            FeatureProtocolIds.GeoServicesGeometryService);

        Assert.Contains(FeatureCapabilities.Geometry, capabilities);
        Assert.Contains(FeatureCapabilities.Connect, capabilities);
    }

    // -- exportImage (MapServer / ImageServer raster export) ------------------

    [Fact]
    public void MapService_DefaultCapabilities_AdvertiseRenderForExportImage()
    {
        // exportImage is a raster export operation exposed by GeoServices
        // MapServer and ImageServer endpoints. It is surfaced through the
        // `render` (and, for ImageServer, `image`) capability rather than a
        // dedicated capability id.
        var mapCapabilities = FeatureProtocolCapabilities.DefaultsFor(
            FeatureProtocolIds.GeoServicesMapService);
        var imageCapabilities = FeatureProtocolCapabilities.DefaultsFor(
            FeatureProtocolIds.GeoServicesImageService);

        Assert.Contains(FeatureCapabilities.Render, mapCapabilities);
        Assert.Contains(FeatureCapabilities.Render, imageCapabilities);
        Assert.Contains(FeatureCapabilities.Image, imageCapabilities);
    }
}
