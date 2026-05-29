// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Offline.Abstractions;

namespace OfflineConflictConsole;

/// <summary>
/// Minimal in-memory offline feature, checkpoint, and state store for the conflict demo.
/// A real host would back these with GeoPackage, SQLite, or browser storage.
/// </summary>
public sealed class InMemoryOfflineStore : IOfflineFeatureStore, IOfflineSyncCheckpointStore, IOfflineSyncStateStore
{
    /// <summary>Feature pages stored locally.</summary>
    public List<OfflineFeaturePage> Pages { get; } = [];

    /// <summary>Source checkpoints keyed by source identifier.</summary>
    public Dictionary<string, OfflineSyncCheckpoint> Checkpoints { get; } = [];

    /// <summary>Recorded sync states, in order.</summary>
    public List<OfflineSyncState> States { get; } = [];

    /// <inheritdoc />
    public Task SaveFeaturesAsync(OfflineFeaturePage page, CancellationToken cancellationToken = default)
    {
        Pages.Add(page);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteFeaturesAsync(
        string packageId,
        string sourceId,
        IReadOnlyList<string> featureIds,
        IReadOnlyList<long> objectIds,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public Task<OfflineSyncCheckpoint?> GetCheckpointAsync(
        string packageId,
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        Checkpoints.TryGetValue(sourceId, out var checkpoint);
        return Task.FromResult<OfflineSyncCheckpoint?>(checkpoint);
    }

    /// <inheritdoc />
    public Task SaveCheckpointAsync(OfflineSyncCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        Checkpoints[checkpoint.SourceId] = checkpoint;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<OfflineSyncState?> GetStateAsync(
        string packageId,
        string? sourceId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult<OfflineSyncState?>(
            States.LastOrDefault(state => state.PackageId == packageId && state.SourceId == sourceId));

    /// <inheritdoc />
    public Task SaveStateAsync(OfflineSyncState state, CancellationToken cancellationToken = default)
    {
        States.Add(state);
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory change journal that tracks each lifecycle transition the engine drives.
/// </summary>
public sealed class InMemoryChangeJournal : IOfflineChangeJournal
{
    private readonly List<OfflineChangeJournalEntry> _pending;

    /// <summary>Initializes the journal with seeded pending operations.</summary>
    /// <param name="pending">Pending operations to enqueue.</param>
    public InMemoryChangeJournal(IReadOnlyList<OfflineChangeJournalEntry>? pending = null)
    {
        _pending = pending?.ToList() ?? [];
    }

    /// <summary>Operations marked succeeded.</summary>
    public List<string> Succeeded { get; } = [];

    /// <summary>Operations released back to pending.</summary>
    public List<string> PendingMarks { get; } = [];

    /// <summary>Retry checkpoints recorded.</summary>
    public List<OfflineRetryCheckpoint> RetryCheckpoints { get; } = [];

    /// <summary>Operations marked fatally failed, with reason.</summary>
    public Dictionary<string, string> Failed { get; } = [];

    /// <summary>Conflict envelopes recorded by the journal.</summary>
    public List<OfflineConflictEnvelope> Conflicts { get; } = [];

    /// <inheritdoc />
    public Task EnqueueAsync(OfflineChangeJournalEntry entry, CancellationToken cancellationToken = default)
    {
        _pending.Add(entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OfflineChangeJournalEntry>> GetPendingAsync(
        string packageId,
        int maxCount,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OfflineChangeJournalEntry>>(
            _pending.Where(entry => entry.PackageId == packageId).Take(maxCount).ToArray());

    /// <inheritdoc />
    public Task MarkSucceededAsync(string operationId, CancellationToken cancellationToken = default)
    {
        Succeeded.Add(operationId);
        _pending.RemoveAll(entry => entry.OperationId == operationId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkPendingAsync(string operationId, CancellationToken cancellationToken = default)
    {
        PendingMarks.Add(operationId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkRetryAsync(OfflineRetryCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        RetryCheckpoints.Add(checkpoint);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkFailedAsync(string operationId, string reason, CancellationToken cancellationToken = default)
    {
        Failed[operationId] = reason;
        _pending.RemoveAll(entry => entry.OperationId == operationId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkConflictAsync(OfflineConflictEnvelope conflict, CancellationToken cancellationToken = default)
    {
        Conflicts.Add(conflict);
        _pending.RemoveAll(entry => entry.OperationId == conflict.OperationId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// In-memory conflict store that supports listing and resolving conflict envelopes.
/// </summary>
public sealed class InMemoryConflictStore : IOfflineConflictStore
{
    private readonly List<OfflineConflictEnvelope> _conflicts = [];
    private readonly HashSet<string> _resolved = [];

    /// <inheritdoc />
    public Task SaveConflictAsync(OfflineConflictEnvelope conflict, CancellationToken cancellationToken = default)
    {
        _conflicts.Add(conflict);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OfflineConflictEnvelope>> ListConflictsAsync(
        string packageId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OfflineConflictEnvelope>>(
            _conflicts
                .Where(conflict => conflict.PackageId == packageId && !_resolved.Contains(conflict.OperationId))
                .ToArray());

    /// <inheritdoc />
    public Task ResolveConflictAsync(string operationId, CancellationToken cancellationToken = default)
    {
        _resolved.Add(operationId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Scripted query client that replays canned feature pages for the pull demonstration.
/// </summary>
public sealed class ScriptedQueryClient : IHonuaFeatureQueryClient
{
    /// <summary>Pages replayed by <see cref="QueryPagesAsync"/>.</summary>
    public Queue<FeatureQueryResult> Pages { get; } = new();

    /// <inheritdoc />
    public string ProviderName => "scripted-offline";

    /// <inheritdoc />
    public Task<FeatureQueryResult> QueryAsync(FeatureQueryRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(Pages.Count > 0
            ? Pages.Peek()
            : new FeatureQueryResult { ProviderName = ProviderName });

    /// <inheritdoc />
    public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
        FeatureQueryRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (Pages.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Pages.Dequeue();
            await Task.Yield();
        }
    }
}

/// <summary>
/// Scripted edit client that returns canned responses keyed by whether the request forces a write.
/// This lets the demo produce a server-side conflict on the first upload and accept a forced retry.
/// </summary>
public sealed class ScriptedEditClient : IHonuaFeatureEditClient
{
    private readonly Func<FeatureEditRequest, FeatureEditResponse> _responder;

    /// <summary>Initializes the client with a response factory.</summary>
    /// <param name="responder">Maps an edit request to a canned response.</param>
    public ScriptedEditClient(Func<FeatureEditRequest, FeatureEditResponse> responder)
    {
        _responder = responder;
    }

    /// <summary>Requests received, in order.</summary>
    public List<FeatureEditRequest> Requests { get; } = [];

    /// <inheritdoc />
    public string ProviderName => "scripted-offline";

    /// <inheritdoc />
    public FeatureEditCapabilities EditCapabilities { get; } = new()
    {
        SupportsAdds = true,
        SupportsUpdates = true,
        SupportsDeletes = true,
        SupportsRollbackOnFailure = true,
    };

    /// <inheritdoc />
    public Task<FeatureEditResponse> ApplyEditsAsync(FeatureEditRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}
