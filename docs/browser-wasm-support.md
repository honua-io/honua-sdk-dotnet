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
| `Honua.Sdk.Geometry` | Candidate | NetTopologySuite and ProjNET-backed geometry, CRS, transform helpers, and generated Geospatial gRPC geometry conversion. Pure managed compile smoke is covered, but host apps should still validate their projection set and payload sizes under their browser runtime. No display, map-rendering, or gRPC transport responsibility. |
| `Honua.Sdk.Offline.Abstractions` | Supported | Offline manifests, checkpoints, sync state, change journals, storage interfaces, and conflict contracts only. |
| `Honua.Sdk.Offline` | Conditional | Planner and sync engine are portable, but the host must provide browser-safe storage, scheduling, conflict-review, and auth adapters. This package is not a GeoPackage, SQLite, service-worker, or background-sync implementation. |
| `Honua.Sdk.Admin` | Candidate | REST client over injected `HttpClient`. Browser hosts must use same-origin/BFF credentials or delegated bearer tokens, and the server must allow the required CORS policy when cross-origin. Static privileged admin API keys must not be shipped in browser config. |
| Geocoding client in `Honua.Sdk.Admin` | Candidate | Same REST, CORS, and browser credential requirements as `Honua.Sdk.Admin`. |
| `Honua.Sdk.Spec` | Candidate | REST validation/plan/cancel paths are browser candidates. Apply streaming uses SSE-style responses and still needs runtime validation under Blazor WebAssembly before being called supported. |
| `Honua.Sdk.Wfs` | Candidate | REST/XML/GeoJSON client over browser `HttpClient`; requires server CORS and browser-owned auth. |
| `Honua.Sdk.GeoServices` | Candidate | REST/JSON FeatureServer client over browser `HttpClient`; requires server CORS and browser-owned auth. |
| Routing client in `Honua.Sdk.GeoServices` | Candidate | REST/JSON NAServer client over browser `HttpClient`; requires server CORS and browser-owned auth. Host apps own current-location acquisition, route display, and map interaction. |
| `Honua.Sdk.Scenes` | Candidate | REST/JSON scene metadata client over browser `HttpClient`; requires server CORS and browser-owned auth. Display, Cesium, WebGL/WebGPU, and renderer caches remain outside the SDK. |
| `Honua.Sdk.Field` | Candidate | Pure form, validation, calculated field, duplicate detection, and record workflow contracts. Browser hosts own rendering, storage, device capture, local media handling, and any map display. |
| `Honua.Sdk.OgcFeatures` | Candidate | REST/JSON OGC API Features client over browser `HttpClient`; requires server CORS and browser-owned auth. |
| `Honua.Sdk.Grpc` | Not supported for browser runtime | Native gRPC/HTTP2 is not a supported browser path. Treat current browser builds as compile-only. Add gRPC-Web support only after `honua-server` exposes a compatible endpoint and the SDK has a browser-specific transport plan. |
| Realtime feed contracts | Candidate | `IHonuaFeatureStreamClient`, normalized event envelopes, bounded buffers, and duplicate/stale sequence processors are pure contracts. Browser hosts still own the SSE/WebSocket/gRPC-Web adapter and auth/CORS behavior. |
| Plugin contracts | Supported | `HonuaPluginManifest` parsing, validation, compatibility metadata, permissions, safe configuration envelopes, and non-UI extension declarations are pure DTO/validator contracts. Browser hosts own plugin loading, sandboxing, UI composition, and execution. |
| Utility network trace contracts | Supported | `IHonuaUtilityNetworkTraceClient`, trace requests/results, elements, associations, terminals, barriers, and named configurations are pure contracts. Browser hosts own transport adapters and trace-result display. |
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
dependencies. It also compile-checks pure contracts for field records, offline
manifests, plugin manifests, realtime stream envelopes, and utility-network
trace requests.

The smoke app also contains a compile-checked browser feature-map sample. It
uses `IHonuaOgcFeaturesClient` to query a GeoJSON feature collection, uses
`IHonuaGeocodingClient` to forward-geocode an address, and hands both to an
injected `IBrowserGeoJsonDisplayAdapter`. The adapter is intentionally a no-op
in this repository: MapLibre/deck.gl, Cesium, Mapsui, canvas/WebGL lifecycle,
and picking controls remain in viewer packages or host apps.

Live runtime validation still belongs to the consuming app or a follow-up SDK
sample wired to a test Honua deployment:

- browser `HttpClient` requests against a real or fake Honua server;
- cross-origin CORS behavior where the deployment is not same-origin;
- delegated bearer-token or BFF authentication;
- `Honua.Sdk.Spec` apply-stream behavior;
- offline storage adapters such as IndexedDB.

This live validation remains blocked on the shared Honua Server integration
test substrate tracked in
<https://github.com/honua-io/honua-server/issues/813>. Until that exists, this
repo's CI can prove browser compilation and host-boundary discipline, but not
deployment-specific CORS or auth behavior.

Minimum browser host configuration for live REST validation:

- use same-origin routes or configure CORS for the SDK REST origins;
- allow `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, and `OPTIONS` as required by
  the selected SDK clients;
- allow `Authorization` and content headers used by the host auth flow;
- never place privileged admin API keys or server-only connection secrets in
  static browser configuration.

## Browser gRPC Plan

Browser consumers should not register `Honua.Sdk.Grpc` as a runtime transport.
Feature reads and edits should route through OGC API Features or GeoServices
REST clients until `honua-server` exposes a gRPC-Web endpoint and the SDK has a
browser-specific transport package. That future package should be separate from
the native gRPC package so browser apps do not accidentally assume raw HTTP/2
channel support.
