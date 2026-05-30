# Honua .NET SDK

Official .NET client libraries for [Honua](https://github.com/honua-io/honua-server) --
an open-source geospatial feature server. The SDK provides typed clients for
querying and editing features over gRPC, querying via OGC WFS 2.0, managing
services through the Admin REST API, geocoding addresses, and reading features
through GeoServices FeatureServer, OGC API Features, OGC API Processes, scene
metadata endpoints, OGC API Records and STAC catalog endpoints, and shared
real-time feature stream contracts.

> **New here? Pick your path:**
> - Just want to call the server in 5 minutes → [docs/quickstart.md](docs/quickstart.md)
> - Want a map of the 14 packages before you choose → [docs/architecture.md](docs/architecture.md)
> - Want to browse the public docs → [docs/README.md](docs/README.md)
> - Want to install pre-release packages → [INSTALL.md](INSTALL.md)
> - Hit a problem → [docs/troubleshooting.md](docs/troubleshooting.md)

Current SDK capabilities are summarized in [docs/features/README.md](docs/features/README.md).

## Packages

| Package | Description |
|---------|-------------|
| **Honua.Sdk** | **Umbrella / meta package -- recommended starting point.** One install + one `AddHonua(o => o.BaseAddress = ...)` registers every enabled sub-package. Pick the narrower packages directly if you want fewer transitive dependencies. |
| **Honua.Sdk.Abstractions** | Shared feature query/edit/stream abstractions, source facades, Console shell/route/environment contracts, host-neutral plugin manifests, and browser-safe offline sync contracts (manifests, sync state, checkpoints, conflicts, storage) |
| **Honua.Sdk.Offline** | Provider-neutral offline push/pull planner and sync engine over the shared feature abstractions |
| **Honua.Sdk.Grpc** | gRPC client for `FeatureService` and native `ProcessService` jobs -- typed queries, feature streaming, edits, spatial filters, job lifecycle |
| **Honua.Sdk.Admin** | Admin REST client -- services, layers, connections, styles, metadata, RBAC/users, alerts, observability, feature-event replay, streaming operations |
| **Honua.Sdk.Processes** | Browser-safe OGC API Processes REST client -- process discovery, async jobs, polling, dismissal, results, shared job models |
| **Honua.Sdk.Spec** | Spec workspace REST/SSE client -- validate, plan, apply stream, cancel, cached artifact retrieval |
| **Honua.Sdk.Studio** | Console Studio analysis-report read client -- retrieve the structured report envelope and render Markdown/HTML for completed jobs |
| **Honua.Sdk.ConsoleShare** | Console Share client -- read share detail, update access, validate dependency closure, and manage public-link and embed-token lifecycle |
| **Honua.Sdk.Field** | Field form, validation, calculated field, duplicate detection, and record workflow contracts |
| **Honua.Sdk.Geometry** | NTS/ProjNet-backed geometry conversion, spatial references, projection, planar analysis, and geofence evaluation |
| **Honua.Sdk.GeoServices** | GeoServices FeatureServer read/query client -- service/layer metadata, query, count, IDs, extent, statistics |
| **Honua.Sdk.Scenes** | Scene metadata client -- list/detail/resolve scene endpoints plus offline scene package contracts |
| **Honua.Sdk.OgcFeatures** | OGC API Features and WFS 2.0 read/query client -- landing page, conformance, collections, queryables, items, plus WFS GetCapabilities / GetFeature (GeoJSON) / DescribeFeatureType |
| **Honua.Sdk.Catalogs** | OGC API Records + STAC catalog client -- landing pages, conformance, collections, item / record pages, GET/POST search with paging, raw JSON |
| *Geocoding* (in Admin) | Forward/reverse geocoding and autocomplete via `IHonuaGeocodingClient` |

Browser and WebAssembly consumers should start with
[docs/browser-wasm-support.md](docs/browser-wasm-support.md). The browser-safe
surface is contracts plus REST clients over browser `HttpClient`; native gRPC,
local storage engines, background schedulers, and display renderers stay out of
SDK core.

## Install

```bash
# Easiest: install the umbrella to get every Honua.Sdk.* package at once.
dotnet add package Honua.Sdk
```

<details>
<summary>Want narrower dependencies? Install per-package instead.</summary>

```bash
dotnet add package Honua.Sdk.Abstractions
dotnet add package Honua.Sdk.Offline
dotnet add package Honua.Sdk.Grpc
dotnet add package Honua.Sdk.Admin
dotnet add package Honua.Sdk.Processes
dotnet add package Honua.Sdk.Spec
dotnet add package Honua.Sdk.Studio
dotnet add package Honua.Sdk.Field
dotnet add package Honua.Sdk.Geometry
dotnet add package Honua.Sdk.GeoServices
dotnet add package Honua.Sdk.Scenes
dotnet add package Honua.Sdk.OgcFeatures
dotnet add package Honua.Sdk.Catalogs
```

</details>

Pre-1.0 / dry-run builds are also available from
[GitHub Packages](INSTALL.md#install-from-github-packages).

## Quick usage

A complete `Program.cs` you can drop into a `dotnet new console` project
(after installing the packages you use — see [docs/quickstart.md](docs/quickstart.md)
for the minimal install set):

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Honua.Sdk;
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Models;

var builder = Host.CreateApplicationBuilder(args);
var serverUri = new Uri("https://localhost:5001");

// One call registers every enabled Honua SDK client. Defaults register the
// common gRPC, Admin + Catalog, Geocoding, OGC API Features, OGC API
// Processes, and WFS 2.0 clients. Flip the situational Use* flags to opt in to Scenes,
// Spec, Studio, Stac, OgcRecords, GeoServices, or Routing.
builder.Services.AddHonua(o =>
{
    o.BaseAddress = serverUri;
    // o.BearerTokenProvider = ct => tokenCache.GetAccessTokenAsync(ct);
});

using var host = builder.Build();
var grpc = host.Services.GetRequiredService<IHonuaGrpcClient>();

var response = await grpc.QueryFeaturesAsync(new QueryFeaturesRequest
{
    ServiceId      = "parks",
    LayerId        = 0,
    Where          = "status = 'open'",
    ReturnGeometry = true,
});

foreach (var feature in response.Features)
{
    Console.WriteLine($"{feature.Id}: {feature.Attributes["name"]}");
}
```

<details>
<summary>Want to register individually instead of using the umbrella?</summary>

The per-package `AddHonua*` extensions remain available and unchanged for
callers who want explicit, narrow control:

```csharp
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.OgcFeatures.Wfs.Extensions;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.Processes.Extensions;
using Honua.Sdk.Studio.Extensions;

builder.Services.AddHonuaGrpc       (o => o.BaseAddress = serverUri);
builder.Services.AddHonuaAdmin      (o => o.BaseAddress = serverUri); // + IHonuaCatalogClient
builder.Services.AddHonuaGeocoding  (o => o.BaseAddress = serverUri);
builder.Services.AddHonuaWfs        (o => o.BaseAddress = serverUri);
builder.Services.AddHonuaOgcFeatures(o => o.BaseAddress = serverUri);
builder.Services.AddHonuaProcesses  (o => o.BaseAddress = serverUri);
builder.Services.AddHonuaStudio     (o => o.BaseAddress = serverUri);
```

</details>

## Beyond the quickstart

The deeper capabilities each have their own guide:

| Topic | Guide |
|---|---|
| Authentication, providers, token refresh, mTLS | [docs/authentication.md](docs/authentication.md) |
| Apply edits over gRPC / OGC / GeoServices | [docs/feature-edits.md](docs/feature-edits.md) |
| Streaming + paging via `IAsyncEnumerable<T>` | [docs/client-behavior.md](docs/client-behavior.md) |
| Shared `IHonuaFeatureQueryClient` abstraction | [docs/source-facade.md](docs/source-facade.md) |
| Catalog / OGC Records / STAC discovery | [docs/metadata-catalog-parity.md](docs/metadata-catalog-parity.md) |
| Plugin contracts (manifests, permissions) | [docs/plugin-contracts.md](docs/plugin-contracts.md) |
| Console shell / route guards / environment profiles / Studio reports | [docs/console-client-contracts.md](docs/console-client-contracts.md) |
| Offline sync (planner, conflicts, manifests) | [docs/offline-sync-core.md](docs/offline-sync-core.md) |
| Field form / validation / record workflow | [Honua.Sdk.Field README](src/Honua.Sdk.Field/README.md) |
| Retry / timeout / resilience defaults | [docs/client-behavior.md](docs/client-behavior.md) |
| WFS 2.0 query surface | [Honua.Sdk.OgcFeatures README](src/Honua.Sdk.OgcFeatures/README.md) |
| Admin compatibility gate | [docs/compatibility.md](docs/compatibility.md) |
| Spec workspace validate / plan / apply / artifact retrieval | [docs/spec-workspace-contracts.md](docs/spec-workspace-contracts.md) |
| Admin bootstrap end-to-end sample | [examples/AdminBootstrapConsole](examples/AdminBootstrapConsole/) |
| Geometry, CRS, geofence evaluation | [docs/geometry-analysis.md](docs/geometry-analysis.md) + [docs/geofencing.md](docs/geofencing.md) |
| Scene metadata + offline scene packages | [docs/scenes.md](docs/scenes.md) |
| Browser / WASM hosting | [docs/browser-wasm-support.md](docs/browser-wasm-support.md) |

For an overview diagram of the package layering, see
[docs/architecture.md](docs/architecture.md). For the full doc index, see
[docs/README.md](docs/README.md).

## Repository layout

```
src/
  Honua.Sdk.Abstractions/        Shared feature query/edit/stream contracts + offline sync contracts (manifests, sync state, checkpoints, conflicts)
  Honua.Sdk.Offline/             Offline push/pull planner and sync engine
  Honua.Sdk.Grpc/                gRPC FeatureService + native ProcessService clients
  Honua.Sdk.Processes/           OGC API Processes REST client + shared job models
  Honua.Sdk.Geometry/            NTS/ProjNet geometry, CRS, planar analysis, geofence
  Honua.Sdk.Admin/               Admin + Catalog + Geocoding client package
  Honua.Sdk.Spec/                Spec workspace validate/plan/apply/artifact client package
  Honua.Sdk.Studio/              Console Studio analysis-report read client package
  Honua.Sdk.ConsoleShare/        Console Share access/public-link/embed-token client package
  Honua.Sdk.Field/               Field form, validation, and workflow contracts
  Honua.Sdk.GeoServices/         GeoServices FeatureServer + routing client package
  Honua.Sdk.Scenes/              Scene metadata and offline package contract client
  Honua.Sdk.OgcFeatures/         OGC API Features + WFS 2.0 read/query/edit client package
  Honua.Sdk.Catalogs/            OGC API Records + STAC catalog client package
tests/
  Honua.Sdk.*.Tests/             Unit tests per package
  Honua.Sdk.IntegrationTests/    Staging-gated read-only protocol coverage
  Honua.Sdk.BrowserSmoke[.Tests]/ Browser WASM runtime smoke harness
  DemoSuite.Tests/               Deterministic end-to-end demo workflow tests
examples/
  AdminBootstrapConsole/         Canonical operator/bootstrap sample (admin + gRPC verify)
  SpecPlanApplyConsole/          Spec validate/plan/apply stream sample
  StudioAnalysisReportConsole/   Studio analysis report retrieve/render sample
  RealtimeWorker/                Real-time feature stream worker sample
  RoutingGeofenceConsole/        Routing + geofence evaluation sample
  FieldDataCollection/           Archived MAUI reference assets for offline / forms
docs/                            Per-topic guides (see Documentation below)
contracts/                       Golden JSON/protobuf fixtures shared with consuming repos
third_party/geospatial-grpc/     Vendored proto input from the geospatial-grpc source of truth
```

## Documentation

- **[Hosted API reference](https://honua-io.github.io/honua-sdk-dotnet/)** -- full DocFX-generated reference for every public type and member, deployed from `trunk`.

### Getting started

- **[Quickstart](docs/quickstart.md)** -- build a console app that queries
  features through native clients and the shared abstraction, lists services,
  and geocodes an address in 5 minutes
- **[INSTALL.md](INSTALL.md)** -- NuGet and GitHub Packages setup, version
  policy and server compatibility baseline
- **[Authentication](docs/authentication.md)** -- credential providers,
  refresh, HTTPS-only transport, and diagnostics
- **[Console client contracts](docs/console-client-contracts.md)** -- Blazor
  Web and MAUI contract map, route guards, environment profiles, native mTLS
  state, and fixtures
- **[Troubleshooting](docs/troubleshooting.md)** -- common errors and fixes
  for configuration, auth, retry, browser, and compatibility issues
- **[Browser / WASM support](docs/browser-wasm-support.md)** -- supported
  surface, gRPC-Web, and known browser-host constraints

### Samples

- **[Admin Bootstrap Console](examples/AdminBootstrapConsole/)** -- the
  canonical sample app for this repo; bootstrap a PostGIS table with
  `Honua.Sdk.Admin`, preserve existing protocols while enabling `Grpc`, verify
  it with a bounded `Honua.Sdk.Grpc` query, and troubleshoot the exact error
  surfaces returned by the sample
- **[Studio Analysis Report Console](examples/StudioAnalysisReportConsole/)** --
  retrieve a structured Studio analysis report and render Markdown through
  `IHonuaStudioReportsClient`
- **[Field Data Collection](examples/FieldDataCollection/)** -- archived MAUI
  reference assets for offline sync and map views
- **[Demo Suite](docs/demo-suite.md)** -- deterministic end-to-end demo
  workflows used in CI

### Capability guides

- **[Feature Edits](docs/feature-edits.md)** -- shared edit abstraction,
  current gRPC support, and provider-specific write backlog boundaries
- **[Geometry analysis](docs/geometry-analysis.md)** -- NTS/ProjNet-backed
  geometry, CRS transforms, planar predicates, and spatial indexes
- **[Geofencing](docs/geofencing.md)** -- evaluation contracts, dwell logic,
  and geofence sources
- **[Scene metadata and packages](docs/scenes.md)** -- scene discovery,
  render endpoint resolution, and offline scene package validation
- **[Offline sync core](docs/offline-sync-core.md)** -- planner, checkpoints,
  conflict envelopes, change journals, and storage contracts
- **[Spec workspace contracts](docs/spec-workspace-contracts.md)** -- package
  ownership, repo boundaries, and JSON fixtures for spec plan/apply/artifact
  contracts
- **[Source facade](docs/source-facade.md)** -- source descriptors, protocol
  aliases, capabilities, and native protocol escape hatches
- **[Plugin contracts](docs/plugin-contracts.md)** -- host-neutral plugin
  manifests, permissions, edition gates, and compatibility requirements
- **[Client behavior](docs/client-behavior.md)** -- timeout, retry, error,
  pagination, and typed endpoint coverage behavior across packages
- **[Metadata catalog parity](docs/metadata-catalog-parity.md)** -- discovery,
  Catalog, Records, and STAC surface comparisons

### Operations

- **[Release and NuGet publishing](docs/release.md)** -- package versioning,
  release tags, dry runs, and GitHub Packages publishing
- **[Compatibility](docs/compatibility.md)** -- server matrix and CI API
  compatibility gate used before publish
- **[Staging integration guide](docs/staging-integration.md)** -- staging
  environment inputs, CI evidence artifacts, and bounded follow-on tickets
- **[Protocol integration tests](docs/protocol-integration-tests.md)** --
  Testcontainers-backed local protocol coverage and fixture contract

## License

[Apache 2.0](LICENSE)
