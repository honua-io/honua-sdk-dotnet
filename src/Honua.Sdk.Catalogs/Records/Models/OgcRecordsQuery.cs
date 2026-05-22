// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Catalogs.Records.Models;

/// <summary>
/// Query parameters for OGC API Records item searches.
/// </summary>
public sealed record OgcRecordsQuery
{
    /// <summary>Maximum number of records to return per page.</summary>
    public int? Limit { get; init; }

    /// <summary>Number of records to skip when the server supports offset paging.</summary>
    public int? Offset { get; init; }

    /// <summary>Bounding box filter as [minLon, minLat, maxLon, maxLat], or 3D equivalent.</summary>
    public IReadOnlyList<double>? Bbox { get; init; }

    /// <summary>Temporal filter as an ISO 8601 instant or interval.</summary>
    public string? Datetime { get; init; }

    /// <summary>Free-text search expression mapped to the Records <c>q</c> parameter.</summary>
    public string? Query { get; init; }

    /// <summary>Specific record identifiers to return.</summary>
    public IReadOnlyList<string>? Ids { get; init; }

    /// <summary>Record resource types to include, mapped to the Records <c>type</c> parameter.</summary>
    public IReadOnlyList<string>? Types { get; init; }

    /// <summary>External source identifiers to include.</summary>
    public IReadOnlyList<string>? ExternalIds { get; init; }

    /// <summary>CQL2 or server-supported filter expression.</summary>
    public string? Filter { get; init; }

    /// <summary>Filter language, such as <c>cql2-text</c>.</summary>
    public string? FilterLang { get; init; }

    /// <summary>Sort expression, when supported by the backing server.</summary>
    public string? SortBy { get; init; }

    /// <summary>Output format.</summary>
    public OgcRecordsFormat? Format { get; init; }

    /// <summary>
    /// Additional stable server query parameters not yet promoted to typed SDK
    /// properties. Typed properties win when keys overlap.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}
