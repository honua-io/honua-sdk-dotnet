// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Offline.Abstractions;

namespace Honua.Sdk.Offline;

/// <summary>
/// Provider-neutral offline sync engine for pushing local edits and pulling feature pages.
/// </summary>
public sealed class OfflineSyncEngine : IOfflineSyncRunner
{
    private const string MaxAttemptsReason = "max attempts reached";

    private readonly IHonuaFeatureQueryClient _queryClient;
    private readonly IHonuaFeatureEditClient _editClient;
    private readonly IOfflineFeatureStore _featureStore;
    private readonly IOfflineChangeJournal _changeJournal;
    private readonly IOfflineSyncCheckpointStore _checkpointStore;
    private readonly IOfflineSyncStateStore? _stateStore;
    private readonly IOfflineConflictStore? _conflictStore;
    private readonly OfflineSyncEngineOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfflineSyncEngine"/> class.
    /// </summary>
    /// <param name="queryClient">Provider-neutral query client.</param>
    /// <param name="editClient">Provider-neutral edit client.</param>
    /// <param name="featureStore">Local feature store.</param>
    /// <param name="changeJournal">Local change journal.</param>
    /// <param name="checkpointStore">Sync checkpoint store.</param>
    /// <param name="options">Sync engine options.</param>
    /// <param name="stateStore">Optional sync state store.</param>
    /// <param name="conflictStore">Optional conflict store.</param>
    public OfflineSyncEngine(
        IHonuaFeatureQueryClient queryClient,
        IHonuaFeatureEditClient editClient,
        IOfflineFeatureStore featureStore,
        IOfflineChangeJournal changeJournal,
        IOfflineSyncCheckpointStore checkpointStore,
        OfflineSyncEngineOptions? options = null,
        IOfflineSyncStateStore? stateStore = null,
        IOfflineConflictStore? conflictStore = null)
    {
        _queryClient = queryClient ?? throw new ArgumentNullException(nameof(queryClient));
        _editClient = editClient ?? throw new ArgumentNullException(nameof(editClient));
        _featureStore = featureStore ?? throw new ArgumentNullException(nameof(featureStore));
        _changeJournal = changeJournal ?? throw new ArgumentNullException(nameof(changeJournal));
        _checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
        _stateStore = stateStore;
        _conflictStore = conflictStore;
        _options = options ?? new OfflineSyncEngineOptions();
        _options.Validate();
    }

    /// <inheritdoc />
    public async Task<OfflineSyncRunResult> SyncAsync(OfflinePackageManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.PackageId);

        try
        {
            await SaveStateAsync(manifest.PackageId, null, OfflineSyncPhase.Pushing, null, cancellationToken).ConfigureAwait(false);
            var push = await PushAsync(manifest.PackageId, cancellationToken).ConfigureAwait(false);

            await SaveStateAsync(manifest.PackageId, null, OfflineSyncPhase.Pulling, null, cancellationToken).ConfigureAwait(false);
            var pull = await PullAsync(manifest, cancellationToken).ConfigureAwait(false);

            var result = new OfflineSyncRunResult
            {
                PackageId = manifest.PackageId,
                Push = push,
                Pull = pull,
            };

            await SaveStateAsync(
                manifest.PackageId,
                null,
                result.Succeeded ? OfflineSyncPhase.Completed : OfflineSyncPhase.Failed,
                result.Succeeded ? null : "sync completed with failures",
                cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            await SaveStateAsync(manifest.PackageId, null, OfflineSyncPhase.Failed, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (TimeoutException ex)
        {
            await SaveStateAsync(manifest.PackageId, null, OfflineSyncPhase.Failed, ex.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Pulls remote feature pages into the local feature store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a <strong>full refresh</strong>: every call re-queries each source
    /// from the beginning and re-stores all matching features. It is
    /// <em>not</em> an incremental/delta pull. The provider-neutral feature query
    /// API (<see cref="Honua.Sdk.Abstractions.Features.FeatureQueryResult"/>)
    /// exposes no server high-water-mark / sync token, so there is nothing to
    /// advance between runs; the persisted checkpoint therefore tracks only the
    /// number of features pulled and does not record an advancing
    /// <see cref="OfflineSyncCheckpoint.SyncToken"/>.
    /// </para>
    /// <para>
    /// Callers that need server-driven delta sync (a <c>serverGen</c> high-water
    /// mark) should use <see cref="ReplicaSyncClient"/> instead, which threads the
    /// server generation through extract-changes requests.
    /// </para>
    /// </remarks>
    /// <param name="manifest">Offline package manifest.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Pull result.</returns>
    public async Task<OfflinePullResult> PullAsync(OfflinePackageManifest manifest, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifest.PackageId);

        var failures = new List<OfflineSyncFailure>();
        var storedPageCount = 0;
        var storedFeatureCount = 0;

        foreach (var source in manifest.Sources)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(source.SourceId);
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await SaveStateAsync(manifest.PackageId, source.SourceId, OfflineSyncPhase.Pulling, null, cancellationToken).ConfigureAwait(false);
                var checkpoint = await _checkpointStore.GetCheckpointAsync(manifest.PackageId, source.SourceId, cancellationToken).ConfigureAwait(false);
                var request = OfflineDownloadPlanner.CreateRequest(manifest, source, checkpoint);
                var remaining = request.MaxFeatureCount;
                var sourceFeatureCount = 0;

                await foreach (var page in _queryClient.QueryPagesAsync(request.Query, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var pageToStore = LimitPage(page, remaining);
                    await _featureStore.SaveFeaturesAsync(new OfflineFeaturePage
                    {
                        PackageId = manifest.PackageId,
                        SourceId = source.SourceId,
                        Source = source.Source,
                        Result = pageToStore,
                        SyncToken = request.LastSyncToken,
                    }, cancellationToken).ConfigureAwait(false);

                    storedPageCount++;
                    storedFeatureCount += pageToStore.NumberReturned;
                    sourceFeatureCount += pageToStore.NumberReturned;

                    if (remaining is null)
                    {
                        continue;
                    }

                    remaining -= pageToStore.NumberReturned;
                    if (remaining <= 0)
                    {
                        break;
                    }
                }

                await _checkpointStore.SaveCheckpointAsync(new OfflineSyncCheckpoint
                {
                    PackageId = manifest.PackageId,
                    SourceId = source.SourceId,
                    // Full-refresh pull: the query API surfaces no server-advanced
                    // sync token, so there is no high-water mark to persist. Leave
                    // SyncToken null rather than re-storing the (unchanged) request
                    // token, which would falsely imply incremental progress.
                    SyncToken = null,
                    PulledFeatureCount = sourceFeatureCount,
                }, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                failures.Add(new OfflineSyncFailure
                {
                    PackageId = manifest.PackageId,
                    SourceId = source.SourceId,
                    Retryable = true,
                    Reason = ex.Message,
                });
            }
            catch (TimeoutException ex)
            {
                failures.Add(new OfflineSyncFailure
                {
                    PackageId = manifest.PackageId,
                    SourceId = source.SourceId,
                    Retryable = true,
                    Reason = ex.Message,
                });
            }
        }

        return new OfflinePullResult
        {
            PackageId = manifest.PackageId,
            PlannedSourceCount = manifest.Sources.Count,
            StoredPageCount = storedPageCount,
            StoredFeatureCount = storedFeatureCount,
            Failures = failures,
        };
    }

    /// <summary>
    /// Pushes pending local edits to the provider.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pending operations are uploaded with <b>at-least-once</b> delivery: a retryable
    /// failure (transport error or timeout) re-uploads the same operation on the next
    /// push. For <see cref="OfflineEditOperationKind.Add"/> operations this means a
    /// response lost <i>after</i> the server has already committed the insert results in
    /// the feature being re-added, creating a duplicate, because the request carries no
    /// server-honored idempotency key. Callers that require exactly-once add semantics
    /// must reconcile duplicates out of band (e.g. by querying a stable client-assigned
    /// GlobalId after sync). Durable idempotency requires honua-server to treat a
    /// client-supplied operation key as a no-op on replay; see the SDK release notes.
    /// </para>
    /// </remarks>
    /// <param name="packageId">Offline package identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Push result.</returns>
    public async Task<OfflinePushResult> PushAsync(string packageId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);

        var pending = await _changeJournal.GetPendingAsync(packageId, _options.BatchSize, cancellationToken).ConfigureAwait(false);
        var failures = new List<OfflineSyncFailure>();
        var succeeded = 0;
        var conflicts = 0;
        var retryableFailures = 0;
        var fatalFailures = 0;

        for (var index = 0; index < pending.Count; index++)
        {
            var operation = pending[index];
            cancellationToken.ThrowIfCancellationRequested();

            if (operation.AttemptCount >= _options.MaxAttempts)
            {
                await _changeJournal.MarkFailedAsync(operation.OperationId, MaxAttemptsReason, cancellationToken).ConfigureAwait(false);
                failures.Add(ToFailure(operation, MaxAttemptsReason, retryable: false));
                fatalFailures++;
                continue;
            }

            try
            {
                var upload = await UploadOperationAsync(operation, forceWrite: false, cancellationToken).ConfigureAwait(false);
                var outcome = await ApplyUploadOutcomeAsync(operation, upload, cancellationToken).ConfigureAwait(false);
                succeeded += outcome.Succeeded;
                conflicts += outcome.Conflicts;
                retryableFailures += outcome.RetryableFailures;
                fatalFailures += outcome.FatalFailures;
                failures.AddRange(outcome.Failures);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await ReleasePendingOperationsAsync(pending, index).ConfigureAwait(false);
                throw;
            }
            catch (HttpRequestException ex)
            {
                await MarkRetryAsync(operation, ex.Message, cancellationToken).ConfigureAwait(false);
                failures.Add(ToFailure(operation, ex.Message, retryable: true));
                retryableFailures++;
            }
            catch (TimeoutException ex)
            {
                await MarkRetryAsync(operation, ex.Message, cancellationToken).ConfigureAwait(false);
                failures.Add(ToFailure(operation, ex.Message, retryable: true));
                retryableFailures++;
            }
        }

        return new OfflinePushResult
        {
            PackageId = packageId,
            Loaded = pending.Count,
            Succeeded = succeeded,
            Conflicts = conflicts,
            RetryableFailures = retryableFailures,
            FatalFailures = fatalFailures,
            Failures = failures,
        };
    }

    private async Task<UploadApplicationResult> ApplyUploadOutcomeAsync(
        OfflineChangeJournalEntry operation,
        UploadEvaluation upload,
        CancellationToken cancellationToken)
    {
        if (upload.Outcome == UploadEvaluationOutcome.Success)
        {
            await _changeJournal.MarkSucceededAsync(operation.OperationId, cancellationToken).ConfigureAwait(false);
            return UploadApplicationResult.Success;
        }

        if (upload.Outcome == UploadEvaluationOutcome.Conflict)
        {
            return await HandleConflictAsync(operation, upload, cancellationToken).ConfigureAwait(false);
        }

        if (upload.Outcome == UploadEvaluationOutcome.RetryableFailure)
        {
            var reason = upload.Reason ?? "retryable upload failure";
            await MarkRetryAsync(operation, reason, cancellationToken).ConfigureAwait(false);
            return UploadApplicationResult.RetryableFailure(ToFailure(operation, reason, retryable: true));
        }

        var fatalReason = upload.Reason ?? "fatal upload failure";
        await _changeJournal.MarkFailedAsync(operation.OperationId, fatalReason, cancellationToken).ConfigureAwait(false);
        return UploadApplicationResult.FatalFailure(ToFailure(operation, fatalReason, retryable: false));
    }

    private async Task<UploadApplicationResult> HandleConflictAsync(
        OfflineChangeJournalEntry operation,
        UploadEvaluation upload,
        CancellationToken cancellationToken)
    {
        if (_options.ConflictStrategy == OfflineConflictStrategy.ServerWins)
        {
            await _changeJournal.MarkSucceededAsync(operation.OperationId, cancellationToken).ConfigureAwait(false);
            return UploadApplicationResult.Success;
        }

        if (_options.ConflictStrategy == OfflineConflictStrategy.ClientWins)
        {
            var forced = await UploadOperationAsync(operation, forceWrite: true, cancellationToken).ConfigureAwait(false);
            if (forced.Outcome != UploadEvaluationOutcome.Conflict)
            {
                return await ApplyUploadOutcomeAsync(operation, forced, cancellationToken).ConfigureAwait(false);
            }
        }

        var reason = upload.Reason ?? "conflict requires review";
        var conflict = new OfflineConflictEnvelope
        {
            OperationId = operation.OperationId,
            PackageId = operation.PackageId,
            SourceId = operation.SourceId,
            LocalOperation = operation,
            Error = upload.Error,
            Reason = reason,
        };

        await _changeJournal.MarkConflictAsync(conflict, cancellationToken).ConfigureAwait(false);
        if (_conflictStore is not null)
        {
            await _conflictStore.SaveConflictAsync(conflict, cancellationToken).ConfigureAwait(false);
        }

        return UploadApplicationResult.Conflict(ToFailure(operation, reason, retryable: false));
    }

    private async Task<UploadEvaluation> UploadOperationAsync(
        OfflineChangeJournalEntry operation,
        bool forceWrite,
        CancellationToken cancellationToken)
    {
        var requestResult = TryCreateEditRequest(operation, forceWrite);
        if (requestResult.FailureReason is not null)
        {
            return UploadEvaluation.Fatal(requestResult.FailureReason);
        }

        var response = await _editClient.ApplyEditsAsync(requestResult.Request, cancellationToken).ConfigureAwait(false);
        if (response.Succeeded)
        {
            return UploadEvaluation.Success;
        }

        var error = FindFirstError(response);
        var reason = error?.Message ?? response.Error?.Message ?? "provider rejected edit batch";
        if (IsConflict(error))
        {
            return UploadEvaluation.Conflict(reason, error);
        }

        if (IsRetryable(error))
        {
            return UploadEvaluation.Retryable(reason, error);
        }

        return UploadEvaluation.Fatal(reason, error);
    }

    private static EditRequestBuildResult TryCreateEditRequest(OfflineChangeJournalEntry operation, bool forceWrite)
    {
        return operation.OperationKind switch
        {
            OfflineEditOperationKind.Add when operation.Feature is not null => new EditRequestBuildResult(new FeatureEditRequest
            {
                Source = operation.Source,
                Adds = [operation.Feature],
                RollbackOnFailure = false,
                ForceWrite = forceWrite,
            }),
            OfflineEditOperationKind.Update when operation.Feature is not null => new EditRequestBuildResult(new FeatureEditRequest
            {
                Source = operation.Source,
                Updates = [operation.Feature],
                RollbackOnFailure = false,
                ForceWrite = forceWrite,
            }),
            OfflineEditOperationKind.Delete when operation.DeleteIds.Count > 0 || operation.DeleteObjectIds.Count > 0 =>
                new EditRequestBuildResult(new FeatureEditRequest
                {
                    Source = operation.Source,
                    DeleteIds = operation.DeleteIds,
                    DeleteObjectIds = operation.DeleteObjectIds,
                    RollbackOnFailure = false,
                    ForceWrite = forceWrite,
                }),
            OfflineEditOperationKind.Add => new EditRequestBuildResult("add operation requires a feature payload"),
            OfflineEditOperationKind.Update => new EditRequestBuildResult("update operation requires a feature payload"),
            OfflineEditOperationKind.Delete => new EditRequestBuildResult("delete operation requires feature identifiers"),
            _ => new EditRequestBuildResult("unsupported offline operation kind"),
        };
    }

    private async Task MarkRetryAsync(OfflineChangeJournalEntry operation, string reason, CancellationToken cancellationToken)
    {
        await _changeJournal.MarkRetryAsync(new OfflineRetryCheckpoint
        {
            OperationId = operation.OperationId,
            PackageId = operation.PackageId,
            SourceId = operation.SourceId,
            AttemptCount = operation.AttemptCount + 1,
            RetryAfterUtc = DateTimeOffset.UtcNow + _options.RetryDelay,
            Reason = reason,
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveStateAsync(
        string packageId,
        string? sourceId,
        OfflineSyncPhase phase,
        string? error,
        CancellationToken cancellationToken)
    {
        if (_stateStore is null)
        {
            return;
        }

        await _stateStore.SaveStateAsync(new OfflineSyncState
        {
            PackageId = packageId,
            SourceId = sourceId,
            Phase = phase,
            LastStartedAtUtc = DateTimeOffset.UtcNow,
            LastSucceededAtUtc = phase == OfflineSyncPhase.Completed ? DateTimeOffset.UtcNow : null,
            LastError = error,
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReleasePendingOperationsAsync(IReadOnlyList<OfflineChangeJournalEntry> pending, int startIndex)
    {
        for (var i = startIndex; i < pending.Count; i++)
        {
            await _changeJournal.MarkPendingAsync(pending[i].OperationId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static FeatureQueryResult LimitPage(FeatureQueryResult page, int? remaining)
    {
        if (remaining is null || page.Features.Count <= remaining.Value)
        {
            return page;
        }

        var limitedFeatures = page.Features.Take(remaining.Value).ToArray();
        return page with
        {
            Features = limitedFeatures,
            NumberReturned = limitedFeatures.Length,
            HasMoreResults = true,
        };
    }

    private static FeatureEditError? FindFirstError(FeatureEditResponse response)
        => response.Error ??
           response.AddResults.FirstOrDefault(result => result.Error is not null)?.Error ??
           response.UpdateResults.FirstOrDefault(result => result.Error is not null)?.Error ??
           response.DeleteResults.FirstOrDefault(result => result.Error is not null)?.Error;

    private static bool IsConflict(FeatureEditError? error)
        => error?.Code == 409 ||
           (error?.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool IsRetryable(FeatureEditError? error)
        => error?.Code is 408 or 429 or 500 or 502 or 503 or 504;

    private static OfflineSyncFailure ToFailure(OfflineChangeJournalEntry operation, string reason, bool retryable)
        => new()
        {
            OperationId = operation.OperationId,
            PackageId = operation.PackageId,
            SourceId = operation.SourceId,
            Retryable = retryable,
            Reason = reason,
        };

    private sealed record EditRequestBuildResult
    {
        public EditRequestBuildResult(FeatureEditRequest request)
        {
            Request = request;
        }

        public EditRequestBuildResult(string failureReason)
        {
            FailureReason = failureReason;
            Request = new FeatureEditRequest();
        }

        public FeatureEditRequest Request { get; }

        public string? FailureReason { get; }
    }

    private sealed record UploadEvaluation(UploadEvaluationOutcome Outcome, string? Reason, FeatureEditError? Error)
    {
        public static readonly UploadEvaluation Success = new(UploadEvaluationOutcome.Success, null, null);

        public static UploadEvaluation Conflict(string reason, FeatureEditError? error) =>
            new(UploadEvaluationOutcome.Conflict, reason, error);

        public static UploadEvaluation Retryable(string reason, FeatureEditError? error) =>
            new(UploadEvaluationOutcome.RetryableFailure, reason, error);

        public static UploadEvaluation Fatal(string reason, FeatureEditError? error = null) =>
            new(UploadEvaluationOutcome.FatalFailure, reason, error);
    }

    private enum UploadEvaluationOutcome
    {
        Success,
        Conflict,
        RetryableFailure,
        FatalFailure,
    }

    private sealed record UploadApplicationResult(
        int Succeeded,
        int Conflicts,
        int RetryableFailures,
        int FatalFailures,
        IReadOnlyList<OfflineSyncFailure> Failures)
    {
        public static readonly UploadApplicationResult Success = new(1, 0, 0, 0, []);

        public static UploadApplicationResult Conflict(OfflineSyncFailure failure) =>
            new(0, 1, 0, 0, [failure]);

        public static UploadApplicationResult RetryableFailure(OfflineSyncFailure failure) =>
            new(0, 0, 1, 0, [failure]);

        public static UploadApplicationResult FatalFailure(OfflineSyncFailure failure) =>
            new(0, 0, 0, 1, [failure]);
    }
}
