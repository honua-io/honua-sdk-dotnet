// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.OgcFeatures.Models;

/// <summary>
/// Query parameters for OGC API Features items endpoint.
/// Uses init-only properties; supports <c>record with { ... }</c> for paging advancement.
/// </summary>
public sealed record OgcItemsParams
{
    /// <summary>Maximum number of features to return per page.</summary>
    public int? Limit { get; init; }

    /// <summary>Number of features to skip (for offset-based paging).</summary>
    public int? Offset { get; init; }

    /// <summary>Bounding box filter as [minLon, minLat, maxLon, maxLat].</summary>
    public IReadOnlyList<double>? Bbox { get; init; }

    /// <summary>CRS of the bounding box.</summary>
    public string? BboxCrs { get; init; }

    /// <summary>CRS for the response geometry.</summary>
    public string? Crs { get; init; }

    /// <summary>Temporal filter (ISO 8601 instant or interval, e.g., "2020-01-01/2020-12-31").</summary>
    public string? Datetime { get; init; }

    /// <summary>CQL2-Text filter expression.</summary>
    public string? Filter { get; init; }

    /// <summary>Filter language (default: "cql2-text").</summary>
    public string? FilterLang { get; init; }

    /// <summary>CRS for the filter geometry.</summary>
    public string? FilterCrs { get; init; }

    /// <summary>Specific feature IDs to return.</summary>
    public IReadOnlyList<string>? Ids { get; init; }

    /// <summary>Comma-separated list of properties to return (projection).</summary>
    public string? Properties { get; init; }

    /// <summary>Sort expression (e.g., "+name,-population").</summary>
    public string? Sortby { get; init; }

    /// <summary>Output format.</summary>
    public OgcFeaturesFormat? Format { get; init; }
}
