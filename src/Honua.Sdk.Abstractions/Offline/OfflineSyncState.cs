// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Offline.Abstractions;

/// <summary>
/// High-level phase of an offline sync operation.
/// </summary>
public enum OfflineSyncPhase
{
    /// <summary>No sync operation is currently active.</summary>
    Idle,

    /// <summary>The sync engine is planning source downloads.</summary>
    Planning,

    /// <summary>The sync engine is pushing local edits.</summary>
    Pushing,

    /// <summary>The sync engine is pulling remote feature changes.</summary>
    Pulling,

    /// <summary>The last sync operation completed.</summary>
    Completed,

    /// <summary>The last sync operation failed.</summary>
    Failed,
}

/// <summary>
/// Persisted state for a package or source sync run.
/// </summary>
public sealed record OfflineSyncState
{
    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Optional source identifier when the state is source-specific.</summary>
    public string? SourceId { get; init; }

    /// <summary>Current sync phase.</summary>
    public OfflineSyncPhase Phase { get; init; } = OfflineSyncPhase.Idle;

    /// <summary>Time when the current or last sync run started.</summary>
    public DateTimeOffset? LastStartedAtUtc { get; init; }

    /// <summary>Time when the last sync run completed successfully.</summary>
    public DateTimeOffset? LastSucceededAtUtc { get; init; }

    /// <summary>Last provider sync token stored for this package or source.</summary>
    public string? LastSyncToken { get; init; }

    /// <summary>Number of local changes still pending when the state was captured.</summary>
    public int PendingChangeCount { get; init; }

    /// <summary>Diagnostic message from the last failed sync run.</summary>
    public string? LastError { get; init; }
}

/// <summary>
/// Provider checkpoint for an offline source.
/// </summary>
public sealed record OfflineSyncCheckpoint
{
    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Source identifier inside the package.</summary>
    public required string SourceId { get; init; }

    /// <summary>Provider sync token associated with the checkpoint.</summary>
    public string? SyncToken { get; init; }

    /// <summary>Time when the checkpoint was recorded.</summary>
    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Number of features pulled before this checkpoint was recorded.</summary>
    public int PulledFeatureCount { get; init; }
}
