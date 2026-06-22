// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Text.Json;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Vector;

/// <summary>
/// Reads GeoServices/Esri JSON feature payloads into typed vector features.
/// </summary>
public sealed class EsriJsonVectorPayloadReader : IVectorPayloadReader
{
    private static readonly GeometryFactory DefaultGeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory();

    /// <summary>Shared stateless reader instance.</summary>
    public static EsriJsonVectorPayloadReader Instance { get; } = new();

    /// <inheritdoc />
    public VectorPayloadFormat Format => VectorPayloadFormat.EsriJson;

    /// <inheritdoc />
    public async Task<VectorPayloadFeatureSet> ReadAsync(
        Stream stream,
        VectorPayloadReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Read(document.RootElement, options);
    }

    /// <summary>
    /// Reads a GeoServices/Esri JSON payload from a JSON element.
    /// </summary>
    /// <param name="root">Payload root element.</param>
    /// <param name="options">Optional reader settings.</param>
    /// <returns>The typed feature collection.</returns>
    public VectorPayloadFeatureSet Read(JsonElement root, VectorPayloadReadOptions? options = null)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Esri JSON vector payload must be a JSON object.");
        }

        var spatialReference = root.TryGetProperty("spatialReference", out var srElement)
            ? ReadSpatialReferenceIdentifier(srElement)
            : null;
        var rootFactory = CreateGeometryFactory(spatialReference, options?.GeometryFactory);
        var objectIdFieldName = TryGetString(root, "objectIdFieldName");
        var features = ReadFeatures(root, objectIdFieldName, rootFactory).ToList();

        return new VectorPayloadFeatureSet
        {
            Format = Format,
            Features = features,
            NumberMatched = TryGetInt64(root, "count"),
            NumberReturned = features.Count,
            HasMoreResults = TryGetBoolean(root, "exceededTransferLimit"),
            ObjectIdFieldName = objectIdFieldName,
            ObjectIds = ReadObjectIds(root),
            Extent = ReadExtent(root, rootFactory),
            Crs = spatialReference
        };
    }

    private static IEnumerable<VectorPayloadFeature> ReadFeatures(
        JsonElement root,
        string? objectIdFieldName,
        GeometryFactory rootFactory)
    {
        if (!root.TryGetProperty("features", out var featuresElement) ||
            featuresElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var featureElement in featuresElement.EnumerateArray())
        {
            if (featureElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var attributes = featureElement.TryGetProperty("attributes", out var attributesElement)
                ? ReadAttributes(attributesElement)
                : new Dictionary<string, JsonElement>();
            var id = TryResolveId(attributes, objectIdFieldName);
            var geometryJson = featureElement.TryGetProperty("geometry", out var geometryElement) &&
                geometryElement.ValueKind is JsonValueKind.Object
                    ? geometryElement.Clone()
                    : (JsonElement?)null;
            var geometry = geometryJson.HasValue
                ? ReadGeometry(geometryJson.Value, rootFactory)
                : null;

            yield return new VectorPayloadFeature
            {
                Id = id,
                Attributes = attributes,
                Geometry = geometry,
                GeometryJson = geometryJson,
                Crs = geometry?.SRID > 0
                    ? FormatWkidAuthority(geometry.SRID)
                    : null
            };
        }
    }

    private static Dictionary<string, JsonElement> ReadAttributes(JsonElement attributesElement)
    {
        if (attributesElement.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in attributesElement.EnumerateObject())
        {
            attributes[property.Name] = property.Value.Clone();
        }

        return attributes;
    }

    private static NetTopologySuite.Geometries.Geometry ReadGeometry(
        JsonElement geometryElement,
        GeometryFactory rootFactory)
    {
        return geometryElement.TryGetProperty("spatialReference", out var geometrySpatialReference) &&
            geometrySpatialReference.ValueKind == JsonValueKind.Object
                ? GeoServicesGeometryConverter.ReadGeometry(geometryElement)
                : GeoServicesGeometryConverter.ReadGeometry(geometryElement, rootFactory);
    }

    private static string? TryResolveId(
        Dictionary<string, JsonElement> attributes,
        string? objectIdFieldName)
    {
        if (!string.IsNullOrWhiteSpace(objectIdFieldName) &&
            attributes.TryGetValue(objectIdFieldName, out var idElement))
        {
            return JsonElementToString(idElement);
        }

        foreach (var candidate in new[] { "OBJECTID", "ObjectID", "objectid", "FID", "id" })
        {
            if (attributes.TryGetValue(candidate, out var candidateIdElement))
            {
                return JsonElementToString(candidateIdElement);
            }
        }

        return null;
    }

    private static List<long> ReadObjectIds(JsonElement root)
    {
        if (!root.TryGetProperty("objectIds", out var objectIdsElement) ||
            objectIdsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var objectIds = new List<long>();
        foreach (var objectIdElement in objectIdsElement.EnumerateArray())
        {
            if (objectIdElement.TryGetInt64(out var objectId))
            {
                objectIds.Add(objectId);
            }
        }

        return objectIds;
    }

    private static Envelope? ReadExtent(JsonElement root, GeometryFactory rootFactory)
    {
        if (!root.TryGetProperty("extent", out var extentElement) ||
            extentElement.ValueKind != JsonValueKind.Object ||
            !TryGetDouble(extentElement, "xmin", out var minX) ||
            !TryGetDouble(extentElement, "ymin", out var minY) ||
            !TryGetDouble(extentElement, "xmax", out var maxX) ||
            !TryGetDouble(extentElement, "ymax", out var maxY))
        {
            return null;
        }

        var envelope = new Envelope(minX, maxX, minY, maxY);
        return rootFactory.SRID > 0
            ? envelope.Copy()
            : envelope;
    }

    private static GeometryFactory CreateGeometryFactory(string? spatialReference, GeometryFactory? fallback)
    {
        if (TryReadSrid(spatialReference, out var srid))
        {
            return (fallback ?? DefaultGeometryFactory).WithSRID(srid);
        }

        return fallback ?? DefaultGeometryFactory;
    }

    private static string? ReadSpatialReferenceIdentifier(JsonElement spatialReference)
    {
        if (spatialReference.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Esri provides latestWkid as the EPSG-correct code when the wkid is an
        // Esri-proprietary identifier; prefer it when present.
        if (TryGetInt32(spatialReference, "latestWkid", out var latestWkid) && latestWkid > 0)
        {
            return FormatWkidAuthority(latestWkid);
        }

        if (TryGetInt32(spatialReference, "wkid", out var wkid) && wkid > 0)
        {
            return FormatWkidAuthority(wkid);
        }

        return TryGetString(spatialReference, "wkt");
    }

    /// <summary>
    /// Formats an Esri WKID as an authority-qualified CRS identifier. Known
    /// Esri-proprietary WKIDs are mapped to their EPSG equivalents (e.g. 102100 and
    /// 102113 to Web Mercator EPSG:3857). WKIDs that fall in Esri-only ranges with no
    /// EPSG equivalent are emitted with an <c>ESRI:</c> authority prefix so EPSG-based
    /// resolvers do not choke; all other codes are treated as standard EPSG codes.
    /// </summary>
    private static string FormatWkidAuthority(int wkid)
    {
        switch (wkid)
        {
            case 102100:
            case 102113:
                return "EPSG:3857";
        }

        return IsEsriProprietaryWkid(wkid)
            ? string.Create(CultureInfo.InvariantCulture, $"ESRI:{wkid}")
            : string.Create(CultureInfo.InvariantCulture, $"EPSG:{wkid}");
    }

    /// <summary>
    /// Returns whether the given WKID falls in a range reserved for Esri-proprietary
    /// spatial references that are not EPSG codes (the 53xxx, 54xxx, and 102xxx
    /// ranges), excluding codes already mapped to an EPSG equivalent.
    /// </summary>
    private static bool IsEsriProprietaryWkid(int wkid)
        => wkid is (>= 53000 and <= 54999) or (>= 102000 and <= 103999);

    private static bool TryReadSrid(string? spatialReference, out int srid)
    {
        srid = 0;
        if (string.IsNullOrWhiteSpace(spatialReference))
        {
            return false;
        }

        var trimmed = spatialReference.Trim();
        var separator = trimmed.LastIndexOf(':');
        var code = separator >= 0 ? trimmed[(separator + 1)..] : trimmed;
        return int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out srid) && srid > 0;
    }

    private static string? JsonElementToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string? TryGetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryGetBoolean(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.True;

    private static long? TryGetInt64(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.TryGetInt64(out var value)
            ? value
            : null;

    private static bool TryGetInt32(JsonElement element, string name, out int value)
    {
        if (element.TryGetProperty(name, out var property) && property.TryGetInt32(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetDouble(JsonElement element, string name, out double value)
    {
        if (element.TryGetProperty(name, out var property) && property.TryGetDouble(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}
