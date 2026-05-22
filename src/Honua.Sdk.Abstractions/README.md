# Honua.Sdk.Abstractions

Shared, provider-neutral contracts used across the Honua .NET SDK. Defines the
core feature interfaces (`IHonuaFeatureQueryClient`, `IHonuaFeatureEditClient`,
`IHonuaFeatureAttachmentClient`, `IHonuaFeatureDescriptorClient`,
`IHonuaSource`, `SourceDescriptor`), feature query/edit request and result
models, feature stream-event processors, the provider-neutral routing client
(`IHonuaRoutingClient`) and routing models, scene client and scene-package
contracts, utility-network trace contracts, plugin contracts, and
`HonuaException`.

Also ships the **offline sync contracts** under the
`Honua.Sdk.Offline.Abstractions.*` namespaces: `OfflineReplicaManifest`,
`OfflinePackageManifest`, `OfflineSyncState`, `OfflineSyncCheckpoint`,
`OfflineChangeJournalEntry`, conflict envelopes, push/pull result records, the
`IOfflineFeatureStore` / `IOfflineChangeJournal` / `IOfflineConflictStore` /
`IOfflineSyncCheckpointStore` / `IOfflineSyncStateStore` / `IOfflineSyncRunner`
storage interfaces, and `IReplicaSyncClient`.

This package does not ship a server client. Take a dependency on it from a
library to write code that targets multiple Honua protocols (FeatureServer, gRPC,
WFS, OGC API Features, etc.) through a single set of interfaces; concrete
implementations live in the protocol-specific packages.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

```bash
dotnet add package Honua.Sdk.Abstractions
```

In a library `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Honua.Sdk.Abstractions" Version="*-*" />
</ItemGroup>
```

## Quick usage

```csharp
using Honua.Sdk.Abstractions.Features;

public sealed class FeatureExporter(IHonuaFeatureQueryClient client)
{
    public async Task<int> CountAsync(SourceDescriptor source, CancellationToken cancellationToken)
    {
        var request = new FeatureQueryRequest
        {
            Source = source,
            Where = "1=1",
            ReturnGeometry = false,
        };

        var total = 0;
        await foreach (var page in client.QueryPagesAsync(request, cancellationToken))
        {
            total += page.Features.Count;
        }
        return total;
    }
}
```

A concrete provider package (for example `Honua.Sdk.GeoServices` or
`Honua.Sdk.Grpc`) registers an `IHonuaFeatureQueryClient` implementation in DI;
your library code stays provider-neutral.

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Source facade](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/source-facade.md)
- [Plugin contracts](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/plugin-contracts.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
