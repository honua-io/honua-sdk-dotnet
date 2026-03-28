// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Xml.Linq;
using Honua.Sdk.Wfs.Models;

namespace Honua.Sdk.Wfs.Parsing;

/// <summary>
/// Parses WFS 2.0 GetCapabilities XML responses.
/// </summary>
internal static class WfsCapabilitiesParser
{
    public static WfsCapabilities Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidOperationException("Empty capabilities document.");

        var version = root.Attribute("version")?.Value ?? "";

        // ServiceIdentification
        var si = root.Element(WfsNamespaces.Ows + "ServiceIdentification");
        var title = si?.Element(WfsNamespaces.Ows + "Title")?.Value;
        var abstract_ = si?.Element(WfsNamespaces.Ows + "Abstract")?.Value;
        var serviceType = si?.Element(WfsNamespaces.Ows + "ServiceType")?.Value;
        var serviceTypeVersion = si?.Element(WfsNamespaces.Ows + "ServiceTypeVersion")?.Value;

        // Global output formats from OperationsMetadata
        var outputFormats = ParseGlobalOutputFormats(root);

        // Feature types
        var featureTypeList = root.Element(WfsNamespaces.Wfs + "FeatureTypeList");
        var featureTypes = featureTypeList?
            .Elements(WfsNamespaces.Wfs + "FeatureType")
            .Select(ParseFeatureType)
            .ToList() ?? [];

        return new WfsCapabilities
        {
            Version = version,
            Title = title,
            Abstract = abstract_,
            ServiceType = serviceType,
            ServiceTypeVersion = serviceTypeVersion,
            FeatureTypes = featureTypes,
            OutputFormats = outputFormats,
        };
    }

    private static WfsFeatureType ParseFeatureType(XElement el)
    {
        var name = el.Element(WfsNamespaces.Wfs + "Name")?.Value ?? "";
        var title = el.Element(WfsNamespaces.Wfs + "Title")?.Value;
        var abstract_ = el.Element(WfsNamespaces.Wfs + "Abstract")?.Value;
        var defaultCrs = el.Element(WfsNamespaces.Wfs + "DefaultCRS")?.Value;

        var otherCrs = el.Elements(WfsNamespaces.Wfs + "OtherCRS")
            .Select(e => e.Value)
            .ToList();

        var outputFormats = el.Element(WfsNamespaces.Wfs + "OutputFormats")?
            .Elements(WfsNamespaces.Wfs + "Format")
            .Select(e => e.Value)
            .ToList() ?? [];

        // WGS84BoundingBox
        (double, double)? lower = null;
        (double, double)? upper = null;
        var bbox = el.Element(WfsNamespaces.Ows + "WGS84BoundingBox");
        if (bbox is not null)
        {
            lower = ParseCorner(bbox.Element(WfsNamespaces.Ows + "LowerCorner")?.Value);
            upper = ParseCorner(bbox.Element(WfsNamespaces.Ows + "UpperCorner")?.Value);
        }

        return new WfsFeatureType
        {
            Name = name,
            Title = title,
            Abstract = abstract_,
            DefaultCrs = defaultCrs,
            OtherCrs = otherCrs,
            OutputFormats = outputFormats,
            LowerCorner = lower,
            UpperCorner = upper,
        };
    }

    private static List<string> ParseGlobalOutputFormats(XElement root)
    {
        var opsMeta = root.Element(WfsNamespaces.Ows + "OperationsMetadata");
        if (opsMeta is null) return [];

        var getFeatureOp = opsMeta.Elements(WfsNamespaces.Ows + "Operation")
            .FirstOrDefault(e => e.Attribute("name")?.Value == "GetFeature");
        if (getFeatureOp is null) return [];

        var outputFormatParam = getFeatureOp.Elements(WfsNamespaces.Ows + "Parameter")
            .FirstOrDefault(p => p.Attribute("name")?.Value == "outputFormat");
        if (outputFormatParam is null) return [];

        return outputFormatParam
            .Element(WfsNamespaces.Ows + "AllowedValues")?
            .Elements(WfsNamespaces.Ows + "Value")
            .Select(v => v.Value)
            .ToList() ?? [];
    }

    private static (double X, double Y)? ParseCorner(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;

        if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            return (x, y);
        }

        return null;
    }
}
