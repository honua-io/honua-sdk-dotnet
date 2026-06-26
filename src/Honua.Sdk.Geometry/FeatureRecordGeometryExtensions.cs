// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Nts = NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Identifies the JSON encoding of a feature geometry.
/// </summary>
public enum FeatureGeometryFormat
{
    /// <summary>Infer the encoding from the geometry shape (Esri JSON vs GeoJSON).</summary>
    Auto = 0,

    /// <summary>Esri / GeoServices JSON (<c>x</c>/<c>y</c>, <c>rings</c>, <c>paths</c>, <c>points</c>).</summary>
    EsriJson = 1,

    /// <summary>GeoJSON (<c>type</c> + <c>coordinates</c>).</summary>
    GeoJson = 2,
}

/// <summary>
/// NetTopologySuite accessors for provider-neutral <see cref="FeatureRecord"/> geometries.
/// </summary>
/// <remarks>
/// Provides arcpy-style <c>feature.geometry</c> access over the JSON geometry that protocol clients
/// return. GeoServices clients populate Esri JSON; OGC API Features / WFS clients populate GeoJSON.
/// The format is auto-detected by default, or can be supplied explicitly.
/// </remarks>
public static class FeatureRecordGeometryExtensions
{
    /// <summary>
    /// Returns the record geometry as a NetTopologySuite <see cref="Nts.Geometry"/>.
    /// </summary>
    /// <param name="record">The feature record.</param>
    /// <param name="format">The geometry encoding, or <see cref="FeatureGeometryFormat.Auto"/> to infer it.</param>
    /// <param name="geometryFactory">Optional factory used to construct the geometry.</param>
    /// <returns>The NTS geometry, or <see langword="null"/> when the record carries no geometry.</returns>
    public static Nts.Geometry? GetGeometry(
        this FeatureRecord record,
        FeatureGeometryFormat format = FeatureGeometryFormat.Auto,
        Nts.GeometryFactory? geometryFactory = null)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.Geometry is not { } geometry || geometry.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var resolved = format == FeatureGeometryFormat.Auto ? DetectFormat(geometry) : format;
        return resolved switch
        {
            FeatureGeometryFormat.GeoJson => GeoJsonGeometryConverter.ReadGeometry(geometry, geometryFactory),
            _ => GeoServicesGeometryConverter.ReadGeometry(geometry, geometryFactory),
        };
    }

    private static FeatureGeometryFormat DetectFormat(JsonElement geometry)
    {
        // GeoJSON is the only encoding with a "type" discriminator plus a "coordinates"/"geometries"
        // member; Esri JSON keys the shape by x/y, rings, paths, or points.
        if (geometry.TryGetProperty("type", out var type) &&
            type.ValueKind == JsonValueKind.String &&
            (geometry.TryGetProperty("coordinates", out _) || geometry.TryGetProperty("geometries", out _)))
        {
            return FeatureGeometryFormat.GeoJson;
        }

        return FeatureGeometryFormat.EsriJson;
    }
}
