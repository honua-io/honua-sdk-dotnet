// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.GeoServices.FeatureServer.Models;

/// <summary>
/// Query parameters for FeatureServer query operations.
/// Uses init-only properties; advance paging with <c>record with { ... }</c>.
/// </summary>
public sealed record FeatureServerQueryParams
{
    /// <summary>SQL WHERE clause to filter features.</summary>
    public string? Where { get; init; }

    /// <summary>Comma-separated list of fields to return (default: all with "*").</summary>
    public string? OutFields { get; init; }

    /// <summary>Specific object IDs to return.</summary>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>Fields to order results by (e.g., "NAME ASC, POP DESC").</summary>
    public string? OrderByFields { get; init; }

    /// <summary>Whether to return geometry (default: true).</summary>
    public bool? ReturnGeometry { get; init; }

    /// <summary>Output format.</summary>
    public FeatureServerFormat? Format { get; init; }

    /// <summary>Number of records to skip (for paging).</summary>
    public int? ResultOffset { get; init; }

    /// <summary>Maximum number of records to return per page.</summary>
    public int? ResultRecordCount { get; init; }

    /// <summary>Spatial filter for the query.</summary>
    public FeatureServerSpatialFilter? SpatialFilter { get; init; }

    /// <summary>Output spatial reference WKID.</summary>
    public int? OutSR { get; init; }

    /// <summary>Input spatial reference WKID for the spatial filter geometry.</summary>
    public int? InSR { get; init; }

    /// <summary>Time instant or range filter (epoch milliseconds, e.g., "1609459200000" or "1609459200000,1640995200000").</summary>
    public string? Time { get; init; }

    /// <summary>Temporal relationship for the time filter.</summary>
    public TimeRelation? TimeRelation { get; init; }

    /// <summary>Whether to return only distinct values.</summary>
    public bool? ReturnDistinctValues { get; init; }
}

/// <summary>
/// Spatial filter for FeatureServer queries.
/// </summary>
public sealed record FeatureServerSpatialFilter
{
    /// <summary>Geometry JSON for the spatial filter (Esri JSON format).</summary>
    public string? Geometry { get; init; }

    /// <summary>Geometry type (e.g., "esriGeometryEnvelope", "esriGeometryPoint").</summary>
    public string? GeometryType { get; init; }

    /// <summary>Spatial relationship to apply.</summary>
    public SpatialRelationship SpatialRel { get; init; } = SpatialRelationship.Intersects;
}
