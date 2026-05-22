# Honua.Sdk.Scenes

REST client for Honua 3D scene discovery: list scenes, fetch scene metadata,
and resolve render-ready endpoint URLs (3D Tiles, terrain, I3S, elevation
profile) for renderers such as CesiumJS or the `<honua-scene>` web component.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

```bash
dotnet add package Honua.Sdk.Scenes
```

## Quick usage

```csharp
using Honua.Sdk.Abstractions.Scenes;
using Honua.Sdk.Scenes;
using Honua.Sdk.Scenes.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHonuaScenes(o => o.BaseAddress = new Uri("https://your-honua-server"));
var provider = services.BuildServiceProvider();

var client = provider.GetRequiredService<IHonuaSceneClient>();

var scenes = await client.ListScenesAsync(
    new HonuaSceneListRequest
    {
        Capabilities = [HonuaSceneCapabilities.ThreeDimensionalTiles],
    },
    cancellationToken);

var resolution = await client.ResolveSceneAsync(
    scenes[0].Id,
    new HonuaSceneResolveRequest
    {
        RequiredCapabilities = [HonuaSceneCapabilities.ThreeDimensionalTiles],
        IncludeTerrain = true,
    },
    cancellationToken);
```

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Scenes](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/scenes.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
