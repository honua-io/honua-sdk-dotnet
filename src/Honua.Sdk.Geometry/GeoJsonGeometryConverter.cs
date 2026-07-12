// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Converts between GeoJSON geometry objects and NetTopologySuite geometries.
/// </summary>
public static class GeoJsonGeometryConverter
{
    private static readonly JsonSerializerOptions DefaultSerializerOptions = CreateDefaultSerializerOptions();
    private static readonly GeoJsonGeometryJsonContext DefaultJsonContext = new(DefaultSerializerOptions);
    private static readonly ConditionalWeakTable<GeometryFactory, JsonSerializerOptions> SerializerOptionsByGeometryFactory = new();
    private static readonly ConditionalWeakTable<GeometryFactory, GeoJsonGeometryJsonContext> JsonContextsByGeometryFactory = new();

    /// <summary>
    /// Reads a GeoJSON geometry from a JSON element.
    /// </summary>
    /// <param name="geoJson">The GeoJSON geometry object.</param>
    /// <param name="geometryFactory">Optional factory used to construct the geometry.</param>
    /// <returns>The parsed geometry.</returns>
    public static NetTopologySuite.Geometries.Geometry ReadGeometry(
        JsonElement geoJson,
        GeometryFactory? geometryFactory = null)
    {
        return ReadGeometry(geoJson.GetRawText(), geometryFactory);
    }

    /// <summary>
    /// Reads a GeoJSON geometry from a JSON string.
    /// </summary>
    /// <param name="geoJson">The GeoJSON geometry object.</param>
    /// <param name="geometryFactory">Optional factory used to construct the geometry.</param>
    /// <returns>The parsed geometry.</returns>
    public static NetTopologySuite.Geometries.Geometry ReadGeometry(
        string geoJson,
        GeometryFactory? geometryFactory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(geoJson);

        var context = GetJsonContext(geometryFactory);
        return JsonSerializer.Deserialize(geoJson, context.Geometry)
            ?? throw new JsonException("GeoJSON did not contain a geometry object.");
    }

    /// <summary>
    /// Writes a geometry as a GeoJSON JSON element.
    /// </summary>
    /// <param name="geometry">The geometry to write.</param>
    /// <returns>The GeoJSON geometry object.</returns>
    public static JsonElement WriteGeometry(NetTopologySuite.Geometries.Geometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        return JsonSerializer.SerializeToElement(geometry, DefaultJsonContext.Geometry);
    }

    /// <summary>
    /// Writes a geometry as a GeoJSON string.
    /// </summary>
    /// <param name="geometry">The geometry to write.</param>
    /// <returns>The GeoJSON geometry object.</returns>
    public static string WriteGeometryString(NetTopologySuite.Geometries.Geometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        return JsonSerializer.Serialize(geometry, DefaultJsonContext.Geometry);
    }

    private static GeoJsonGeometryJsonContext GetJsonContext(GeometryFactory? geometryFactory)
        => geometryFactory is null
            ? DefaultJsonContext
            : JsonContextsByGeometryFactory.GetValue(geometryFactory, CreateJsonContext);

    private static GeoJsonGeometryJsonContext CreateJsonContext(GeometryFactory geometryFactory)
        => new(GetSerializerOptions(geometryFactory));

    private static JsonSerializerOptions GetSerializerOptions(GeometryFactory geometryFactory)
        => SerializerOptionsByGeometryFactory.GetValue(geometryFactory, CreateSerializerOptions);

    private static JsonSerializerOptions CreateDefaultSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new GeoJsonConverterFactory());
        return options;
    }

    private static JsonSerializerOptions CreateSerializerOptions(GeometryFactory geometryFactory)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new GeoJsonConverterFactory(geometryFactory));
        return options;
    }
}
