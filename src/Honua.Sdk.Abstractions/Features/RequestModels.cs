// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Public request DTO for FeatureServer query operations.
/// </summary>
public sealed record QueryFeaturesRequest
{
    /// <summary>FeatureServer service identifier (e.g. catalog or service name).</summary>
    public required string ServiceId { get; init; }

    /// <summary>Zero-based layer index within the service.</summary>
    public required int LayerId { get; init; }

    /// <summary>Optional SQL-like WHERE clause filter. <see langword="null"/> defers to the server default (typically <c>1=1</c>).</summary>
    public string? Where { get; init; }

    /// <summary>Optional explicit object-id filter; mutually exclusive with <see cref="Where"/> semantics depending on server.</summary>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>Optional list of output field names; <see langword="null"/> returns all fields.</summary>
    public IReadOnlyList<string>? OutFields { get; init; }

    /// <summary>When <see langword="true"/>, geometry is included in the response.</summary>
    public bool ReturnGeometry { get; init; } = true;

    /// <summary>Optional zero-based offset for pagination.</summary>
    public int? ResultOffset { get; init; }

    /// <summary>Optional maximum number of records per page. <see langword="null"/> means the server default (typically unlimited within server policy).</summary>
    public int? ResultRecordCount { get; init; }

    /// <summary>Optional ORDER BY clause.</summary>
    public string? OrderBy { get; init; }

    /// <summary>When <see langword="true"/>, return only distinct rows.</summary>
    public bool ReturnDistinct { get; init; }

    /// <summary>When <see langword="true"/>, return only the matching row count.</summary>
    public bool ReturnCountOnly { get; init; }

    /// <summary>When <see langword="true"/>, return only object identifiers, not full features.</summary>
    public bool ReturnIdsOnly { get; init; }

    /// <summary>When <see langword="true"/>, return only the aggregate extent of matches.</summary>
    public bool ReturnExtentOnly { get; init; }

    /// <summary>Response format selector. Defaults to <c>json</c>.</summary>
    public string ResponseFormat { get; init; } = "json";
}

/// <summary>
/// Public request DTO for FeatureServer applyEdits operations.
/// </summary>
public sealed record ApplyEditsRequest
{
    /// <summary>FeatureServer service identifier.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Zero-based layer index within the service.</summary>
    public required int LayerId { get; init; }

    /// <summary>Features to insert.</summary>
    public IReadOnlyList<FeatureEditFeature>? Adds { get; init; }

    /// <summary>Features to update; each must carry its provider object identifier.</summary>
    public IReadOnlyList<FeatureEditFeature>? Updates { get; init; }

    /// <summary>Object identifiers to delete.</summary>
    public IReadOnlyList<long>? Deletes { get; init; }

    /// <summary>Optional pre-serialized JSON adds payload, used when <see cref="Adds"/> is <see langword="null"/>.</summary>
    public string? AddsJson { get; init; }

    /// <summary>Optional pre-serialized JSON updates payload, used when <see cref="Updates"/> is <see langword="null"/>.</summary>
    public string? UpdatesJson { get; init; }

    /// <summary>Optional comma-separated object identifiers to delete, used when <see cref="Deletes"/> is <see langword="null"/>.</summary>
    public string? DeletesCsv { get; init; }

    /// <summary>When <see langword="true"/>, fail the entire batch if any single edit fails.</summary>
    public bool RollbackOnFailure { get; init; }

    /// <summary>When <see langword="true"/>, bypass server-side conflict checks.</summary>
    public bool ForceWrite { get; init; }

    /// <summary>Response format selector. Defaults to <c>json</c>.</summary>
    public string ResponseFormat { get; init; } = "json";
}

/// <summary>
/// Public request DTO for the OGC API Features <c>GET /collections/{id}/items</c> endpoint.
/// </summary>
public sealed record OgcItemsRequest
{
    /// <summary>OGC collection identifier.</summary>
    public required string CollectionId { get; init; }

    /// <summary>Optional page size limit.</summary>
    public int? Limit { get; init; }

    /// <summary>Optional zero-based offset for pagination.</summary>
    public int? Offset { get; init; }

    /// <summary>Optional list of property names to project; <see langword="null"/> returns all.</summary>
    public IReadOnlyList<string>? PropertyNames { get; init; }

    /// <summary>Optional CQL filter expression.</summary>
    public string? CqlFilter { get; init; }

    /// <summary>Response format selector. Defaults to <c>json</c>.</summary>
    public string ResponseFormat { get; init; } = "json";
}

/// <summary>
/// Public request DTO for creating an OGC API Features item (HTTP POST).
/// </summary>
public sealed record OgcCreateItemRequest
{
    /// <summary>OGC collection identifier.</summary>
    public required string CollectionId { get; init; }

    /// <summary>GeoJSON feature payload to create.</summary>
    public required JsonElement Feature { get; init; }
}

/// <summary>
/// Public request DTO for replacing an OGC API Features item (HTTP PUT).
/// </summary>
public sealed record OgcReplaceItemRequest
{
    /// <summary>OGC collection identifier.</summary>
    public required string CollectionId { get; init; }

    /// <summary>Identifier of the existing feature to replace.</summary>
    public required string FeatureId { get; init; }

    /// <summary>Full GeoJSON feature payload that replaces the existing item.</summary>
    public required JsonElement Feature { get; init; }
}

/// <summary>
/// Public request DTO for partially updating an OGC API Features item via JSON Merge Patch (RFC 7396).
/// </summary>
public sealed record OgcPatchItemRequest
{
    /// <summary>OGC collection identifier.</summary>
    public required string CollectionId { get; init; }

    /// <summary>Identifier of the existing feature to patch.</summary>
    public required string FeatureId { get; init; }

    /// <summary>JSON Merge Patch document describing the partial update.</summary>
    public required JsonElement Patch { get; init; }
}

/// <summary>
/// Public request DTO for deleting an OGC API Features item.
/// </summary>
public sealed record OgcDeleteItemRequest
{
    /// <summary>OGC collection identifier.</summary>
    public required string CollectionId { get; init; }

    /// <summary>Identifier of the existing feature to delete.</summary>
    public required string FeatureId { get; init; }
}
