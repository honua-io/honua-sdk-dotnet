# AGENTS.md

## Overview

The Honua .NET SDK provides official .NET client libraries for [Honua](https://github.com/honua-io/honua-server),
an open-source geospatial feature server. It ships ~14 `Honua.Sdk.*` NuGet
packages with typed clients for querying/editing features over gRPC, OGC WFS 2.0,
OGC API Features, OGC API Processes, GeoServices FeatureServer, the Admin REST
API, geocoding/routing, scene metadata, OGC Records / STAC catalogs, real-time
feature streams, NTS/ProjNet-backed geometry, and provider-neutral offline sync.

This repo owns reusable, platform-neutral SDK contracts, service clients,
protocol adapters, serialization formats, and tests. UI, native, MAUI/Blazor,
and renderer code does not belong here (see Conventions & Gotchas).

## Tech Stack

- **Language/runtime:** C# on .NET 10 (`net10.0`); SDK `10.0.100` pinned in `global.json` (`rollForward: latestFeature`).
- **DI/hosting:** `Microsoft.Extensions.*` 10.x; HTTP resilience via `Microsoft.Extensions.Http.Resilience`.
- **gRPC:** `Grpc.Net.Client`, `Google.Protobuf`; generated code consumes the `Geospatial.Grpc` package (vendored protos in `third_party/geospatial-grpc/`).
- **Geometry/CRS:** `NetTopologySuite`, `NetTopologySuite.IO.GeoJSON4STJ`, `ProjNET`.
- **Tests:** xUnit, Moq, coverlet; `Microsoft.Playwright` (browser WASM smoke) and `Testcontainers` (protocol integration).
- **Central package management:** versions live in `Directory.Packages.props` (`ManagePackageVersionsCentrally`); common build props in `Directory.Build.props`.

## Setup

1. Install the [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later.
2. Configure the GitHub Packages source so `Geospatial.Grpc` and `Honua.*`
   packages restore (see `NuGet.config` + `INSTALL.md`):
   ```bash
   dotnet nuget update source github-honua --username <github-user> --password <github-token> --store-password-in-clear-text
   ```
3. Restore: `dotnet restore Honua.Sdk.sln`

## Commands

Build (strict, matches CI — warnings are errors):
```bash
dotnet build Honua.Sdk.sln --configuration Release /p:TreatWarningsAsErrors=true
```

Run all tests (after a build):
```bash
dotnet test Honua.Sdk.sln --configuration Release --no-build
```

Test a single package, e.g. Grpc:
```bash
dotnet test tests/Honua.Sdk.Grpc.Tests/Honua.Sdk.Grpc.Tests.csproj --configuration Release
```

Tests with coverage (CI floors: 75% line / 60% branch, enforced on merged report):
```bash
dotnet test Honua.Sdk.sln --configuration Release --no-build \
  --collect:"XPlat Code Coverage" --settings ./coverlet.runsettings --results-directory ./coverage
```

Public-API compatibility check (uses the `apicompat` local tool in `.config/dotnet-tools.json`):
```bash
dotnet tool restore
scripts/validate-api-compat.sh   # honors HONUA_API_COMPAT_BASE_REF / HONUA_API_COMPAT_ALLOW_BREAKING
```

Run a sample (no separate run script — these are console/worker apps):
```bash
dotnet run --project examples/AdminBootstrapConsole/AdminBootstrapConsole.csproj
```

Lint/style: there is no separate lint step. Roslyn analyzers run during build
(`EnableNETAnalyzers`, `AnalysisMode=AllEnabledByDefault`, `EnforceCodeStyleInBuild`,
`TreatWarningsAsErrors`); `.editorconfig` governs style and per-file analyzer suppressions.

## Architecture

Layered, provider-neutral SDK. `Honua.Sdk.Abstractions` defines shared feature
query/edit/stream contracts, source facades, plugin manifests, and offline sync
contracts. Protocol packages each wrap one server surface with a typed client,
an `*Options` class (`BaseAddress` required, validated `Timeout`/`MaxRetryAttempts`),
an auth handler, and an `AddHonua*` DI extension over `HttpClient`/`Grpc.Net.Client`.
The `Honua.Sdk` umbrella meta-package registers every enabled client via a single
`AddHonua(o => o.BaseAddress = ...)` with situational `Use*` opt-in flags.
`Honua.Sdk.Geometry` adapts NTS/ProjNet for geometry, CRS, planar analysis, and
geofencing; `Honua.Sdk.Offline` builds a push/pull planner and sync engine on the
abstractions. Browser/WASM consumers get the contracts + REST-over-`HttpClient`
subset only (no native gRPC, local storage engines, or schedulers).

## Directory Layout

```
src/Honua.Sdk*/          One shipped NuGet package each (Abstractions, Grpc, Admin,
                         Processes, Spec, Studio, Field, Geometry, GeoServices,
                         Scenes, OgcFeatures, Catalogs, Offline, + Honua.Sdk umbrella)
tests/                   Per-package *.Tests, plus IntegrationTests, ProtocolIntegration.Tests,
                         BrowserSmoke[.Tests], DemoSuite.Tests, AdminBootstrapConsole.Tests
examples/                Console/worker sample apps (AdminBootstrapConsole is canonical)
docs/                    Per-topic guides + DocFX config (docfx.json, toc.yml); docs/internal/ is contributor-only
contracts/               Golden JSON/protobuf fixtures shared with consuming repos
third_party/geospatial-grpc/  Vendored proto input (source of truth lives in geospatial-grpc)
scripts/                 validate-api-compat.sh
.github/workflows/       ci.yml, codeql.yml, docs.yml, publish-dotnet-sdk.yml, release-please.yml, staging-integration.yml
Honua.Sdk.sln            Solution; Directory.Build.props / Directory.Packages.props at root
```

## Conventions & Gotchas

- **Strict build:** `TreatWarningsAsErrors` is on solution-wide; missing XML doc
  comments on new public members fail the build. Suppress analyzers narrowly via
  `.editorconfig` with a comment, never broadly.
- **API naming:** async methods use the `*Async` suffix and a
  `CancellationToken cancellationToken = default` parameter (not `ct`).
- **Options pattern:** `BaseAddress` (`Uri?`, required, no localhost default),
  `Timeout` validated (10ms <= T < 24h), `EnableRetry` (default true),
  `MaxRetryAttempts` (default 3, throws outside `[2, 5]`).
- **Exceptions:** every SDK exception derives from `Honua.Sdk.Abstractions.HonuaException`;
  config-time failures use `HonuaConfigurationException`, runtime protocol failures
  use the protocol-specific sealed type.
- **Geometry/CRS:** always use NetTopologySuite + ProjNet via `Honua.Sdk.Geometry`
  rather than rolling your own predicates, WKT/WKB parsing, or transforms.
- **Protos:** `.proto` changes go in `geospatial-grpc` first; this repo consumes
  generated/vendored code only. `NU5104` is intentionally suppressed because stable
  1.0 packages depend on the still-prerelease `Geospatial.Grpc 0.1.0-alpha.1`.
- **Integration tests** under `tests/Honua.Sdk.IntegrationTests` and
  `tests/Honua.Sdk.ProtocolIntegration.Tests` are environment-gated and skipped by
  default (see `docs/staging-integration.md`, `docs/protocol-integration-tests.md`).
  Browser smoke needs `HONUA_BROWSER_RUNTIME_SMOKE=true` plus Playwright Chromium.
- **Default branch is `trunk`** (not `main`); CI and docs deploy from it.
- **New package checklist** (per CONTRIBUTING.md): add `src/Honua.Sdk.<X>/README.md`,
  a `tests/Honua.Sdk.<X>.Tests/` project, a CI matrix leg in `ci.yml`, a
  `<None Include="README.md" Pack="true" />` line, and rows in `INSTALL.md` +
  `README.md` + `docs/architecture.md`.

---

## Repo Boundary — Belongs / Does Not Belong Here

This repo owns reusable .NET SDK contracts, service clients, protocol adapters,
serialization formats, and tests. Prefer adding platform-neutral capability here
when code can run without MAUI, DOM APIs, renderer APIs, OS permissions, native
file placement, or app lifecycle hooks.

## Belongs Here

- Provider-neutral feature query/edit contracts, schemas, capabilities, source
  descriptors, geometry, spatial references, attachments, and edit diagnostics.
- NetTopologySuite-backed geometry APIs, ProjNet-backed CRS transforms,
  conversion adapters, geometry validation, planar predicates,
  WKT/WKB/GeoJSON interop, and spatial indexes.
- Protocol clients for gRPC, GeoServices FeatureServer, OGC API Features, WFS,
  admin REST, geocoding, routing, real-time feeds, scene metadata, catalog, and
  replica/offline sync APIs.
- Offline sync contracts, manifests, conflict envelopes, change journals,
  sync orchestration, fake stores, and browser-safe abstractions.
- Scene metadata, scene endpoint resolution, and offline scene package manifest
  parse/validate contracts.
- Field form schemas, field validation, calculated fields, duplicate detection,
  and record workflow rules that do not render UI or acquire device data.
- Non-UI plugin contracts such as manifests, permissions, edition gates,
  compatibility requirements, field validators, data transforms, and workflow
  hooks.
- Golden JSON/protobuf fixtures shared with consuming repos.
- gRPC client and conversion code generated from the canonical
  `geospatial-grpc` definitions.

## Does Not Belong Here

- MAUI controls, WPF/WinUI/Blazor/React components, map or scene controls,
  renderer setup, symbols, labels, graphics overlays, drawing UI, popups, or
  route/navigation display.
- Camera/GPS/media capture, OS permissions, background execution, battery or
  reachability behavior, native AR anchors, native file placement, or app
  lifecycle integration.
- Cesium, MapLibre, deck.gl, WebGL/WebGPU, DOM custom elements, browser cache
  adapters for rendered assets, or display-specific packaging.
- Admin UI page state, MudBlazor form models, demo stubs, or operator workspace
  composition.
- Canonical `.proto` definitions. Those stay in `geospatial-grpc`; this repo may
  consume generated code or pinned/vendored snapshots only for build mechanics.

## Mismatch Checks

- If a new type targets plain `net*` and has no platform dependency, ask why it
  is not in this SDK.
- If code starts implementing geometry predicates, topology, WKT/WKB parsing,
  ring orientation, buffers, simplification, or spatial indexes, use
  NetTopologySuite or a thin adapter over it before writing custom logic.
- If code starts implementing CRS transforms, use ProjNet alongside NTS.
- If code starts implementing geodesic math, use a proven geodesy library or
  server-side calculation; NTS is the planar geometry engine.
- If a client talks to Honua Server over HTTP/gRPC or parses a stable server
  contract, it belongs here unless it is explicitly a UI-only stub.
- If a change requires editing a `.proto`, make the protocol change in
  `geospatial-grpc` first, then update generated consumers here.
- If a feature depends on server work, link the `honua-server` issue in the SDK
  issue before implementation starts.
- If mobile/admin needs different runtime behavior, put the stable contract here
  and put the adapter/runtime issue in the consuming repo.
- Keep migration issues cross-linked with `honua-mobile` and `honua-server-admin`.

## Companion Repos

- `honua-mobile`: mobile runtime, MAUI adapters, native storage adapters,
  permissions, background sync scheduling, mobile UI, AR/VR, and display
  integration.
- `honua-server-admin`: Blazor/MudBlazor operator UI, page composition, local
  stubs, and admin workspace UX.
- `honua-server`: server APIs and backend functionality that SDK clients depend
  on.

## Package Consumption

Sibling repos should consume this SDK through published, versioned
`Honua.Sdk.*` NuGet packages. Do not copy SDK source into consuming repos. Avoid
long-lived sibling `ProjectReference` links; if a local project reference is
used for short-lived development, document the removal path and replace it with
NuGet before release/merge.

## Shared dev-environment rules (multi-agent WSL)

This machine runs many agents concurrently (**Codex + Claude**, often via agentflow with multiple tabs/agents). To prevent host lockups and lost work, every agent MUST follow these:

1. **Heavy builds/tests are throttled by a shared lock.** `dotnet` and `npm` are PATH-shimmed, so their build/test/publish/pack and ci/install/test/run-build/run-test subcommands automatically run under a global semaphore (default 1 concurrent, `HONUA_BUILD_SLOTS`). For other heavy tools, call the wrapper explicitly: `with-build-lock pytest ...`, `with-build-lock cargo build`, `with-build-lock make build`. The lock is shared across ALL of this user's processes (every Codex/Claude tab, agentflow children). Do not bypass it for compiles or test suites. Long-running servers (`dotnet run`, `npm run dev`) are intentionally NOT locked — never wrap those.

2. **Commit and push when you finish a task** so your worktree can be reclaimed. An hourly job (`honua-clean`) removes a worktree ONLY when it is clean AND fully pushed (merged, remote-gone, or idle >=2d). Dirty or unpushed worktrees are NEVER touched — but uncommitted/unpushed work blocks reclamation and is at risk if the instance is reset. Build artifacts (bin/obj and untracked node_modules) are reclaimed automatically and safely.

3. **Commit hygiene — no agent attribution.** Author every commit as the repo owner only (git identity: Mike McDougall <mike@honua.io>). Do **NOT** add any agent/tool attribution to commits: no `Co-Authored-By: Claude ...`, no `Co-Authored-By: Codex ...` (or other bot co-authors), and no "Generated with Claude Code" / "Generated with Codex" / "🤖" lines in the message or PR body. Write a plain, descriptive commit message and stop.
