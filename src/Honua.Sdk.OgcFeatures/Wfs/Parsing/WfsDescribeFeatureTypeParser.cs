// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Xml.Linq;
using Honua.Sdk.OgcFeatures.Wfs.Models;

namespace Honua.Sdk.OgcFeatures.Wfs.Parsing;

/// <summary>
/// Parses WFS DescribeFeatureType XSD responses.
/// </summary>
internal static class WfsDescribeFeatureTypeParser
{
    public static WfsFeatureTypeSchema Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new InvalidOperationException("Empty schema document.");

        var targetNamespace = root.Attribute("targetNamespace")?.Value;

        // Find the complexType that defines the feature properties
        var complexType = root.Descendants(WfsNamespaces.Xsd + "complexType").FirstOrDefault();
        var sequence = complexType?
            .Descendants(WfsNamespaces.Xsd + "sequence")
            .FirstOrDefault();

        // Find root element name
        var rootElement = root.Elements(WfsNamespaces.Xsd + "element").FirstOrDefault();
        var elementName = rootElement?.Attribute("name")?.Value;

        var properties = new List<WfsSchemaProperty>();
        if (sequence is not null)
        {
            foreach (var el in sequence.Elements(WfsNamespaces.Xsd + "element"))
            {
                var name = el.Attribute("name")?.Value ?? "";
                var type = el.Attribute("type")?.Value ?? "";
                var minOccurs = ParseInt(el.Attribute("minOccurs")?.Value, 1);
                var maxOccursStr = el.Attribute("maxOccurs")?.Value;
                var maxOccurs = maxOccursStr == "unbounded" ? -1 : ParseInt(maxOccursStr, 1);
                var nillable = string.Equals(el.Attribute("nillable")?.Value, "true",
                    StringComparison.OrdinalIgnoreCase);

                properties.Add(new WfsSchemaProperty
                {
                    Name = name,
                    Type = type,
                    MinOccurs = minOccurs,
                    MaxOccurs = maxOccurs,
                    Nillable = nillable,
                });
            }
        }

        return new WfsFeatureTypeSchema
        {
            TargetNamespace = targetNamespace,
            ElementName = elementName,
            Properties = properties,
        };
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        if (value is null) return defaultValue;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : defaultValue;
    }
}
