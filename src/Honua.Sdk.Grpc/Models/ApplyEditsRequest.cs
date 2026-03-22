// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>
/// Request parameters for applying feature edits to a service layer.
/// </summary>
public sealed class ApplyEditsRequest
{
    /// <summary>Service identifier.</summary>
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>Layer index within the service.</summary>
    public int LayerId { get; set; }

    /// <summary>Features to add.</summary>
    public IReadOnlyList<Feature>? Adds { get; set; }

    /// <summary>Features to update (must include Id).</summary>
    public IReadOnlyList<Feature>? Updates { get; set; }

    /// <summary>Object IDs of features to delete.</summary>
    public IReadOnlyList<long>? Deletes { get; set; }

    /// <summary>Whether to roll back all edits if any single edit fails.</summary>
    public bool RollbackOnFailure { get; set; }

    /// <summary>Whether to force writes that would otherwise be rejected by conflict detection.</summary>
    public bool ForceWrite { get; set; }
}
