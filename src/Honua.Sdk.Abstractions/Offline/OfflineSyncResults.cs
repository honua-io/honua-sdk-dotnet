// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Offline.Abstractions;

/// <summary>
/// Strategy used when a push operation reports an edit conflict.
/// </summary>
public enum OfflineConflictStrategy
{
    /// <summary>Keep the server version and mark the local operation handled.</summary>
    ServerWins,

    /// <summary>Retry the local operation with provider force-write semantics.</summary>
    ClientWins,

    /// <summary>Record a conflict envelope for host-specific manual review.</summary>
    ManualReview,
}

/// <summary>
/// Failure reported by a pull, push, or full sync operation.
/// </summary>
public sealed record OfflineSyncFailure
{
    /// <summary>Operation identifier, when the failure came from a local edit.</summary>
    public string? OperationId { get; init; }

    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Source identifier inside the package, when known.</summary>
    public string? SourceId { get; init; }

    /// <summary>Whether the failure can be retried later.</summary>
    public bool Retryable { get; init; }

    /// <summary>Failure reason.</summary>
    public required string Reason { get; init; }
}

/// <summary>
/// Result of pulling remote features into a local offline store.
/// </summary>
public sealed record OfflinePullResult
{
    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Number of source descriptors planned for download.</summary>
    public int PlannedSourceCount { get; init; }

    /// <summary>Number of result pages stored locally.</summary>
    public int StoredPageCount { get; init; }

    /// <summary>Number of features stored locally.</summary>
    public int StoredFeatureCount { get; init; }

    /// <summary>Pull failures.</summary>
    public IReadOnlyList<OfflineSyncFailure> Failures { get; init; } = [];
}

/// <summary>
/// Result of pushing local offline edits to a provider.
/// </summary>
public sealed record OfflinePushResult
{
    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Number of pending local operations loaded from the journal.</summary>
    public int Loaded { get; init; }

    /// <summary>Number of operations applied successfully.</summary>
    public int Succeeded { get; init; }

    /// <summary>Number of operations placed into conflict review.</summary>
    public int Conflicts { get; init; }

    /// <summary>Number of operations recorded as retryable failures.</summary>
    public int RetryableFailures { get; init; }

    /// <summary>Number of operations recorded as fatal failures.</summary>
    public int FatalFailures { get; init; }

    /// <summary>Push failures.</summary>
    public IReadOnlyList<OfflineSyncFailure> Failures { get; init; } = [];
}

/// <summary>
/// Combined result of a full push and pull sync run.
/// </summary>
public sealed record OfflineSyncRunResult
{
    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Push result.</summary>
    public required OfflinePushResult Push { get; init; }

    /// <summary>Pull result.</summary>
    public required OfflinePullResult Pull { get; init; }

    /// <summary>Whether the sync run completed without failures.</summary>
    public bool Succeeded => Push.Failures.Count == 0 && Pull.Failures.Count == 0;
}
