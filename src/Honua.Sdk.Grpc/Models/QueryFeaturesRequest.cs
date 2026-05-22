// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>
/// Request parameters for a feature query.
/// </summary>
public sealed record QueryFeaturesRequest
{
    /// <summary>Service identifier.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Layer index within the service.</summary>
    public required int LayerId { get; init; }

    /// <summary>SQL-like where clause. <see langword="null"/> defers to the server default (typically <c>1=1</c>).</summary>
    public string? Where { get; init; }

    /// <summary>Specific object IDs to return.</summary>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>Fields to include in results.</summary>
    public IReadOnlyList<string>? OutFields { get; init; }

    /// <summary>Whether to return geometry.</summary>
    public bool ReturnGeometry { get; init; } = true;

    /// <summary>Output spatial reference.</summary>
    public SpatialReference? OutSr { get; init; }

    /// <summary>Result offset for pagination.</summary>
    public int ResultOffset { get; init; }

    /// <summary>Maximum number of records to return. <see langword="null"/> means the server default (typically unlimited within server policy).</summary>
    public int? ResultRecordCount { get; init; }

    /// <summary>Order by clause. <see langword="null"/> means the server default ordering.</summary>
    public string? OrderBy { get; init; }

    /// <summary>Return distinct values only.</summary>
    public bool ReturnDistinct { get; init; }

    /// <summary>Return only the count of matching features.</summary>
    public bool ReturnCountOnly { get; init; }

    /// <summary>Return only object IDs.</summary>
    public bool ReturnIdsOnly { get; init; }

    /// <summary>Return only the extent.</summary>
    public bool ReturnExtentOnly { get; init; }

    /// <summary>Statistics to compute.</summary>
    public IReadOnlyList<StatisticDefinition>? OutStatistics { get; init; }

    /// <summary>Fields to group by.</summary>
    public IReadOnlyList<string>? GroupBy { get; init; }

    /// <summary>Geometry coordinate precision.</summary>
    public int GeometryPrecision { get; init; }

    /// <summary>Maximum allowable offset for geometry generalization.</summary>
    public double MaxAllowableOffset { get; init; }

    /// <summary>Optional spatial filter for geometry-constrained queries.</summary>
    public SpatialFilter? SpatialFilter { get; init; }
}
