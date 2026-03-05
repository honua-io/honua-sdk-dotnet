// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>
/// Spatial filter parameters for feature queries.
/// Geometry uses Esri JSON shape conventions.
/// </summary>
public sealed class SpatialFilter
{
    /// <summary>
    /// Filter geometry in Esri JSON form (point, envelope, multipoint, paths, or rings).
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Geometry { get; set; }

    /// <summary>
    /// Spatial relationship used for filtering.
    /// </summary>
    public SpatialRelationship SpatialRelationship { get; set; } = SpatialRelationship.Unspecified;

    /// <summary>
    /// Spatial reference of the filter geometry.
    /// </summary>
    public SpatialReference? SpatialReference { get; set; }

    /// <summary>
    /// Distance threshold for distance-based relationships.
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// Distance unit for distance-based relationships.
    /// </summary>
    public DistanceUnit DistanceUnit { get; set; } = DistanceUnit.Unspecified;

    /// <summary>
    /// Number of nearest neighbors to return for KNN filtering.
    /// </summary>
    public int NearestCount { get; set; }

    /// <summary>
    /// Whether to include computed distance in KNN results.
    /// </summary>
    public bool ReturnDistance { get; set; }
}
