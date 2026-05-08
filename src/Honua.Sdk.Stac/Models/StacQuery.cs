// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Stac.Models;

/// <summary>
/// Query parameters for STAC collection item requests.
/// </summary>
public sealed record StacItemsQuery
{
    /// <summary>Maximum number of items to return per page.</summary>
    public int? Limit { get; init; }

    /// <summary>Number of items to skip when the server supports offset paging.</summary>
    public int? Offset { get; init; }

    /// <summary>Opaque server-driven page token used by some STAC servers.</summary>
    public string? Next { get; init; }

    /// <summary>Bounding box filter as [minLon, minLat, maxLon, maxLat], or 3D equivalent.</summary>
    public IReadOnlyList<double>? Bbox { get; init; }

    /// <summary>Temporal filter as an ISO 8601 instant or interval.</summary>
    public string? Datetime { get; init; }

    /// <summary>Specific item identifiers to return.</summary>
    public IReadOnlyList<string>? Ids { get; init; }

    /// <summary>CQL2 or server-supported filter expression.</summary>
    public string? Filter { get; init; }

    /// <summary>Filter language, such as <c>cql2-text</c>.</summary>
    public string? FilterLang { get; init; }

    /// <summary>Sort expression, when supported by the backing server.</summary>
    public string? SortBy { get; init; }

    /// <summary>Subset of item or asset fields to include or exclude.</summary>
    public StacFields? Fields { get; init; }

    /// <summary>
    /// Additional stable server query parameters not yet promoted to typed SDK
    /// properties. Typed properties win when keys overlap.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Query parameters for GET <c>/stac/search</c>.
/// </summary>
public sealed record StacSearchQuery
{
    /// <summary>Maximum number of items to return per page.</summary>
    public int? Limit { get; init; }

    /// <summary>Number of items to skip when the server supports offset paging.</summary>
    public int? Offset { get; init; }

    /// <summary>Opaque server-driven page token used by some STAC servers.</summary>
    public string? Next { get; init; }

    /// <summary>Bounding box filter as [minLon, minLat, maxLon, maxLat], or 3D equivalent.</summary>
    public IReadOnlyList<double>? Bbox { get; init; }

    /// <summary>Temporal filter as an ISO 8601 instant or interval.</summary>
    public string? Datetime { get; init; }

    /// <summary>Specific item identifiers to return.</summary>
    public IReadOnlyList<string>? Ids { get; init; }

    /// <summary>Collection identifiers to include in the search.</summary>
    public IReadOnlyList<string>? Collections { get; init; }

    /// <summary>GeoJSON geometry filter encoded as JSON for GET search.</summary>
    public JsonElement? Intersects { get; init; }

    /// <summary>STAC Query extension payload encoded as JSON for GET search.</summary>
    public JsonElement? Query { get; init; }

    /// <summary>CQL2 or server-supported filter expression.</summary>
    public string? Filter { get; init; }

    /// <summary>Filter language, such as <c>cql2-text</c>.</summary>
    public string? FilterLang { get; init; }

    /// <summary>Sort expression, when supported by the backing server.</summary>
    public string? SortBy { get; init; }

    /// <summary>Subset of item or asset fields to include or exclude.</summary>
    public StacFields? Fields { get; init; }

    /// <summary>
    /// Additional stable server query parameters not yet promoted to typed SDK
    /// properties. Typed properties win when keys overlap.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// JSON request body for POST <c>/stac/search</c>.
/// </summary>
public sealed record StacSearchRequest
{
    /// <summary>Collection identifiers to include in the search.</summary>
    [JsonPropertyName("collections")]
    public IReadOnlyList<string>? Collections { get; init; }

    /// <summary>Specific item identifiers to return.</summary>
    [JsonPropertyName("ids")]
    public IReadOnlyList<string>? Ids { get; init; }

    /// <summary>Bounding box filter as [minLon, minLat, maxLon, maxLat], or 3D equivalent.</summary>
    [JsonPropertyName("bbox")]
    public IReadOnlyList<double>? Bbox { get; init; }

    /// <summary>Temporal filter as an ISO 8601 instant or interval.</summary>
    [JsonPropertyName("datetime")]
    public string? Datetime { get; init; }

    /// <summary>GeoJSON geometry filter.</summary>
    [JsonPropertyName("intersects")]
    public JsonElement? Intersects { get; init; }

    /// <summary>STAC Query extension payload.</summary>
    [JsonPropertyName("query")]
    public JsonElement? Query { get; init; }

    /// <summary>CQL2 or server-supported filter expression.</summary>
    [JsonPropertyName("filter")]
    public string? Filter { get; init; }

    /// <summary>Filter language, such as <c>cql2-text</c>.</summary>
    [JsonPropertyName("filter-lang")]
    public string? FilterLang { get; init; }

    /// <summary>Maximum number of items to return per page.</summary>
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>Number of items to skip when the server supports offset paging.</summary>
    [JsonPropertyName("offset")]
    public int? Offset { get; init; }

    /// <summary>Opaque server-driven page token used by some STAC servers.</summary>
    [JsonPropertyName("next")]
    public string? Next { get; init; }

    /// <summary>Sort expression, when supported by the backing server.</summary>
    [JsonPropertyName("sortby")]
    public string? SortBy { get; init; }

    /// <summary>Subset of item or asset fields to include or exclude.</summary>
    [JsonPropertyName("fields")]
    public StacFields? Fields { get; init; }

    /// <summary>Additional server-supported search request members.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>
/// STAC fields projection request.
/// </summary>
public sealed record StacFields
{
    /// <summary>Fields to include in the response.</summary>
    [JsonPropertyName("include")]
    public IReadOnlyList<string>? Include { get; init; }

    /// <summary>Fields to exclude from the response.</summary>
    [JsonPropertyName("exclude")]
    public IReadOnlyList<string>? Exclude { get; init; }
}
