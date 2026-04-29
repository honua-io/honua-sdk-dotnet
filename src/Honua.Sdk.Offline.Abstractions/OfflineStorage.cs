// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Offline.Abstractions;

/// <summary>
/// Feature page persisted by a local offline feature store.
/// </summary>
public sealed record OfflineFeaturePage
{
    /// <summary>Offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Source identifier inside the package.</summary>
    public required string SourceId { get; init; }

    /// <summary>Source descriptor used to query the page.</summary>
    public required SourceDescriptor Source { get; init; }

    /// <summary>Provider-neutral feature page to persist.</summary>
    public required FeatureQueryResult Result { get; init; }

    /// <summary>Provider sync token used for the pull that produced this page.</summary>
    public string? SyncToken { get; init; }
}

/// <summary>
/// Local feature storage adapter implemented by mobile, desktop, browser, or test stores.
/// </summary>
public interface IOfflineFeatureStore
{
    /// <summary>
    /// Persists a feature result page for an offline source.
    /// </summary>
    /// <param name="page">Feature page to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the page is stored.</returns>
    Task SaveFeaturesAsync(OfflineFeaturePage page, CancellationToken ct = default);

    /// <summary>
    /// Deletes features from a local source cache.
    /// </summary>
    /// <param name="packageId">Offline package identifier.</param>
    /// <param name="sourceId">Source identifier inside the package.</param>
    /// <param name="featureIds">String feature identifiers to delete.</param>
    /// <param name="objectIds">Numeric object identifiers to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when deletes are applied.</returns>
    Task DeleteFeaturesAsync(
        string packageId,
        string sourceId,
        IReadOnlyList<string> featureIds,
        IReadOnlyList<long> objectIds,
        CancellationToken ct = default);
}

/// <summary>
/// Change journal adapter for local offline edit operations.
/// </summary>
public interface IOfflineChangeJournal
{
    /// <summary>
    /// Adds a local edit operation to the change journal.
    /// </summary>
    /// <param name="entry">Change journal entry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the entry is stored.</returns>
    Task EnqueueAsync(OfflineChangeJournalEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Gets pending local edit operations for a package.
    /// </summary>
    /// <param name="packageId">Offline package identifier.</param>
    /// <param name="maxCount">Maximum number of operations to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Pending operations ordered for upload.</returns>
    Task<IReadOnlyList<OfflineChangeJournalEntry>> GetPendingAsync(
        string packageId,
        int maxCount,
        CancellationToken ct = default);

    /// <summary>
    /// Marks an operation as uploaded successfully.
    /// </summary>
    /// <param name="operationId">Operation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the journal is updated.</returns>
    Task MarkSucceededAsync(string operationId, CancellationToken ct = default);

    /// <summary>
    /// Releases a claimed operation back to pending state.
    /// </summary>
    /// <param name="operationId">Operation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the journal is updated.</returns>
    Task MarkPendingAsync(string operationId, CancellationToken ct = default);

    /// <summary>
    /// Records a retryable upload failure.
    /// </summary>
    /// <param name="checkpoint">Retry checkpoint.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the journal is updated.</returns>
    Task MarkRetryAsync(OfflineRetryCheckpoint checkpoint, CancellationToken ct = default);

    /// <summary>
    /// Records an unrecoverable upload failure.
    /// </summary>
    /// <param name="operationId">Operation identifier.</param>
    /// <param name="reason">Failure reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the journal is updated.</returns>
    Task MarkFailedAsync(string operationId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Records a conflict that requires review or host-specific resolution.
    /// </summary>
    /// <param name="conflict">Conflict envelope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the journal is updated.</returns>
    Task MarkConflictAsync(OfflineConflictEnvelope conflict, CancellationToken ct = default);
}

/// <summary>
/// Conflict store adapter for hosts that keep conflict review separate from the change journal.
/// </summary>
public interface IOfflineConflictStore
{
    /// <summary>
    /// Persists a conflict envelope.
    /// </summary>
    /// <param name="conflict">Conflict envelope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the conflict is stored.</returns>
    Task SaveConflictAsync(OfflineConflictEnvelope conflict, CancellationToken ct = default);

    /// <summary>
    /// Lists unresolved conflicts for an offline package.
    /// </summary>
    /// <param name="packageId">Offline package identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unresolved conflicts.</returns>
    Task<IReadOnlyList<OfflineConflictEnvelope>> ListConflictsAsync(string packageId, CancellationToken ct = default);

    /// <summary>
    /// Marks a conflict as resolved.
    /// </summary>
    /// <param name="operationId">Operation identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the conflict is marked resolved.</returns>
    Task ResolveConflictAsync(string operationId, CancellationToken ct = default);
}

/// <summary>
/// Checkpoint store adapter for provider sync tokens and source-level pull progress.
/// </summary>
public interface IOfflineSyncCheckpointStore
{
    /// <summary>
    /// Gets the last checkpoint for a package source.
    /// </summary>
    /// <param name="packageId">Offline package identifier.</param>
    /// <param name="sourceId">Source identifier inside the package.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The checkpoint, or null when no checkpoint exists.</returns>
    Task<OfflineSyncCheckpoint?> GetCheckpointAsync(string packageId, string sourceId, CancellationToken ct = default);

    /// <summary>
    /// Saves the last checkpoint for a package source.
    /// </summary>
    /// <param name="checkpoint">Checkpoint to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the checkpoint is stored.</returns>
    Task SaveCheckpointAsync(OfflineSyncCheckpoint checkpoint, CancellationToken ct = default);
}

/// <summary>
/// Sync state store adapter for package and source sync progress.
/// </summary>
public interface IOfflineSyncStateStore
{
    /// <summary>
    /// Gets the last sync state for a package or source.
    /// </summary>
    /// <param name="packageId">Offline package identifier.</param>
    /// <param name="sourceId">Optional source identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Persisted sync state, or null when no state exists.</returns>
    Task<OfflineSyncState?> GetStateAsync(string packageId, string? sourceId = null, CancellationToken ct = default);

    /// <summary>
    /// Saves sync state for a package or source.
    /// </summary>
    /// <param name="state">State to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when state is stored.</returns>
    Task SaveStateAsync(OfflineSyncState state, CancellationToken ct = default);
}

/// <summary>
/// Scheduler-facing contract for hosts that trigger offline sync from UI, background, or service workers.
/// </summary>
public interface IOfflineSyncRunner
{
    /// <summary>
    /// Runs a full push and pull sync for an offline package.
    /// </summary>
    /// <param name="manifest">Offline package manifest.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full sync run result.</returns>
    Task<OfflineSyncRunResult> SyncAsync(OfflinePackageManifest manifest, CancellationToken ct = default);
}
