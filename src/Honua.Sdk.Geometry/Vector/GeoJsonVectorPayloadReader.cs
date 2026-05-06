// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Vector;

/// <summary>
/// Reads GeoJSON feature payloads into typed vector features.
/// </summary>
public sealed class GeoJsonVectorPayloadReader : IVectorPayloadReader
{
    /// <summary>Shared stateless reader instance.</summary>
    public static GeoJsonVectorPayloadReader Instance { get; } = new();

    /// <inheritdoc />
    public VectorPayloadFormat Format => VectorPayloadFormat.GeoJson;

    /// <inheritdoc />
    public async Task<VectorPayloadFeatureSet> ReadAsync(
        Stream stream,
        VectorPayloadReadOptions? options = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        return Read(document.RootElement, options);
    }

    /// <summary>
    /// Reads a GeoJSON payload from a JSON element.
    /// </summary>
    /// <param name="root">Payload root element.</param>
    /// <param name="options">Optional reader settings.</param>
    /// <returns>The typed feature collection.</returns>
    public VectorPayloadFeatureSet Read(JsonElement root, VectorPayloadReadOptions? options = null)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("GeoJSON vector payload must be a JSON object.");
        }

        var type = TryGetString(root, "type");
        if (string.Equals(type, "Feature", StringComparison.OrdinalIgnoreCase))
        {
            var feature = ReadFeature(root, options);
            return new VectorPayloadFeatureSet
            {
                Format = Format,
                Features = [feature],
                NumberReturned = 1,
                Crs = ReadCrs(root)
            };
        }

        if (!string.Equals(type, "FeatureCollection", StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException("GeoJSON vector payload must be a Feature or FeatureCollection.");
        }

        var features = root.TryGetProperty("features", out var featuresElement) &&
            featuresElement.ValueKind == JsonValueKind.Array
                ? featuresElement.EnumerateArray()
                    .Where(featureElement => featureElement.ValueKind == JsonValueKind.Object)
                    .Select(featureElement => ReadFeature(featureElement, options))
                    .ToList()
                : [];
        var links = ReadLinks(root).ToList();

        return new VectorPayloadFeatureSet
        {
            Format = Format,
            Features = features,
            NumberMatched = TryGetInt64(root, "numberMatched"),
            NumberReturned = TryGetInt32(root, "numberReturned") ?? features.Count,
            HasMoreResults = links.Any(link => string.Equals(link.Rel, "next", StringComparison.OrdinalIgnoreCase)),
            Extent = ReadBbox(root),
            Crs = ReadCrs(root),
            Links = links
        };
    }

    private static VectorPayloadFeature ReadFeature(JsonElement featureElement, VectorPayloadReadOptions? options)
    {
        var geometryJson = featureElement.TryGetProperty("geometry", out var geometryElement) &&
            geometryElement.ValueKind is JsonValueKind.Object
                ? geometryElement.Clone()
                : (JsonElement?)null;
        var geometry = geometryJson.HasValue
            ? GeoJsonGeometryConverter.ReadGeometry(geometryJson.Value, options?.GeometryFactory)
            : null;

        return new VectorPayloadFeature
        {
            Id = featureElement.TryGetProperty("id", out var idElement) ? JsonElementToString(idElement) : null,
            Attributes = featureElement.TryGetProperty("properties", out var propertiesElement)
                ? ReadProperties(propertiesElement)
                : new Dictionary<string, JsonElement>(),
            Geometry = geometry,
            GeometryJson = geometryJson
        };
    }

    private static Dictionary<string, JsonElement> ReadProperties(JsonElement propertiesElement)
    {
        if (propertiesElement.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in propertiesElement.EnumerateObject())
        {
            properties[property.Name] = property.Value.Clone();
        }

        return properties;
    }

    private static IEnumerable<VectorPayloadLink> ReadLinks(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var linksElement) ||
            linksElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var linkElement in linksElement.EnumerateArray())
        {
            if (linkElement.ValueKind != JsonValueKind.Object ||
                !linkElement.TryGetProperty("href", out var hrefElement) ||
                hrefElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(hrefElement.GetString()))
            {
                continue;
            }

            yield return new VectorPayloadLink
            {
                Href = hrefElement.GetString()!,
                Rel = TryGetString(linkElement, "rel"),
                Type = TryGetString(linkElement, "type"),
                Title = TryGetString(linkElement, "title")
            };
        }
    }

    private static Envelope? ReadBbox(JsonElement root)
    {
        if (!root.TryGetProperty("bbox", out var bboxElement) ||
            bboxElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var coordinates = bboxElement.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Number)
            .Select(item => item.GetDouble())
            .Take(4)
            .ToArray();
        return coordinates.Length >= 4
            ? new Envelope(coordinates[0], coordinates[2], coordinates[1], coordinates[3])
            : null;
    }

    private static string? ReadCrs(JsonElement root)
    {
        if (!root.TryGetProperty("crs", out var crsElement) ||
            crsElement.ValueKind != JsonValueKind.Object ||
            !crsElement.TryGetProperty("properties", out var propertiesElement) ||
            propertiesElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return TryGetString(propertiesElement, "name");
    }

    private static long? TryGetInt64(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt64(out var value) => value,
            JsonValueKind.String when long.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }

    private static int? TryGetInt32(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;

    private static string? TryGetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

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
}
