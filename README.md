# Honua .NET SDK

Official .NET client libraries for [Honua](https://github.com/honua-io/honua-server) --
an open-source geospatial feature server. The SDK provides typed clients for
querying and editing features over gRPC, querying via OGC WFS 2.0, managing
services through the Admin REST API, geocoding addresses, and reading features
through GeoServices FeatureServer, OGC API Features, and scene metadata
endpoints.

## Packages

| Package | Description |
|---------|-------------|
| **Honua.Sdk.Abstractions** | Shared feature query/edit abstractions implemented by provider-specific clients |
| **Honua.Sdk.Offline.Abstractions** | Browser-safe offline manifests, sync state, checkpoints, conflicts, and storage contracts |
| **Honua.Sdk.Offline** | Provider-neutral offline push/pull planner and sync engine over the shared feature abstractions |
| **Honua.Sdk.Grpc** | gRPC client for `FeatureService` -- typed queries, streaming, edits, spatial filters |
| **Honua.Sdk.Admin** | Admin REST client -- services, layers, connections, styles, metadata |
| **Honua.Sdk.Spec** | Spec workspace REST/SSE client -- validate, plan, apply stream, cancel |
| **Honua.Sdk.Wfs** | WFS 2.0 read/query client -- GetCapabilities, GetFeature (GeoJSON), DescribeFeatureType |
| **Honua.Sdk.GeoServices** | GeoServices FeatureServer read/query client -- service/layer metadata, query, count, IDs, extent, statistics |
| **Honua.Sdk.Scenes** | Scene metadata client -- list/detail/resolve scene endpoints plus offline scene package contracts |
| **Honua.Sdk.OgcFeatures** | OGC API Features read/query client -- landing page, conformance, collections, queryables, items |
| *Geocoding* (in Admin) | Forward/reverse geocoding and autocomplete via `IHonuaGeocodingClient` |

Browser and WebAssembly consumers should start with
[docs/browser-wasm-support.md](docs/browser-wasm-support.md). The browser-safe
surface is contracts plus REST clients over browser `HttpClient`; native gRPC,
local storage engines, background schedulers, and display renderers stay out of
SDK core.

## Install

```bash
dotnet add package Honua.Sdk.Abstractions --prerelease
dotnet add package Honua.Sdk.Offline.Abstractions --prerelease
dotnet add package Honua.Sdk.Offline --prerelease
dotnet add package Honua.Sdk.Grpc --prerelease
dotnet add package Honua.Sdk.Admin --prerelease
dotnet add package Honua.Sdk.Spec --prerelease
dotnet add package Honua.Sdk.Wfs --prerelease
dotnet add package Honua.Sdk.GeoServices --prerelease
dotnet add package Honua.Sdk.Scenes --prerelease
dotnet add package Honua.Sdk.OgcFeatures --prerelease
```

Pre-release builds are also available from
[GitHub Packages](INSTALL.md#install-from-github-packages-pre-release).

## Quick usage

Register the clients with dependency injection and query features:

```csharp
using Honua.Sdk.Grpc.Models;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.Spec.Extensions;
using Honua.Sdk.Wfs.Extensions;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.Scenes.Extensions;
using Honua.Sdk.OgcFeatures.Extensions;

// Register clients
builder.Services.AddHonuaGrpc(o => o.Address = "https://localhost:5001");
builder.Services.AddHonuaAdmin(o => o.BaseAddress = new Uri("https://localhost:5001"));
builder.Services.AddHonuaGeocoding(o => o.BaseAddress = new Uri("https://localhost:5001"));
builder.Services.AddHonuaSpec(o => o.BaseAddress = new Uri("https://localhost:5001"));
builder.Services.AddHonuaWfs(o => o.BaseAddress = new Uri("https://localhost:5001"));
builder.Services.AddHonuaFeatureServer(o => o.BaseAddress = new Uri("https://localhost:5001"));
builder.Services.AddHonuaScenes(o => o.BaseAddress = new Uri("https://localhost:5001"));
builder.Services.AddHonuaOgcFeatures(o => o.BaseAddress = new Uri("https://localhost:5001"));

// Query features (injected IHonuaGrpcClient)
var response = await grpcClient.QueryFeaturesAsync(new QueryFeaturesRequest
{
    ServiceId = "parks",
    LayerId = 0,
    Where = "status = 'open'",
    ReturnGeometry = true,
});

foreach (var feature in response.Features)
    Console.WriteLine($"{feature.Id}: {feature.Attributes["name"]}");
```

## Authentication and token refresh

All client packages support static `ApiKey` / `BearerToken` values and
request-time `ApiKeyProvider` / `BearerTokenProvider` delegates. Use providers
when credentials can refresh, rotate, or be revoked while the process is
running:

```csharp
builder.Services.AddHonuaGrpc(o =>
{
    o.Address = "https://localhost:5001";
    o.BearerTokenProvider = ct => tokenCache.GetAccessTokenAsync(ct);
});
```

Credentials are sent only over HTTPS except for loopback HTTP during local
development. See [docs/authentication.md](docs/authentication.md) for secure
storage guidance and retry/failure behavior.

## Apply edits

The gRPC client supports feature edits (adds, updates, deletes):

```csharp
var response = await grpcClient.ApplyEditsAsync(new ApplyEditsRequest
{
    ServiceId = "parks",
    LayerId = 0,
    Adds = [new Feature { Attributes = new() { ["name"] = "New Park" } }],
    RollbackOnFailure = true,
});

Console.WriteLine($"Added: {response.AddResults.Count}");
```

## Streaming

Stream large result sets without buffering the entire response:

```csharp
await foreach (var page in grpcClient.QueryFeaturesStreamAsync(request))
{
    foreach (var feature in page.Features)
        Console.WriteLine(feature.Id);
}
```

## Shared query abstraction

Protocol packages keep their native APIs, and the read/query clients also
implement `IHonuaFeatureQueryClient` from `Honua.Sdk.Abstractions` for common
application code:

```csharp
using Honua.Sdk.Abstractions.Features;

IHonuaFeatureQueryClient queryClient = featureQueryClients
    .Single(c => c.ProviderName == "ogc-features");

var page = await queryClient.QueryAsync(new FeatureQueryRequest
{
    Source = new FeatureSource { CollectionId = "parks" },
    Filter = "status = 'open'",
    FilterLanguage = FeatureFilterLanguage.Cql2Text,
    OutFields = ["name", "status"],
    Limit = 10,
});
```

For source-oriented application code, wrap a provider client in
`HonuaSource`. The source descriptor owns the provider locator, so query code
does not switch on gRPC, WFS, GeoServices, or OGC-specific source fields:

```csharp
var source = new HonuaSource(
    new SourceDescriptor
    {
        Id = "parks",
        Protocol = FeatureProtocolIds.OgcFeatures,
        Locator = new SourceLocator { CollectionId = "parks" }
    },
    queryClient,
    editClient: queryClient as IHonuaFeatureEditClient,
    nativeClient: queryClient);

var result = await source.QueryAsync(new SourceQuery
{
    Where = "status = 'open'",
    FilterLanguage = FeatureFilterLanguage.Cql2Text,
    OutFields = ["name", "status"],
    Limit = 10,
});

var native = source.Protocol<IHonuaOgcFeaturesClient>(FeatureProtocolIds.OgcFeatures);
```

See [docs/source-facade.md](docs/source-facade.md) for the descriptor,
capability, and protocol alias model.

## Shared edit abstraction

Feature providers expose shared write support through `IHonuaFeatureEditClient`
from `Honua.Sdk.Abstractions`. Today gRPC, GeoServices FeatureServer, and OGC
API Features advertise write capabilities; WFS registers unsupported
capabilities with a clear reason until WFS-T support is added.

```csharp
using Honua.Sdk.Abstractions.Features;

IHonuaFeatureEditClient edits = featureEditClients
    .Single(c => c.ProviderName == "grpc");

var result = await edits.ApplyEditsAsync(new FeatureEditRequest
{
    Source = new FeatureSource { ServiceId = "parks", LayerId = 0 },
    DeleteObjectIds = [42],
    RollbackOnFailure = true,
});
```

See [docs/feature-edits.md](docs/feature-edits.md) for shared result models,
provider support, and unsupported-provider behavior.

## Offline sync core

Offline-capable apps can use `Honua.Sdk.Offline.Abstractions` for package
manifests, source descriptors, sync state, checkpoints, change journals,
conflict envelopes, and storage adapter interfaces. `Honua.Sdk.Offline` adds a
platform-neutral planner and sync engine over `IHonuaFeatureQueryClient` and
`IHonuaFeatureEditClient`.

```csharp
using Honua.Sdk.Offline;

var result = await offlineSyncEngine.SyncAsync(manifest, cancellationToken);
```

See [docs/offline-sync-core.md](docs/offline-sync-core.md) for package
boundaries, adapter expectations, and mobile integration guidance.

## Retry

The gRPC, WFS, GeoServices, and OGC API Features clients retry automatically on
transient read failures with exponential backoff and jitter. gRPC retries
`QueryFeatures` and `QueryFeaturesStream` on `Unavailable` / `Internal`; HTTP
clients retry safe methods on `429`, `502`, `503`. Each DI client also exposes
a `Timeout` option that defaults to 100 seconds and accepts any value greater
than 10 milliseconds and less than 24 hours. Configurable on each client:

```csharp
builder.Services.AddHonuaGrpc(o =>
{
    o.Address = "https://localhost:5001";
    o.Timeout = TimeSpan.FromSeconds(30);
    o.EnableRetry = true;       // default
    o.MaxRetryAttempts = 3;     // default, range 2-5
});

builder.Services.AddHonuaWfs(o =>
{
    o.BaseAddress = new Uri("https://localhost:5001");
    o.Timeout = TimeSpan.FromSeconds(30);
    o.EnableRetry = true;       // default
    o.MaxRetryAttempts = 3;     // default, range 2-5
});
```

Timeout, retry, error, pagination, and endpoint coverage behavior is documented
in [docs/client-behavior.md](docs/client-behavior.md).

## WFS 2.0 queries

Query features via OGC WFS 2.0 with GeoJSON output:

```csharp
var caps = await wfsClient.GetCapabilitiesAsync();
Console.WriteLine($"WFS {caps.Version}: {caps.FeatureTypes.Count} feature types");

var result = await wfsClient.GetFeaturesAsync(new GetFeaturesRequest
{
    TypeNames = "parcels",
    Count = 10,
    Bbox = new WfsBoundingBox { MinX = -122.5, MinY = 37.5, MaxX = -122.0, MaxY = 38.0 },
});

foreach (var feature in result.Features)
    Console.WriteLine($"{feature.Id}: {feature.Properties["name"]}");
```

Auto-paginate large result sets with `IAsyncEnumerable`:

```csharp
await foreach (var feature in wfsClient.GetFeaturesAsyncEnumerable(new GetFeaturesRequest
{
    TypeNames = "parcels",
    Count = 100,
}))
{
    Console.WriteLine(feature.Id);
}
```

## Admin compatibility checks

`Honua.Sdk.Admin` validates a connected server against
`GET /api/v1/admin/capabilities`:

```csharp
var compatibility = await adminClient.CheckCompatibilityAsync();

if (!compatibility.IsSupported)
{
    throw new InvalidOperationException(
        $"Honua Server {compatibility.ServerVersion} is not supported. " +
        $"Minimum supported version: {compatibility.MinimumSupportedServerVersion}. " +
        $"{compatibility.UnsupportedReason}");
}
```

The current SDK compatibility policy and CI package API gate are documented in
[docs/compatibility.md](docs/compatibility.md).

## Spec workspace contracts

`Honua.Sdk.Spec` provides the stable client surface for spec validation,
planning, apply-event streaming, and cancellation:

```csharp
var document = new SpecDocumentRequest
{
    GrammarVersion = "spec/v1",
    ProcessFamilyVersion = "process/v1",
    Nodes =
    [
        new SpecNodeRequest
        {
            Id = "source",
            Kind = SpecResourceKind.Dataset,
            Op = "catalog.source"
        }
    ]
};

var plan = await specClient.PlanAsync(document);
await using var apply = await specClient.ApplyAsync(document);
await foreach (var evt in apply.Events)
{
    Console.WriteLine($"{evt.Sequence}: {evt.Kind}");
}
```

Server implementation details stay in `honua-server`; admin editor state and
local stubs stay in `honua-server-admin`. See
[docs/spec-workspace-contracts.md](docs/spec-workspace-contracts.md).

## Admin bootstrap flow

For the canonical runnable sample app for this repo's bootstrap and publish
operator flow, see
[examples/AdminBootstrapConsole](examples/AdminBootstrapConsole/).

- `CheckCompatibilityAsync()` is the first remote call. It validates server
  version `0.1.0` or newer, release channel `preview` or newer, control-plane
  API major `1`, and base path `/api/v1/admin`.
- Existing connections are reused only when the configured name also matches
  host, port, database, username, and SSL settings. Same-name connections that
  point somewhere else fail fast.
- Existing layers are reused only when the configured service and source table
  match. The sample enables the layer and union-adds `Grpc` to the current
  enabled protocol list instead of replacing it.
- Publishing requires discovery metadata for the geometry column, geometry
  type, SRID, and a single primary key.
- Verification uses a bounded `QueryFeaturesAsync()` request with
  `Where = "1=1"`, `ReturnGeometry = false`, `ResultRecordCount = 3`,
  `OrderBy = primary key`, and `OutFields` selected from discovery metadata.

## Repository layout

```
src/
  Honua.Sdk.Grpc/          gRPC client package (query, stream, edit)
  Honua.Sdk.Admin/          Admin + Geocoding client package
  Honua.Sdk.Spec/           Spec workspace validate/plan/apply client package
  Honua.Sdk.Wfs/           WFS 2.0 read/query client package
  Honua.Sdk.GeoServices/   GeoServices FeatureServer read/query client package
  Honua.Sdk.Scenes/        Scene metadata and package contract client
  Honua.Sdk.OgcFeatures/   OGC API Features read/query client package
  Honua.Sdk.Abstractions/  Shared provider-neutral feature query contracts
tests/
  Honua.Sdk.Grpc.Tests/     gRPC client tests
  Honua.Sdk.Admin.Tests/    Admin + Geocoding tests
  Honua.Sdk.Spec.Tests/     Spec workspace contract/client tests
  Honua.Sdk.Wfs.Tests/      WFS client tests
  Honua.Sdk.GeoServices.Tests/
  Honua.Sdk.Scenes.Tests/
  Honua.Sdk.OgcFeatures.Tests/
examples/
  AdminBootstrapConsole/     Canonical console sample for admin bootstrap + gRPC verification
  FieldDataCollection/       Archived MAUI reference assets; buildable as a marker project
docs/
  quickstart.md             5-minute quickstart tutorial
  scenes.md                 Scene metadata and offline package contract guide
  staging-integration.md    Staging CI inputs, evidence, and troubleshooting
third_party/
  geospatial-grpc/          Vendored proto input from the geospatial-grpc source of truth
```

## Documentation

- **[Admin Bootstrap Console](examples/AdminBootstrapConsole/)** -- the
  canonical sample app for this repo; bootstrap a PostGIS table with
  `Honua.Sdk.Admin`, preserve existing protocols while enabling `Grpc`, verify
  it with a bounded `Honua.Sdk.Grpc` query, and troubleshoot the exact error
  surfaces returned by the sample
- **[Quickstart](docs/quickstart.md)** -- build a console app that queries
  features through native clients and the shared abstraction, lists services,
  and geocodes an address in 5 minutes
- **[Feature Edits](docs/feature-edits.md)** -- shared edit abstraction,
  current gRPC support, and provider-specific write backlog boundaries
- **[Staging Integration Guide](docs/staging-integration.md)** -- staging
  environment inputs, CI evidence artifacts, common failures, and bounded
  follow-on tickets for shared staging ownership
- **[Client Behavior](docs/client-behavior.md)** -- timeout, retry, error,
  pagination, and typed endpoint coverage behavior across packages
- **[Spec Workspace Contracts](docs/spec-workspace-contracts.md)** -- package
  ownership, repo boundaries, and JSON fixtures for spec plan/apply contracts
- **[Mobile Contract Harmonization](docs/mobile-contract-harmonization.md)** --
  ownership map and fixture baseline for moving reusable mobile contracts to
  published `Honua.Sdk.*` packages
- **[Release and NuGet Publishing](docs/release.md)** -- package versioning,
  release tags, dry runs, and GitHub Packages publishing
- **[INSTALL.md](INSTALL.md)** -- NuGet and GitHub Packages setup, version
  policy and server compatibility baseline
- **[Backlog cadence](docs/backlog-cadence.md)** -- weekly triage, scope gate,
  and close hygiene for this repository
- **[SDK Capability Backlog](docs/sdk-capability-backlog.md)** -- prioritized
  non-display SDK feature backlog mapped against mature GIS SDK capability
  areas, plus a separate display/maps approach
- **[Field Data Collection](examples/FieldDataCollection/)** -- archived MAUI
  reference assets for offline sync and map views

## License

[Apache 2.0](LICENSE)
