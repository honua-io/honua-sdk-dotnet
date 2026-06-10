// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.GeoServices.GeometryServer;

/// <summary>
/// A request to project geometries between spatial references (GeometryServer <c>project</c>).
/// </summary>
public sealed class ProjectGeometriesRequest
{
    /// <summary>Optional service id override; falls back to the configured GeometryServer id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>
    /// GeoServices geometry type (e.g. <c>esriGeometryPoint</c>, <c>esriGeometryPolyline</c>,
    /// <c>esriGeometryPolygon</c>). Required.
    /// </summary>
    public required string GeometryType { get; init; }

    /// <summary>
    /// The geometries to project, as GeoServices JSON objects (each entry is one geometry object
    /// such as <c>{ "x": .., "y": .. }</c> or <c>{ "rings": [...] }</c>). Required.
    /// </summary>
    public required IReadOnlyList<JsonElement> Geometries { get; init; }

    /// <summary>Input spatial reference wkid. Required.</summary>
    public required int InSpatialReference { get; init; }

    /// <summary>Output spatial reference wkid. Required.</summary>
    public required int OutSpatialReference { get; init; }

    /// <summary>Optional datum transformation id.</summary>
    public string? Transformation { get; init; }
}

/// <summary>
/// A request to buffer geometries (GeometryServer <c>buffer</c>).
/// </summary>
public sealed class BufferGeometriesRequest
{
    /// <summary>Optional service id override; falls back to the configured GeometryServer id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>GeoServices geometry type. Required.</summary>
    public required string GeometryType { get; init; }

    /// <summary>The geometries to buffer, as GeoServices JSON objects. Required.</summary>
    public required IReadOnlyList<JsonElement> Geometries { get; init; }

    /// <summary>Input spatial reference wkid. Required.</summary>
    public required int InSpatialReference { get; init; }

    /// <summary>Output spatial reference wkid. Required.</summary>
    public required int OutSpatialReference { get; init; }

    /// <summary>Spatial reference wkid in which the buffer distances are expressed.</summary>
    public int? BufferSpatialReference { get; init; }

    /// <summary>Buffer distances (one or more). Required.</summary>
    public required IReadOnlyList<double> Distances { get; init; }

    /// <summary>Linear unit (e.g. <c>esriSRUnit_Meter</c>).</summary>
    public string? Unit { get; init; }

    /// <summary>Whether to union the resulting buffers.</summary>
    public bool UnionResults { get; init; }

    /// <summary>Whether to compute geodesic buffers.</summary>
    public bool Geodesic { get; init; }
}

/// <summary>
/// Result of a GeometryServer operation that returns a <c>geometries</c> array (project, buffer).
/// </summary>
public sealed class GeometryCollectionResult
{
    /// <summary>The returned geometries as raw GeoServices JSON objects.</summary>
    public required IReadOnlyList<JsonElement> Geometries { get; init; }

    /// <summary>Raw JSON response.</summary>
    public JsonElement RawResponse { get; init; }
}

/// <summary>
/// A request to compute polyline lengths (GeometryServer <c>lengths</c>).
/// </summary>
public sealed class LengthsRequest
{
    /// <summary>Optional service id override; falls back to the configured GeometryServer id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>The polylines, as GeoServices JSON objects. Required.</summary>
    public required IReadOnlyList<JsonElement> Polylines { get; init; }

    /// <summary>Spatial reference wkid. Required.</summary>
    public required int SpatialReference { get; init; }

    /// <summary>Length unit (e.g. <c>esriSRUnit_Meter</c>).</summary>
    public string? LengthUnit { get; init; }

    /// <summary>Calculation type (<c>planar</c>, <c>geodesic</c>, or <c>preserveShape</c>).</summary>
    public string? CalculationType { get; init; }
}

/// <summary>
/// A request to compute polygon areas and lengths (GeometryServer <c>areasAndLengths</c>).
/// </summary>
public sealed class AreasAndLengthsRequest
{
    /// <summary>Optional service id override; falls back to the configured GeometryServer id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>The polygons, as GeoServices JSON objects. Required.</summary>
    public required IReadOnlyList<JsonElement> Polygons { get; init; }

    /// <summary>Spatial reference wkid. Required.</summary>
    public required int SpatialReference { get; init; }

    /// <summary>Length unit (e.g. <c>esriSRUnit_Meter</c>).</summary>
    public string? LengthUnit { get; init; }

    /// <summary>Area unit (e.g. <c>esriSquareMeters</c>).</summary>
    public string? AreaUnit { get; init; }

    /// <summary>Calculation type (<c>planar</c>, <c>geodesic</c>, or <c>preserveShape</c>).</summary>
    public string? CalculationType { get; init; }
}

/// <summary>
/// Result of a GeometryServer <c>lengths</c> request.
/// </summary>
public sealed class LengthsResult
{
    /// <summary>Computed lengths, one per input polyline.</summary>
    public required IReadOnlyList<double> Lengths { get; init; }

    /// <summary>Raw JSON response.</summary>
    public JsonElement RawResponse { get; init; }
}

/// <summary>
/// Result of a GeometryServer <c>areasAndLengths</c> request.
/// </summary>
public sealed class AreasAndLengthsResult
{
    /// <summary>Computed areas, one per input polygon.</summary>
    public required IReadOnlyList<double> Areas { get; init; }

    /// <summary>Computed perimeter lengths, one per input polygon.</summary>
    public required IReadOnlyList<double> Lengths { get; init; }

    /// <summary>Raw JSON response.</summary>
    public JsonElement RawResponse { get; init; }
}
