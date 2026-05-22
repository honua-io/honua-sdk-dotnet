// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.OgcFeatures.Wfs.Parsing;

namespace Honua.Sdk.OgcFeatures.WfsTests.Parsing;

public sealed class WfsCapabilitiesParserTests
{
    private const string SampleCapabilities = """
        <?xml version="1.0" encoding="UTF-8"?>
        <wfs:WFS_Capabilities version="2.0.0"
          xmlns:wfs="http://www.opengis.net/wfs/2.0"
          xmlns:ows="http://www.opengis.net/ows/1.1"
          xmlns:xlink="http://www.w3.org/1999/xlink">
          <ows:ServiceIdentification>
            <ows:Title>Honua WFS</ows:Title>
            <ows:Abstract>Test WFS service</ows:Abstract>
            <ows:ServiceType>WFS</ows:ServiceType>
            <ows:ServiceTypeVersion>2.0.0</ows:ServiceTypeVersion>
          </ows:ServiceIdentification>
          <ows:OperationsMetadata>
            <ows:Operation name="GetFeature">
              <ows:Parameter name="outputFormat">
                <ows:AllowedValues>
                  <ows:Value>application/geo+json</ows:Value>
                  <ows:Value>application/gml+xml; version=3.2</ows:Value>
                </ows:AllowedValues>
              </ows:Parameter>
            </ows:Operation>
          </ows:OperationsMetadata>
          <wfs:FeatureTypeList>
            <wfs:FeatureType>
              <wfs:Name>parcels</wfs:Name>
              <wfs:Title>Land Parcels</wfs:Title>
              <wfs:Abstract>Cadastral parcels</wfs:Abstract>
              <wfs:DefaultCRS>urn:ogc:def:crs:EPSG::4326</wfs:DefaultCRS>
              <wfs:OtherCRS>urn:ogc:def:crs:EPSG::3857</wfs:OtherCRS>
              <ows:WGS84BoundingBox>
                <ows:LowerCorner>-180.0 -90.0</ows:LowerCorner>
                <ows:UpperCorner>180.0 90.0</ows:UpperCorner>
              </ows:WGS84BoundingBox>
            </wfs:FeatureType>
          </wfs:FeatureTypeList>
        </wfs:WFS_Capabilities>
        """;

    [Fact]
    public void Parse_ValidCapabilities_ReturnsVersion()
    {
        var result = WfsCapabilitiesParser.Parse(SampleCapabilities);

        Assert.Equal("2.0.0", result.Version);
    }

    [Fact]
    public void Parse_ValidCapabilities_ReturnsServiceIdentification()
    {
        var result = WfsCapabilitiesParser.Parse(SampleCapabilities);

        Assert.Equal("Honua WFS", result.Title);
        Assert.Equal("Test WFS service", result.Abstract);
        Assert.Equal("WFS", result.ServiceType);
        Assert.Equal("2.0.0", result.ServiceTypeVersion);
    }

    [Fact]
    public void Parse_ValidCapabilities_ReturnsOutputFormats()
    {
        var result = WfsCapabilitiesParser.Parse(SampleCapabilities);

        Assert.Equal(2, result.OutputFormats.Count);
        Assert.Contains("application/geo+json", result.OutputFormats);
        Assert.Contains("application/gml+xml; version=3.2", result.OutputFormats);
    }

    [Fact]
    public void Parse_ValidCapabilities_ReturnsFeatureTypes()
    {
        var result = WfsCapabilitiesParser.Parse(SampleCapabilities);

        Assert.Single(result.FeatureTypes);
        var ft = result.FeatureTypes[0];
        Assert.Equal("parcels", ft.Name);
        Assert.Equal("Land Parcels", ft.Title);
        Assert.Equal("Cadastral parcels", ft.Abstract);
        Assert.Equal("urn:ogc:def:crs:EPSG::4326", ft.DefaultCrs);
        Assert.Single(ft.OtherCrs);
        Assert.Equal("urn:ogc:def:crs:EPSG::3857", ft.OtherCrs[0]);
    }

    [Fact]
    public void Parse_ValidCapabilities_ReturnsBoundingBox()
    {
        var result = WfsCapabilitiesParser.Parse(SampleCapabilities);

        var ft = result.FeatureTypes[0];
        Assert.NotNull(ft.LowerCorner);
        Assert.Equal(-180.0, ft.LowerCorner.Value.X);
        Assert.Equal(-90.0, ft.LowerCorner.Value.Y);
        Assert.NotNull(ft.UpperCorner);
        Assert.Equal(180.0, ft.UpperCorner.Value.X);
        Assert.Equal(90.0, ft.UpperCorner.Value.Y);
    }

    [Fact]
    public void Parse_MinimalCapabilities_ReturnsDefaults()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <wfs:WFS_Capabilities version="2.0.0"
              xmlns:wfs="http://www.opengis.net/wfs/2.0"
              xmlns:ows="http://www.opengis.net/ows/1.1">
              <wfs:FeatureTypeList/>
            </wfs:WFS_Capabilities>
            """;

        var result = WfsCapabilitiesParser.Parse(xml);

        Assert.Equal("2.0.0", result.Version);
        Assert.Null(result.Title);
        Assert.Empty(result.FeatureTypes);
        Assert.Empty(result.OutputFormats);
    }
}
