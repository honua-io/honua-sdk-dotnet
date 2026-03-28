// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Xml.Linq;
using Honua.Sdk.Wfs.Exceptions;

namespace Honua.Sdk.Wfs.Parsing;

/// <summary>
/// Detects and parses OGC ExceptionReport XML from WFS error responses.
/// </summary>
internal static class WfsExceptionParser
{
    /// <summary>
    /// Attempts to parse an OGC ExceptionReport from the response body.
    /// Returns <c>null</c> if the body is not an ExceptionReport.
    /// </summary>
    public static WfsExceptionReport? TryParse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        // Quick check before full XML parse
        if (!body.Contains("ExceptionReport", StringComparison.Ordinal))
            return null;

        try
        {
            var doc = XDocument.Parse(body);
            var root = doc.Root;
            if (root is null || root.Name.LocalName != "ExceptionReport")
                return null;

            var exception = root.Element(WfsNamespaces.Ows + "Exception")
                ?? root.Elements().FirstOrDefault(e => e.Name.LocalName == "Exception");

            if (exception is null)
                return null;

            var exceptionCode = exception.Attribute("exceptionCode")?.Value ?? "NoApplicableCode";
            var locator = exception.Attribute("locator")?.Value;

            var exceptionText = exception.Element(WfsNamespaces.Ows + "ExceptionText")?.Value
                ?? exception.Elements().FirstOrDefault(e => e.Name.LocalName == "ExceptionText")?.Value;

            return new WfsExceptionReport
            {
                ExceptionCode = exceptionCode,
                ExceptionText = exceptionText,
                Locator = locator,
            };
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}
