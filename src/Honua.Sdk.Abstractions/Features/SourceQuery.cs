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

    /// <summary>Optional bounding box spatial filter.</summary>
    public FeatureBoundingBox? Bbox { get; init; }

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
            Bbox = Bbox,
            OutputCrs = OutputCrs
        };
}
