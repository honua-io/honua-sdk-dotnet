// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Nearest vertex result for a geometry in planar analysis coordinates.
/// </summary>
public sealed record NearestGeometryVertex
{
    /// <summary>
    /// Gets the nearest vertex as a point.
    /// </summary>
    public required Point Vertex { get; init; }

    /// <summary>
    /// Gets the zero-based coordinate index in the analyzed geometry coordinate sequence.
    /// </summary>
    public int CoordinateIndex { get; init; }

    /// <summary>
    /// Gets the planar distance from the vertex to the target geometry.
    /// </summary>
    public double Distance { get; init; }
}
