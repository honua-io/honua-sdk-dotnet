# Cross-Repo SDK Sequencing

This is the coordination map for moving reusable Honua client/contracts work
into `honua-sdk-dotnet` while keeping server runtime, admin UI, mobile runtime,
display, and protocol ownership in the right repos.

GitHub issues remain authoritative for acceptance criteria. This document only
defines dependency order and repo ownership.

## Non-Negotiable Rules

- Canonical `.proto` definitions stay in `geospatial-grpc`.
- Sibling repos consume `Honua.Sdk.*` through published, versioned NuGet
  packages.
- Do not copy SDK source into sibling repos.
- Avoid long-lived sibling `ProjectReference` links. Temporary local references
  need an explicit removal issue.
- Server owns runtime pipelines, storage, authorization, and endpoint behavior.
- Admin/mobile/viewer repos own UI, device/runtime adapters, display, local
  stubs, and page/app workflow state.

## Sequence

| Gate | Work | Owner | Blocks |
|------|------|-------|--------|
| 0 | Lock ownership rules in repo guidance and issue bodies | all repos | Every migration task |
| 1 | Canonicalize protobuf source of truth | `geospatial-grpc` | SDK/server gRPC sync |
| 2 | Ensure SDK packages can be published and consumed as NuGet | `honua-sdk-dotnet` | Admin/mobile/server cutovers |
| 3 | Finish SDK foundation contracts: query, schema, edits, geometry, admin, transport | `honua-sdk-dotnet` | Mobile/admin replacement work |
| 4 | Align server transport/proto snapshots with SDK and `geospatial-grpc` | `honua-server`, SDK | gRPC/form/spec consumers |
| 5 | Replace admin REST DTO/client duplicates with `Honua.Sdk.Admin` NuGet packages | `honua-server-admin`, SDK | Admin UI contract drift cleanup |
| 6 | Replace `Honua.Mobile.Sdk` reusable clients with `Honua.Sdk.*` NuGet packages | `honua-mobile`, SDK | Offline, routing, scene, field cutovers |
| 7 | Move reusable offline, field, scene, routing, realtime contracts into SDK packages | SDK plus server deps | Mobile/runtime adapters |
| 8 | Build consuming adapters only after contracts stabilize | mobile/admin/viewers | Native/display/app work |
| 9 | Keep display work outside SDK core | mobile/viewer packages | MapLibre/deck.gl/Mapsui/Cesium implementation |

## Gate Details

### Gate 1: Proto Ownership

Canonical protocol changes start in:

- `geospatial-grpc`: https://github.com/honua-io/geospatial-grpc/issues/12

Consumers then sync generated bindings or pinned snapshots:

- SDK cleanup: https://github.com/honua-io/honua-sdk-dotnet/issues/73
- Server cleanup: https://github.com/honua-io/honua-server/issues/854

Do not make `honua-server/src/Honua.Core/Transport/Proto` or
`honua-sdk-dotnet/third_party/geospatial-grpc` the protocol source of truth.

The protocol repo now has a generated .NET binding package path,
`Geospatial.Grpc`, for canonical `geospatial.v1` messages and service clients.
Downstream .NET repos should cut over to a published package version after the
protocol package is available from GitHub Packages.

### Gate 2: NuGet Consumption

Before sibling repos cut over, SDK packages need published versions that can be
referenced with `PackageReference`.

The generated protocol package is separate from `Honua.Sdk.*`: SDK packages may
depend on `Geospatial.Grpc`, while admin/mobile/server repos should consume
Honua SDK behavior through `Honua.Sdk.*` packages. Both package families use
`https://nuget.pkg.github.com/honua-io/index.json` for prerelease/private
distribution.

Expected consumers:

- `honua-server-admin` consumes `Honua.Sdk.Admin`.
- `honua-mobile` consumes `Honua.Sdk.Abstractions`, `Honua.Sdk.Grpc`,
  `Honua.Sdk.GeoServices`, `Honua.Sdk.Scenes`, `Honua.Sdk.OgcFeatures`,
  `Honua.Sdk.OgcRecords`, `Honua.Sdk.Stac`, and field/offline packages through
  published NuGet versions.
- Server-side tools/tests consume SDK packages only when they need client
  behavior.

### Gate 3: SDK Foundation

Foundation work should land before repo cutovers:

- Query parity: https://github.com/honua-io/honua-sdk-dotnet/issues/52
- Schema/capabilities: https://github.com/honua-io/honua-sdk-dotnet/issues/53
- Edits/attachments: https://github.com/honua-io/honua-sdk-dotnet/issues/54
- Geometry/NTS/ProjNet: https://github.com/honua-io/honua-sdk-dotnet/issues/55
- Admin contracts: https://github.com/honua-io/honua-sdk-dotnet/issues/69
- Server transport alignment: https://github.com/honua-io/honua-sdk-dotnet/issues/73
- Browser/WASM matrix: https://github.com/honua-io/honua-sdk-dotnet/issues/62

The browser/WASM package boundary lives in
[Browser And WebAssembly Support](browser-wasm-support.md). Browser consumers
should treat REST packages as candidates until runtime `HttpClient`, CORS, and
auth validation exists in the consuming app.

The SDK-side server transport inventory and first converter fixture slice lives
in [Server Transport Ownership](server-transport-ownership.md).

### Gate 4: Admin Cutover

Admin should keep Blazor/MudBlazor UI state, local stubs, workspace state
machines, page composition, and operator copy.

Move stable admin API clients/DTOs into SDK and consume them through NuGet:

- SDK side: https://github.com/honua-io/honua-sdk-dotnet/issues/69
- Admin side: https://github.com/honua-io/honua-server-admin/issues/66
- WASM validation: https://github.com/honua-io/honua-server-admin/issues/67

### Gate 5: Mobile Cutover

Mobile should keep MAUI/runtime adapters, device permissions, native storage,
background scheduling, capture UX, AR/VR, and display integration.

Reusable service clients/contracts move to SDK first:

- SDK/mobile harmonization: https://github.com/honua-io/honua-sdk-dotnet/issues/68
- Mobile umbrella: https://github.com/honua-io/honua-mobile/issues/48
- Replace mobile SDK clients: https://github.com/honua-io/honua-mobile/issues/54

The SDK-side ownership map and compatibility baseline live in
[Mobile Contract Harmonization](mobile-contract-harmonization.md) and
`contracts/fixtures/mobile-sdk-contract-harmonization.v1.json`. Keep the SDK
fixture aligned with the mobile fixture before moving shared contracts or
deleting mobile DTO shims.

### Gate 6: Capability Packages

These move after foundation contracts are stable:

- Offline core: https://github.com/honua-io/honua-sdk-dotnet/issues/56
- Mobile offline adapter: https://github.com/honua-io/honua-mobile/issues/49
- Routing: https://github.com/honua-io/honua-sdk-dotnet/issues/60
- Realtime feeds: https://github.com/honua-io/honua-sdk-dotnet/issues/61
- Scene contracts: https://github.com/honua-io/honua-sdk-dotnet/issues/70
- Mobile scene adapter: https://github.com/honua-io/honua-mobile/issues/55
- Field contracts: https://github.com/honua-io/honua-sdk-dotnet/issues/71
- Mobile field adapter: https://github.com/honua-io/honua-mobile/issues/56
- Plugin contracts: https://github.com/honua-io/honua-sdk-dotnet/issues/72
- Spec workspace clients: https://github.com/honua-io/honua-sdk-dotnet/issues/74
  (`Honua.Sdk.Spec` owns the stable REST/SSE DTOs and client; server/admin
  keep runtime and UI state)

### Gate 7: Server Dependencies

SDK/client work that depends on backend behavior must link server issues before
implementation starts.

Current dependency clusters:

- Offline/versioning: `honua-server#830`, `#831`, `#371`
- Routing: `honua-server#366`
- Realtime: `honua-server#339`, `#692`
- Plugin/server extension support: `honua-server#347`
- SDK integration testing: `honua-server#813`
- 3D scenes: `honua-server#530`, `#837`, `#838`, `#839`, `#840`, `#841`,
  `#842`, `#843`, `#844`, `#849`
- Raster/elevation/enrichment: `honua-server#521`, `#522`, `#381`, `#374`,
  `#839`, `#840`

### Gate 8: Display And Native Viewers

Display is downstream of SDK contracts.

- Web display adapter: https://github.com/honua-io/honua-mobile/issues/50
- Native .NET/Mapsui evaluation: https://github.com/honua-io/honua-mobile/issues/57
- Mobile 3D epic: https://github.com/honua-io/honua-mobile/issues/12
- Browser offline 3D cache: https://github.com/honua-io/honua-mobile/issues/42

MapLibre/deck.gl, Cesium, Mapsui, WebGL/WebGPU, AR/VR anchors, renderer caches,
and map controls stay outside SDK core.

## Ready-To-Start Rule

An issue is ready to implement only when:

- its owner repo is clear;
- upstream server/proto/SDK package dependencies are linked;
- required SDK package versions are published or the task is explicitly scoped
  as SDK implementation work;
- `.proto` changes, if any, are filed in `geospatial-grpc`;
- no sibling repo needs to copy source or add a long-lived project reference.

If those are not true, the issue can be planned or scaffolded, but not treated
as implementation-ready.

## Maintenance

Update this document when:

- a cross-repo issue is created or closed;
- an SDK package becomes published and consumable;
- a `.proto` source-of-truth decision changes;
- a mobile/admin/server task starts depending on a different upstream issue.
