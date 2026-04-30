// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Converts between GeoJSON geometry objects and NetTopologySuite geometries.
/// </summary>
public static class GeoJsonGeometryConverter
{
    private static readonly JsonSerializerOptions DefaultSerializerOptions = CreateSerializerOptions(null);

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

        var options = geometryFactory is null
            ? DefaultSerializerOptions
            : CreateSerializerOptions(geometryFactory);
        return JsonSerializer.Deserialize<NetTopologySuite.Geometries.Geometry>(geoJson, options)
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

        return JsonSerializer.SerializeToElement(geometry, DefaultSerializerOptions);
    }

    /// <summary>
    /// Writes a geometry as a GeoJSON string.
    /// </summary>
    /// <param name="geometry">The geometry to write.</param>
    /// <returns>The GeoJSON geometry object.</returns>
    public static string WriteGeometryString(NetTopologySuite.Geometries.Geometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        return JsonSerializer.Serialize(geometry, DefaultSerializerOptions);
    }

    private static JsonSerializerOptions CreateSerializerOptions(GeometryFactory? geometryFactory)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(geometryFactory is null
            ? new GeoJsonConverterFactory()
            : new GeoJsonConverterFactory(geometryFactory));
        return options;
    }
}
