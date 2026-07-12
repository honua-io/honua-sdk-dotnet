# Honua.Sdk.Offline

Provider-neutral offline sync engine. Pulls feature pages from any
`IHonuaFeatureQueryClient`, applies queued edits through `IHonuaFeatureEditClient`,
and tracks conflicts, checkpoints, retries, and state through the storage
contracts in the `Honua.Sdk.Offline.Abstractions` namespace, shipped by
[`Honua.Sdk.Abstractions`](https://www.nuget.org/packages/Honua.Sdk.Abstractions).

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

```bash
dotnet add package Honua.Sdk.Offline
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

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Offline sync core](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/offline-sync-core.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
