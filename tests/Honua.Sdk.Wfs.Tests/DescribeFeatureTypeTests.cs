// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Wfs.Exceptions;
using Honua.Sdk.Wfs.Tests.Fixtures;

namespace Honua.Sdk.Wfs.Tests;

public sealed class DescribeFeatureTypeTests
{
    private const string ValidSchema = """
        <?xml version="1.0" encoding="UTF-8"?>
        <xsd:schema xmlns:xsd="http://www.w3.org/2001/XMLSchema"
          xmlns:gml="http://www.opengis.net/gml/3.2"
          targetNamespace="http://honua.io/parcels"
          elementFormDefault="qualified">
          <xsd:element name="parcels" type="parcels:parcelsType"
            xmlns:parcels="http://honua.io/parcels"
            substitutionGroup="gml:AbstractFeature"/>
          <xsd:complexType name="parcelsType">
            <xsd:complexContent>
              <xsd:extension base="gml:AbstractFeatureType">
                <xsd:sequence>
                  <xsd:element name="geometry" type="gml:PointPropertyType" minOccurs="0" maxOccurs="1" nillable="true"/>
                  <xsd:element name="parcel_id" type="xsd:string" minOccurs="1" maxOccurs="1" nillable="false"/>
                  <xsd:element name="area_sqm" type="xsd:double" minOccurs="0" maxOccurs="1" nillable="true"/>
                </xsd:sequence>
              </xsd:extension>
            </xsd:complexContent>
          </xsd:complexType>
        </xsd:schema>
        """;

    [Fact]
    public async Task DescribeFeatureType_Success_ParsesSchema()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("REQUEST=DescribeFeatureType", req.RequestUri!.Query);
            Assert.Contains("TYPENAMES=parcels", req.RequestUri.Query);
            return Task.FromResult(TestHelpers.CreateXmlResponse(ValidSchema));
        });

        var schema = await client.DescribeFeatureTypeAsync("parcels");

        Assert.Equal("http://honua.io/parcels", schema.TargetNamespace);
        Assert.Equal("parcels", schema.ElementName);
        Assert.Equal(3, schema.Properties.Count);
    }

    [Fact]
    public async Task DescribeFeatureType_ParsesPropertyDetails()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateXmlResponse(ValidSchema)));

        var schema = await client.DescribeFeatureTypeAsync("parcels");

        var geometry = schema.Properties[0];
        Assert.Equal("geometry", geometry.Name);
        Assert.Equal("gml:PointPropertyType", geometry.Type);
        Assert.Equal(0, geometry.MinOccurs);
        Assert.True(geometry.Nillable);

        var parcelId = schema.Properties[1];
        Assert.Equal("parcel_id", parcelId.Name);
        Assert.Equal("xsd:string", parcelId.Type);
        Assert.Equal(1, parcelId.MinOccurs);
        Assert.False(parcelId.Nillable);
    }

    [Fact]
    public async Task DescribeFeatureType_NullTypeName_ThrowsArgumentNullException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateXmlResponse("<xsd:schema/>")));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.DescribeFeatureTypeAsync(null!));
    }

    [Fact]
    public async Task DescribeFeatureType_ExceptionReport_ThrowsHonuaWfsException()
    {
        var exceptionXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
              <ows:Exception exceptionCode="InvalidParameterValue" locator="TYPENAMES">
                <ows:ExceptionText>Feature type 'bad' not found</ows:ExceptionText>
              </ows:Exception>
            </ows:ExceptionReport>
            """;

        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateXmlResponse(exceptionXml, HttpStatusCode.BadRequest)));

        var ex = await Assert.ThrowsAsync<HonuaWfsException>(
            () => client.DescribeFeatureTypeAsync("bad"));

        Assert.Equal("InvalidParameterValue", ex.ExceptionCode);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }
}
