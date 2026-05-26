# Honua .NET SDK Examples

This directory carries the .NET demo suite for SDK-owned workflows. Samples are
kept language-fit for .NET console, worker, and service-host scenarios and do
not replace mobile, server, or admin UI demos.

## Demo Suite

| Demo | Target user | Status | Capability | Validation |
|------|-------------|--------|------------|------------|
| [AdminBootstrapConsole](AdminBootstrapConsole/) | Operators and platform engineers | Canonical runnable sample | Operator/admin bootstrap, PostGIS table discovery, layer publish, protocol enablement, bounded gRPC verification | `dotnet run --project examples/AdminBootstrapConsole/AdminBootstrapConsole.csproj` against local Honua Server |
| [SpecPlanApplyConsole](SpecPlanApplyConsole/) | Spec authors and automation engineers | Lightweight runnable scaffold | Spec document construction, plan request, apply SSE stream consumption, deterministic simulated apply fallback | `dotnet run --project examples/SpecPlanApplyConsole/SpecPlanApplyConsole.csproj` |
| [StudioAnalysisReportConsole](StudioAnalysisReportConsole/) | Console / Blazor and MAUI host developers | Lightweight runnable scaffold | Analysis-report retrieve and Markdown render via `IHonuaStudioReportsClient`, polymorphic section walk, deterministic simulated fallback | `dotnet run --project examples/StudioAnalysisReportConsole/StudioAnalysisReportConsole.csproj`; set `HONUA_STUDIO_MODE=server` for live |
| [RealtimeWorker](RealtimeWorker/) | Worker and service developers | Runnable simulated worker; live transport gated | Feature stream subscription envelopes, event buffering, duplicate/stale sequence rejection, resume-token projection | `dotnet run --project examples/RealtimeWorker/RealtimeWorker.csproj`; `HONUA_REALTIME_MODE=server` documents the server dependency |
| [RoutingGeofenceConsole](RoutingGeofenceConsole/) | Routing and operations developers | Runnable deterministic geofence plus simulated routing; live NAServer optional | `IHonuaRoutingClient` route solve plus `HonuaGeofenceEvaluator` enter/exit/approach/depart transitions | `dotnet run --project examples/RoutingGeofenceConsole/RoutingGeofenceConsole.csproj`; set `HONUA_ROUTE_MODE=server` for live routing |
| Mobile offline boundary | Mobile/offline integrators | Documented SDK boundary | Portable offline manifests, journals, checkpoints, conflicts, and `Honua.Sdk.Offline.OfflineSyncEngine` contracts | Contract tests in `tests/Honua.Sdk.Offline.Tests`; native GeoPackage/runtime validation remains in `honua-mobile` |

## Cloud Configuration

Samples use environment variables instead of checked-in secrets:

- `HONUA_*_SERVER_URL` or sample-specific server URL variables for the target
  Honua deployment.
- `HONUA_*_API_KEY` for API-key auth when enabled.
- `HONUA_*_BEARER_TOKEN` for OAuth/OIDC or service-account bearer tokens.
- Loopback HTTP is allowed for local development. Non-local authenticated
  targets should use HTTPS.

Each sample README lists its exact variable names and whether it can run without
a live Honua Server.

## Validation Policy

- Prefer deterministic local scaffolds for CI-friendly demos.
- Keep `AdminBootstrapConsole` canonical for operator bootstrap and avoid
  broadening it into unrelated workflows.
- For server-gated capabilities, document the live path and keep a simulated
  event or fixture fallback so the SDK usage remains runnable.
- Keep native mobile runtime, GeoPackage file lifecycle, background scheduling,
  device permissions, and map display out of this repo's examples.

See [../docs/demo-suite.md](../docs/demo-suite.md) for the issue #128 demo
suite, smoke instructions, and SDK/mobile boundary notes.
