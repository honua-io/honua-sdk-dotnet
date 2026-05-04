# Honua .NET SDK Examples

This directory carries the .NET demo suite for SDK-owned workflows. Samples are
kept language-fit for .NET console, worker, and service-host scenarios and do
not replace mobile, server, or admin UI demos.

## Demo Suite

| Demo | Status | Capability | Validation |
|------|--------|------------|------------|
| [AdminBootstrapConsole](AdminBootstrapConsole/) | Canonical runnable sample | Operator/admin bootstrap, PostGIS table discovery, layer publish, protocol enablement, bounded gRPC verification | `dotnet run --project examples/AdminBootstrapConsole/AdminBootstrapConsole.csproj` against local Honua Server |
| [SpecPlanApplyConsole](SpecPlanApplyConsole/) | Lightweight runnable scaffold | Spec document construction, plan request, apply SSE stream consumption, deterministic simulated apply fallback | `dotnet run --project examples/SpecPlanApplyConsole/SpecPlanApplyConsole.csproj` |
| Realtime worker | Planned | `IHonuaFeatureStreamClient` subscription processing, reconnect/buffer behavior, duplicate/stale sequence handling | Gated on Honua Server realtime transport support; use deterministic simulated stream events until server endpoints are ready |
| Routing/geofence | Planned | `IHonuaRoutingClient` route solve plus `HonuaGeofenceEvaluator` enter/exit/approach/depart transitions | Routing requires configured GeoServices/NAServer; geofence evaluation can run offline with NTS fixtures |
| Mobile offline boundary | Documented | Portable offline manifests, journals, checkpoints, conflicts, and `Honua.Sdk.Offline.OfflineSyncEngine` contracts | Contract tests in `tests/Honua.Sdk.Offline.Tests`; native GeoPackage/runtime validation remains in `honua-mobile` |

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

See [../docs/demo-suite.md](../docs/demo-suite.md) for the issue #128 rollout
plan and remaining work.
