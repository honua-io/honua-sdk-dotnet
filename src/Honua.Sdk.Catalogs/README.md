# Honua.Sdk.Catalogs

REST clients for the Honua metadata catalog endpoints — both **STAC** (SpatioTemporal
Asset Catalog) and **OGC API Records**. Single package, two standards-based read
surfaces: landing pages, conformance, collections, item / record paging with bbox,
datetime, and CQL2 filtering, GET and POST search with automatic next-link
paging, and raw JSON escape hatches for extension fields not yet promoted to
typed properties.

STAC types live under `Honua.Sdk.Catalogs.Stac.*` and OGC API Records types
under `Honua.Sdk.Catalogs.Records.*`.

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
dotnet add package Honua.Sdk.Catalogs --source honua
```

## STAC

```csharp
using Honua.Sdk.Catalogs.Stac;
using Honua.Sdk.Catalogs.Stac.Extensions;
using Honua.Sdk.Catalogs.Stac.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHonuaStac(o => o.BaseAddress = new Uri("https://your-honua-server"));
var provider = services.BuildServiceProvider();

var client = provider.GetRequiredService<IHonuaStacClient>();

var collections = await client.ListCollectionsAsync(cancellationToken);

await foreach (var page in client.SearchPagesAsync(
    new StacSearchQuery
    {
        Collections = ["sentinel-2-l2a"],
        Bbox = [-105.3, 39.9, -105.1, 40.1],
        Datetime = "2024-06-01T00:00:00Z/2024-09-01T00:00:00Z",
        Limit = 100,
    },
    cancellationToken))
{
    Console.WriteLine($"Page with {page.Features?.Count ?? 0} items");
}
```

## OGC API Records

```csharp
using Honua.Sdk.Catalogs.Records;
using Honua.Sdk.Catalogs.Records.Extensions;
using Honua.Sdk.Catalogs.Records.Models;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHonuaOgcRecords(o => o.BaseAddress = new Uri("https://your-honua-server"));
var provider = services.BuildServiceProvider();

var client = provider.GetRequiredService<IHonuaOgcRecordsClient>();

var collections = await client.ListCollectionsAsync(cancellationToken);

await foreach (var page in client.GetRecordsPagesAsync(
    "metadata",
    new OgcRecordsQuery
    {
        Query = "imagery",
        Bbox = [-180, -90, 180, 90],
        Limit = 50,
    },
    cancellationToken))
{
    Console.WriteLine($"Page with {page.Records?.Count ?? 0} records");
}
```

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Metadata catalog parity](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/metadata-catalog-parity.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
