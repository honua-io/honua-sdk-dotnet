# Honua.Sdk.Offline

Provider-neutral offline sync engine. Pulls feature pages from any
`IHonuaFeatureQueryClient`, applies queued edits through `IHonuaFeatureEditClient`,
and tracks conflicts, checkpoints, retries, and state through the storage
contracts in the `Honua.Sdk.Offline.Abstractions` namespace, shipped by
[`Honua.Sdk.Abstractions`](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/src/Honua.Sdk.Abstractions/README.md).

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

Honua SDK packages are currently published to the authenticated GitHub Packages
feed only — nuget.org publishing is planned but not yet available. One-time
setup: configure the feed with a GitHub **classic** PAT that has the
`read:packages` scope, then install with `--source honua`. Full setup (CI,
package source mapping): [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

```bash
dotnet nuget add source https://nuget.pkg.github.com/honua-io/index.json \
  --name honua --username YOUR_GITHUB_USERNAME --password YOUR_CLASSIC_PAT \
  --store-password-in-clear-text
dotnet add package Honua.Sdk.Offline --source honua
```

## Quick usage

```csharp
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Offline;
using Honua.Sdk.Offline.Abstractions;

// Bring your own provider-neutral query/edit clients (FeatureServer, gRPC, OGC, ...)
// plus the storage adapters from your host (SQLite, IndexedDB, in-memory, ...).
IHonuaFeatureQueryClient queryClient = /* from your provider package */;
IHonuaFeatureEditClient editClient = /* from your provider package */;
IOfflineFeatureStore featureStore = /* host-provided */;
IOfflineChangeJournal changeJournal = /* host-provided */;
IOfflineSyncCheckpointStore checkpoints = /* host-provided */;

var engine = new OfflineSyncEngine(
    queryClient,
    editClient,
    featureStore,
    changeJournal,
    checkpoints,
    new OfflineSyncEngineOptions());

var manifest = new OfflinePackageManifest
{
    PackageId = "field-pkg-1",
    Sources = [/* OfflineSourceDescriptor entries */],
};

OfflinePushResult pushed = await engine.PushAsync(manifest.PackageId, cancellationToken);
OfflinePullResult pulled = await engine.PullAsync(manifest, cancellationToken);
OfflineSyncRunResult run = await engine.SyncAsync(manifest, cancellationToken);
```

## Replica delta downloads

Use `ReplicaSyncClient` for server-driven replica changes. A synchronization
downloads changes and advances the server's delivery cursor. The returned
`ServerGen` describes that delivery; it does not mean the changes were applied
to your local store.

Pass the generation already applied locally as `receivedServerGen`. Apply all
`LayerChanges` (including `DeleteIds` tombstones) and persist the returned
generation together with those changes in your local transaction. If local
application fails, retry with the preceding applied generation so the server
can redeliver that window even if its stored cursor has advanced.

```csharp
// Begin with the generation of the replica snapshot stored locally.
long appliedGeneration = /* load the local replica checkpoint */;
SynchronizeResult delivery;
do
{
    delivery = await replicaClient.SynchronizeReplicaAsync(
        serviceId, replicaId, receivedServerGen: appliedGeneration,
        cancellationToken: cancellationToken);

    // In one local transaction: apply adds/updates/deletes from LayerChanges,
    // then save delivery.ServerGen as the checkpoint. Commit before continuing.
    await ApplyAndCheckpointAsync(delivery.LayerChanges, delivery.ServerGen, cancellationToken);
    appliedGeneration = delivery.ServerGen;
} while (delivery.ExceededTransferLimit);
```

`ExceededTransferLimit` means another delivery remains. `LayerServerGens`
retains the generations delivered for individual layers. `ExtractChangesAsync`
also exposes this metadata; after applying an extract, pass its delivered
generation to synchronization and apply that synchronization's changes too,
since remote edits may arrive between the two requests.

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Offline sync core](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/offline-sync-core.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
