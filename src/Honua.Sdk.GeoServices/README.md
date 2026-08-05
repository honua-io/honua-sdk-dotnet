# Honua.Sdk.GeoServices

REST clients for the Honua FeatureServer (ArcGIS GeoServices-compatible) read,
query, statistics, and apply-edits API, plus the GeoServices NAServer routing,
service-area, and closest-facility client.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

Honua SDK packages are currently published to the authenticated GitHub Packages
feed only — nuget.org publishing is planned but not yet available. One-time
setup: configure the feed with a GitHub **classic** PAT that has the
`read:packages` scope, then install with `--source honua`. Full setup (CI,
package source mapping): [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

```bash
dotnet nuget add source https://nuget.pkg.github.com/honua-io/index.json \
  --name honua --username YOUR_GITHUB_USERNAME --password YOUR_CLASSIC_PAT \
  --store-password-in-clear-text
dotnet add package Honua.Sdk.GeoServices --source honua
```

## Quick usage

```csharp
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHonuaFeatureServer(o => o.BaseAddress = new Uri("https://your-honua-server"));
var provider = services.BuildServiceProvider();

var client = provider.GetRequiredService<IHonuaFeatureServerClient>();

await foreach (var page in client.QueryPagesAsync(
    "parcels",
    layerId: 0,
    new FeatureServerQueryParams
    {
        Where = "state = 'CO'",
        OutFields = "objectid,parcel_no,acres",
        ResultRecordCount = 1000,
        OutSR = 4326,
    },
    cancellationToken))
{
    Console.WriteLine($"Page with {page.Features?.Count ?? 0} features");
}
```

### Routing

```csharp
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.GeoServices.Routing;

services.AddHonuaRouting(o => o.BaseAddress = new Uri("https://your-honua-server"));

var router = provider.GetRequiredService<IHonuaRoutingClient>();
var route = await router.GetDirectionsAsync(
    new RouteDirectionsRequest
    {
        Origin = RoutingLocation.FromLatitudeLongitude(40.0150, -105.2705, "Boulder"),
        Destination = RoutingLocation.FromLatitudeLongitude(39.7392, -104.9903, "Denver"),
    },
    cancellationToken);
```

### Raster (ImageServer)

`AddHonuaImageServer` registers the read-only ImageServer client plus the
provider-neutral `IHonuaRasterDataClient` (raster metadata, coverage statistics,
and a windowed/subset read). A raster geoprocessing tool can resolve
`IHonuaRasterDataClient` from DI and read a clipped window of a large raster
rather than transferring the whole dataset:

```csharp
using Honua.Sdk.Abstractions.Data;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.Extensions;

services.AddHonuaImageServer(o => o.BaseAddress = new Uri("https://your-honua-server"));

var raster = provider.GetRequiredService<IHonuaRasterDataClient>();

var metadata = await raster.GetRasterMetadataAsync(
    new RasterMetadataRequest { Source = new SpatialDataSource { ServiceId = "Elevation" } },
    cancellationToken);

// Read a clipped window (bbox extent sampled to a target pixel size) as GeoTIFF.
await using var window = await raster.ReadWindowAsync(
    new RasterWindowReadRequest
    {
        Source = new SpatialDataSource { ServiceId = "Elevation" },
        Extent = new FeatureBoundingBox { MinX = -158, MinY = 21, MaxX = -157, MaxY = 22, Crs = "4326" },
        Width = 512,
        Height = 512,
        Format = RasterWindowFormat.GeoTiff,
    },
    cancellationToken);

await using var file = File.Create("window.tif");
await window.Content.CopyToAsync(file, cancellationToken);
```

The umbrella `Honua.Sdk` package exposes this via the `UseImageServer` flag on
`AddHonua(...)`.

#### Raster output (write) stance

`IHonuaRasterDataClient` is **read-only** by design: it covers raster metadata,
coverage statistics, and windowed reads. The Honua server does expose a raster
*write* path — the admin multipart raster import endpoint
(`POST /api/v1/admin/import/raster`, GeoTIFF / world-file upload into PostGIS) —
but it lives on the privileged Admin surface and is not part of the raster-data
read contract. A geoprocessing tool that produces a raster should write a GeoTIFF
locally (for example from a `ReadWindowAsync` window or a computed result) and
register it through that admin raster import endpoint.

The write half is exposed by `Honua.Sdk.Admin` via `IHonuaAdminRasterImportClient`
(included in `IHonuaAdminClient`), completing the raster geoprocessing round-trip:

```csharp
// Read half  (Honua.Sdk.GeoServices): IHonuaRasterDataClient.ReadWindowAsync(...)
// Write half (Honua.Sdk.Admin):
await using var tiff = File.OpenRead("output.tif");
var result = await adminClient.ImportRasterAsync(new RasterImportRequest
{
    Content = tiff,
    FileName = "output.tif",
    LayerId = 7,
    Name = "GP result",
    Srid = 4326,
});

var formats = await adminClient.GetSupportedRasterFormatsAsync();
```

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Feature edits](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/feature-edits.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
