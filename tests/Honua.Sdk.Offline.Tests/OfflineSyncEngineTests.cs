// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Offline.Abstractions;
using Microsoft.Extensions.Logging;

namespace Honua.Sdk.Offline.Tests;

public sealed class OfflineSyncEngineTests
{
    [Fact]
    public async Task PullAsync_PlansBoundedQueryAndStoresPages()
    {
        var queryClient = new FakeQueryClient();
        queryClient.Pages.Enqueue(new FeatureQueryResult
        {
            ProviderName = "fake",
            Features = [CreateFeature("1"), CreateFeature("2")],
            NumberReturned = 2,
        });

        var store = new FakeOfflineStore();
        var engine = CreateEngine(queryClient, new FakeEditClient(), store, new FakeChangeJournal());

        var result = await engine.PullAsync(CreateManifest());

        Assert.Equal("field-area-1", result.PackageId);
        Assert.Equal(1, result.PlannedSourceCount);
        Assert.Equal(1, result.StoredPageCount);
        Assert.Equal(2, result.StoredFeatureCount);
        Assert.Empty(result.Failures);

        var request = Assert.Single(queryClient.Requests);
        Assert.Equal("status = 'open'", request.Filter);
        Assert.Equal(FeatureFilterLanguage.Cql2Text, request.FilterLanguage);
        Assert.Equal(100, request.Limit);
        Assert.True(request.ReturnGeometry);
        Assert.NotNull(request.OutFields);
        Assert.Equal(["name", "status"], request.OutFields);
        Assert.Equal("parks", request.Source.CollectionId);
        Assert.Equal(-158.5, request.Bbox?.MinX);

        var storedPage = Assert.Single(store.Pages);
        Assert.Equal("parks", storedPage.SourceId);
        Assert.Equal("token-1", storedPage.SyncToken);

        var checkpoint = Assert.Single(store.Checkpoints.Values);
        // Full-refresh pull: no server high-water mark exists to advance, so the
        // checkpoint must not persist a (misleading) static sync token.
        Assert.Null(checkpoint.SyncToken);
        Assert.Equal(2, checkpoint.PulledFeatureCount);
    }

    [Fact]
    public async Task PushAsync_AppliesPendingAddsAndMarksSucceeded()
    {
        var editClient = new FakeEditClient();
        editClient.Responses.Enqueue(new FeatureEditResponse
        {
            ProviderName = "fake",
            AddResults = [new FeatureEditResult { Succeeded = true, ObjectId = 42 }],
        });

        var journal = new FakeChangeJournal([CreateOperation(OfflineEditOperationKind.Add)]);
        var engine = CreateEngine(new FakeQueryClient(), editClient, new FakeOfflineStore(), journal);

        var result = await engine.PushAsync("field-area-1");

        Assert.Equal(1, result.Loaded);
        Assert.Equal(1, result.Succeeded);
        Assert.Empty(result.Failures);
        Assert.Equal(["op-1"], journal.Succeeded);

        var request = Assert.Single(editClient.Requests);
        Assert.False(request.ForceWrite);
        Assert.False(request.RollbackOnFailure);
        Assert.Single(request.Adds);
        Assert.Equal("parks", request.Source.CollectionId);
    }

    [Fact]
    public async Task PushAsync_ManualReviewConflictStoresConflictEnvelope()
    {
        var editClient = new FakeEditClient();
        editClient.Responses.Enqueue(new FeatureEditResponse
        {
            ProviderName = "fake",
            UpdateResults =
            [
                new FeatureEditResult
                {
                    Succeeded = false,
                    Error = new FeatureEditError { Code = 409, Message = "edit conflict" },
                },
            ],
        });

        var journal = new FakeChangeJournal([CreateOperation(OfflineEditOperationKind.Update)]);
        var conflictStore = new FakeConflictStore();
        var engine = CreateEngine(
            new FakeQueryClient(),
            editClient,
            new FakeOfflineStore(),
            journal,
            conflictStore,
            new OfflineSyncEngineOptions { ConflictStrategy = OfflineConflictStrategy.ManualReview });

        var result = await engine.PushAsync("field-area-1");

        Assert.Equal(1, result.Conflicts);
        Assert.Single(result.Failures);
        var conflict = Assert.Single(journal.Conflicts);
        Assert.Equal("op-1", conflict.OperationId);
        Assert.Equal("edit conflict", conflict.Reason);
        Assert.Single(conflictStore.Conflicts);
    }

    [Fact]
    public async Task PushAsync_ClientWinsRetriesConflictWithForceWrite()
    {
        var editClient = new FakeEditClient();
        editClient.Responses.Enqueue(new FeatureEditResponse
        {
            ProviderName = "fake",
            UpdateResults =
            [
                new FeatureEditResult
                {
                    Succeeded = false,
                    Error = new FeatureEditError { Code = 409, Message = "edit conflict" },
                },
            ],
        });
        editClient.Responses.Enqueue(new FeatureEditResponse
        {
            ProviderName = "fake",
            UpdateResults = [new FeatureEditResult { Succeeded = true, ObjectId = 42 }],
        });

        var journal = new FakeChangeJournal([CreateOperation(OfflineEditOperationKind.Update)]);
        var engine = CreateEngine(
            new FakeQueryClient(),
            editClient,
            new FakeOfflineStore(),
            journal,
            options: new OfflineSyncEngineOptions { ConflictStrategy = OfflineConflictStrategy.ClientWins });

        var result = await engine.PushAsync("field-area-1");

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(0, result.Conflicts);
        Assert.Empty(result.Failures);
        Assert.Equal(2, editClient.Requests.Count);
        Assert.False(editClient.Requests[0].ForceWrite);
        Assert.True(editClient.Requests[1].ForceWrite);
    }

    [Fact]
    public async Task SyncAsync_PushesPendingEditsThenPullsFeatures()
    {
        var queryClient = new FakeQueryClient();
        queryClient.Pages.Enqueue(new FeatureQueryResult
        {
            ProviderName = "fake",
            Features = [CreateFeature("1")],
            NumberReturned = 1,
        });

        var editClient = new FakeEditClient();
        editClient.Responses.Enqueue(new FeatureEditResponse
        {
            ProviderName = "fake",
            AddResults = [new FeatureEditResult { Succeeded = true, ObjectId = 42 }],
        });

        var store = new FakeOfflineStore();
        var journal = new FakeChangeJournal([CreateOperation(OfflineEditOperationKind.Add)]);
        var engine = CreateEngine(queryClient, editClient, store, journal, stateStore: store);

        var result = await engine.SyncAsync(CreateManifest());

        Assert.True(result.Succeeded);
        Assert.Equal(1, result.Push.Succeeded);
        Assert.Equal(1, result.Pull.StoredFeatureCount);
        Assert.Collection(
            store.States,
            state => Assert.Equal(OfflineSyncPhase.Pushing, state.Phase),
            state => Assert.Equal(OfflineSyncPhase.Pulling, state.Phase),
            state => Assert.Equal(OfflineSyncPhase.Pulling, state.Phase),
            state => Assert.Equal(OfflineSyncPhase.Completed, state.Phase));
    }

    [Fact]
    public async Task PullAsync_AutoPaginationCap_RecordsNonRetryableFailureAndFailedState()
    {
        // The provider QueryPagesAsync throws InvalidOperationException once its
        // auto-pagination safety limit is hit. The pull must capture this as a
        // non-retryable per-source failure (and a terminal Failed state) instead of
        // letting it escape and abort every remaining source.
        var error = new InvalidOperationException(
            "Auto-pagination safety limit reached (100 pages).");
        var queryClient = new FakeQueryClient
        {
            PagesError = error,
        };
        var store = new FakeOfflineStore();
        var logger = new FakeLogger<OfflineSyncEngine>();
        var engine = CreateEngine(
            queryClient,
            new FakeEditClient(),
            store,
            new FakeChangeJournal(),
            stateStore: store,
            logger: logger);

        var result = await engine.PullAsync(CreateManifest());

        var failure = Assert.Single(result.Failures);
        Assert.Equal("parks", failure.SourceId);
        Assert.False(failure.Retryable);
        Assert.Contains("safety limit", failure.Reason, StringComparison.Ordinal);
        Assert.Contains("System.InvalidOperationException", failure.Reason, StringComparison.Ordinal);
        Assert.Contains(nameof(FakeQueryClient.QueryPagesAsync), failure.Reason, StringComparison.Ordinal);

        var failedState = Assert.Single(
            store.States,
            state => state.SourceId == "parks" && state.Phase == OfflineSyncPhase.Failed);
        Assert.Equal(failure.Reason, failedState.LastError);

        var logEntry = Assert.Single(logger.Entries, entry => entry.EventId.Id == 1013);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Same(error, logEntry.Exception);
        Assert.Contains("field-area-1", logEntry.Message, StringComparison.Ordinal);
        Assert.Contains("parks", logEntry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SyncAsync_AutoPaginationCap_EndsInTerminalFailedState()
    {
        // A pagination-cap failure during pull must not leave sync stuck at an
        // in-progress phase: the run completes with failures and a terminal Failed state.
        var queryClient = new FakeQueryClient
        {
            PagesError = new InvalidOperationException(
                "Auto-pagination safety limit reached (100 pages)."),
        };
        var store = new FakeOfflineStore();
        var engine = CreateEngine(
            queryClient,
            new FakeEditClient(),
            store,
            new FakeChangeJournal(),
            stateStore: store);

        var result = await engine.SyncAsync(CreateManifest());

        Assert.False(result.Succeeded);
        Assert.Equal(OfflineSyncPhase.Failed, store.States[^1].Phase);
    }

    [Fact]
    public async Task SyncAsync_UnhandledPushException_LogsAndStoresExceptionDiagnostics()
    {
        var error = new InvalidOperationException("edit service failed unexpectedly");
        var editClient = new FakeEditClient
        {
            ApplyError = error,
        };
        var store = new FakeOfflineStore();
        var logger = new FakeLogger<OfflineSyncEngine>();
        var engine = CreateEngine(
            new FakeQueryClient(),
            editClient,
            store,
            new FakeChangeJournal([CreateOperation(OfflineEditOperationKind.Add)]),
            stateStore: store,
            logger: logger);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SyncAsync(CreateManifest()));

        Assert.Same(error, thrown);
        var failedState = Assert.Single(
            store.States,
            state => state.SourceId is null && state.Phase == OfflineSyncPhase.Failed);
        Assert.Contains("System.InvalidOperationException", failedState.LastError, StringComparison.Ordinal);
        Assert.Contains(nameof(FakeEditClient.ApplyEditsAsync), failedState.LastError, StringComparison.Ordinal);

        var logEntry = Assert.Single(logger.Entries, entry => entry.EventId.Id == 1006);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Same(error, logEntry.Exception);
        Assert.Contains("field-area-1", logEntry.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(OfflineSyncPhase.Pushing), logEntry.Message, StringComparison.Ordinal);
    }

    private static OfflineSyncEngine CreateEngine(
        FakeQueryClient queryClient,
        FakeEditClient editClient,
        FakeOfflineStore store,
        FakeChangeJournal journal,
        FakeConflictStore? conflictStore = null,
        OfflineSyncEngineOptions? options = null,
        IOfflineSyncStateStore? stateStore = null,
        ILogger<OfflineSyncEngine>? logger = null)
        => new(
            queryClient,
            editClient,
            store,
            journal,
            store,
            options,
            stateStore,
            conflictStore,
            logger);

    private static OfflinePackageManifest CreateManifest()
        => new()
        {
            PackageId = "field-area-1",
            DisplayName = "Field Area 1",
            Sources =
            [
                new OfflineSourceDescriptor
                {
                    SourceId = "parks",
                    Source = new SourceDescriptor
                    {
                        Id = "parks",
                        Protocol = FeatureProtocolIds.OgcFeatures,
                        Locator = new SourceLocator { CollectionId = "parks" },
                    },
                    Where = "status = 'open'",
                    FilterLanguage = FeatureFilterLanguage.Cql2Text,
                    Extent = new FeatureBoundingBox
                    {
                        MinX = -158.5,
                        MinY = 21.1,
                        MaxX = -157.6,
                        MaxY = 21.8,
                        Crs = "EPSG:4326",
                    },
                    OutFields = ["name", "status"],
                    PageSize = 100,
                    MaxFeatureCount = 150,
                    LastSyncToken = "token-1",
                },
            ],
        };

    private static OfflineChangeJournalEntry CreateOperation(OfflineEditOperationKind kind)
        => new()
        {
            OperationId = "op-1",
            PackageId = "field-area-1",
            SourceId = "parks",
            Source = new FeatureSource { CollectionId = "parks" },
            OperationKind = kind,
            Feature = kind == OfflineEditOperationKind.Delete ? null : CreateEditFeature(),
            DeleteObjectIds = kind == OfflineEditOperationKind.Delete ? [42] : [],
        };

    private static FeatureEditFeature CreateEditFeature()
        => new()
        {
            ObjectId = 42,
            Attributes = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("Ala Moana"),
            },
        };

    private static FeatureRecord CreateFeature(string id)
        => new()
        {
            Id = id,
            Attributes = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement($"Park {id}"),
            },
        };

    private sealed class FakeQueryClient : IHonuaFeatureQueryClient
    {
        public string ProviderName => "fake";

        public Queue<FeatureQueryResult> Pages { get; } = new();

        public List<FeatureQueryRequest> Requests { get; } = [];

        /// <summary>
        /// When set, thrown after all queued pages are drained, simulating the provider's
        /// auto-pagination safety-limit <see cref="InvalidOperationException"/>.
        /// </summary>
        public Exception? PagesError { get; set; }

        public Task<FeatureQueryResult> QueryAsync(FeatureQueryRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Pages.Peek());
        }

        public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
            FeatureQueryRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            while (Pages.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return Pages.Dequeue();
                await Task.Yield();
            }

            if (PagesError is not null)
            {
                throw PagesError;
            }
        }
    }

    private sealed class FakeEditClient : IHonuaFeatureEditClient
    {
        public string ProviderName => "fake";

        public FeatureEditCapabilities EditCapabilities { get; } = new()
        {
            SupportsAdds = true,
            SupportsUpdates = true,
            SupportsDeletes = true,
            SupportsRollbackOnFailure = true,
        };

        public Queue<FeatureEditResponse> Responses { get; } = new();

        public List<FeatureEditRequest> Requests { get; } = [];

        public Exception? ApplyError { get; init; }

        public Task<FeatureEditResponse> ApplyEditsAsync(FeatureEditRequest request, CancellationToken cancellationToken = default)
        {
            if (ApplyError is not null)
            {
                throw ApplyError;
            }

            Requests.Add(request);
            return Task.FromResult(Responses.Dequeue());
        }
    }

    private sealed class FakeOfflineStore : IOfflineFeatureStore, IOfflineSyncCheckpointStore, IOfflineSyncStateStore
    {
        public List<OfflineFeaturePage> Pages { get; } = [];

        public Dictionary<string, OfflineSyncCheckpoint> Checkpoints { get; } = [];

        public List<OfflineSyncState> States { get; } = [];

        public Task SaveFeaturesAsync(OfflineFeaturePage page, CancellationToken cancellationToken = default)
        {
            Pages.Add(page);
            return Task.CompletedTask;
        }

        public Task DeleteFeaturesAsync(
            string packageId,
            string sourceId,
            IReadOnlyList<string> featureIds,
            IReadOnlyList<long> objectIds,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<OfflineSyncCheckpoint?> GetCheckpointAsync(
            string packageId,
            string sourceId,
            CancellationToken cancellationToken = default)
        {
            Checkpoints.TryGetValue(sourceId, out var checkpoint);
            return Task.FromResult<OfflineSyncCheckpoint?>(checkpoint);
        }

        public Task SaveCheckpointAsync(OfflineSyncCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            Checkpoints[checkpoint.SourceId] = checkpoint;
            return Task.CompletedTask;
        }

        public Task<OfflineSyncState?> GetStateAsync(
            string packageId,
            string? sourceId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<OfflineSyncState?>(States.LastOrDefault(state => state.PackageId == packageId && state.SourceId == sourceId));

        public Task SaveStateAsync(OfflineSyncState state, CancellationToken cancellationToken = default)
        {
            States.Add(state);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeChangeJournal : IOfflineChangeJournal
    {
        private readonly List<OfflineChangeJournalEntry> _pending;

        public FakeChangeJournal(IReadOnlyList<OfflineChangeJournalEntry>? pending = null)
        {
            _pending = pending?.ToList() ?? [];
        }

        public List<string> Succeeded { get; } = [];

        public List<string> PendingMarks { get; } = [];

        public List<OfflineRetryCheckpoint> RetryCheckpoints { get; } = [];

        public Dictionary<string, string> Failed { get; } = [];

        public List<OfflineConflictEnvelope> Conflicts { get; } = [];

        public Task EnqueueAsync(OfflineChangeJournalEntry entry, CancellationToken cancellationToken = default)
        {
            _pending.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OfflineChangeJournalEntry>> GetPendingAsync(
            string packageId,
            int maxCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OfflineChangeJournalEntry>>(
                _pending.Where(entry => entry.PackageId == packageId).Take(maxCount).ToArray());

        public Task MarkSucceededAsync(string operationId, CancellationToken cancellationToken = default)
        {
            Succeeded.Add(operationId);
            return Task.CompletedTask;
        }

        public Task MarkPendingAsync(string operationId, CancellationToken cancellationToken = default)
        {
            PendingMarks.Add(operationId);
            return Task.CompletedTask;
        }

        public Task MarkRetryAsync(OfflineRetryCheckpoint checkpoint, CancellationToken cancellationToken = default)
        {
            RetryCheckpoints.Add(checkpoint);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(string operationId, string reason, CancellationToken cancellationToken = default)
        {
            Failed[operationId] = reason;
            return Task.CompletedTask;
        }

        public Task MarkConflictAsync(OfflineConflictEnvelope conflict, CancellationToken cancellationToken = default)
        {
            Conflicts.Add(conflict);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConflictStore : IOfflineConflictStore
    {
        public List<OfflineConflictEnvelope> Conflicts { get; } = [];

        public Task SaveConflictAsync(OfflineConflictEnvelope conflict, CancellationToken cancellationToken = default)
        {
            Conflicts.Add(conflict);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OfflineConflictEnvelope>> ListConflictsAsync(string packageId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OfflineConflictEnvelope>>(
                Conflicts.Where(conflict => conflict.PackageId == packageId).ToArray());

        public Task ResolveConflictAsync(string operationId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeLogger<T> : ILogger<T>
    {
        public List<FakeLogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            Entries.Add(new FakeLogEntry(logLevel, eventId, formatter(state, exception), exception));
        }
    }

    private sealed record FakeLogEntry(LogLevel Level, EventId EventId, string Message, Exception? Exception);
}
