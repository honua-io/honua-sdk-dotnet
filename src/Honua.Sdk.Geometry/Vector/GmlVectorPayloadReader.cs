// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.GML2;

namespace Honua.Sdk.Geometry.Vector;

/// <summary>
/// Reads GML feature payloads into typed vector features.
/// </summary>
public sealed class GmlVectorPayloadReader : IVectorPayloadReader
{
    private static readonly XNamespace Wfs20 = "http://www.opengis.net/wfs/2.0";
    private static readonly XNamespace Wfs11 = "http://www.opengis.net/wfs";
    private static readonly XNamespace Gml32 = "http://www.opengis.net/gml/3.2";
    private static readonly XNamespace Gml = "http://www.opengis.net/gml";
    private static readonly XNamespace Xml = "http://www.w3.org/XML/1998/namespace";
    private static readonly GeometryFactory DefaultGeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory();

    /// <summary>Shared stateless reader instance.</summary>
    public static GmlVectorPayloadReader Instance { get; } = new();

    /// <inheritdoc />
    public VectorPayloadFormat Format => VectorPayloadFormat.Gml;

    /// <inheritdoc />
    public async Task<VectorPayloadFeatureSet> ReadAsync(
        Stream stream,
        VectorPayloadReadOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var reader = XmlReader.Create(stream, settings);
        var document = await XDocument.LoadAsync(reader, LoadOptions.None, ct).ConfigureAwait(false);
        return Read(document, options);
    }

    /// <summary>
    /// Reads a GML payload from an XML document.
    /// </summary>
    /// <param name="document">GML or WFS XML document.</param>
    /// <param name="options">Optional reader settings.</param>
    /// <returns>The typed feature collection.</returns>
    public VectorPayloadFeatureSet Read(XDocument document, VectorPayloadReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.Root ?? throw new XmlException("GML vector payload has no root element.");
        var features = ReadFeatureElements(root, options).ToList();
        var numberReturned = TryReadInt(root.Attribute("numberReturned")?.Value) ?? features.Count;
        var numberMatched = ReadNumberMatched(root.Attribute("numberMatched")?.Value);

        return new VectorPayloadFeatureSet
        {
            Format = Format,
            Features = features,
            NumberMatched = numberMatched,
            NumberReturned = numberReturned,
            HasMoreResults = numberMatched.HasValue && numberReturned < numberMatched.Value,
            Extent = ReadEnvelope(root, options?.GeometryFactory),
            Crs = ReadFirstSrsName(root)
        };
    }

    private static IEnumerable<VectorPayloadFeature> ReadFeatureElements(
        XElement root,
        VectorPayloadReadOptions? options)
    {
        var members = GetMemberElements(root).ToList();
        if (members.Count == 0)
        {
            if (IsGeometryElement(root))
            {
                yield return ReadGeometryOnlyFeature(root, options);
            }

            yield break;
        }

        foreach (var member in members)
        {
            var featureElement = GetFeatureElement(member);
            if (featureElement is null)
            {
                continue;
            }

            yield return ReadFeature(featureElement, options);
        }
    }

    private static IEnumerable<XElement> GetMemberElements(XElement root)
    {
        var directMembers = root.Elements().Where(IsMemberElement).SelectMany(ExpandMemberElement).ToList();
        if (directMembers.Count > 0)
        {
            return directMembers;
        }

        if (IsMemberElement(root))
        {
            return ExpandMemberElement(root);
        }

        return root.Descendants().Where(IsMemberElement).SelectMany(ExpandMemberElement);
    }

    private static IEnumerable<XElement> ExpandMemberElement(XElement member)
    {
        if (string.Equals(member.Name.LocalName, "featureMembers", StringComparison.Ordinal))
        {
            return member.Elements().Where(element => !IsFrameworkElement(element));
        }

        return [member];
    }

    private static XElement? GetFeatureElement(XElement member)
    {
        if (!IsMemberElement(member))
        {
            return member;
        }

        return member.Elements().FirstOrDefault(element => !IsFrameworkElement(element));
    }

    private static VectorPayloadFeature ReadGeometryOnlyFeature(XElement geometryElement, VectorPayloadReadOptions? options)
    {
        var geometry = ReadGeometry(geometryElement, options?.GeometryFactory);
        var crs = ReadSrsName(geometryElement);
        ApplySrid(geometry, crs);
        return new VectorPayloadFeature
        {
            Geometry = geometry,
            NativeTypeName = geometryElement.Name.LocalName,
            Crs = crs
        };
    }

    private static VectorPayloadFeature ReadFeature(XElement featureElement, VectorPayloadReadOptions? options)
    {
        NetTopologySuite.Geometries.Geometry? geometry = null;
        string? crs = null;
        var attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var propertyElement in featureElement.Elements())
        {
            if (IsFrameworkElement(propertyElement))
            {
                continue;
            }

            var geometryElement = IsGeometryElement(propertyElement)
                ? propertyElement
                : propertyElement.Descendants().FirstOrDefault(IsGeometryElement);

            if (geometryElement is not null && geometry is null)
            {
                geometry = ReadGeometry(geometryElement, options?.GeometryFactory);
                crs = ReadSrsName(geometryElement);
                ApplySrid(geometry, crs);
                continue;
            }

            if (!propertyElement.HasElements)
            {
                attributes[propertyElement.Name.LocalName] = CreateJsonElement(propertyElement.Value);
            }
        }

        return new VectorPayloadFeature
        {
            Id = ReadFeatureId(featureElement),
            Attributes = attributes,
            Geometry = geometry,
            NativeTypeName = featureElement.Name.LocalName,
            Crs = crs
        };
    }

    private static NetTopologySuite.Geometries.Geometry ReadGeometry(
        XElement geometryElement,
        GeometryFactory? geometryFactory)
    {
        var reader = new GMLReader(geometryFactory ?? DefaultGeometryFactory);
        return reader.Read(new XDocument(new XElement(geometryElement)));
    }

    private static Envelope? ReadEnvelope(XElement root, GeometryFactory? geometryFactory)
    {
        var envelopeElement = root.Descendants()
            .FirstOrDefault(element => IsGeometryNamespace(element.Name.Namespace) &&
                string.Equals(element.Name.LocalName, "Envelope", StringComparison.Ordinal));
        if (envelopeElement is null)
        {
            return null;
        }

        try
        {
            var geometry = ReadGeometry(envelopeElement, geometryFactory);
            return geometry.EnvelopeInternal;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? ReadFeatureId(XElement featureElement)
    {
        var gmlId = featureElement.Attribute(Gml32 + "id") ??
            featureElement.Attribute(Gml + "id") ??
            featureElement.Attribute("id") ??
            featureElement.Attribute(Xml + "id");
        return gmlId?.Value;
    }

    private static string? ReadFirstSrsName(XElement root)
        => root.DescendantsAndSelf()
            .Select(ReadSrsName)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string? ReadSrsName(XElement element)
        => element.Attribute("srsName")?.Value;

    private static void ApplySrid(NetTopologySuite.Geometries.Geometry geometry, string? crs)
    {
        if (TryReadSrid(crs, out var srid))
        {
            geometry.SRID = srid;
        }
    }

    private static bool TryReadSrid(string? crs, out int srid)
    {
        srid = 0;
        if (string.IsNullOrWhiteSpace(crs))
        {
            return false;
        }

        var trimmed = crs.Trim();
        var separators = new[] { '/', ':' };
        var index = trimmed.LastIndexOfAny(separators);
        var code = index >= 0 ? trimmed[(index + 1)..] : trimmed;
        return int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out srid) && srid > 0;
    }

    private static bool IsMemberElement(XElement element)
    {
        return (IsWfsNamespace(element.Name.Namespace) || IsGeometryNamespace(element.Name.Namespace)) &&
            (string.Equals(element.Name.LocalName, "member", StringComparison.Ordinal) ||
             string.Equals(element.Name.LocalName, "featureMember", StringComparison.Ordinal) ||
             string.Equals(element.Name.LocalName, "featureMembers", StringComparison.Ordinal));
    }

    private static bool IsFrameworkElement(XElement element)
    {
        return IsWfsNamespace(element.Name.Namespace) ||
            IsGeometryNamespace(element.Name.Namespace) ||
            string.Equals(element.Name.LocalName, "boundedBy", StringComparison.Ordinal);
    }

    private static bool IsGeometryElement(XElement element)
    {
        if (!IsGeometryNamespace(element.Name.Namespace))
        {
            return false;
        }

        return element.Name.LocalName switch
        {
            "Point" or
            "LineString" or
            "LinearRing" or
            "Polygon" or
            "MultiPoint" or
            "MultiLineString" or
            "MultiCurve" or
            "MultiPolygon" or
            "MultiSurface" or
            "GeometryCollection" or
            "Envelope" => true,
            _ => false
        };
    }

    private static bool IsWfsNamespace(XNamespace ns)
        => ns == Wfs20 || ns == Wfs11;

    private static bool IsGeometryNamespace(XNamespace ns)
        => ns == Gml32 || ns == Gml;

    private static long? ReadNumberMatched(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int? TryReadInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static JsonElement CreateJsonElement(string value)
    {
        if (bool.TryParse(value, out var boolean))
        {
            return boolean ? JsonElementFromLiteral("true") : JsonElementFromLiteral("false");
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            return JsonElementFromLiteral(value);
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
            value.Any(char.IsDigit))
        {
            return JsonElementFromLiteral(value);
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStringValue(value);
        }

        return JsonElementFromBytes(stream.ToArray());
    }

    private static JsonElement JsonElementFromLiteral(string literal)
        => JsonElementFromBytes(Encoding.UTF8.GetBytes(literal));

    private static JsonElement JsonElementFromBytes(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }
}
