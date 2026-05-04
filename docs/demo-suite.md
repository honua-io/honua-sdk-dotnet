# .NET Demo Suite

Issue `honua-sdk-dotnet#128` tracks a .NET-first demo suite that shows how the
SDK packages fit operator, automation, worker, routing, geofence, and offline
contract workflows.

## Current Suite

| Workflow | Owner package(s) | Demo asset | Notes |
|----------|------------------|------------|-------|
| Operator/admin bootstrap | `Honua.Sdk.Admin`, `Honua.Sdk.Grpc` | `examples/AdminBootstrapConsole` | Canonical sample. It should stay focused on compatibility, connection reuse, layer publish, protocol enablement, and bounded gRPC verification. |
| Spec plan/apply | `Honua.Sdk.Spec` | `examples/SpecPlanApplyConsole` | Runnable scaffold. Uses a deterministic in-process Spec API by default and can target a real Honua Server with env vars. |
| Realtime worker | `Honua.Sdk.Abstractions` | Planned `examples/RealtimeWorker` | Blocked on concrete server realtime transport. The first worker should process `FeatureStreamEvent` envelopes through SDK buffering with simulated insert/update/delete events as a fallback. |
| Routing/geofence | `Honua.Sdk.Abstractions`, `Honua.Sdk.GeoServices`, `Honua.Sdk.Geometry` | Planned `examples/RoutingGeofenceConsole` | Routing needs a configured GeoServices/NAServer endpoint; geofence evaluation can be deterministic with local NTS geometry fixtures. |
| Mobile offline boundary | `Honua.Sdk.Offline.Abstractions`, `Honua.Sdk.Offline` | Docs and contract tests | SDK owns portable offline manifests, journals, checkpoints, conflicts, adapters, planners, and `OfflineSyncEngine`. `honua-mobile` owns native GeoPackage storage and mobile runtime. |

## Cloud Configuration

Use environment variables for live deployments:

```bash
export HONUA_SPEC_MODE=server
export HONUA_SPEC_SERVER_URL=https://your-honua.example
export HONUA_SPEC_API_KEY=
export HONUA_SPEC_BEARER_TOKEN=
```

Samples must not require checked-in credentials. API keys and bearer tokens may
be sent to HTTPS targets or loopback HTTP used for local development. Non-local
plain HTTP should remain a configuration error for authenticated samples.

## Realtime Worker Plan

The realtime worker demo should wait for server realtime support before
claiming live coverage. Until then, the scaffold should:

- Use `IHonuaFeatureStreamClient`-shaped code and SDK stream envelopes.
- Feed deterministic simulated events through `FeatureStreamEventProcessor`.
- Show reconnect cursor handling and duplicate/stale event rejection.
- Keep host behavior limited to console logging or an in-memory projection.
- Avoid server, mobile, browser, map-display, and notification concerns.

Live validation should be added once Honua Server exposes the negotiated
subscription endpoint and authentication requirements.

## Routing And Geofence Plan

The routing/geofence demo should be split so one half is deterministic and one
half is live-configured:

- Geofence: build NTS polygons and position samples in a projected spatial
  reference, evaluate enter/exit/approach/depart transitions, and print a
  stable event table.
- Routing: register `IHonuaRoutingClient` through `Honua.Sdk.GeoServices` and
  solve a small route only when a configured NAServer-compatible service is
  available.
- Validation: always run geofence evaluation locally; skip or clearly fail the
  routing live path when the route service variables are absent.

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
