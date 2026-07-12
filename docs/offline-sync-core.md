# Offline sync core

`Honua.Sdk.Abstractions` (through its `Honua.Sdk.Offline.Abstractions`
namespace) and `Honua.Sdk.Offline` provide the reusable, platform-neutral
offline sync surface for .NET consumers.

The SDK owns:

- Offline package manifests and source descriptors.
- Sync state, source checkpoints, retry checkpoints, and conflict envelopes.
- Local feature store, change journal, checkpoint store, conflict store, and
  sync runner interfaces.
- Bounded pull planning by source, extent, where clause, output fields, page
  size, maximum feature count, and last sync token.
- Push, pull, replay, checkpoint, conflict reporting, cancellation, and
  retryable failure handling over `IHonuaFeatureQueryClient` and
  `IHonuaFeatureEditClient`.

The SDK does not own rendering, map display, MAUI registration, OS background
execution, device permissions, camera or location acquisition, native file
placement, SQLite, or GeoPackage implementation details. Mobile and desktop
hosts provide adapters for the storage interfaces and decide when to schedule
sync runs.

## Package layout

Use the `Honua.Sdk.Offline.Abstractions` namespace from the
`Honua.Sdk.Abstractions` package anywhere contracts must cross an application
boundary, including browser-safe consumers:

```csharp
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Offline.Abstractions;

var manifest = new OfflinePackageManifest
{
    PackageId = "oahu-parks",
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
            OutFields = ["name", "status"],
            PageSize = 500,
            MaxFeatureCount = 10_000,
            LastSyncToken = "server-token",
        },
    ],
};
```

Use `Honua.Sdk.Offline` when a process can run the orchestration engine:

```csharp
using Honua.Sdk.Offline;

var engine = new OfflineSyncEngine(
    queryClient,
    editClient,
    featureStore,
    changeJournal,
    checkpointStore,
    new OfflineSyncEngineOptions
    {
        BatchSize = 50,
        MaxAttempts = 8,
        ConflictStrategy = OfflineConflictStrategy.ManualReview,
    },
    stateStore,
    conflictStore);

var result = await engine.SyncAsync(manifest, cancellationToken);
```

`SyncAsync` pushes pending local edits first, then pulls remote feature pages.
Hosts that need finer control can call `PushAsync(packageId)` and
`PullAsync(manifest)` separately.

## Mobile adapter expectations

The mobile app should map its native store onto the SDK interfaces:

- GeoPackage or SQLite feature cache: `IOfflineFeatureStore`
- Local edit queue: `IOfflineChangeJournal`
- Replica/server generation cursors: `IOfflineSyncCheckpointStore`
- Sync progress UI or diagnostics: `IOfflineSyncStateStore`
- Manual review queue: `IOfflineConflictStore`

The mobile app remains responsible for registering those adapters in MAUI,
placing native files, acquiring permissions, checking connectivity, and invoking
the sync runner from foreground or background workflows.
