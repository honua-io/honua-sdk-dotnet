# SDK Capability Backlog

This backlog maps Honua .NET SDK gaps against the capability areas exposed by
ArcGIS Maps SDK for .NET 300.x, while deliberately excluding UI, map display,
scene display, renderer, labeling, and toolkit component work from the core SDK
scope.

Cross-repo implementation order is tracked in
[`cross-repo-sdk-sequencing.md`](cross-repo-sdk-sequencing.md).

Reference capability areas:

- ArcGIS .NET key features:
  https://developers.arcgis.com/net/key-features/
- Query:
  https://developers.arcgis.com/net/query/
- Edit features:
  https://developers.arcgis.com/net/edit-features/
- Geometry and spatial reference:
  https://developers.arcgis.com/net/geometry-and-spatial-reference/
- Offline maps, scenes, and data:
  https://developers.arcgis.com/net/offline-maps-scenes-and-data/
- Geocode and search:
  https://developers.arcgis.com/net/geocode-and-search/
- Route and directions:
  https://developers.arcgis.com/net/route-and-directions/
- Real-time:
  https://developers.arcgis.com/net/real-time/
- Portal metadata and search:
  https://developers.arcgis.com/net/arcgis-organization-portals/portal-metadata-and-search/
- Security and authentication:
  https://developers.arcgis.com/net/security-and-authentication/

## Scope Gate

In scope for this SDK:

- Typed service clients and provider-neutral contracts.
- Query, edit, schema, metadata, auth, offline, geocoding, routing, real-time,
  geometry, and analysis data APIs.
- Data packages and adapters that make rendering clients easier to build.

Out of scope for this SDK:

- Map or scene controls.
- Basemap display, graphics overlays, 3D scene rendering, symbols, labels,
  clustering UI, popups, draw tools, and feature form UI.
- Platform UI components for WPF, WinUI, MAUI, Blazor, React, or native mobile.

If display work is needed, keep it in a separate viewer package or application
that consumes SDK data contracts.

## Cross-Repo Harmonization

Local sibling repositories already cover adjacent product surfaces:

- `/home/makani/honua-mobile` currently contains both mobile runtime work and
  reusable SDK code: transport clients, field collection rules, offline
  GeoPackage sync, MAUI integration, background sync, routing, scene metadata,
  and an `@honua/embed` web component surface. Reusable service clients and
  contracts should graduate into this SDK; mobile should consume them.
- `/home/makani/honua-mobile-sdk` appears to be an older MAUI/native-map SDK
  line with `Honua.Mobile.Core`, `Honua.Mobile`, and a field collection example.
- `/home/makani/honua-server-admin` is the Blazor WebAssembly operator UI for
  Honua Server. It owns dashboards, connections, publishing, service settings,
  deploy control, observability, identity, license, spatial SQL, annotations,
  print/export, and other operator workspaces. It also contains in-repo
  WASM-safe HTTP clients, stubs, and DTOs for admin REST endpoints. Current
  inspection shows it does not reference `Honua.Sdk.Admin`; it registers a
  hand-rolled `IHonuaAdminClient`/`HonuaAdminClient` typed `HttpClient` and
  falls back to `StubHonuaAdminClient` when no server URL is configured.

Coordination rule:

- `honua-sdk-dotnet` should own protocol clients, provider-neutral contracts,
  shared models, source descriptors, schema/capability discovery, routing
  clients, scene metadata/package contracts, offline sync contracts/state
  machines, field validation/workflow rules, non-UI plugin contracts, and
  serialization formats. `Honua.Sdk.Admin` should be the reusable typed client
  for stable admin REST operations.
- `honua-mobile` should own device integrations, mobile field workflow
  composition, native storage adapters, GeoPackage file placement and lifecycle,
  background sync scheduling, camera/location/media permissions, MAUI
  registration, native/mobile map UI, AR/VR device integration, and display
  package integration.
- `honua-server-admin` should own Blazor/MudBlazor UI state, operator page
  composition, local stub data for offline demos, workspace-specific view
  models, and admin-only UX flows. When an admin API becomes stable and useful
  outside that UI, its DTOs and HTTP calls should graduate into
  `Honua.Sdk.Admin` or a clearly named admin contract package.
- `geospatial-grpc` owns canonical `.proto` definitions. The SDK and server may
  consume generated bindings or pinned snapshots for build mechanics, but new
  services, fields, enum values, and wire-contract changes must be made in
  `geospatial-grpc` first and then synced into consuming repos.
- Sibling repos should consume `honua-sdk-dotnet` through published, versioned
  NuGet packages. Avoid copying SDK source or adding long-lived sibling
  `ProjectReference` links; local project references are acceptable only as
  short-lived development scaffolding with a removal issue.
- `@honua/embed` or another viewer package should own browser rendering,
  web-component packaging, MapLibre/deck.gl/Cesium integration, browser cache
  adapters for displayed assets, and app-level display behavior.

Shared contract candidates:

- Source descriptors, layer schema, capabilities, geometry models, spatial
  references, edit envelopes, sync manifests, conflict envelopes, attachment
  metadata, route/geocode request models, scene metadata, scene package
  manifests, field form schemas, field validation results, record workflow
  models, plugin permission manifests, admin API envelopes, connection summaries,
  publishing requests, deploy control models, observability DTOs, identity
  provider DTOs, license DTOs, and service settings DTOs.
- Golden JSON/protobuf fixtures that both repos consume in tests.
- A versioned compatibility matrix so mobile packages can declare the exact
  SDK contract version, admin UI contract version, and server contract version
  they were tested against.

Ownership mismatch checks:

- If a type or service client targets plain `net*` and has no direct dependency
  on MAUI, browser DOM, OS permissions, local file placement, renderer APIs, or
  app lifecycle, default ownership is `honua-sdk-dotnet`.
- If code talks to Honua Server over REST, gRPC, OGC, GeoServices, WFS, routing,
  geocoding, catalog, admin, scene metadata, or replica sync APIs, default
  ownership is `honua-sdk-dotnet`.
- If a task requires editing a `.proto`, file that change in `geospatial-grpc`
  and then update SDK/server generated consumers. Do not make server or SDK
  copies the protocol source of truth.
- If a consuming repo needs an SDK package, add a `PackageReference` to the
  published NuGet package and pin the version tested by that repo. Do not copy
  SDK code into the consuming repo.
- If code manages camera, GPS acquisition, background execution, battery/network
  reachability, native SQLite/GeoPackage file lifecycle, AR anchors, MAUI
  registration, or visible controls, default ownership is `honua-mobile` or a
  viewer package.
- If code is a Blazor/MudBlazor view model, operator workspace state machine,
  local demo stub, or admin page composition, default ownership is
  `honua-server-admin`.
- When a feature spans repos, put the stable contract/client in the SDK and put
  adapter/runtime tasks in the consuming repo. Cross-link both issues.

## Priority 0 -- SDK Parity Foundation

### P0.1 Provider-neutral query parity

Outcome: `Honua.Sdk.Abstractions` can express the query features that users
expect from a mature geospatial SDK without forcing callers into protocol-
specific request types.

Acceptance criteria:

- Add provider-neutral request support for out fields, order by, result offset,
  result count, distinct, return IDs only, return count only, return extent only,
  and time filters.
- Represent spatial filters with explicit geometry, spatial reference, and
  spatial relationship values.
- Add provider-neutral statistics/group-by query shapes where the backing
  provider supports them.
- Update gRPC, GeoServices FeatureServer, OGC Features, and WFS adapters with
  documented capability fallbacks.
- Add tests proving unsupported query facets fail with clear
  `NotSupportedException` messages rather than silently weakening queries.

### P0.2 Layer schema and capability discovery

Outcome: callers can inspect a source before querying or editing it.

Acceptance criteria:

- Add shared models for layer/source schema, field metadata, field aliases,
  nullability, type, length, domains, default values, object ID field, global ID
  field, geometry type, extent, spatial reference, time info, and edit
  capabilities.
- Expose a provider-neutral `GetSchemaAsync` or `GetDescriptorAsync` path from
  each feature source.
- Preserve native metadata access for protocol-specific details.
- Add capability flags for attachments, relationships, statistics, IDs,
  extents, edits, offline, time filters, and spatial relationships.

### P0.3 Editing completeness

Outcome: feature edits have consistent behavior and diagnostics across the SDK.

Acceptance criteria:

- Finish add/update/delete parity across gRPC, GeoServices FeatureServer, and
  OGC Features where the server supports it.
- Add attachment operations: list, download, add, update metadata when
  supported, and delete.
- Add edit validation helpers for required fields, domains, object IDs, geometry
  type, rollback support, and partial failure handling.
- Add optimistic concurrency support where the protocol exposes ETag,
  generation, or version metadata.
- Document exact rollback and partial-failure semantics per provider.

### P0.4 Geometry and spatial reference core

Outcome: the SDK has a stable geometry vocabulary instead of passing most
geometry through loose JSON, with NetTopologySuite as the default geometry
engine rather than a Honua-specific topology implementation.

Acceptance criteria:

- Adopt NetTopologySuite for point, multipoint, line, polygon, envelope,
  geometry collection, validation, predicates, prepared geometries, STRtree
  indexing, ring orientation, WKT, and WKB support.
- Add thin Honua DTOs only where needed for wire compatibility, such as gRPC,
  GeoServices JSON, GeoJSON payloads, source descriptors, and serialized offline
  manifests.
- Add spatial reference models for WKID, WKT, and authority/code pairs.
- Add conversion helpers between NetTopologySuite geometries and GeoJSON, WKT,
  WKB, gRPC geometry, and GeoServices JSON geometry.
- Preserve coordinate dimensionality handling, including XY, XYZ, XYM, and XYZM
  where supported by the backing protocol.
- Use ProjNet alongside NTS for CRS definitions and coordinate transforms; do
  not hand-roll projection transforms.
- Treat geodesic distance/area calculations as a separate explicit behavior and
  back them with an established geodesy library or documented server-side
  support rather than custom formulas.

### P0.5 Offline data and sync foundation

Outcome: field apps can use a reusable SDK-owned offline sync core without
depending on a UI sample or a mobile-only package for sync semantics.

Acceptance criteria:

- Define offline package manifests, source descriptors, sync state, change
  journals, conflict envelopes, and retryable checkpoints.
- Provide provider-neutral sync orchestration over SDK query, schema, editing,
  geometry, and spatial-reference primitives.
- Support bounded download planning by source, extent, where clause, out fields,
  and last-sync token.
- Define local store abstractions for feature caches and change journals, plus a
  deterministic fake store for tests.
- Add sync APIs for push edits, pull server changes, conflict reporting, replay,
  checkpointing, and cancellation.
- Graduate reusable sync engine, replica-sync client contracts, and upload
  result handling from `honua-mobile/src/Honua.Mobile.Offline/Sync`.
- Keep map rendering, offline map display, OS background execution, device
  permissions, camera/location acquisition, MAUI registration, and native file
  placement out of SDK core.

Server dependencies:

- https://github.com/honua-io/honua-server/issues/830
- https://github.com/honua-io/honua-server/issues/831
- https://github.com/honua-io/honua-server/issues/371

### P0.6 Mobile contract harmonization

Outcome: `honua-sdk-dotnet` and `honua-mobile` share one contract vocabulary
instead of evolving duplicate feature, geometry, sync, and schema models.

Acceptance criteria:

- Inventory overlapping models between this repo and `honua-mobile`, including
  gRPC feature queries, edit envelopes, offline sync state, form-related
  feature schemas, scene metadata, routing, and GeoPackage sync.
- Choose authoritative package ownership for each model family.
- Add adapter tests or golden fixtures consumed by both repos.
- Publish a compatibility note that states which mobile package versions are
  validated against which `Honua.Sdk.*` package versions.
- Retire or quarantine older `honua-mobile-sdk` contracts once their useful
  concepts are migrated or superseded.

Migration input paths:

- `honua-mobile/src/Honua.Mobile.Sdk`: feature query/edit REST fallback, gRPC
  transport negotiation, OGC Features helpers, routing client, scene metadata
  client, and scene package models.
- `honua-mobile/src/Honua.Mobile.Offline`: sync contracts, sync state machine,
  replica sync client, operation uploader, GeoPackage abstractions, map-area
  package downloader, and scene package downloader.
- `honua-mobile/src/Honua.Mobile.Field`: form schema, field validation,
  calculated fields, duplicate detection, field record, and workflow rules.
- `honua-mobile/src/Honua.Mobile.Maui/Annotations`: generic coordinate and
  bounds primitives should be replaced with SDK geometry types; drawing and
  annotation layer state remain display/runtime code.
- `honua-mobile/src/Honua.Embed`: browser display, web components, Cesium,
  MapLibre/deck.gl, and browser cache adapters stay outside SDK core, but they
  should consume SDK data contracts through generated or hand-written adapters.

Migration tasks:

- Replace duplicated mobile feature query/edit models with
  `Honua.Sdk.Abstractions`, `Honua.Sdk.Grpc`, `Honua.Sdk.GeoServices`, and
  `Honua.Sdk.OgcFeatures` types.
- Move routing request/response models and NAServer client behavior into the
  SDK routing package, then make mobile current-location routing a thin adapter.
- Move scene metadata, scene endpoint resolution, and scene package manifest
  parse/validate models into a new SDK scene package.
- Move field form schema, field validation, calculated-field evaluation,
  duplicate detection, and record workflow rules into an SDK field/contracts
  package.
- Split offline store contracts by responsibility: edit queue, sync cursor,
  feature cache, map-area catalog, and scene-package catalog.
- Keep native SQLite/GeoPackage implementation details in mobile or separate
  platform-specific storage packages.
- Add golden JSON/protobuf fixtures for each migrated model family before
  deleting mobile duplicates.

### P0.7 Admin contract harmonization

Outcome: `Honua.Sdk.Admin` and `honua-server-admin` share stable admin REST
contracts instead of carrying divergent DTOs and HTTP client implementations.

Acceptance criteria:

- Inventory overlapping DTOs and client methods between `Honua.Sdk.Admin` and
  `honua-server-admin`, including API envelopes, service settings, secure
  connections, layer publishing, deploy control, observability, configuration
  discovery, identity providers, license status, spatial SQL, print/export, and
  data connection workflows.
- Classify each surface as SDK-stable, UI-only, stub-only, or server-gap.
- Move SDK-stable DTOs and source-generated JSON contexts into
  `Honua.Sdk.Admin` or a dedicated `Honua.Sdk.Admin.Contracts` package.
- Keep workspace state machines, MudBlazor form models, local demo stubs,
  operator copy, and page-specific validation inside `honua-server-admin`.
- Replace graduated contracts via versioned `Honua.Sdk.*` NuGet packages, not
  sibling project references or copied SDK source.
- Add contract fixture tests consumed by both repos for every graduated admin
  surface.
- Document the production browser auth boundary: Blazor WebAssembly must not
  ship API keys; production admin deployments need OIDC bearer auth or a
  same-origin BFF that injects credentials server-side.

### P0.8 Mobile SDK client migration

Outcome: `honua-mobile` consumes `honua-sdk-dotnet` clients instead of carrying a
parallel `.NET SDK` under `Honua.Mobile.Sdk`.

Acceptance criteria:

- Inventory every public type in `honua-mobile/src/Honua.Mobile.Sdk`.
- Map feature query/edit types to existing SDK abstractions and protocol
  packages.
- Map gRPC transport/protobuf conversion behavior to `Honua.Sdk.Grpc`.
- Map GeoServices FeatureServer and OGC Features helper behavior to
  `Honua.Sdk.GeoServices` and `Honua.Sdk.OgcFeatures`.
- Map routing behavior to the SDK routing package.
- Map scene metadata and package manifest behavior to the SDK scene package.
- Leave only mobile-specific DI extensions, token provider wiring, current
  location providers, and compatibility shims in `honua-mobile`.
- Consume SDK replacements through versioned `Honua.Sdk.*` NuGet packages.
- Add compile-time or analyzer checks in mobile that prevent new plain
  `net*` service clients from being added under mobile packages without an
  explicit ownership note.

### P0.9 Server transport and proto ownership cleanup

Outcome: `honua-server` keeps server pipelines and endpoint adapters, while
portable clients/contracts move to SDK packages and canonical protobuf
definitions stay in `geospatial-grpc`.

Acceptance criteria:

- Inventory `honua-server/src/Honua.Core/Transport/Clients`,
  `honua-server/src/Honua.Core/Transport/Converters`, and
  `honua-server/src/Honua.Core/Transport/Proto`.
- Move or replace portable feature/form service client abstractions with
  `Honua.Sdk.Grpc` and SDK field/form contracts where the code is useful outside
  the server runtime.
- If server tooling needs SDK client behavior, consume released `Honua.Sdk.*`
  NuGet packages rather than adding sibling project references or copied SDK
  source.
- Keep server-domain-to-canonical-pipeline mapping, authorization, storage,
  query/edit execution, and endpoint handlers in `honua-server`.
- Do not move `.proto` definitions into this SDK. Canonical protocol changes
  belong in `geospatial-grpc`; SDK/server repos should consume generated
  bindings or pinned snapshots.
- Add fixture tests proving SDK gRPC converters and server protocol adapters
  agree on feature, edit, form, spatial reference, and error payloads.

## Priority 1 -- Workflow Clients

### P1.1 Authentication upgrade path

Outcome: the SDK supports production identity flows beyond static API key and
bearer token assignment.

Acceptance criteria:

- Add token refresh abstractions and built-in OIDC/OAuth client-credential and
  authorization-code provider hooks.
- Support per-request scopes/audiences where services require them.
- Add certificate/mTLS configuration hooks for enterprise deployments.
- Provide safe diagnostics that never log secrets.

### P1.2 Portal and catalog discovery

Outcome: apps can discover services, layers, metadata, and saved source
definitions programmatically.

Acceptance criteria:

- Add catalog search/list/detail APIs for services, layers, groups, and saved
  source descriptors exposed by Honua Server.
- Add filtering by service type, tags, owner/namespace, geometry type, and
  capability flags.
- Add pagination and sorting.
- Keep web-map/web-scene display documents out of scope unless they are treated
  strictly as metadata inputs for another renderer.

### P1.3 Geocode and place search parity

Outcome: the existing geocoding client covers the common non-UI search flows.

Acceptance criteria:

- Ensure forward geocode, reverse geocode, suggestions, local/extent-biased
  search, category filters, result attributes, and batch geocode are covered.
- Add provider-neutral request/response models where useful.
- Add tests for malformed candidates, empty suggestions, rate-limit behavior,
  and partial batch failures.

### P1.4 Routing and directions client

Outcome: route solving is available as a service client without tying the SDK to
any map control.

Acceptance criteria:

- Add route solve APIs for point-to-point and multi-stop routing.
- Support travel modes, barriers/restrictions, time windows, route geometry,
  directions, localized instruction text, and stop sequencing where supported.
- Add service metadata discovery for supported travel modes and languages.
- Treat live navigation UI, voice guidance, and route display as downstream app
  concerns.

Server dependencies:

- https://github.com/honua-io/honua-server/issues/366

### P1.5 Real-time feature feeds

Outcome: apps can consume live feature updates through a typed stream contract.

Acceptance criteria:

- Define `IHonuaFeatureStreamClient` for connect, reconnect, heartbeat,
  subscribe, and unsubscribe workflows.
- Normalize insert/update/delete event envelopes with source IDs, feature IDs,
  timestamps, geometry, attributes, and sequence tokens.
- Support backpressure and cancellation.
- Add test coverage for reconnect, duplicate sequence handling, and stale event
  rejection.

Server dependencies:

- https://github.com/honua-io/honua-server/issues/339
- https://github.com/honua-io/honua-server/issues/692

### P1.6 Browser and WebAssembly support

Outcome: browser apps can reuse the parts of the SDK that are safe under the
WebAssembly/browser sandbox.

Acceptance criteria:

- Add a WASM compatibility matrix for each package: Abstractions, Admin, WFS,
  GeoServices, OGC Features, gRPC, offline, routing, and geocoding.
- Keep `Honua.Sdk.Abstractions` browser-safe by avoiding filesystem, sockets,
  native dependencies, reflection-heavy serialization paths, and unsupported
  threading assumptions.
- Validate REST clients in a Blazor WebAssembly test app using browser
  `HttpClient` behavior and CORS-aware server configuration.
- Add a browser-specific gRPC plan. Native gRPC over HTTP/2 is not enough for
  browser clients; use a gRPC-Web adapter/package if Honua Server exposes
  gRPC-Web, otherwise route browser feature calls through REST/OGC/GeoServices.
- Exclude GeoPackage/SQLite, background sync schedulers, certificate auth,
  local filesystem packages, and native MAUI integrations from the browser
  package surface unless a WASM-specific implementation is added.
- Produce a small sample that queries features and geocodes from Blazor
  WebAssembly, then hands GeoJSON to the web display adapter.

### P1.7 Scene metadata and package contracts

Outcome: scene discovery, endpoint resolution, and offline scene package
contracts are reusable SDK capabilities while display remains viewer-owned.

Acceptance criteria:

- Add `Honua.Sdk.Scenes` or equivalent package for scene list/detail/resolve
  service clients.
- Move scene summary, metadata, bounds, endpoint, auth/access, capability, and
  attribution models from `honua-mobile/src/Honua.Mobile.Sdk/Scenes`.
- Move `HonuaScenePackageManifest`, asset, byte budget, LOD, validation, and
  package state models from mobile into the SDK.
- Keep Cesium, MapLibre, deck.gl, WebGL/WebGPU, AR/VR anchoring, and scene
  rendering out of the SDK.
- Provide adapter guidance for `@honua/embed` and MAUI/mobile consumers.
- Add manifest golden fixtures shared with mobile, server packaging, and embed
  tests.

Server dependencies:

- https://github.com/honua-io/honua-server/issues/530
- https://github.com/honua-io/honua-server/issues/837
- https://github.com/honua-io/honua-server/issues/838
- https://github.com/honua-io/honua-server/issues/839
- https://github.com/honua-io/honua-server/issues/840
- https://github.com/honua-io/honua-server/issues/841
- https://github.com/honua-io/honua-server/issues/842
- https://github.com/honua-io/honua-server/issues/843
- https://github.com/honua-io/honua-server/issues/844
- https://github.com/honua-io/honua-server/issues/849

### P1.8 Field form and record workflow contracts

Outcome: field collection rules are reusable outside mobile UI while mobile
continues to own capture screens and device integrations.

Acceptance criteria:

- Add `Honua.Sdk.Field` or equivalent package for form definitions, sections,
  fields, field types, validation rules, visibility rules, validation results,
  calculated fields, duplicate detection, and record workflow state.
- Move reusable code from `honua-mobile/src/Honua.Mobile.Field`.
- Align server form DTOs in `honua-server/src/Honua.Core/Features/Forms` and
  portable form client abstractions in
  `honua-server/src/Honua.Core/Transport/Clients/IFormServiceClient.cs` with the
  SDK field package.
- Align field types and validation with SDK layer schema, domains, editing
  rules, attachment metadata, and geometry/location types.
- Keep form rendering, camera/media capture, local media file paths, mobile
  workflow screens, and MAUI controls in `honua-mobile`.
- Keep canonical form `.proto` definitions in `geospatial-grpc`; SDK/server
  should consume generated bindings or pinned snapshots.
- Add fixture tests proving mobile form definitions, server form payloads, and
  SDK field contracts serialize identically.

## Priority 2 -- Advanced GIS

### P2.1 Client-side geometry analysis

Outcome: common analysis can run locally when server-side analysis is not
needed.

Acceptance criteria:

- Add distance, area, length, centroid, buffer, simplify, intersection,
  containment, overlap, nearest point/vertex, and envelope operations.
- Separate planar from geodesic behavior explicitly.
- Back planar topology operations with NetTopologySuite.
- Use ProjNet for reprojection before planar NTS analysis when a projected CRS
  is required.
- Use established geodesy/server support for true geodesic behavior; NTS remains
  the planar geometry engine, not a replacement for CRS transformation.

### P2.2 Geofencing and location events

Outcome: apps can evaluate enter/exit/proximity events without a map view.

Acceptance criteria:

- Add geofence definitions based on SDK/NTS geometry, buffer distance, and
  source query.
- Add event evaluation APIs for current positions and position streams.
- Use NTS prepared geometries or spatial indexes where repeated geofence
  evaluation benefits from them.
- Keep device sensor acquisition and background permissions in app-specific
  packages.

### P2.3 Advanced editing rules

Outcome: the SDK can surface data-quality rules before or during edits.

Acceptance criteria:

- Expose field domains, contingent values, attribute rules, related feature
  metadata, and relationship-class descriptors where the server supports them.
- Add client validation results that identify field, rule, severity, and
  suggested fix text.
- Add branch/version-aware edit sessions if Honua Server exposes versioning.

### P2.4 Utility network and graph workflows

Outcome: connected-asset workflows have explicit API space without overloading
feature queries.

Acceptance criteria:

- Add models for network elements, associations, terminals, trace parameters,
  trace results, and named trace configurations.
- Add trace client methods for connected, upstream, downstream, and subnetwork
  workflows if supported by the server.
- Treat display of trace results as a downstream viewer concern.

### P2.5 Raster, elevation, and enrichment data clients

Outcome: future data APIs have a clear home without becoming display features.

Acceptance criteria:

- Add service clients for raster metadata, sampled elevation, coverage
  statistics, and enrichment attributes when Honua Server exposes those
  services.
- Keep image rendering, hillshade display, 3D terrain display, and thematic
  styling out of the SDK core.

Server dependencies:

- https://github.com/honua-io/honua-server/issues/521
- https://github.com/honua-io/honua-server/issues/522
- https://github.com/honua-io/honua-server/issues/381
- https://github.com/honua-io/honua-server/issues/374
- https://github.com/honua-io/honua-server/issues/839
- https://github.com/honua-io/honua-server/issues/840

### P2.6 Non-UI plugin contracts

Outcome: plugin extension contracts that are not UI/runtime-specific can be
shared by mobile, web, server, and admin surfaces.

Acceptance criteria:

- Define plugin manifests, declared permissions, edition gates, capability
  flags, compatibility/version requirements, and safe configuration envelopes in
  the SDK or a shared contracts package.
- Define non-UI extension points for field validators, calculated fields, data
  transformation, and workflow hooks.
- Keep MAUI assembly loading, React/Vue component registration, map controls,
  custom renderers, marketplace UX, sandbox runtime isolation, and code signing
  implementation in mobile/web/server hosts.
- Add test fixtures consumed by host repos so plugin manifests validate the same
  way everywhere.

Server dependencies:

- https://github.com/honua-io/honua-server/issues/347

### P2.7 Spec workspace client contracts

Outcome: plan/apply/spec-authoring clients can be reused by admin UI, CLI, and
automation without copying server or admin workspace models.

Acceptance criteria:

- Add SDK contracts or a client package for stable spec plan/apply request,
  response, diagnostic, warning, apply-event, and cancellation payloads once the
  server S1 API stabilizes.
- Treat `honua-server/src/Honua.Core/Features/Spec/Domain` and
  `honua-server/src/Honua.Server/Features/Spec/Models` as server-side source
  inputs, not packages to copy wholesale.
- Treat `honua-server-admin/src/Honua.Admin/Models/SpecWorkspace` and
  `Services/SpecWorkspace` as UI/stub inputs; keep editor state, grounding demo
  stubs, panes, and preview models in admin UI.
- Keep canonical spec/process `.proto` definitions in `geospatial-grpc` if the
  spec service is exposed over gRPC.
- Consume this SDK package from admin/CLI through versioned NuGet packages, not
  sibling project references.

Related dependencies:

- https://github.com/honua-io/honua-server-admin/issues/25
- https://github.com/honua-io/geospatial-grpc/issues/12

## Display And Maps Approach

Display should be a separate package or app layer over the SDK, not a
dependency of the SDK packages. The core SDK should expose renderer-friendly
data contracts:

- GeoJSON feature collections for immediate interoperability.
- Optional binary feature batches for high-volume rendering.
- Stable source descriptors with schema, extent, spatial reference, geometry
  type, query capabilities, and tile/feed URLs.
- Style metadata as plain data when available, without binding to a specific UI
  renderer.

For web display, a pragmatic stack is:

- MapLibre GL JS for base map, camera, vector tile styles, controls, and normal
  map interactions.
- deck.gl for high-volume feature overlays, picking/highlighting, heatmaps,
  paths, polygons, point clouds, temporal animation, and GPU aggregation.
- A thin adapter package that converts `FeatureQueryResult` pages or streaming
  feature events into deck.gl `GeoJsonLayer` data first, then binary attributes
  once volume requires it.

This is a good fit because deck.gl is built for high-performance WebGPU/WebGL2
visualization, accepts GeoJSON through `GeoJsonLayer`, supports picking, and
integrates with basemap providers including MapLibre and ArcGIS. MapLibre should
own cartographic base-map behavior; deck.gl should own data visualization
overlays. Native .NET display can be evaluated separately with MAUI/WPF-specific
packages, but those should consume the same source descriptors and GeoJSON or
binary batches rather than pulling display dependencies into the SDK core.

Mapsui is useful inspiration for native .NET display architecture, especially
its separation between map controls, layers, data providers, projection wrappers,
and renderer packages. The SDK should borrow that separation as data contracts:
provider/source descriptors carry CRS, extent, schema, fetch capability, and
query/filter hints; display packages decide whether to wrap those contracts in
Mapsui, MapLibre/deck.gl, Cesium, or another renderer. Mapsui itself should be
evaluated in `honua-mobile` or a viewer package, not added to core SDK packages.
Relevant design cues:

- NTS-first geometry integration matches the SDK geometry plan.
- Projection should be an explicit provider/source concern, with ProjNet-backed
  transforms before data reaches a renderer.
- Offline rendered tiles can use MBTiles/BruTile-style packaging where the
  target is map display; feature/offline sync data remains SDK-owned and store
  adapter-specific.
- Raster tile reprojection should not be assumed. Serve or package rasters in
  the viewer's target CRS when possible.
- Static map/image export can be a downstream display package capability over
  SDK source descriptors, not a core SDK concern.

WASM deployment should start with browser-safe data and service clients, not the
whole SDK. The first target should be `Honua.Sdk.Abstractions` plus REST-backed
Admin/GeoServices/OGC/WFS/geocoding/routing/scene metadata clients in a Blazor
WebAssembly sample. gRPC should be treated as a separate browser transport
decision because browser networking cannot assume the same channel behavior as
desktop/server .NET. Browser offline sync can consume SDK offline contracts only
after an IndexedDB, Cache Storage, or OPFS storage strategy is designed and
tested; native SQLite/GeoPackage and background scheduling remain
platform-specific adapters.
