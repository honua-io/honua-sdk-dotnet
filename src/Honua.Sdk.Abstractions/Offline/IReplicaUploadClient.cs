// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Offline.Abstractions;

/// <summary>Uploads feature edits to a replica registered with the per-replica sync model.</summary>
public interface IReplicaUploadClient
{
    /// <summary>Uploads edits based on an explicitly supplied server generation.</summary>
    Task<ReplicaUploadResult> UploadReplicaAsync(
        string serviceId,
        string replicaId,
        ReplicaUploadRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>A batch of feature edits based on the client's applied download cursor.</summary>
public sealed class ReplicaUploadRequest
{
    /// <summary>Creates an upload request for the given generation and layer edits.</summary>
    public ReplicaUploadRequest(long receivedServerGen, IReadOnlyList<LayerChangeSet> edits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(receivedServerGen);
        ArgumentNullException.ThrowIfNull(edits);
        ReceivedServerGen = receivedServerGen;
        Edits = edits;
    }

    /// <summary>The server generation through which the client has locally applied changes.</summary>
    public long ReceivedServerGen { get; }

    /// <summary>Feature changes grouped by layer, with at most one batch per layer.</summary>
    public IReadOnlyList<LayerChangeSet> Edits { get; }

    /// <summary>Requests manual review of conflicts on servers with durable conflict storage.</summary>
    public bool ManualReview { get; init; }

    /// <summary>Optional idempotency key to retain across retries of the same upload.</summary>
    public string? UploadId { get; init; }
}

/// <summary>The server's outcome for a per-replica upload.</summary>
public sealed class ReplicaUploadResult
{
    /// <summary>Whether the server accepted the batch, which may still contain conflicts.</summary>
    public bool Success { get; init; }

    /// <summary>The server's replica cursor after upload. Advance the local download cursor only after applying downloaded changes.</summary>
    public long ServerGen { get; init; }

    /// <summary>The number of applied adds.</summary>
    public int AppliedAdds { get; init; }

    /// <summary>The number of applied updates.</summary>
    public int AppliedUpdates { get; init; }

    /// <summary>The number of applied deletes.</summary>
    public int AppliedDeletes { get; init; }

    /// <summary>Per-layer outcomes returned by the server. Honua currently returns assigned identities for applied adds.</summary>
    public IReadOnlyList<ReplicaUploadLayerResult> EditResults { get; init; } = [];

    /// <summary>Conflicts reported by the server, including edits held for manual review.</summary>
    public IReadOnlyList<ReplicaUploadConflict> Conflicts { get; init; } = [];
}

/// <summary>Individual edit outcomes supplied by the server for one layer.</summary>
public sealed class ReplicaUploadLayerResult
{
    /// <summary>The numeric layer ID.</summary>
    public int LayerId { get; init; }

    /// <summary>Outcomes for additions reported by the server, including assigned identifiers.</summary>
    public IReadOnlyList<FeatureEditResult> AddResults { get; init; } = [];

    /// <summary>Outcomes for updates when reported by the server.</summary>
    public IReadOnlyList<FeatureEditResult> UpdateResults { get; init; } = [];

    /// <summary>Outcomes for deletions when reported by the server.</summary>
    public IReadOnlyList<FeatureEditResult> DeleteResults { get; init; } = [];
}

/// <summary>A conflict between an uploaded edit and a server-side change.</summary>
public sealed class ReplicaUploadConflict
{
    /// <summary>The numeric layer ID.</summary>
    public int LayerId { get; init; }

    /// <summary>The object ID of the conflicting feature.</summary>
    public long ObjectId { get; init; }

    /// <summary>The conflict classification code returned by Honua Server.</summary>
    public int ConflictType { get; init; }

    /// <summary>Whether the server applied the conflicting edit.</summary>
    public bool Applied { get; init; }

    /// <summary>The zero-based edit index within the layer's combined adds, updates, and deletes.</summary>
    public int EditIndex { get; init; }

    /// <summary>The durable review identifier, when available.</summary>
    public string? ConflictId { get; init; }
}
