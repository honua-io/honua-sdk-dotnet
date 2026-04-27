// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Wfs.Models;

/// <summary>
/// A GeoJSON geometry object.
/// </summary>
/// <remarks>
/// Primitive geometry types (Point, LineString, Polygon, MultiPoint, MultiLineString, MultiPolygon)
/// use <see cref="Coordinates"/> with varying nesting depth per RFC 7946.
/// GeometryCollection types use <see cref="Geometries"/> to hold child geometry objects (RFC 7946 §3.1.8).
/// Coordinates are exposed as <see cref="JsonElement"/> to avoid deep recursive model hierarchies.
/// Callers can traverse the element to extract typed coordinates.
/// </remarks>
public sealed class GeoJsonGeometry
{
    /// <summary>The geometry type (e.g. Point, Polygon, MultiPolygon, GeometryCollection).</summary>
    public string Type { get; init; } = "";

    /// <summary>The coordinate array, structure depends on <see cref="Type"/>. Null for GeometryCollection.</summary>
    public JsonElement? Coordinates { get; init; }

    /// <summary>Child geometries for GeometryCollection types (RFC 7946 §3.1.8). Null for other types.</summary>
    public IReadOnlyList<GeoJsonGeometry>? Geometries { get; init; }
}
