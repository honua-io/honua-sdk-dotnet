# Scene Metadata And Packages

`Honua.Sdk.Scenes` provides portable scene discovery and offline scene package
contracts. It deliberately does not render scenes or manage viewer caches.

## SDK Responsibilities

- List scenes visible to the current credentials.
- Fetch scene metadata, including capabilities, bounds, attribution, endpoint
  metadata, auth requirements, and access envelopes.
- Resolve render-ready endpoint URLs for viewers such as Cesium, MapLibre,
  deck.gl, native MAUI hosts, or `@honua/embed`.
- Parse and validate offline scene package manifests.

## Viewer Responsibilities

- Cesium, MapLibre, deck.gl, WebGL/WebGPU, Mapsui, and native map controls.
- Browser cache adapters, service workers, IndexedDB, and renderer asset
  prefetching.
- MAUI/native file placement, package download scheduling, background transfer,
  and eviction.
- AR/VR anchors, camera controls, display state, and user interaction.

## Unreal Integration Spike

The Unreal path is documented in
[`../.specifica/unreal-integration-path-honua-scenes-dotnet-tooling/README.md`](../.specifica/unreal-integration-path-honua-scenes-dotnet-tooling/README.md).
The current recommendation is:

- keep the first 3D construction demo web/CesiumJS-first;
- use `.NET` for scene manifest discovery, validation, handoff manifests, and
  offline-package inspection;
- use Cesium for Unreal plus native Unreal HTTP/OpenAPI calls for runtime scene
  loading, selection, UI, input, physics, and packaged-cache behavior;
- avoid embedding .NET into Unreal until a concrete runtime need outweighs the
  packaging and debugging cost.

## Usage

```csharp
using Honua.Sdk.Abstractions.Scenes;
using Honua.Sdk.Scenes.Extensions;

builder.Services.AddHonuaScenes(options =>
{
    options.BaseAddress = new Uri("https://api.honua.example");
    options.BearerTokenProvider = ct => tokenProvider.GetTokenAsync(ct);
});

var scenes = await sceneClient.ListScenesAsync(new HonuaSceneListRequest
{
    Capabilities = [HonuaSceneCapabilities.ThreeDimensionalTiles],
});

var resolution = await sceneClient.ResolveSceneAsync(
    scenes[0].Id,
    new HonuaSceneResolveRequest
    {
        RequiredCapabilities = [HonuaSceneCapabilities.ThreeDimensionalTiles],
    });
```

Adapters should pass `resolution.TilesetUrl`, `resolution.TerrainUrl`,
`resolution.Endpoints`, and `resolution.Access` to the viewer layer without
adding renderer dependencies to the SDK.

## Offline Manifests

```csharp
var manifest = HonuaScenePackageManifest.ParseJson(json);
var validation = manifest.Validate(DateTimeOffset.UtcNow, availableAssetKeys);

if (!validation.IsValid)
{
    // Treat validation.Issues as package state, not renderer state.
}
```

The SDK validates package identity, expiry, extent, LOD, byte budget, asset
paths, hashes, and required local assets. Downloading assets, storing files,
and wiring offline URLs into a renderer stay in the consuming app or viewer
package.
