# Honua .NET SDK

[![.NET SDK CI](https://github.com/honua-io/honua-sdk-dotnet/actions/workflows/ci.yml/badge.svg?branch=trunk)](https://github.com/honua-io/honua-sdk-dotnet/actions/workflows/ci.yml)
[![Docs](https://github.com/honua-io/honua-sdk-dotnet/actions/workflows/docs.yml/badge.svg?branch=trunk)](https://honua-io.github.io/honua-sdk-dotnet/)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/honua-io/honua-sdk-dotnet/badge)](https://scorecard.dev/viewer/?uri=github.com/honua-io/honua-sdk-dotnet)
[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

Official .NET client libraries for [Honua](https://github.com/honua-io/honua-server),
a cloud-native geospatial server that exposes one shared capability set through
many protocol adapters. The SDK gives .NET applications typed, DI-friendly
clients for that surface: feature query/edit/streaming over gRPC, OGC API
Features, OGC WFS 2.0, GeoServices FeatureServer, OGC API Processes jobs, OGC
API Records + STAC catalogs, scene metadata, geocoding and routing, the Admin
REST API, NTS/ProjNet-backed geometry, and provider-neutral offline sync — all
sharing one options/auth/resilience pattern.

> **New here? Pick your path:**
> - Just want to call the server in 5 minutes → [docs/quickstart.md](docs/quickstart.md)
> - Want a map of the 15 packages and the separate CLI tool before you choose → [docs/architecture.md](docs/architecture.md)
> - Want to browse the public docs → [docs/README.md](docs/README.md)
> - Want to set up the package feed (all versions install from GitHub Packages today) → [INSTALL.md](INSTALL.md)
> - Hit a problem → [docs/troubleshooting.md](docs/troubleshooting.md)

## Status

| | |
|---|---|
| Current version | 1.5.0 (single version across all packages, managed by Release Please; see [CHANGELOG.md](CHANGELOG.md)) | <!-- x-release-please-version -->
| Target framework | `net10.0` — requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) |
| Package feed | **GitHub Packages today** (authenticated; see [Install](#install)) for every version, stable or prerelease. nuget.org publishing is planned but deliberately deferred until the `Geospatial.Grpc` protocol dependency has a stable public release there. |
| API stability | SemVer with a CI [public-API compatibility gate](docs/compatibility.md); breaking changes only in majors |
| License | [Apache 2.0](LICENSE) |

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
| **Honua.Sdk.ConsoleShare** | Console Share clients -- read share detail, update access, validate dependency closure, manage public-link / embed-token lifecycle, drive scheduled exports and traffic, and publish open-data (DCAT / STAC) |
| **Honua.Sdk.Field** | Field form, validation, calculated field, duplicate detection, and record workflow contracts |
| **Honua.Sdk.Geometry** | NTS/ProjNet-backed geometry conversion, spatial references, projection, planar analysis, and geofence evaluation |
| **Honua.Sdk.GeoServices** | GeoServices FeatureServer read/query/edit client -- service/layer metadata, query, count, IDs, extent, statistics, apply-edits -- plus NAServer routing and the ImageServer raster client (`IHonuaRasterDataClient`: raster metadata, coverage statistics, windowed reads) |
| **Honua.Sdk.Scenes** | Scene metadata client -- list/detail/resolve scene endpoints plus offline scene package contracts |
| **Honua.Sdk.OgcFeatures** | OGC API Features and WFS 2.0 read/query client -- landing page, conformance, collections, queryables, items, plus WFS GetCapabilities / GetFeature (GeoJSON) / DescribeFeatureType |
| **Honua.Sdk.Catalogs** | OGC API Records + STAC catalog client -- landing pages, conformance, collections, item / record pages, GET/POST search with paging, raw JSON |
| **Honua.Sdk.Cli** | `honua doctor` .NET tool -- schema-pinned sanitized diagnostic bundles, anonymous capability probe, and bounded read-only replay |
| *Geocoding* (in Admin) | Forward/reverse geocoding and autocomplete via `IHonuaGeocodingClient` |

Browser and WebAssembly consumers should start with
[docs/browser-wasm-support.md](docs/browser-wasm-support.md). The browser-safe
surface is contracts plus REST clients over browser `HttpClient` (validated by
a trim-safety CI workflow); native gRPC, local storage engines, background
schedulers, and display renderers stay out of SDK core.

## Install

Packages are not yet on nuget.org: stable release tags will publish there once
the `Geospatial.Grpc` protocol dependency has a stable public release. Until
then, all packages (stable and preview) install from the authenticated GitHub
Packages feed. Follow [INSTALL.md](INSTALL.md) to add the `honua` source with a
GitHub classic PAT (`read:packages` scope) and map the package patterns, then:

```bash
# Easiest: install the umbrella to get every Honua.Sdk.* package at once.
dotnet add package Honua.Sdk --source honua
```

<details>
<summary>Want narrower dependencies? Install per-package instead.</summary>

```bash
dotnet add package Honua.Sdk.Abstractions --source honua
dotnet add package Honua.Sdk.Offline --source honua
dotnet add package Honua.Sdk.Grpc --source honua
dotnet add package Honua.Sdk.Admin --source honua
dotnet add package Honua.Sdk.Processes --source honua
dotnet add package Honua.Sdk.Spec --source honua
dotnet add package Honua.Sdk.Studio --source honua
dotnet add package Honua.Sdk.ConsoleShare --source honua
dotnet add package Honua.Sdk.Field --source honua
dotnet add package Honua.Sdk.Geometry --source honua
dotnet add package Honua.Sdk.GeoServices --source honua
dotnet add package Honua.Sdk.Scenes --source honua
dotnet add package Honua.Sdk.OgcFeatures --source honua
dotnet add package Honua.Sdk.Catalogs --source honua
```

</details>

Install the support-safe diagnostics tool separately. `dotnet tool install`
does not read a repository `NuGet.config`, so pass the feed explicitly (the
credentials configured in [INSTALL.md](INSTALL.md) are matched by source URL):

```bash
dotnet tool install --global Honua.Sdk.Cli \
  --add-source https://nuget.pkg.github.com/honua-io/index.json
honua doctor --help
```

See [Sanitized diagnostic bundles](docs/diagnostic-bundles.md) for capture,
consent, privacy, and replay guidance.

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
// Spec, Studio, ConsoleShare, Stac, OgcRecords, GeoServices, Routing, or
// ImageServer (raster).
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
| Streaming + paging via `IAsyncEnumerable<T>`, retry / timeout / resilience defaults | [docs/client-behavior.md](docs/client-behavior.md) |
| Shared `IHonuaFeatureQueryClient` abstraction | [docs/source-facade.md](docs/source-facade.md) |
| Catalog / OGC Records / STAC discovery | [docs/metadata-catalog-parity.md](docs/metadata-catalog-parity.md) |
| Plugin contracts (manifests, permissions) | [docs/plugin-contracts.md](docs/plugin-contracts.md) |
| Console shell / route guards / environment profiles / Studio reports | [docs/console-client-contracts.md](docs/console-client-contracts.md) |
| Offline sync (planner, conflicts, manifests) | [docs/offline-sync-core.md](docs/offline-sync-core.md) |
| Field form / validation / record workflow | [Honua.Sdk.Field README](src/Honua.Sdk.Field/README.md) |
| WFS 2.0 query surface | [Honua.Sdk.OgcFeatures README](src/Honua.Sdk.OgcFeatures/README.md) |
| Admin compatibility gate | [docs/compatibility.md](docs/compatibility.md) |
| Spec workspace validate / plan / apply / artifact retrieval | [docs/spec-workspace-contracts.md](docs/spec-workspace-contracts.md) |
| Geometry, CRS, geofence evaluation | [docs/geometry-analysis.md](docs/geometry-analysis.md) + [docs/geofencing.md](docs/geofencing.md) |
| Scene metadata + offline scene packages | [docs/scenes.md](docs/scenes.md) |
| Browser / WASM hosting | [docs/browser-wasm-support.md](docs/browser-wasm-support.md) |

For an overview diagram of the package layering, see
[docs/architecture.md](docs/architecture.md). For the full doc index, see
[docs/README.md](docs/README.md).

## Samples

Runnable console/worker samples live in [examples/](examples/README.md):

- **[AdminBootstrapConsole](examples/AdminBootstrapConsole/)** -- the canonical
  sample: bootstrap a PostGIS table with `Honua.Sdk.Admin`, enable `Grpc`
  while preserving existing protocols, verify with a bounded `Honua.Sdk.Grpc`
  query
- **[SpecPlanApplyConsole](examples/SpecPlanApplyConsole/)** -- spec validate /
  plan / apply SSE stream consumption
- **[StudioAnalysisReportConsole](examples/StudioAnalysisReportConsole/)** --
  retrieve a structured Studio analysis report and render Markdown
- **[RealtimeWorker](examples/RealtimeWorker/)** -- real-time feature stream
  worker with buffering and resume tokens
- **[RoutingGeofenceConsole](examples/RoutingGeofenceConsole/)** -- route solve
  plus geofence enter/exit/approach/depart transitions
- **[OfflineConflictConsole](examples/OfflineConflictConsole/)** -- offline
  sync conflict production, detection, and resolution strategies
- **[FieldFormConsole](examples/FieldFormConsole/)** -- field form validation
  and calculated-field evaluation
- **[FieldDataCollection](examples/FieldDataCollection/)** -- archived MAUI
  reference assets for offline sync and map views

The deterministic end-to-end demo workflows used in CI are documented in
[docs/demo-suite.md](docs/demo-suite.md).

## Repository layout

```
src/
  Honua.Sdk/                     Umbrella meta-package: AddHonua(...) fan-out registration
  Honua.Sdk.Abstractions/        Shared feature query/edit/stream contracts + offline sync contracts (manifests, sync state, checkpoints, conflicts)
  Honua.Sdk.Offline/             Offline push/pull planner and sync engine
  Honua.Sdk.Grpc/                gRPC FeatureService + native ProcessService clients
  Honua.Sdk.Processes/           OGC API Processes REST client + shared job models
  Honua.Sdk.Geometry/            NTS/ProjNet geometry, CRS, planar analysis, geofence
  Honua.Sdk.Admin/               Admin + Catalog + Geocoding client package
  Honua.Sdk.Spec/                Spec workspace validate/plan/apply/artifact client package
  Honua.Sdk.Studio/              Console Studio analysis-report read client package
  Honua.Sdk.ConsoleShare/        Console Share access/public-link/embed-token, export/traffic, and open-data (DCAT/STAC) client package
  Honua.Sdk.Cli/                 `honua doctor` support-safe diagnostic .NET tool
  Honua.Sdk.Field/               Field form, validation, and workflow contracts
  Honua.Sdk.GeoServices/         GeoServices FeatureServer + routing + ImageServer raster client package
  Honua.Sdk.Scenes/              Scene metadata and offline package contract client
  Honua.Sdk.OgcFeatures/         OGC API Features + WFS 2.0 read/query client package
  Honua.Sdk.Catalogs/            OGC API Records + STAC catalog client package
tests/
  Honua.Sdk.*.Tests/             Unit tests per package
  Honua.Sdk.IntegrationTests/    Staging-gated read-only protocol coverage
  Honua.Sdk.Conformance.Tests/   Shared geospatial-grpc conformance fixture gate
  Honua.Sdk.BrowserSmoke[.Tests]/ Browser WASM runtime smoke harness
  DemoSuite.Tests/               Deterministic end-to-end demo workflow tests
examples/                        Runnable samples (see Samples above)
docs/                            Per-topic guides (see Documentation below)
contracts/                       Golden JSON/protobuf fixtures shared with consuming repos
third_party/geospatial-grpc/     Vendored proto input from the geospatial-grpc source of truth
```

## Documentation

- **[Hosted API reference](https://honua-io.github.io/honua-sdk-dotnet/)** --
  full DocFX-generated reference for every public type and member, deployed
  from `trunk`
- **[Documentation index](docs/README.md)** -- every getting-started,
  capability, and operations guide in one place
- **[Quickstart](docs/quickstart.md)** -- 60-second hello-features, then a
  five-step guided tour
- **[INSTALL.md](INSTALL.md)** -- feed setup, version policy, and the server
  compatibility baseline
- **[Troubleshooting](docs/troubleshooting.md)** -- concrete failure modes and
  fixes for configuration, auth, retry, browser, and compatibility issues

Operations docs: [release and NuGet publishing](docs/release.md),
[server/API compatibility gates](docs/compatibility.md),
[staging integration](docs/staging-integration.md), and
[Testcontainers-backed protocol integration tests](docs/protocol-integration-tests.md).

## Related Honua projects

| Repo | What it is |
|---|---|
| [honua-server](https://github.com/honua-io/honua-server) | Flagship multi-protocol geospatial server this SDK talks to |
| [honua-console](https://github.com/honua-io/honua-console) | Unified web console (Studio, Catalog, Operate, Share) |
| [honua-sdk-js](https://github.com/honua-io/honua-sdk-js) | JavaScript/TypeScript SDKs + MCP server |
| [honua-sdk-python](https://github.com/honua-io/honua-sdk-python) | Python SDK |
| [honua-mobile](https://github.com/honua-io/honua-mobile) | .NET MAUI mobile SDK building on these packages |
| [geospatial-grpc](https://github.com/honua-io/geospatial-grpc) | Vendor-neutral gRPC protocol standard the `Honua.Sdk.Grpc` client implements |

Hosted product docs live at [honua.gitbook.io/honuaio](https://honua.gitbook.io/honuaio/).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for setup, house rules, the public-API
approval workflow, and the pull-request checklist. The build is strict
(warnings as errors, XML docs required on public members), commits follow
Conventional Commits, and the default branch is `trunk`.

## Security

Report vulnerabilities privately to <security@honua.io> — see the
[organization security policy](https://github.com/honua-io/.github/blob/main/SECURITY.md).
Please do not open public issues for security reports.

## License

[Apache 2.0](LICENSE)
