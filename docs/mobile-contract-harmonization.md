# Mobile Contract Harmonization

Issue #68 aligns `honua-sdk-dotnet` and `honua-mobile` around one contract
vocabulary for reusable clients and host-neutral DTOs. The SDK-side baseline
fixture is `contracts/fixtures/mobile-sdk-contract-harmonization.v1.json`.

The fixture is plain JSON by design. Mobile, SDK, server, and admin repos can
read the same ownership map without referencing each other's assemblies.

## Compatibility Baseline

| Mobile baseline | Shared SDK baseline | Status |
|-----------------|---------------------|--------|
| `honua-mobile` source packages from `main` after `honua-mobile#68` | `Honua.Sdk.*` `0.1.6-alpha.1` | Fixture-level compatibility for shared feature, source, edit, attachment, routing, scene, and offline contracts |

`honua-mobile` does not currently publish versioned NuGet packages. Until it
does, compatibility is stated as source-baseline compatibility against the
published `Honua.Sdk.*` package versions in the fixture.

## Ownership Summary

| Model family | Owner | Mobile disposition |
|--------------|-------|--------------------|
| Feature query requests/results | `Honua.Sdk.Abstractions` | Mobile DTOs become transport shims or adapters. |
| Feature edit envelopes/results | `Honua.Sdk.Abstractions` | Mobile edit DTOs and queued offline payloads map to SDK edit contracts. |
| Feature attachment requests/results | `Honua.Sdk.Abstractions` | Mobile delegates through SDK attachment clients and keeps only runtime adapter behavior. |
| Geometry and spatial references | Split pending SDK geometry contracts | Keep platform coordinates at mobile edges; use NetTopologySuite and ProjNet rather than custom geometry engines where possible. |
| Offline sync state, journals, conflicts | `Honua.Sdk.Offline.Abstractions` plus mobile runtime adapters | Mobile owns native queues, GeoPackage persistence, scheduling, and background execution. |
| Form-related feature schemas | `Honua.Sdk.Abstractions` now; pending field package for workflow contracts | Mobile owns form rendering, validation UX, capture workflow, and device media handling. |
| Scene metadata and offline scene packages | `Honua.Sdk.Abstractions.Scenes` plus `Honua.Sdk.Scenes` client | Mobile and embed own renderers, caches, downloads, file placement, and display lifecycle. |
| Routing and network analysis | `Honua.Sdk.Abstractions` contracts plus `Honua.Sdk.GeoServices` NAServer client | Mobile owns device location providers, platform permission flows, route display, and map interaction. |
| GeoPackage sync and native storage | `honua-mobile` | SDK describes portable manifests and journals only. |
| Display/embed maps | `honua-mobile` / `Honua.Embed` | SDK returns portable contracts only; MapLibre, deck.gl, Cesium, Mapsui, WebGL/WebGPU, and map controls stay outside SDK core. |
| Non-UI plugin contracts | Pending SDK plugin contracts after server dependency | Hosts own runtime loading, UI registration, sandboxing, and signing. |
| Legacy `honua-mobile-sdk` contracts | Quarantine | Migrate concepts only after the fixture assigns ownership. |

## Migration Rules

- New provider-neutral feature read code targets
  `Honua.Sdk.Abstractions.Features.FeatureQueryRequest`,
  `FeatureQueryResult`, `FeatureSource`, `SourceDescriptor`, and `SourceQuery`.
- New provider-neutral feature edit code targets `FeatureEditRequest`,
  `FeatureEditResponse`, and related edit result models.
- New provider-neutral feature attachment code targets
  `IHonuaFeatureAttachmentClient` and the `FeatureAttachment*` request/result
  contracts.
- New provider-neutral routing code targets
  `Honua.Sdk.Abstractions.Routing.IHonuaRoutingClient` and the
  `Route*`/`ServiceArea*`/`ClosestFacility*` contracts.
- New scene discovery, endpoint resolution, access envelope, and offline scene
  package manifest code targets `Honua.Sdk.Abstractions.Scenes` contracts and
  the `Honua.Sdk.Scenes` client.
- New portable offline code targets `Honua.Sdk.Offline.Abstractions` for
  manifests, source descriptors, change journal entries, checkpoints, retry
  checkpoints, and conflict envelopes.
- Sibling repos consume SDK contracts through published NuGet packages from
  GitHub Packages. Do not copy SDK source and do not add long-lived project
  references.
- Canonical `.proto` definitions stay in `geospatial-grpc`; SDK and mobile
  consume generated or published protocol bindings instead of redefining them.
- Mobile-only APIs may keep MAUI, camera, GPS/location provider, native storage,
  GeoPackage, background execution, route-location-provider, and display
  concerns.
- Any migrated SDK contract that requires backend behavior must link the
  corresponding `honua-server` dependency issue before implementation starts.

## Follow-Up Work

- `honua-mobile#54` replaces `Honua.Mobile.Sdk` service clients with published
  `Honua.Sdk.*` packages.
- `honua-mobile#49` maps current mobile offline queues and GeoPackage state to
  SDK offline contracts while keeping runtime storage in mobile.
- `honua-sdk-dotnet#55` defines geometry ownership and should lean on
  NetTopologySuite and ProjNet rather than custom geometry/projection code.
- `honua-mobile#55` should replace local scene metadata and package manifest
  models with the published SDK scene contracts while keeping renderer and
  package-download runtime behavior in mobile/embed.
- `honua-sdk-dotnet#71` and `#72` graduate field and plugin contracts into SDK
  packages after linked server dependencies are ready.
