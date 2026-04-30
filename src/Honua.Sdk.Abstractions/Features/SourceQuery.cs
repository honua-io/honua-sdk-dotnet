// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Source-oriented feature query shape that maps to <see cref="FeatureQueryRequest"/>.
/// </summary>
public sealed record SourceQuery
{
    /// <summary>Logical filter expression in the language specified by <see cref="FilterLanguage"/>.</summary>
    public string? Where { get; init; }

    /// <summary>Filter language. Provider default uses the wrapped client's native default.</summary>
    public FeatureFilterLanguage FilterLanguage { get; init; } = FeatureFilterLanguage.ProviderDefault;

    /// <summary>Feature identifiers for providers with string IDs.</summary>
    public IReadOnlyList<string>? FeatureIds { get; init; }

    /// <summary>Object identifiers for providers with numeric object IDs.</summary>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>Fields/properties to include in the response.</summary>
    public IReadOnlyList<string>? OutFields { get; init; }

    /// <summary>Whether to return geometry.</summary>
    public bool? ReturnGeometry { get; init; }

    /// <summary>Zero-based offset of the first returned record.</summary>
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

    internal FeatureQueryRequest ToFeatureQueryRequest(FeatureSource source)
        => new()
        {
            Source = source,
            Filter = Where,
            FilterLanguage = FilterLanguage,
            FeatureIds = FeatureIds,
            ObjectIds = ObjectIds,
            OutFields = OutFields,
            ReturnGeometry = ReturnGeometry,
            Offset = Offset,
            Limit = Limit,
            OrderBy = OrderBy,
            ReturnDistinct = ReturnDistinct,
            ReturnCountOnly = ReturnCountOnly,
            ReturnIdsOnly = ReturnIdsOnly,
            ReturnExtentOnly = ReturnExtentOnly,
            TimeFilter = TimeFilter,
            OutStatistics = OutStatistics,
            GroupBy = GroupBy,
            Having = Having,
            Bbox = Bbox,
            SpatialFilter = SpatialFilter,
            OutputCrs = OutputCrs
        };
}
