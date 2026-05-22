# Honua.Sdk.GeoServices

REST clients for the Honua FeatureServer (ArcGIS GeoServices-compatible) read,
query, statistics, and apply-edits API, plus the GeoServices NAServer routing,
service-area, and closest-facility client.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

```bash
dotnet add package Honua.Sdk.GeoServices
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

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Feature edits](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/feature-edits.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
