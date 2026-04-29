// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Offline.Abstractions;

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
        Assert.Equal("token-1", checkpoint.SyncToken);
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

    private static OfflineSyncEngine CreateEngine(
        FakeQueryClient queryClient,
        FakeEditClient editClient,
        FakeOfflineStore store,
        FakeChangeJournal journal,
        FakeConflictStore? conflictStore = null,
        OfflineSyncEngineOptions? options = null,
        IOfflineSyncStateStore? stateStore = null)
        => new(
            queryClient,
            editClient,
            store,
            journal,
            store,
            options,
            stateStore,
            conflictStore);

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

        public Task<FeatureQueryResult> QueryAsync(FeatureQueryRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(Pages.Peek());
        }

        public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
            FeatureQueryRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            Requests.Add(request);
            while (Pages.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                yield return Pages.Dequeue();
                await Task.Yield();
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

        public Task<FeatureEditResponse> ApplyEditsAsync(FeatureEditRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(Responses.Dequeue());
        }
    }

    private sealed class FakeOfflineStore : IOfflineFeatureStore, IOfflineSyncCheckpointStore, IOfflineSyncStateStore
    {
        public List<OfflineFeaturePage> Pages { get; } = [];

        public Dictionary<string, OfflineSyncCheckpoint> Checkpoints { get; } = [];

        public List<OfflineSyncState> States { get; } = [];

        public Task SaveFeaturesAsync(OfflineFeaturePage page, CancellationToken ct = default)
        {
            Pages.Add(page);
            return Task.CompletedTask;
        }

        public Task DeleteFeaturesAsync(
            string packageId,
            string sourceId,
            IReadOnlyList<string> featureIds,
            IReadOnlyList<long> objectIds,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<OfflineSyncCheckpoint?> GetCheckpointAsync(
            string packageId,
            string sourceId,
            CancellationToken ct = default)
        {
            Checkpoints.TryGetValue(sourceId, out var checkpoint);
            return Task.FromResult<OfflineSyncCheckpoint?>(checkpoint);
        }

        public Task SaveCheckpointAsync(OfflineSyncCheckpoint checkpoint, CancellationToken ct = default)
        {
            Checkpoints[checkpoint.SourceId] = checkpoint;
            return Task.CompletedTask;
        }

        public Task<OfflineSyncState?> GetStateAsync(
            string packageId,
            string? sourceId = null,
            CancellationToken ct = default)
            => Task.FromResult<OfflineSyncState?>(States.LastOrDefault(state => state.PackageId == packageId && state.SourceId == sourceId));

        public Task SaveStateAsync(OfflineSyncState state, CancellationToken ct = default)
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

        public Task EnqueueAsync(OfflineChangeJournalEntry entry, CancellationToken ct = default)
        {
            _pending.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OfflineChangeJournalEntry>> GetPendingAsync(
            string packageId,
            int maxCount,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OfflineChangeJournalEntry>>(
                _pending.Where(entry => entry.PackageId == packageId).Take(maxCount).ToArray());

        public Task MarkSucceededAsync(string operationId, CancellationToken ct = default)
        {
            Succeeded.Add(operationId);
            return Task.CompletedTask;
        }

        public Task MarkPendingAsync(string operationId, CancellationToken ct = default)
        {
            PendingMarks.Add(operationId);
            return Task.CompletedTask;
        }

        public Task MarkRetryAsync(OfflineRetryCheckpoint checkpoint, CancellationToken ct = default)
        {
            RetryCheckpoints.Add(checkpoint);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(string operationId, string reason, CancellationToken ct = default)
        {
            Failed[operationId] = reason;
            return Task.CompletedTask;
        }

        public Task MarkConflictAsync(OfflineConflictEnvelope conflict, CancellationToken ct = default)
        {
            Conflicts.Add(conflict);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeConflictStore : IOfflineConflictStore
    {
        public List<OfflineConflictEnvelope> Conflicts { get; } = [];

        public Task SaveConflictAsync(OfflineConflictEnvelope conflict, CancellationToken ct = default)
        {
            Conflicts.Add(conflict);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OfflineConflictEnvelope>> ListConflictsAsync(string packageId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<OfflineConflictEnvelope>>(
                Conflicts.Where(conflict => conflict.PackageId == packageId).ToArray());

        public Task ResolveConflictAsync(string operationId, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
