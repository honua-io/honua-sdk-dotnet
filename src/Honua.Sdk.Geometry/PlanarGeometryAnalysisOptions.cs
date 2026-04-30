// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Geometry;

/// <summary>
/// Options that control planar geometry analysis coordinate handling.
/// </summary>
public sealed record PlanarGeometryAnalysisOptions
{
    /// <summary>
    /// Gets the source spatial reference for geometries that do not carry an SRID.
    /// </summary>
    public HonuaSpatialReference? SourceSpatialReference { get; init; }

    /// <summary>
    /// Gets the projected spatial reference used before planar analysis.
    /// </summary>
    public HonuaSpatialReference? AnalysisSpatialReference { get; init; }

    /// <summary>
    /// Gets the coordinate transformer used when <see cref="AnalysisSpatialReference"/> is supplied.
    /// </summary>
    public HonuaCoordinateTransformer? CoordinateTransformer { get; init; }

    /// <summary>
    /// Gets a value indicating whether measurement operations may use geographic coordinates directly.
    /// </summary>
    public bool AllowGeographicMeasurements { get; init; }
}
