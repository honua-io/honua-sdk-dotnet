// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>Feature mutations targeting a single layer within a service-wide edit request.</summary>
public sealed record ServiceLayerEdits
{
    /// <summary>Layer identifier within the service.</summary>
    public required int LayerId { get; init; }

    /// <summary>Features to create.</summary>
    public IReadOnlyList<Feature>? Adds { get; init; }

    /// <summary>Features to update.</summary>
    public IReadOnlyList<Feature>? Updates { get; init; }

    /// <summary>Object IDs of features to delete.</summary>
    public IReadOnlyList<long>? Deletes { get; init; }

    /// <summary>Whether to override optimistic concurrency checks for this layer.</summary>
    public bool ForceWrite { get; init; }

    /// <summary>Whether updates and deletes address features by global ID.</summary>
    public bool UseGlobalIds { get; init; }

    /// <summary>Global IDs of features to delete when UseGlobalIds is true.</summary>
    public IReadOnlyList<string>? DeleteGlobalIds { get; init; }
}

/// <summary>Request parameters for applying ordered multi-layer edits within one service.</summary>
public sealed record ApplyServiceEditsRequest
{
    /// <summary>Service identifier.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Ordered per-layer edit batches targeting distinct layers.</summary>
    public IReadOnlyList<ServiceLayerEdits> Edits { get; init; } = [];

    /// <summary>Whether any failure rolls back the complete multi-layer transaction.</summary>
    public bool RollbackOnFailure { get; init; }

    /// <summary>Optional client-generated idempotency key deduplicating the complete request.</summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>Per-layer outcome within a service-wide edit response.</summary>
public sealed class ServiceLayerEditResults
{
    /// <summary>Layer identifier within the service.</summary>
    public int LayerId { get; init; }

    /// <summary>Per-operation results for the layer.</summary>
    public ApplyEditsResponse Results { get; init; } = new();
}

/// <summary>Response from applying ordered multi-layer edits within one service.</summary>
public sealed class ApplyServiceEditsResponse
{
    /// <summary>Per-layer edit results in request layer order.</summary>
    public IReadOnlyList<ServiceLayerEditResults> Results { get; init; } = [];

    /// <summary>Global transaction error when the batch failed or rolled back.</summary>
    public EditError? Error { get; init; }
}
