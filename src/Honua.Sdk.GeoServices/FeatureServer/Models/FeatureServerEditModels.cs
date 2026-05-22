// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.GeoServices.FeatureServer.Models;

/// <summary>
/// Request payload for FeatureServer add, update, and delete edit operations.
/// </summary>
public sealed record FeatureServerEditRequest
{
    /// <summary>Features to add.</summary>
    public IReadOnlyList<FeatureServerFeature>? Adds { get; init; }

    /// <summary>Features to update. Each feature must include the layer object ID field.</summary>
    public IReadOnlyList<FeatureServerFeature>? Updates { get; init; }

    /// <summary>Object IDs of features to delete.</summary>
    public IReadOnlyList<long>? Deletes { get; init; }

    /// <summary>Whether the server should roll back all edits if any individual edit fails.</summary>
    public bool RollbackOnFailure { get; init; } = true;

    /// <summary>Whether the server should force conflict-overriding writes when supported.</summary>
    public bool ForceWrite { get; init; }
}

/// <summary>
/// Response from FeatureServer add, update, and delete edit operations.
/// </summary>
public sealed class FeatureServerEditResponse
{
    /// <summary>Results for add operations.</summary>
    [JsonPropertyName("addResults")]
    public IReadOnlyList<FeatureServerEditResult> AddResults { get; init; } = [];

    /// <summary>Results for update operations.</summary>
    [JsonPropertyName("updateResults")]
    public IReadOnlyList<FeatureServerEditResult> UpdateResults { get; init; } = [];

    /// <summary>Results for delete operations.</summary>
    [JsonPropertyName("deleteResults")]
    public IReadOnlyList<FeatureServerEditResult> DeleteResults { get; init; } = [];

    /// <summary>Edit moment reported by the server, when available.</summary>
    [JsonPropertyName("editMoment")]
    public long? EditMoment { get; init; }
}

/// <summary>
/// The outcome of a single FeatureServer edit operation.
/// </summary>
public sealed class FeatureServerEditResult
{
    /// <summary>Object ID of the affected feature.</summary>
    [JsonPropertyName("objectId")]
    public long? ObjectId { get; init; }

    /// <summary>Global ID of the affected feature, when available.</summary>
    [JsonPropertyName("globalId")]
    public string? GlobalId { get; init; }

    /// <summary>Whether the edit operation succeeded.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>Error details when the edit operation failed.</summary>
    [JsonPropertyName("error")]
    public FeatureServerEditError? Error { get; init; }
}

/// <summary>
/// Error details for a failed FeatureServer edit operation.
/// </summary>
public sealed class FeatureServerEditError
{
    /// <summary>Provider-specific error code.</summary>
    [JsonPropertyName("code")]
    public int? Code { get; init; }

    /// <summary>Error description returned by GeoServices.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Alternate error message returned by some GeoServices implementations.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
