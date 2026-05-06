# Spike: Unreal Integration Path For Honua Scenes And .NET Tooling

## Recommendation

Use `.NET` for scene discovery, validation, offline-package inspection, and
export/build tooling. Use Unreal for runtime interaction, input, physics,
selection UI, and native HTTP/OpenAPI calls. Use Cesium for Unreal as the first
Unreal rendering path for Honua 3D Tiles, terrain, imagery overlays, and
feature metadata.

Do not embed .NET into the Unreal runtime for the first integration. A runtime
bridge would add deployment, memory ownership, threading, platform packaging,
and debugging complexity without proving a better scene experience. The SDK
already has the right neutral contracts in `Honua.Sdk.Scenes`; Unreal should
consume those contracts over HTTP or generated native bindings rather than
loading the .NET assemblies in-process.

The first 3D construction demo remains web/CesiumJS-first. Unreal is a follow-up
evaluation path for immersive walkthroughs, simulation, and sales/operations
showcases after the server construction scene fixture is stable.

## Architecture

```text
Honua Server scene registry
  -> scene list/detail/resolve JSON
  -> 3D Tiles, terrain, imagery, and metadata endpoints
  -> optional offline scene package manifest

Honua.Sdk.Scenes (.NET)
  -> CLI/tool validates scene metadata and package manifests
  -> CLI/tool resolves demo scene and writes an Unreal handoff manifest
  -> optional package builder/export helper after server packaging matures

Unreal runtime
  -> Cesium for Unreal loads tileset, terrain, and imagery URLs
  -> native Unreal code or generated OpenAPI bindings call Honua APIs
  -> Blueprint/C++ selection layer reads feature metadata and resolves Honua IDs
  -> Unreal owns camera, controls, UI, physics, packaged assets, and local cache
```

## Path Comparison

| Path | Fit | .NET role | Unreal role | Recommendation |
|------|-----|-----------|-------------|----------------|
| Live scene manifest | Best first path after server fixture | Resolve and validate `HonuaSceneResolution`; emit a handoff manifest for Unreal | Load 3D Tiles/terrain with Cesium for Unreal; call Honua APIs natively for details | Primary path |
| Offline scene package | Useful for controlled demos and field/offline review | Validate `HonuaScenePackageManifest`; inspect asset completeness, expiry, bounds, LOD, hashes | Use packaged URLs or copied assets through Unreal/Cesium runtime cache rules | Secondary path after package serving is stable |
| Static export | Useful for hand-carried demos, but can drift from Honua metadata | Export helper or manifest-to-bundle tool | Load a generated local asset set; use static metadata tables | Limited fallback |
| Generated Unreal assets | Highest runtime polish, highest drift risk | Possible build-time converter only | Native Unreal assets, actors, materials, collision, and gameplay | Defer until a real demo requires it |
| Embedded .NET runtime bridge | Technically possible but not justified yet | Runtime SDK loaded in Unreal process | Calls .NET directly from C++/Blueprint | Do not pursue now |

## .NET Responsibilities

- Consume `Honua.Sdk.Scenes` for scene list/detail/resolve calls.
- Validate scene metadata shape, endpoint capabilities, bounds, attribution, and
  access envelopes before a demo run.
- Validate offline scene package manifests using SDK package contracts.
- Produce a deterministic Unreal handoff manifest containing scene id, display
  name, camera hints, tileset URL, terrain URL, imagery overlays, required auth
  mode, attribution, and metadata field names.
- Provide command-line smoke checks for local fixture scenes from
  `honua-server#898`.
- Optionally build or inspect static export bundles after server packaging and
  OpenUSD follow-up work prove the mapping.

## Unreal Runtime Responsibilities

- Install and configure Cesium for Unreal.
- Load 3D Tiles, terrain, and imagery through Unreal actors/components.
- Own camera, input, movement mode, VR/XR, physics, collision, UI, and packaged
  cache behavior.
- Use native Unreal C++/Blueprint HTTP or generated bindings for Honua feature,
  observation, issue, evidence, and selection detail calls.
- Map picked Cesium feature metadata back to Honua scene layer ids, feature ids,
  issue ids, observation ids, or asset ids.
- Handle auth token storage and refresh in the runtime host.

## Cesium For Unreal Fit

Cesium for Unreal is the right first Unreal renderer because it is built around
geospatial 3D Tiles, terrain, tiled imagery, georeferenced sublevels, and runtime
metadata workflows. Its documentation describes loading 3D Tiles, terrain, and
tiled imagery, and its metadata docs cover feature tables, feature IDs, and
runtime property lookup.

Known constraints:

- Vector formats such as KML, SHP, GeoJSON, and CZML are not the right direct
  runtime path for Cesium for Unreal. Honua vectors should appear as 3D Tiles
  metadata, server APIs, generated Unreal actors, or separate native overlays.
- Unsupported 3D Tiles payload variants must be tested against the actual
  server fixture before promising demo coverage.
- The first offline path should rely on Honua package manifests and local asset
  URLs, not on Cesium ion archive assumptions.
- Runtime selection depends on stable metadata in the tiles, not only external
  feature service lookups.

## Metadata And Selection Bridge

The server fixture and any later package/export path should preserve these
fields for Unreal inspection:

- `honuaSceneId`
- `honuaLayerId`
- `honuaLayerKind`
- `honuaFeatureId`
- `honuaAssetId`
- `honuaObservationId`
- `honuaIssueId`
- `honuaWorkPackageId`
- `honuaTimelineState`
- `displayName`
- `status`
- `updatedAt`

The Unreal selection flow should be:

1. Line trace or Cesium metadata query identifies a picked feature.
2. Runtime reads the Honua metadata fields from the tile feature.
3. Runtime calls Honua APIs natively for full detail when needed.
4. Runtime renders Unreal UI or actor state from the returned detail.

The bridge should tolerate missing metadata by falling back to scene/layer-level
inspection rather than failing the entire scene.

## Local Demo Feasibility

Feasibility is medium after `honua-server#898` because the construction fixture
is closed and should provide the right deterministic input. A narrow Unreal
demo is credible if these prerequisites are true:

- The server fixture resolves through `Honua.Sdk.Scenes` with stable scene ids,
  bounds, camera hints, tileset URLs, terrain fallback, layer ids, and metadata.
- At least one 3D Tiles URL loads in Cesium for Unreal without cloud-only
  dependencies.
- Demo metadata includes stable Honua IDs at either feature or layer granularity.
- Auth is public, proxy-based, or signed URL based for the first demo; header
  auth in asset fetches should be treated as a later runtime-hardening task.
- A small .NET CLI can resolve the scene and write an Unreal handoff manifest
  before opening the Unreal project.

Estimated first implementation slice: one to two days for a .NET handoff CLI
and documentation after the fixture URL is known; two to four additional days
for a minimal Unreal project that loads the scene and displays selected metadata.

## Relationship To OpenUSD

OpenUSD should remain separate from the Unreal runtime path. The OpenUSD spike
in `honua-server#901` can define an export artifact or converter manifest for
offline DCC workflows. Unreal should not wait on OpenUSD, and OpenUSD wording
should not imply current runtime support until an exporter exists.

## Follow-Up Implementation Issues

Implementation is justified, but it should be sliced conservatively:

- Add a .NET `SceneHandoffConsole` example that resolves a Honua scene and
  writes an Unreal handoff JSON manifest.
- Add fixture validation for the `honua-server#898` construction scene metadata
  once the server package/version consumed by this SDK exposes the fixture.
- File an Unreal runtime issue in the appropriate consuming repo for a minimal
  Cesium for Unreal project that loads the handoff manifest and opens the scene.
- File a metadata bridge issue for stable picked-feature IDs and layer/detail API
  lookup semantics if the fixture metadata is incomplete.
- File an offline-package demo issue only after local asset URL rewriting and
  cache behavior are validated in Unreal.

## Non-Goals

- No Unreal plugin implementation in this SDK repo.
- No .NET-in-Unreal runtime bridge.
- No server scene generation or OpenUSD exporter implementation.
- No replacement for the first web/CesiumJS construction demo.
- No claim of production Unreal support until a consuming Unreal project exists.

## External References

- Cesium for Unreal documentation: https://cesium.com/learn/unreal/
- Cesium for Unreal FAQ, supported data types and vector limitations:
  https://cesium.com/learn/unreal/unreal-faq/
- Cesium for Unreal metadata reference:
  https://cesium.com/learn/unreal/unreal-metadata-reference/
- Cesium for Unreal metadata tutorial:
  https://cesium.com/learn/unreal/unreal-visualize-metadata/

## Canonical References

- `honua-io/honua-sales#35` is retired; keep wording vendor-neutral.
- `honua-io/honua-server#898` is the construction scene fixture input.
- `honua-io/honua-server#901` owns OpenUSD export path analysis.
- `honua-io/honua-sdk-dotnet#70` delivered SDK scene contracts.
- `honua-io/honua-sdk-dotnet#128` delivered the .NET demo suite.
- `honua-io/honua-sdk-dotnet#129` tracks this spike.
