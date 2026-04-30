# Browser And WebAssembly Support

This matrix defines what `honua-sdk-dotnet` considers browser-safe today. The
line is intentionally conservative: DTOs, abstractions, and REST clients that
run through the host's browser `HttpClient` are candidates; native transport,
local storage engines, background schedulers, certificate stores, and display
rendering stay in host applications or downstream adapter packages.

## Support Matrix

| Package or capability | Browser/WASM status | Notes |
| --- | --- | --- |
| `Honua.Sdk.Abstractions` | Supported | Pure contracts and source/query/edit abstractions. No transport, storage, or native runtime dependency. |
| `Honua.Sdk.Offline.Abstractions` | Supported | Offline manifests, checkpoints, sync state, change journals, storage interfaces, and conflict contracts only. |
| `Honua.Sdk.Offline` | Conditional | Planner and sync engine are portable, but the host must provide browser-safe storage, scheduling, conflict-review, and auth adapters. This package is not a GeoPackage, SQLite, service-worker, or background-sync implementation. |
| `Honua.Sdk.Admin` | Candidate | REST client over injected `HttpClient`. Browser hosts must use same-origin/BFF credentials or delegated bearer tokens, and the server must allow the required CORS policy when cross-origin. Static privileged admin API keys must not be shipped in browser config. |
| Geocoding client in `Honua.Sdk.Admin` | Candidate | Same REST, CORS, and browser credential requirements as `Honua.Sdk.Admin`. |
| `Honua.Sdk.Spec` | Candidate | REST validation/plan/cancel paths are browser candidates. Apply streaming uses SSE-style responses and still needs runtime validation under Blazor WebAssembly before being called supported. |
| `Honua.Sdk.Wfs` | Candidate | REST/XML/GeoJSON client over browser `HttpClient`; requires server CORS and browser-owned auth. |
| `Honua.Sdk.GeoServices` | Candidate | REST/JSON FeatureServer client over browser `HttpClient`; requires server CORS and browser-owned auth. |
| `Honua.Sdk.OgcFeatures` | Candidate | REST/JSON OGC API Features client over browser `HttpClient`; requires server CORS and browser-owned auth. |
| `Honua.Sdk.Grpc` | Not supported for browser runtime | Native gRPC/HTTP2 is not a supported browser path. Treat current browser builds as compile-only. Add gRPC-Web support only after `honua-server` exposes a compatible endpoint and the SDK has a browser-specific transport plan. |
| Routing, scene metadata, realtime feeds, field forms, plugins | Not implemented | Future packages should split pure contracts from runtime adapters and update this matrix before browser consumption. |
| Display/maps | Out of SDK core | MapLibre/deck.gl, Cesium, Mapsui, renderer caches, controls, and AR/VR anchors belong in viewer/mobile/admin apps or display adapter packages. SDK packages should hand back portable data/contracts. |

## Explicit Browser Exclusions

Do not add these directly to browser-safe SDK packages:

- GeoPackage or SQLite engines;
- direct filesystem or OS secure-store access;
- native background schedulers, MAUI lifecycle hooks, or platform services;
- client certificate authentication or OS certificate-store discovery;
- raw sockets or native gRPC/HTTP2 browser assumptions;
- map rendering, scene rendering, or display state.

Browser implementations can still use SDK contracts by supplying their own
adapters, such as IndexedDB storage behind `IOfflineFeatureStore` or a BFF that
injects privileged admin credentials server-side.

## Validation Gates

The SDK keeps a Blazor WebAssembly compile smoke in
`tests/Honua.Sdk.BrowserSmoke`. It references the supported/candidate browser
packages and registers the REST clients with retry disabled. That gate proves
the packages can be consumed by a browser app without native compile-time
dependencies.

Runtime validation still belongs to the consuming app or a follow-up SDK sample:

- browser `HttpClient` requests against a real or fake Honua server;
- cross-origin CORS behavior where the deployment is not same-origin;
- delegated bearer-token or BFF authentication;
- `Honua.Sdk.Spec` apply-stream behavior;
- offline storage adapters such as IndexedDB.
