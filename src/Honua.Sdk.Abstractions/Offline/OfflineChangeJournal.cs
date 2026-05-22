// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Offline.Abstractions;

/// <summary>
/// Type of local edit stored in an offline change journal.
/// </summary>
public enum OfflineEditOperationKind
{
    /// <summary>Add a new feature.</summary>
    Add,

    /// <summary>Update an existing feature.</summary>
    Update,

    /// <summary>Delete an existing feature.</summary>
    Delete,
}

/// <summary>
/// Local edit operation queued while offline.
/// </summary>
public sealed record OfflineChangeJournalEntry
{
    /// <summary>Stable operation identifier.</summary>
    public required string OperationId { get; init; }

    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Source identifier inside the package.</summary>
    public required string SourceId { get; init; }

    /// <summary>Provider-specific feature source for the edit operation.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Operation kind.</summary>
    public OfflineEditOperationKind OperationKind { get; init; }

    /// <summary>Feature payload for add or update operations.</summary>
    public FeatureEditFeature? Feature { get; init; }

    /// <summary>String feature identifiers for delete operations.</summary>
    public IReadOnlyList<string> DeleteIds { get; init; } = [];

    /// <summary>Numeric object identifiers for delete operations.</summary>
    public IReadOnlyList<long> DeleteObjectIds { get; init; } = [];

    /// <summary>Provider sync token observed when the local operation was queued.</summary>
    public string? BaseSyncToken { get; init; }

    /// <summary>Time when the operation was queued.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Number of prior upload attempts.</summary>
    public int AttemptCount { get; init; }

    /// <summary>Application metadata associated with the operation.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Retry checkpoint recorded after a retryable offline sync failure.
/// </summary>
public sealed record OfflineRetryCheckpoint
{
    /// <summary>Operation identifier that should be retried.</summary>
    public required string OperationId { get; init; }

    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Source identifier inside the package.</summary>
    public required string SourceId { get; init; }

    /// <summary>Attempt count after the failed upload.</summary>
    public int AttemptCount { get; init; }

    /// <summary>Time before which the operation should not be retried.</summary>
    public DateTimeOffset? RetryAfterUtc { get; init; }

    /// <summary>Failure reason returned by the provider or sync engine.</summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Conflict envelope emitted when a local offline edit cannot be reconciled automatically.
/// </summary>
public sealed record OfflineConflictEnvelope
{
    /// <summary>Operation identifier that produced the conflict.</summary>
    public required string OperationId { get; init; }

    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Source identifier inside the package.</summary>
    public required string SourceId { get; init; }

    /// <summary>Local edit operation that conflicted with the server state.</summary>
    public required OfflineChangeJournalEntry LocalOperation { get; init; }

    /// <summary>Server feature snapshot when the provider supplies one.</summary>
    public FeatureRecord? ServerFeature { get; init; }

    /// <summary>Provider edit error that produced the conflict.</summary>
    public FeatureEditError? Error { get; init; }

    /// <summary>Human-readable conflict reason.</summary>
    public string? Reason { get; init; }

    /// <summary>Time when the conflict was detected.</summary>
    public DateTimeOffset DetectedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
