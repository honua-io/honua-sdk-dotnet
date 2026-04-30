// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Nearest point pair between two geometries in planar analysis coordinates.
/// </summary>
public sealed record NearestGeometryPointPair
{
    /// <summary>
    /// Gets the nearest point on the first geometry.
    /// </summary>
    public required Point FirstPoint { get; init; }

    /// <summary>
    /// Gets the nearest point on the second geometry.
    /// </summary>
    public required Point SecondPoint { get; init; }

    /// <summary>
    /// Gets the planar distance between the two points.
    /// </summary>
    public double Distance { get; init; }
}
