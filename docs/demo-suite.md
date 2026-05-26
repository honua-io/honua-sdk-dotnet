# .NET Demo Suite

Issue `honua-sdk-dotnet#128` tracks a .NET-first demo suite that shows how the
SDK packages fit operator, automation, worker, routing, geofence, and offline
contract workflows.

## Current Suite

| Workflow | Owner package(s) | Demo asset | Notes |
|----------|------------------|------------|-------|
| Operator/admin bootstrap | `Honua.Sdk.Admin`, `Honua.Sdk.Grpc` | `examples/AdminBootstrapConsole` | Canonical sample. It should stay focused on compatibility, connection reuse, layer publish, protocol enablement, and bounded gRPC verification. |
| Spec plan/apply | `Honua.Sdk.Spec` | `examples/SpecPlanApplyConsole` | Runnable scaffold. Uses a deterministic in-process Spec API by default and can target a real Honua Server with env vars. |
| Studio analysis reports | `Honua.Sdk.Studio`, `Honua.Sdk.Abstractions` | `examples/StudioAnalysisReportConsole` | Runnable scaffold. Retrieves the structured report envelope, walks polymorphic sections, and renders Markdown with a deterministic in-process fallback. |
| Realtime worker | `Honua.Sdk.Abstractions` | `examples/RealtimeWorker` | Runnable deterministic worker. Server realtime remains gated; the sample fails fast in server mode and uses `FeatureStreamEvent` buffering with simulated insert/update/delete events by default. |
| Routing/geofence | `Honua.Sdk.Abstractions`, `Honua.Sdk.GeoServices`, `Honua.Sdk.Geometry` | `examples/RoutingGeofenceConsole` | Runnable deterministic geofence fixture with simulated route output by default. Live routing can target a configured GeoServices/NAServer route layer. |
| Mobile offline boundary | `Honua.Sdk.Offline.Abstractions`, `Honua.Sdk.Offline` | Docs and contract tests | SDK owns portable offline manifests, journals, checkpoints, conflicts, adapters, planners, and `OfflineSyncEngine`. `honua-mobile` owns native GeoPackage storage and mobile runtime. |

## Cloud Configuration

Use environment variables for live deployments:

```bash
export HONUA_SPEC_MODE=server
export HONUA_SPEC_SERVER_URL=https://your-honua.example
export HONUA_SPEC_API_KEY=
export HONUA_SPEC_BEARER_TOKEN=
```

Studio report live mode uses its own variables:

```bash
export HONUA_STUDIO_MODE=server
export HONUA_STUDIO_SERVER_URL=https://your-honua.example
export HONUA_STUDIO_JOB_ID=job-id-with-completed-report
export HONUA_STUDIO_API_KEY=
export HONUA_STUDIO_BEARER_TOKEN=
```

Routing live mode uses parallel variables:

```bash
export HONUA_ROUTE_MODE=server
export HONUA_ROUTE_SERVER_URL=https://your-honua.example
export HONUA_ROUTE_API_KEY=
export HONUA_ROUTE_BEARER_TOKEN=
export HONUA_ROUTE_SERVICE_ID=Routing
export HONUA_ROUTE_ROUTE_LAYER=Route
```

Samples must not require checked-in credentials. API keys and bearer tokens may
be sent to HTTPS targets or loopback HTTP used for local development. Non-local
plain HTTP should remain a configuration error for authenticated samples.

## Smoke Validation

Use these local smoke commands for the CI-friendly paths:

```bash
dotnet build examples/AdminBootstrapConsole/AdminBootstrapConsole.csproj
dotnet run --project examples/SpecPlanApplyConsole/SpecPlanApplyConsole.csproj
dotnet run --project examples/StudioAnalysisReportConsole/StudioAnalysisReportConsole.csproj
dotnet run --project examples/RealtimeWorker/RealtimeWorker.csproj
dotnet run --project examples/RoutingGeofenceConsole/RoutingGeofenceConsole.csproj
dotnet test tests/AdminBootstrapConsole.Tests/AdminBootstrapConsole.Tests.csproj
dotnet test tests/DemoSuite.Tests/DemoSuite.Tests.csproj
```

`AdminBootstrapConsole` requires a local or cloud Honua Server plus PostGIS
configuration for a full operator bootstrap run. `SpecPlanApplyConsole`,
`StudioAnalysisReportConsole`, `RealtimeWorker`, and
`RoutingGeofenceConsole` have deterministic default paths that run without live
services.

## Realtime Worker

The realtime worker waits for server realtime support before claiming live
coverage. Until then, the runnable scaffold:

- Uses SDK stream envelopes and a subscription descriptor.
- Feeds deterministic simulated events through `FeatureStreamEventProcessor`.
- Shows resume cursor handling and duplicate/stale event rejection.
- Keep host behavior limited to console logging or an in-memory projection.
- Avoid server, mobile, browser, map-display, and notification concerns.

Live validation should be added once Honua Server exposes the negotiated
subscription endpoint and authentication requirements.

## Routing And Geofence

The routing/geofence demo is split so one half is deterministic and one half is
live-configured:

- Geofence: build NTS polygons and position samples in a projected spatial
  reference, evaluate enter/exit/approach/depart transitions, and print a
  stable event table.
- Routing: use a deterministic `IHonuaRoutingClient` fixture by default, or
  configure `Honua.Sdk.GeoServices.Routing.HonuaRoutingClient` for a
  NAServer-compatible route layer with `HONUA_ROUTE_MODE=server`.
- Validation: always run geofence evaluation locally; live routing fails with a
  clear configuration or server error when the route service is absent.

## Mobile Offline Boundary

Offline is not missing from .NET. The SDK owns the portable contract and sync
engine layer:

- `Honua.Sdk.Offline.Abstractions` owns manifests, source descriptors, sync
  state, checkpoints, retry cursors, change journal entries, conflict records,
  and storage adapter contracts.
- `Honua.Sdk.Offline` owns the provider-neutral planner and
  `Honua.Sdk.Offline.OfflineSyncEngine`.
- `honua-mobile` owns device reachability, background execution, native
  SQLite/GeoPackage placement and lifecycle, mobile storage adapters, map
  display, and user-facing conflict workflows.

The .NET demo suite should show the portable contracts and deterministic sync
adapter behavior. It should link to mobile docs for native runtime validation
instead of presenting GeoPackage support as absent from the platform.
