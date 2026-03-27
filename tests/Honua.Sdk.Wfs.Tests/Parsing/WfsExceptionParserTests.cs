// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Wfs.Parsing;

namespace Honua.Sdk.Wfs.Tests.Parsing;

public sealed class WfsExceptionParserTests
{
    [Fact]
    public void TryParse_ValidExceptionReport_ReturnsReport()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
              <ows:Exception exceptionCode="InvalidParameterValue" locator="TYPENAMES">
                <ows:ExceptionText>Feature type 'nonexistent' not found</ows:ExceptionText>
              </ows:Exception>
            </ows:ExceptionReport>
            """;

        var report = WfsExceptionParser.TryParse(xml);

        Assert.NotNull(report);
        Assert.Equal("InvalidParameterValue", report.ExceptionCode);
        Assert.Equal("TYPENAMES", report.Locator);
        Assert.Equal("Feature type 'nonexistent' not found", report.ExceptionText);
    }

    [Fact]
    public void TryParse_ExceptionWithoutText_ReturnsReportWithNullText()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ows:ExceptionReport xmlns:ows="http://www.opengis.net/ows/1.1" version="2.0.0">
              <ows:Exception exceptionCode="OperationNotSupported"/>
            </ows:ExceptionReport>
            """;

        var report = WfsExceptionParser.TryParse(xml);

        Assert.NotNull(report);
        Assert.Equal("OperationNotSupported", report.ExceptionCode);
        Assert.Null(report.ExceptionText);
        Assert.Null(report.Locator);
    }

    [Fact]
    public void TryParse_NonXml_ReturnsNull()
    {
        var result = WfsExceptionParser.TryParse("not xml at all");

        Assert.Null(result);
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsNull()
    {
        Assert.Null(WfsExceptionParser.TryParse(""));
    }

    [Fact]
    public void TryParse_NullString_ReturnsNull()
    {
        Assert.Null(WfsExceptionParser.TryParse(null!));
    }

    [Fact]
    public void TryParse_XmlWithoutExceptionReport_ReturnsNull()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <root><child>text</child></root>
            """;

        Assert.Null(WfsExceptionParser.TryParse(xml));
    }

    [Fact]
    public void TryParse_GeoJson_ReturnsNull()
    {
        var json = """{ "type": "FeatureCollection", "features": [] }""";

        Assert.Null(WfsExceptionParser.TryParse(json));
    }
}
