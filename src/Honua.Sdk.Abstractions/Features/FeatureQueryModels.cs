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

    /// <summary>Optional bounding box spatial filter.</summary>
    public FeatureBoundingBox? Bbox { get; init; }

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
