// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>
/// Request parameters for applying feature edits to a service layer.
/// </summary>
public sealed record ApplyEditsRequest
{
    /// <summary>Service identifier.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Layer index within the service.</summary>
    public required int LayerId { get; init; }

    /// <summary>Features to add.</summary>
    public IReadOnlyList<Feature>? Adds { get; init; }

    /// <summary>Features to update (must include Id).</summary>
    public IReadOnlyList<Feature>? Updates { get; init; }

    /// <summary>Object IDs of features to delete.</summary>
    public IReadOnlyList<long>? Deletes { get; init; }

    /// <summary>Whether to roll back all edits if any single edit fails.</summary>
    public bool RollbackOnFailure { get; init; }

    /// <summary>Whether to force writes that would otherwise be rejected by conflict detection.</summary>
    public bool ForceWrite { get; init; }
}
