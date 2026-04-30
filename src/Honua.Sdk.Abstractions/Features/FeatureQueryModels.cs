// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Provider-specific feature source identifiers used by the shared query abstraction.
/// </summary>
public sealed record FeatureSource
{
    /// <summary>Honua service identifier for gRPC and GeoServices FeatureServer providers.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Layer identifier for gRPC and GeoServices FeatureServer providers.</summary>
    public int? LayerId { get; init; }

    /// <summary>OGC API Features collection identifier.</summary>
    public string? CollectionId { get; init; }

    /// <summary>WFS qualified feature type name.</summary>
    public string? TypeName { get; init; }
}

/// <summary>
/// Bounding box used by the shared feature query abstraction.
/// </summary>
public sealed record FeatureBoundingBox
{
    /// <summary>Minimum X coordinate.</summary>
    public required double MinX { get; init; }

    /// <summary>Minimum Y coordinate.</summary>
    public required double MinY { get; init; }

    /// <summary>Maximum X coordinate.</summary>
    public required double MaxX { get; init; }

    /// <summary>Maximum Y coordinate.</summary>
    public required double MaxY { get; init; }

    /// <summary>Optional coordinate reference system identifier.</summary>
    public string? Crs { get; init; }
}

/// <summary>
/// Geometry shape type for provider-neutral spatial filters.
/// </summary>
public enum FeatureSpatialGeometryType
{
    /// <summary>Infer the geometry type from the geometry object.</summary>
    Unspecified = 0,

    /// <summary>Point geometry.</summary>
    Point = 1,

    /// <summary>Envelope geometry.</summary>
    Envelope = 2,

    /// <summary>Multi-point geometry.</summary>
    MultiPoint = 3,

    /// <summary>Line or polyline geometry.</summary>
    Polyline = 4,

    /// <summary>Polygon geometry.</summary>
    Polygon = 5
}

/// <summary>
/// Spatial relationship for provider-neutral spatial filters.
/// </summary>
public enum FeatureSpatialRelationship
{
    /// <summary>Use the provider default, normally intersects.</summary>
    Unspecified = 0,

    /// <summary>Features intersect the filter geometry.</summary>
    Intersects = 1,

    /// <summary>Features are within the filter geometry.</summary>
    Within = 2,

    /// <summary>Features contain the filter geometry.</summary>
    Contains = 3,

    /// <summary>Feature envelopes intersect the filter geometry envelope.</summary>
    EnvelopeIntersects = 4,

    /// <summary>Features cross the filter geometry.</summary>
    Crosses = 5,

    /// <summary>Features touch the filter geometry.</summary>
    Touches = 6,

    /// <summary>Features overlap the filter geometry.</summary>
    Overlaps = 7,

    /// <summary>Features are found by the provider's spatial index intersect behavior.</summary>
    IndexIntersects = 8
}

/// <summary>
/// Explicit geometry spatial filter used by the shared feature query abstraction.
/// </summary>
public sealed record FeatureSpatialFilter
{
    /// <summary>Filter geometry object using GeoServices-style JSON shape keys.</summary>
    public required JsonElement Geometry { get; init; }

    /// <summary>Geometry shape type, or <see cref="FeatureSpatialGeometryType.Unspecified"/> to infer from <see cref="Geometry"/>.</summary>
    public FeatureSpatialGeometryType GeometryType { get; init; } = FeatureSpatialGeometryType.Unspecified;

    /// <summary>Optional coordinate reference system identifier for the filter geometry.</summary>
    public string? Crs { get; init; }

    /// <summary>Spatial relationship to apply between queried features and the filter geometry.</summary>
    public FeatureSpatialRelationship Relationship { get; init; } = FeatureSpatialRelationship.Intersects;
}

/// <summary>
/// Temporal relationship for provider-neutral time filters.
/// </summary>
public enum FeatureTimeRelation
{
    /// <summary>Use the provider's default temporal relationship.</summary>
    Unspecified = 0,

    /// <summary>Features whose time extent overlaps the requested time or interval.</summary>
    Overlaps = 1,

    /// <summary>Features whose end time is after the requested start and whose start time is within the requested end.</summary>
    AfterStartWithinEnd = 2,

    /// <summary>Features whose time extent is fully within the requested time or interval.</summary>
    Within = 3
}

/// <summary>
/// Time instant or closed interval used by the shared feature query abstraction.
/// </summary>
public sealed record FeatureTimeFilter
{
    /// <summary>Inclusive start time, or the queried instant when <see cref="End"/> is not set.</summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>Inclusive end time for interval queries.</summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>Optional temporal relationship to apply when the provider supports it.</summary>
    public FeatureTimeRelation Relation { get; init; } = FeatureTimeRelation.Unspecified;
}

/// <summary>
/// Aggregate statistic functions for provider-neutral feature queries.
/// </summary>
public enum FeatureStatisticType
{
    /// <summary>Unspecified statistic type.</summary>
    Unspecified = 0,

    /// <summary>Count of non-null values.</summary>
    Count = 1,

    /// <summary>Sum of values.</summary>
    Sum = 2,

    /// <summary>Minimum value.</summary>
    Min = 3,

    /// <summary>Maximum value.</summary>
    Max = 4,

    /// <summary>Average value.</summary>
    Average = 5,

    /// <summary>Standard deviation.</summary>
    StandardDeviation = 6,

    /// <summary>Variance.</summary>
    Variance = 7
}

/// <summary>
/// Provider-neutral aggregate statistic requested by a feature query.
/// </summary>
public sealed record FeatureQueryStatistic
{
    /// <summary>Field or provider-native expression to aggregate.</summary>
    public required string OnField { get; init; }

    /// <summary>Aggregate statistic to compute.</summary>
    public required FeatureStatisticType StatisticType { get; init; }

    /// <summary>Output field name that will contain the aggregate value.</summary>
    public required string OutField { get; init; }
}

/// <summary>
/// Provider-neutral feature query request for common read paths.
/// </summary>
public sealed record FeatureQueryRequest
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Filter expression in the language specified by <see cref="FilterLanguage"/>.</summary>
    public string? Filter { get; init; }

    /// <summary>Filter language. <see cref="FeatureFilterLanguage.ProviderDefault"/> uses the provider's native default.</summary>
    public FeatureFilterLanguage FilterLanguage { get; init; } = FeatureFilterLanguage.ProviderDefault;

    /// <summary>Feature identifiers for providers with string IDs.</summary>
    public IReadOnlyList<string>? FeatureIds { get; init; }

    /// <summary>Object identifiers for providers with numeric object IDs.</summary>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>Fields/properties to include in the response.</summary>
    public IReadOnlyList<string>? OutFields { get; init; }

    /// <summary>Whether to return geometry.</summary>
    public bool? ReturnGeometry { get; init; }

    /// <summary>Number of records to skip.</summary>
    public int? Offset { get; init; }

    /// <summary>Maximum number of records to return.</summary>
    public int? Limit { get; init; }

    /// <summary>Provider-native order-by expression.</summary>
    public string? OrderBy { get; init; }

    /// <summary>Whether to request distinct rows only when the provider supports it.</summary>
    public bool? ReturnDistinct { get; init; }

    /// <summary>Whether to request only the total matching count.</summary>
    public bool? ReturnCountOnly { get; init; }

    /// <summary>Whether to request only matching object or feature IDs.</summary>
    public bool? ReturnIdsOnly { get; init; }

    /// <summary>Whether to request only the matching feature extent.</summary>
    public bool? ReturnExtentOnly { get; init; }

    /// <summary>Optional time instant or interval filter.</summary>
    public FeatureTimeFilter? TimeFilter { get; init; }

    /// <summary>Aggregate statistics to compute when the provider supports statistics queries.</summary>
    public IReadOnlyList<FeatureQueryStatistic>? OutStatistics { get; init; }

    /// <summary>Fields to group statistics by when the provider supports grouped statistics.</summary>
    public IReadOnlyList<string>? GroupBy { get; init; }

    /// <summary>Provider-native having expression for grouped statistics.</summary>
    public string? Having { get; init; }

    /// <summary>Optional bounding box spatial filter.</summary>
    public FeatureBoundingBox? Bbox { get; init; }

    /// <summary>Optional explicit geometry spatial filter.</summary>
    public FeatureSpatialFilter? SpatialFilter { get; init; }

    /// <summary>Optional output coordinate reference system identifier.</summary>
    public string? OutputCrs { get; init; }
}

/// <summary>
/// Provider-neutral feature record.
/// </summary>
public sealed record FeatureRecord
{
    /// <summary>Provider feature identifier, when available.</summary>
    public string? Id { get; init; }

    /// <summary>Feature attributes/properties as JSON values.</summary>
    public IReadOnlyDictionary<string, JsonElement> Attributes { get; init; } =
        new Dictionary<string, JsonElement>();

    /// <summary>Feature geometry as a JSON value, when returned.</summary>
    public JsonElement? Geometry { get; init; }
}

/// <summary>
/// Provider-neutral feature query result page.
/// </summary>
public sealed record FeatureQueryResult
{
    /// <summary>Provider name that produced the page.</summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>Features returned in this page.</summary>
    public IReadOnlyList<FeatureRecord> Features { get; init; } = [];

    /// <summary>Total matching features when the provider reports it.</summary>
    public long? NumberMatched { get; init; }

    /// <summary>Number of features returned in this page.</summary>
    public int NumberReturned { get; init; }

    /// <summary>Matching object IDs when the provider reports an IDs-only response.</summary>
    public IReadOnlyList<long> ObjectIds { get; init; } = [];

    /// <summary>Matching feature extent when the provider reports an extent-only response.</summary>
    public FeatureBoundingBox? Extent { get; init; }

    /// <summary>Whether the provider indicates more results may be available.</summary>
    public bool HasMoreResults { get; init; }

    /// <summary>Provider object ID field name, when available.</summary>
    public string? ObjectIdFieldName { get; init; }
}
