---
type: guide
title: "Install the SDK and make your first call"
description: "Two paths: a 60-second single-package hello, and a seven-step tour registering every client through dependency injection."
resource: "https://www.nuget.org/packages/Honua.Sdk/"
tags: [quickstart, dotnet, dependency-injection]
---
# Quickstart

This page has two paths:

- [60-second hello-features](#60-second-hello-features) — one package, one
  client, one call. Use this if you just want to confirm the SDK talks to
  your server.
- [Full quickstart (7 steps, ~10 minutes)](#full-quickstart-seven-steps) —
  gRPC, Admin, the catalogs, Geocoding, WFS and OGC API Features through the
  shared abstraction, all registered once through dependency injection. Use
  this if you want a guided tour of the SDK.

Every code block on this page is a complete file or a complete command. Copy
it as it is; nothing needs splicing into an earlier file.

## Prerequisites

- The [.NET 10 SDK](https://dotnet.microsoft.com/download).
- A running Honua server. If you do not have one, the
  [honua-server quickstart](https://github.com/honua-io/honua-server/blob/trunk/docs/get-started/quickstart.md)
  brings one up with Docker Compose in a few minutes. The server serves its HTTP protocols on one
  port and gRPC as HTTP/2 cleartext on another (the repository Compose defaults are **8080** and
  **8081**). Pointing a gRPC client at the HTTP port fails at runtime with `HTTP_1_1_REQUIRED`.
- A published feature layer to query. If you have none yet,
  [Publish your first dataset](https://github.com/honua-io/honua-server/blob/trunk/docs/get-started/first-dataset.md)
  publishes one and prints its `serviceName` and `layerId`.
- An admin API key for that server. On a server you started with the honua-server quickstart, the
  admin password it generated (`HONUA_ADMIN_PASSWORD` in that install's `.env`) is accepted as the
  admin key.

## Before you start: point the samples at your server

The samples read your server's addresses, the key and the layer from environment variables, so
nothing on this page needs editing. Set them in the terminal you run the samples from, replacing
the `<...>` values with your own (and the ports, if your server does not use the defaults):

```bash
export HONUA_URL="http://localhost:8080"        # the server's HTTP address
export HONUA_GRPC_URL="http://localhost:8081"   # the server's gRPC (HTTP/2 cleartext) address
export HONUA_API_KEY="<your-api-key>"           # an admin API key
export HONUA_SERVICE_ID="<your-service-id>"     # the serviceName of a published layer
export HONUA_LAYER_ID="<your-layer-id>"         # that layer's layerId (a number)
```

The SDK sends an API key only over HTTPS, with one exception: `localhost`/loopback addresses for
local development.

## 60-second hello-features

Single package, single async call:

```bash
dotnet new console -o HonuaHello
dotnet add HonuaHello package Honua.Sdk.Grpc
dotnet add HonuaHello package Microsoft.Extensions.Hosting
```

An unversioned `dotnet add package` resolves to the newest stable release on nuget.org (the
current one is in the [README status table](../README.md#status)); pin `--version` when you want a
later release not to change what you built against. Prereleases are on GitHub Packages only - see
[INSTALL.md](../INSTALL.md#prereleases-and-the-github-packages-mirror).

Replace `HonuaHello/Program.cs` with:

<!-- doc-run: file=HonuaHello/Program.cs -->
```csharp
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Grpc.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);   // keep per-request logs out of the output
builder.Services.AddHonuaGrpc(o =>
{
    o.BaseAddress = new Uri(Env("HONUA_GRPC_URL"));
    o.ApiKey = Env("HONUA_API_KEY");
});

using var host = builder.Build();
var grpc = host.Services.GetRequiredService<IHonuaGrpcClient>();

var response = await grpc.QueryFeaturesAsync(new QueryFeaturesRequest
{
    ServiceId = Env("HONUA_SERVICE_ID"),
    LayerId = int.Parse(Env("HONUA_LAYER_ID")),
    ResultRecordCount = 5,
});

Console.WriteLine($"Got {response.Features.Count} features.");

static string Env(string name) =>
    Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"Set {name} first.");
```

```bash
dotnet run --project HonuaHello
```

It prints how many features came back (at most 5). That's the whole "is the SDK working?" path. If
you want auth, paging, edits, scenes, or the cross-protocol abstraction, continue below.

---

## Full quickstart (seven steps)

## What You'll Build

A .NET console app that registers every Honua client once, then, one step at a time, queries
features over gRPC, lists services through the Admin REST API, searches the catalog, OGC API
Records and STAC metadata, forward-geocodes an address, queries WFS 2.0, and queries OGC API
Features through the shared abstraction.

Each step replaces `Program.cs` with a short program for that step, so every step runs on its
own. The client registration lives in one file, `HonuaHost.cs`, that every step shares.

## Step 1: Create project and install (30 seconds)

Run this from the same directory as the hello project (not inside it):

```bash
dotnet new console -o HonuaDemo
cd HonuaDemo

# The umbrella package brings in every Honua.Sdk.* client this quickstart uses.
dotnet add package Honua.Sdk

# Generic Host for dependency injection
dotnet add package Microsoft.Extensions.Hosting
```

> Want fewer dependencies? Instead of `Honua.Sdk`, add only the packages a step uses:
> `Honua.Sdk.Grpc` (step 3), `Honua.Sdk.Admin` (steps 2, 4 and 5), `Honua.Sdk.Catalogs`
> (step 4), `Honua.Sdk.OgcFeatures` (steps 6 and 7) and `Honua.Sdk.Abstractions` (step 7), and
> register them one by one as shown in
> [Register clients one by one](#register-clients-one-by-one-instead). The full package catalog is
> in [INSTALL.md](../INSTALL.md).

## Step 2: Register the clients with DI (60 seconds)

Create `HonuaHost.cs`. The **umbrella** `AddHonua` registration from the `Honua.Sdk` package
configures every enabled sub-package with a shared base address, auth, and retry / timeout policy.
Defaults register the common gRPC, Admin + Catalog, Geocoding, OGC API Features, OGC API
Processes, and WFS 2.0 clients; `Use*` flags opt in to the more situational ones (Scenes, Spec,
Studio, ConsoleShare, Stac, OgcRecords, GeoServices, Routing, ImageServer). This quickstart turns
on OGC API Records and STAC for step 4.

<!-- doc-run: file=HonuaHost.cs -->
```csharp
using Honua.Sdk;
using Honua.Sdk.Grpc.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Registers the Honua clients once; each step's Program.cs resolves the ones it uses.</summary>
public static class HonuaHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);   // keep per-request logs out of the output

        builder.Services.AddHonua(o =>
        {
            o.BaseAddress = new Uri(Env("HONUA_URL"));
            o.ApiKey = Env("HONUA_API_KEY");
            o.UseOgcRecords = true;   // step 4
            o.UseStac = true;         // step 4
        });

        // gRPC is served on its own HTTP/2 cleartext port, so one BaseAddress cannot reach both.
        // AddHonua delegates to AddHonuaGrpc internally, so registering it again here wins for
        // the gRPC client. Without this, gRPC calls fail at runtime with HTTP_1_1_REQUIRED.
        builder.Services.AddHonuaGrpc(o =>
        {
            o.BaseAddress = new Uri(Env("HONUA_GRPC_URL"));
            o.ApiKey = Env("HONUA_API_KEY");
        });

        return builder.Build();
    }

    public static string Env(string name) =>
        Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"Set {name} first.");
}
```

Then replace `Program.cs` with a first call: the Admin client's compatibility check, which every
app should run before relying on the server.

<!-- doc-run: file=Program.cs -->
```csharp
using Honua.Sdk.Admin;
using Microsoft.Extensions.DependencyInjection;

using var host = HonuaHost.Build(args);
var admin = host.Services.GetRequiredService<IHonuaAdminClient>();

var compatibility = await admin.CheckCompatibilityAsync();
Console.WriteLine($"Server supported by this SDK: {compatibility.IsSupported}");
if (!compatibility.IsSupported)
{
    Console.WriteLine(compatibility.UnsupportedReason);
}
```

```bash
dotnet run
```

On a supported server it prints:

<!-- doc-run: output -->
```text
Server supported by this SDK: True
```

## Step 3: Query features over gRPC (60 seconds)

Replace `Program.cs` to query your layer, printing each feature's attributes. `Where = "1=1"`
returns every row; narrow it with your own fields, for example `"status = 'open'"`.

<!-- doc-run: file=Program.cs -->
```csharp
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Models;
using Microsoft.Extensions.DependencyInjection;
using static HonuaHost;

using var host = Build(args);
var grpc = host.Services.GetRequiredService<IHonuaGrpcClient>();

var response = await grpc.QueryFeaturesAsync(new QueryFeaturesRequest
{
    ServiceId = Env("HONUA_SERVICE_ID"),
    LayerId = int.Parse(Env("HONUA_LAYER_ID")),
    Where = "1=1",
    ReturnGeometry = true,
    ResultRecordCount = 5,
});

Console.WriteLine($"Returned {response.Features.Count} features (geometry type: {response.GeometryType})");
foreach (var feature in response.Features)
{
    Console.WriteLine($"  [{feature.Id}] " +
        string.Join(", ", feature.Attributes.Select(a => $"{a.Key}={a.Value}")));
}
```

```bash
dotnet run
```

It prints the number of features, the layer's geometry type, and one line of attributes per
feature (at most 5).

## Step 4: Use the Admin client and the catalogs (60 seconds)

List the server's services and read one service's settings through the Admin REST API:

<!-- doc-run: file=Program.cs -->
```csharp
using Honua.Sdk.Admin;
using Microsoft.Extensions.DependencyInjection;
using static HonuaHost;

using var host = Build(args);
var admin = host.Services.GetRequiredService<IHonuaAdminClient>();

var services = await admin.ListServicesAsync();
foreach (var svc in services)
{
    Console.WriteLine($"  {svc.ServiceName} " +
                      $"({svc.LayerCount} layers, " +
                      $"protocols: {string.Join(", ", svc.EnabledProtocols ?? [])})");
}

var settings = await admin.GetServiceSettingsAsync(Env("HONUA_SERVICE_ID"));
Console.WriteLine($"Service '{settings.ServiceName}' details retrieved.");
```

```bash
dotnet run
```

For richer non-display catalog discovery, use `IHonuaCatalogClient` from
`Honua.Sdk.Admin.Catalog`. `AddHonuaAdmin` (and so `AddHonua`) registers it automatically, and
`AddHonuaCatalog` is available when an app only needs discovery:

<!-- doc-run: file=Program.cs -->
```csharp
using Honua.Sdk.Admin.Catalog; // CatalogQueryOptions, CatalogItemKind, IHonuaCatalogClient
using Microsoft.Extensions.DependencyInjection;

using var host = HonuaHost.Build(args);
var catalogClient = host.Services.GetRequiredService<IHonuaCatalogClient>();

var catalog = await catalogClient.SearchAsync(new CatalogQueryOptions
{
    Kinds = [CatalogItemKind.Layer],
    ServiceTypes = ["FeatureServer"],
    Limit = 10,
});

Console.WriteLine($"{catalog.TotalCount} layers");
foreach (var item in catalog.Items)
{
    Console.WriteLine($"  {item.ServiceName}/{item.LayerId}: {item.Name}");
}
```

<!-- doc-run: blocked https://github.com/honua-io/honua-sdk-dotnet/issues/408 -->
```bash
dotnet run
```

> The published SDK pin used by this guide still reads an unavailable metadata route
> ([#408](https://github.com/honua-io/honua-sdk-dotnet/issues/408)). The source fix uses
> canonical service and FeatureServer routes for this search. This executable-docs
> block remains until the corrected package is published, pinned and verified.

Use `IHonuaOgcRecordsClient` when the server exposes the public OGC API Records catalog and the
caller should discover standards-facing metadata records instead of operator/control-plane
inventory. It is in the `Honua.Sdk.Catalogs` package, which `Honua.Sdk` already brings in, and
`HonuaHost` turned it on with `UseOgcRecords` (on its own it is `AddHonuaOgcRecords`). This
searches each record collection for your service:

<!-- doc-run: file=Program.cs -->
```csharp
using Honua.Sdk.Catalogs.Records;
using Honua.Sdk.Catalogs.Records.Models;
using Microsoft.Extensions.DependencyInjection;
using static HonuaHost;

using var host = Build(args);
var recordsClient = host.Services.GetRequiredService<IHonuaOgcRecordsClient>();

foreach (var collection in await recordsClient.ListCollectionsAsync())
{
    var records = await recordsClient.SearchAsync(
        collection.Id,
        new OgcRecordsQuery
        {
            Query = Env("HONUA_SERVICE_ID"),
            Limit = 10
        });

    Console.WriteLine($"{collection.Id}: {records.Records?.Count ?? 0} records match");
}
```

```bash
dotnet run
```

Use `IHonuaStacClient` when the caller needs STAC catalog, collection, item, and asset search
semantics instead of Records metadata records. `HonuaHost` turned it on with `UseStac` (on its
own it is `AddHonuaStac`):

<!-- doc-run: file=Program.cs -->
```csharp
using Honua.Sdk.Catalogs.Stac;
using Honua.Sdk.Catalogs.Stac.Models;
using Microsoft.Extensions.DependencyInjection;

using var host = HonuaHost.Build(args);
var stacClient = host.Services.GetRequiredService<IHonuaStacClient>();

var collections = await stacClient.ListCollectionsAsync();
Console.WriteLine($"{collections.Count} STAC collections");

// Narrow the search with Collections, Bbox and Datetime, for example
// Bbox = [-158.4, 21.2, -157.6, 21.9] and Datetime = "2026-05-01T00:00:00Z/..".
var stacItems = await stacClient.SearchAsync(new StacSearchQuery { Limit = 10 });
foreach (var item in stacItems.Features ?? [])
{
    Console.WriteLine($"  {item.Collection}/{item.Id}");
}
```

```bash
dotnet run
```

## Step 5: Add geocoding (60 seconds)

Forward-geocode an address. This needs a geocoding provider configured on the server; the
candidates and scores come from that provider.

<!-- doc-run: file=Program.cs -->
```csharp
using Honua.Sdk.Admin.Geocoding;
using Microsoft.Extensions.DependencyInjection;

using var host = HonuaHost.Build(args);
var geocodingClient = host.Services.GetRequiredService<IHonuaGeocodingClient>();

var candidates = await geocodingClient.ForwardGeocodeAsync(
    "1600 Pennsylvania Ave NW, Washington, DC",
    new ForwardGeocodeOptions
    {
        MaxResults = 3,
        Location = new GeocodePoint(-77.0365, 38.8977)
    });

foreach (var result in candidates)
{
    Console.WriteLine($"  {result.Address}");
    Console.WriteLine($"    lat={result.Latitude:F6}, lon={result.Longitude:F6}, " +
                      $"score={result.Score}");
}
```

```bash
dotnet run
```

For batch geocoding with partial-failure details, resolve `IHonuaBatchGeocodingClient` or cast
the default client and call `BatchGeocodeDetailedAsync`.

## Step 6: Query via WFS 2.0 (60 seconds)

Query features using the OGC WFS protocol: read the capabilities, then fetch three features of
the first feature type.

<!-- doc-run: file=Program.cs -->
```csharp
using Honua.Sdk.OgcFeatures.Wfs;
using Honua.Sdk.OgcFeatures.Wfs.Models;
using Microsoft.Extensions.DependencyInjection;

using var host = HonuaHost.Build(args);
var wfsClient = host.Services.GetRequiredService<IHonuaWfsClient>();

var caps = await wfsClient.GetCapabilitiesAsync();
Console.WriteLine($"WFS {caps.Version}: {caps.FeatureTypes.Count} feature types");

var wfsResult = await wfsClient.GetFeaturesAsync(new GetFeaturesRequest
{
    TypeNames = caps.FeatureTypes[0].Name,
    Count = 3,
});

foreach (var feature in wfsResult.Features)
{
    Console.WriteLine($"  {feature.Id}");
}
```

```bash
dotnet run
```

## Step 7: Query through the shared abstraction

Every read/query protocol client also registers `IHonuaFeatureQueryClient`. Resolve
`IEnumerable<IHonuaFeatureQueryClient>` when application code should switch providers without
changing query code. Honua serves each published layer as the OGC API Features collection whose
id is the layer's `layerId`.

> **Both namespaces declare `QueryFeaturesRequest`.** It is in `Honua.Sdk.Grpc.Models` and in
> `Honua.Sdk.Abstractions.Features`, so a file that imports both gets `CS0104: ambiguous
> reference`. This step uses only the abstraction; if you combine it with step 3's gRPC code,
> pin the one you mean:
> `using QueryFeaturesRequest = Honua.Sdk.Grpc.Models.QueryFeaturesRequest;`

<!-- doc-run: file=Program.cs -->
```csharp
using Honua.Sdk.Abstractions.Features;
using Microsoft.Extensions.DependencyInjection;
using static HonuaHost;

using var host = Build(args);
var featureQueryClients = host.Services.GetServices<IHonuaFeatureQueryClient>();

var ogc = featureQueryClients.Single(c => c.ProviderName == "ogc-features");
var page = await ogc.QueryAsync(new FeatureQueryRequest
{
    Source = new FeatureSource { CollectionId = Env("HONUA_LAYER_ID") },
    Limit = 3,
});

foreach (var feature in page.Features)
{
    Console.WriteLine($"  {feature.Id}");
}

// To keep provider-specific source identifiers out of call sites, wrap the
// selected client in a source descriptor.
var source = new HonuaSource(
    new SourceDescriptor
    {
        Id = Env("HONUA_SERVICE_ID"),
        Protocol = FeatureProtocolIds.OgcFeatures,
        Locator = new SourceLocator { CollectionId = Env("HONUA_LAYER_ID") }
    },
    ogc,
    editClient: ogc as IHonuaFeatureEditClient,
    nativeClient: ogc);

var ids = await source.QueryObjectIdsAsync(new SourceQuery { Limit = 3 });
Console.WriteLine($"Object ids: {string.Join(", ", ids)}");
```

```bash
dotnet run
```

Filters work the same way through either form: set `Filter` (or `Where` on a `SourceQuery`) with
`FilterLanguage = FeatureFilterLanguage.Cql2Text`, for example `"status = 'open'"` on a layer that
has a `status` field.

## Register clients one by one instead

The per-package `AddHonua*` extensions still work unchanged. Use them when you want strict,
narrow control over which sub-packages register. This `HonuaHost.cs` replaces the umbrella
registration from step 2, and every step above runs unchanged with it:

<!-- doc-run: file=HonuaHost.cs -->
```csharp
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.Catalogs.Records.Extensions;
using Honua.Sdk.Catalogs.Stac.Extensions;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.OgcFeatures.Wfs.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Registers the Honua clients once; each step's Program.cs resolves the ones it uses.</summary>
public static class HonuaHost
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);

        var serverUri = new Uri(Env("HONUA_URL"));
        var apiKey = Env("HONUA_API_KEY");

        // gRPC -- feature queries and native ProcessService jobs, on the gRPC port.
        builder.Services.AddHonuaGrpc(o => { o.BaseAddress = new Uri(Env("HONUA_GRPC_URL")); o.ApiKey = apiKey; });

        // Admin REST -- service management. Registers IHonuaCatalogClient too.
        builder.Services.AddHonuaAdmin(o => { o.BaseAddress = serverUri; o.ApiKey = apiKey; });

        // Geocoding -- shares the Admin base address and auth.
        builder.Services.AddHonuaGeocoding(o => { o.BaseAddress = serverUri; o.ApiKey = apiKey; });

        // OGC API Records and STAC catalogs.
        builder.Services.AddHonuaOgcRecords(o => { o.BaseAddress = serverUri; o.ApiKey = apiKey; });
        builder.Services.AddHonuaStac(o => { o.BaseAddress = serverUri; o.ApiKey = apiKey; });

        // WFS 2.0 and OGC API Features.
        builder.Services.AddHonuaWfs(o => { o.BaseAddress = serverUri; o.ApiKey = apiKey; });
        builder.Services.AddHonuaOgcFeatures(o => { o.BaseAddress = serverUri; o.ApiKey = apiKey; });

        return builder.Build();
    }

    public static string Env(string name) =>
        Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException($"Set {name} first.");
}
```

Run the current step again to check it:

```bash
dotnet run
```

## What's Next

- **[Admin Bootstrap Console](../examples/AdminBootstrapConsole/)** -- the
  canonical sample app for this repo's operator/bootstrap flow; bootstrap a
  PostGIS table with `Honua.Sdk.Admin`, requiring geometry metadata and a
  single primary key; reuse or publish the layer safely, preserve existing
  protocols while enabling `Grpc`, and verify the published layer with a
  bounded query
- **[Staging Integration Guide](staging-integration.md)** -- required staging
  environment variables, CI evidence artifacts, and troubleshooting for the
  read-only staging suite
- **[Source facade](source-facade.md)** -- source descriptors, protocol
  aliases, capabilities, and native protocol escape hatches
- **[INSTALL.md](../INSTALL.md)** -- package sources, GitHub Packages setup,
  and version policy
- **[Field Data Collection example](../examples/FieldDataCollection/)** --
  archived .NET MAUI reference assets for offline sync, forms, and map views
- **[gRPC vs Forms comparison](../examples/FieldDataCollection/GRPC_FORMS_COMPARISON.md)**
  -- when to use gRPC queries versus OpenRosa/XForms for data collection
