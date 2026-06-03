# Honua.Sdk.OgcFeatures

REST client for the Honua OGC API Features endpoint: landing page, conformance,
collections, JSON-Schema queryables, items with CQL2 filtering, automatic next-link
paging, and raw responses for non-JSON encodings such as GML, CSV, or HTML.

Also ships the **WFS 2.0 read/query** surface under
`Honua.Sdk.OgcFeatures.Wfs.*`: `IHonuaWfsClient` / `HonuaWfsClient`,
`AddHonuaWfs(...)`, `HonuaWfsClientOptions`, `WfsJsonContext`, and the
parsing pipeline for GetCapabilities, DescribeFeatureType, and GetFeature
(GeoJSON).

And the **OGC API – Styles** surface under `Honua.Sdk.OgcFeatures.Styles.*`:
`IHonuaOgcStylesClient` / `HonuaOgcStylesClient` keyed by `styleId` over
`/ogc/styles` (ADR-0048) — list styles, get a content-negotiated stylesheet
(MapLibre default, or derived SLD 1.0/1.1), read style metadata, and update a
style's MapLibre stylesheet. This is the canonical styles surface; the per-layer
`IHonuaAdminStylesClient` (keyed by `layerId`) is **deprecated** and retained
only as a back-compat alias for editing a layer's default style. Registered
alongside the features client via `AddHonuaOgcFeatures(...)`.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

```bash
dotnet add package Honua.Sdk.OgcFeatures
```

## Quick usage

```csharp
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.OgcFeatures.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHonuaOgcFeatures(o => o.BaseAddress = new Uri("https://your-honua-server"));
var provider = services.BuildServiceProvider();

var client = provider.GetRequiredService<IHonuaOgcFeaturesClient>();

var collections = await client.ListCollectionsAsync(cancellationToken);

await foreach (var page in client.GetItemsPagesAsync(
    "buildings",
    new OgcItemsParams
    {
        Limit = 500,
        Bbox = [-105.3, 39.9, -105.1, 40.1],
        Filter = "height > 30",
        FilterLang = "cql2-text",
    },
    cancellationToken))
{
    Console.WriteLine($"Page with {page.Features?.Count ?? 0} features");
}
```

### WFS 2.0

```csharp
using Honua.Sdk.OgcFeatures.Wfs;
using Honua.Sdk.OgcFeatures.Wfs.Extensions;
using Honua.Sdk.OgcFeatures.Wfs.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHonuaWfs(o => o.BaseAddress = new Uri("https://your-honua-server"));
var provider = services.BuildServiceProvider();

var wfs = provider.GetRequiredService<IHonuaWfsClient>();
var caps = await wfs.GetCapabilitiesAsync(cancellationToken);
var page = await wfs.GetFeaturesAsync(new GetFeaturesRequest
{
    TypeNames = caps.FeatureTypes[0].Name,
    Count = 50,
}, cancellationToken);
```

### OGC API – Styles

```csharp
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.OgcFeatures.Styles;
using Honua.Sdk.OgcFeatures.Styles.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHonuaOgcFeatures(o => o.BaseAddress = new Uri("https://your-honua-server"));
var provider = services.BuildServiceProvider();

var styles = provider.GetRequiredService<IHonuaOgcStylesClient>();

var list = await styles.ListStylesAsync(cancellationToken);
var styleId = list.Default ?? list.Styles[0].Id;

// MapLibre by default; request SLD via the encoding argument.
var sheet = await styles.GetStylesheetAsync(styleId, OgcStyleEncoding.MapboxStyle, cancellationToken);
var metadata = await styles.GetStyleMetadataAsync(styleId, cancellationToken);

await styles.UpdateStyleAsync(styleId, sheet.Content, strict: true, cancellationToken);
```

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Feature edits](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/feature-edits.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
