# Repository Guidance

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
