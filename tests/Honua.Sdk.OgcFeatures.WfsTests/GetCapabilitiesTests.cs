// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.OgcFeatures.Wfs.Exceptions;
using Honua.Sdk.OgcFeatures.WfsTests.Fixtures;

namespace Honua.Sdk.OgcFeatures.WfsTests;

public sealed class GetCapabilitiesTests
{
    private const string ValidCapabilities = """
        <?xml version="1.0" encoding="UTF-8"?>
        <wfs:WFS_Capabilities version="2.0.0"
          xmlns:wfs="http://www.opengis.net/wfs/2.0"
          xmlns:ows="http://www.opengis.net/ows/1.1">
          <ows:ServiceIdentification>
            <ows:Title>Test WFS</ows:Title>
          </ows:ServiceIdentification>
          <wfs:FeatureTypeList>
            <wfs:FeatureType>
              <wfs:Name>parcels</wfs:Name>
              <wfs:Title>Land Parcels</wfs:Title>
              <wfs:DefaultCRS>urn:ogc:def:crs:EPSG::4326</wfs:DefaultCRS>
              <ows:WGS84BoundingBox>
                <ows:LowerCorner>-180 -90</ows:LowerCorner>
                <ows:UpperCorner>180 90</ows:UpperCorner>
              </ows:WGS84BoundingBox>
            </wfs:FeatureType>
          </wfs:FeatureTypeList>
        </wfs:WFS_Capabilities>
        """;

    [Fact]
    public async Task GetCapabilities_Success_ParsesResponse()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("/wfs?", req.RequestUri!.PathAndQuery);
            Assert.Contains("REQUEST=GetCapabilities", req.RequestUri.Query);
            Assert.Contains("SERVICE=WFS", req.RequestUri.Query);
            Assert.Contains("VERSION=2.0.0", req.RequestUri.Query);
            return Task.FromResult(TestHelpers.CreateXmlResponse(ValidCapabilities));
        });

        var caps = await client.GetCapabilitiesAsync();

        Assert.Equal("2.0.0", caps.Version);
        Assert.Equal("Test WFS", caps.Title);
        Assert.Single(caps.FeatureTypes);
        Assert.Equal("parcels", caps.FeatureTypes[0].Name);
    }

    [Fact]
    public async Task GetCapabilities_HttpError_ThrowsHonuaWfsException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "text/plain")));

        var ex = await Assert.ThrowsAsync<HonuaWfsException>(
            () => client.GetCapabilitiesAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task GetCapabilities_ExceptionReport_ThrowsWithOgcCode()
    {
        var exceptionXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
              <ows:Exception exceptionCode="OperationNotSupported">
                <ows:ExceptionText>GetCapabilities not supported</ows:ExceptionText>
              </ows:Exception>
            </ows:ExceptionReport>
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateXmlResponse(exceptionXml)));

        var ex = await Assert.ThrowsAsync<HonuaWfsException>(
            () => client.GetCapabilitiesAsync());

        Assert.Equal("OperationNotSupported", ex.ExceptionCode);
        Assert.Equal("GetCapabilities not supported", ex.Message);
    }

    [Fact]
    public async Task GetCapabilities_ExceptionReportOn200_ThrowsHonuaWfsException()
    {
        var exceptionXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
              <ows:Exception exceptionCode="NoApplicableCode">
                <ows:ExceptionText>Server error</ows:ExceptionText>
              </ows:Exception>
            </ows:ExceptionReport>
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateXmlResponse(exceptionXml, HttpStatusCode.OK)));

        var ex = await Assert.ThrowsAsync<HonuaWfsException>(
            () => client.GetCapabilitiesAsync());

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
        Assert.Equal("NoApplicableCode", ex.ExceptionCode);
    }
}
