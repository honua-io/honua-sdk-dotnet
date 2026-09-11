# Quickstart

This page has two paths:

- [60-second hello-features](#60-second-hello-features) — one package, one
  client, one call. Use this if you just want to confirm the SDK talks to
  your server.
- [Full quickstart (5 steps, ~10 minutes)](#full-quickstart-five-steps) —
  gRPC + Admin + Geocoding + WFS + OGC API Features through the shared
  abstraction, with the umbrella also registering OGC API Processes by
  default. Use this if you want a guided tour of the SDK.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) **10.0.400 or later**
  (`global.json` pins the band; 10.0.100 does not satisfy it)
- A running Honua server. If you do not have one, the
  [honua-server quickstart](https://github.com/honua-io/honua-server/blob/trunk/docs/get-started/quickstart.md)
  brings one up with Docker Compose in a few minutes. Note the ports: HTTP is **8080** and
  gRPC is HTTP/2 cleartext on **8081**. Pointing a gRPC client at the HTTP port fails at
  runtime with `HTTP_1_1_REQUIRED`, so the samples below use `http://localhost:8081`.
- The authenticated Honua GitHub Packages feed configured as a source named
  `honua` — the packages are **not yet on nuget.org**, so every
  `dotnet add package` below fails with `NU1101` until the feed is set up.
  One-time setup (GitHub classic PAT with `read:packages`):
  [INSTALL.md](../INSTALL.md#install-from-github-packages-current-channel)

## 60-second hello-features

Single package, single async call. Replace the URL with your Honua server.

```bash
dotnet new console -n HonuaHello
cd HonuaHello
dotnet add package Honua.Sdk.Grpc --version 1.6.0 \
  --source "https://nuget.pkg.github.com/honua-io/index.json"
dotnet add package Microsoft.Extensions.Hosting
```

> **Use the full feed URL, not a `--source honua` alias.** The alias can degrade to a
> filesystem-path lookup (`NU1301`) within a session, and the failure reads as though the
> package does not exist. Pinning `--version` is optional — an unversioned add resolves to
> the newest published version, currently **1.6.0** — but pinning keeps a later release from
> silently changing what you built against.

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Grpc.Models;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHonuaGrpc(o => o.BaseAddress = new Uri("http://localhost:8081"));

using var host = builder.Build();
var grpc = host.Services.GetRequiredService<IHonuaGrpcClient>();

var response = await grpc.QueryFeaturesAsync(new QueryFeaturesRequest
{
    ServiceId      = "parks",
    LayerId        = 0,
    ResultRecordCount = 5,
});

Console.WriteLine($"Got {response.Features.Count} features.");
```

```bash
dotnet run
```

That's the whole "is the SDK working?" path. If you want auth, paging,
edits, scenes, or the cross-protocol abstraction, continue below.

---

## Full quickstart (five steps)

## What You'll Build

A .NET console app that connects to a Honua server, queries geospatial features
over gRPC, queries via OGC WFS 2.0, queries FeatureServer and OGC API Features
through a shared abstraction, lists services through the Admin REST API, and
searches OGC API Records and STAC catalog metadata, and forward-geocodes an
address -- all printed to the console.

## Step 1: Create project and install (30 seconds)

```bash
dotnet new console -n HonuaDemo
cd HonuaDemo

# Core packages this quickstart uses (feed setup: see Prerequisites above).
# --version is optional here; pinned so this page stays reproducible.
dotnet add package Honua.Sdk.Grpc --version 1.6.0 --source honua           # gRPC FeatureService + native ProcessService jobs
dotnet add package Honua.Sdk.Abstractions --version 1.6.0 --source honua   # shared query abstraction
dotnet add package Honua.Sdk.Admin --version 1.6.0 --source honua          # Admin + Geocoding REST
dotnet add package Honua.Sdk.OgcFeatures --version 1.6.0 --source honua    # OGC API Features + WFS 2.0

# Generic Host for dependency injection
dotnet add package Microsoft.Extensions.Hosting
```

> Add the rest of the SDK -- `Honua.Sdk.GeoServices`, `Honua.Sdk.Scenes`,
> `Honua.Sdk.Catalogs`, `Honua.Sdk.Field`,
> `Honua.Sdk.Spec`, `Honua.Sdk.Studio`, `Honua.Sdk.Geometry`,
> `Honua.Sdk.Offline` -- only when you reach the step that needs them. The full
> catalog is in [INSTALL.md](../INSTALL.md).

## Step 2: Configure the client with DI (60 seconds)

Replace the contents of `Program.cs` with the following. The Generic Host wires
up the default gRPC, Admin, Geocoding, WFS, OGC API Features, and OGC API
Processes clients so they can be injected anywhere. GeoServices FeatureServer,
scene metadata, OGC API Records, and STAC remain opt-in through the `Use*`
flags or their package-specific `AddHonua*` extensions.

The recommended path is the **umbrella** `AddHonua` registration from the
`Honua.Sdk` meta package: one call configures every enabled sub-package with a
shared base address, auth, and retry / timeout policy. Add
`dotnet add package Honua.Sdk --source honua` to the install step above when
you take this path.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Honua.Sdk;
using Honua.Sdk.Grpc.Extensions;   // AddHonuaGrpc

var builder = Host.CreateApplicationBuilder(args);

var serverUri = new Uri("http://localhost:8080");

// One call registers every enabled Honua SDK client. Defaults register the
// common gRPC, Admin + Catalog, Geocoding, OGC API Features, OGC API
// Processes, and WFS 2.0 clients. Flip Use* flags to opt in to the more situational
// sub-packages (Scenes, Spec, Studio, Stac, OgcRecords, GeoServices, Routing).
builder.Services.AddHonua(o =>
{
    o.BaseAddress = serverUri;
});

// honua-server serves the HTTP protocols on 8080 and gRPC as HTTP/2 cleartext
// on 8081, so one BaseAddress cannot reach both. AddHonua delegates to
// AddHonuaGrpc internally, so registering it again here wins for the gRPC client.
// Without this, gRPC calls fail at runtime with HTTP_1_1_REQUIRED.
builder.Services.AddHonuaGrpc(o => o.BaseAddress = new Uri("http://localhost:8081"));

builder.Services.AddHostedService<DemoWorker>();

var app = builder.Build();
await app.RunAsync();
```

<details>
<summary>Want explicit per-client registration instead?</summary>

The per-package `AddHonua*` extensions still work unchanged. Use this form
when you want strict, narrow control over which sub-packages register:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.OgcFeatures.Wfs.Extensions;
using Honua.Sdk.OgcFeatures.Extensions;

var builder = Host.CreateApplicationBuilder(args);

var serverUri = new Uri("http://localhost:8080");

// gRPC client -- used for feature queries and native ProcessService jobs.
// BaseAddress is preferred for parity with the REST clients; Address (string)
// is still supported.
builder.Services.AddHonuaGrpc(options => options.BaseAddress = serverUri);

// Admin REST client -- service management. Registers IHonuaCatalogClient too.
builder.Services.AddHonuaAdmin(options => options.BaseAddress = serverUri);

// Geocoding client -- shares the Admin base address and auth.
builder.Services.AddHonuaGeocoding(options => options.BaseAddress = serverUri);

// WFS 2.0 client -- OGC feature queries
builder.Services.AddHonuaWfs(options => options.BaseAddress = serverUri);

// OGC API Features client -- used in the shared-abstraction step below
builder.Services.AddHonuaOgcFeatures(options => options.BaseAddress = serverUri);

builder.Services.AddHostedService<DemoWorker>();

var app = builder.Build();
await app.RunAsync();
```

</details>

## Step 3: Query features (60 seconds)

Add a `DemoWorker.cs` file that queries a feature layer, filtering rows with a
`Where` clause and printing each feature's attributes:

```csharp
using Microsoft.Extensions.Hosting;
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Models;

public sealed class DemoWorker(
    IHonuaGrpcClient grpcClient,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // --- 3a. Query features via gRPC ---
        Console.WriteLine("=== Feature Query ===");

        var response = await grpcClient.QueryFeaturesAsync(new QueryFeaturesRequest
        {
            ServiceId   = "parks",
            LayerId     = 0,
            Where       = "status = 'open'",
            OutFields   = ["name", "area_acres", "status"],
            ReturnGeometry = true,
            ResultRecordCount = 5,
        }, ct);

        Console.WriteLine($"Returned {response.Features.Count} features " +
                          $"(geometry type: {response.GeometryType})");

        foreach (var feature in response.Features)
        {
            Console.WriteLine($"  [{feature.Id}] " +
                string.Join(", ", feature.Attributes
                    .Select(a => $"{a.Key}={a.Value}")));
        }

        // Stop the host after the demo finishes
        lifetime.StopApplication();
    }
}
```

Run the app:

```bash
dotnet run
```

Expected output (your data will differ):

```
=== Feature Query ===
Returned 5 features (geometry type: Point)
  [1] name=Kapi'olani Park, area_acres=300, status=open
  [2] name=Ala Moana Park, area_acres=100, status=open
  ...
```

## Step 4: Use the Admin client (60 seconds)

Add admin calls to `DemoWorker.ExecuteAsync`, right before the
`lifetime.StopApplication()` line. Inject `IHonuaAdminClient` via the
constructor:

```csharp
using Honua.Sdk.Admin;

public sealed class DemoWorker(
    IHonuaGrpcClient grpcClient,
    IHonuaAdminClient adminClient,      // <-- add this
    IHostApplicationLifetime lifetime) : BackgroundService
```

Then add the following after the feature query:

```csharp
        // --- 4. List services via Admin REST API ---
        Console.WriteLine("\n=== Services ===");

        var services = await adminClient.ListServicesAsync(ct);
        foreach (var svc in services)
        {
            Console.WriteLine($"  {svc.ServiceName} " +
                              $"({svc.LayerCount} layers, " +
                              $"protocols: {string.Join(", ", svc.EnabledProtocols ?? [])})");
        }

        // Get settings for a specific service
        var settings = await adminClient.GetServiceSettingsAsync("parks", ct);
        Console.WriteLine($"\nService '{settings.ServiceName}' details retrieved.");
```

For richer non-display catalog discovery, inject `IHonuaCatalogClient` from
`Honua.Sdk.Admin.Catalog`. `AddHonuaAdmin` registers it automatically, and
`AddHonuaCatalog` is available when an app only needs discovery:

```csharp
using Honua.Sdk.Admin.Catalog; // CatalogQueryOptions, CatalogItemKind, IHonuaCatalogClient

// constructor: ... IHonuaCatalogClient catalogClient ...
var catalog = await catalogClient.SearchAsync(
    new CatalogQueryOptions
    {
        Kinds = [CatalogItemKind.Layer],
        ServiceTypes = ["FeatureServer"],
        Tags = ["public"],
        Limit = 10
    },
    ct);
```

Use `IHonuaOgcRecordsClient` when the server exposes the public OGC API Records
catalog and the caller should discover standards-facing metadata records instead
of operator/control-plane inventory. First install and register the package:

```bash
dotnet add package Honua.Sdk.Catalogs --source honua
```

```csharp
using Honua.Sdk.Catalogs.Records.Extensions;
builder.Services.AddHonuaOgcRecords(o => o.BaseAddress = serverUri);
```

```csharp
using Honua.Sdk.Catalogs.Records.Models;

var records = await recordsClient.SearchAsync(
    "default",
    new OgcRecordsQuery
    {
        Query = "parks",
        Types = ["service", "layer"],
        Limit = 10
    },
    ct);
```

Use `IHonuaStacClient` when the caller needs STAC catalog, collection, item, and
asset search semantics instead of Records metadata records. First install and
register the package:

```bash
dotnet add package Honua.Sdk.Catalogs --source honua
```

```csharp
using Honua.Sdk.Catalogs.Stac.Extensions;
builder.Services.AddHonuaStac(o => o.BaseAddress = serverUri);
```

```csharp
using Honua.Sdk.Catalogs.Stac.Models;

var stacItems = await stacClient.SearchAsync(
    new StacSearchQuery
    {
        Collections = ["imagery"],
        Bbox = [-158.4, 21.2, -157.6, 21.9],
        Datetime = "2026-05-01T00:00:00Z/..",
        Limit = 10
    },
    ct);
```

## Step 5: Add geocoding (60 seconds)

Inject `IHonuaGeocodingClient` the same way and add a forward-geocode call:

```csharp
using Honua.Sdk.Admin.Geocoding;

public sealed class DemoWorker(
    IHonuaGrpcClient grpcClient,
    IHonuaAdminClient adminClient,
    IHonuaGeocodingClient geocodingClient,  // <-- add this
    IHostApplicationLifetime lifetime) : BackgroundService
```

Then add:

```csharp
        // --- 5. Forward geocode an address ---
        Console.WriteLine("\n=== Geocoding ===");

        var candidates = await geocodingClient.ForwardGeocodeAsync(
            "1600 Pennsylvania Ave NW, Washington, DC",
            new ForwardGeocodeOptions
            {
                MaxResults = 3,
                Categories = ["Address"],
                OutFields = ["Addr_type", "City", "Region"],
                Location = new GeocodePoint(-77.0365, 38.8977)
            },
            ct);

        foreach (var result in candidates)
        {
            Console.WriteLine($"  {result.Address}");
            Console.WriteLine($"    lat={result.Latitude:F6}, lon={result.Longitude:F6}, " +
                              $"score={result.Score}");
        }
```

For batch geocoding with partial-failure details, inject
`IHonuaBatchGeocodingClient` or cast the default client and call
`BatchGeocodeDetailedAsync`.

Run again and you should see all three sections:

```bash
dotnet run
```

```
=== Feature Query ===
Returned 5 features (geometry type: Point)
  [1] name=Kapi'olani Park, area_acres=300, status=open
  ...

=== Services ===
  parks (3 layers, protocols: FeatureServer, MapServer)
  ...

=== Geocoding ===
  1600 Pennsylvania Ave NW, Washington, DC 20500
    lat=38.897676, lon=-77.036530, score=100
```

## Step 6: Query via WFS 2.0 (60 seconds)

Inject `IHonuaWfsClient` and query features using the OGC WFS protocol:

```csharp
using Honua.Sdk.OgcFeatures.Wfs;
using Honua.Sdk.OgcFeatures.Wfs.Models;

public sealed class DemoWorker(
    IHonuaGrpcClient grpcClient,
    IHonuaAdminClient adminClient,
    IHonuaGeocodingClient geocodingClient,
    IHonuaWfsClient wfsClient,            // <-- add this
    IHostApplicationLifetime lifetime) : BackgroundService
```

Then add:

```csharp
        // --- 6. WFS 2.0 feature query ---
        Console.WriteLine("\n=== WFS ===");

        var caps = await wfsClient.GetCapabilitiesAsync(ct);
        Console.WriteLine($"WFS {caps.Version}: {caps.FeatureTypes.Count} feature types");

        var wfsResult = await wfsClient.GetFeaturesAsync(new GetFeaturesRequest
        {
            TypeNames = caps.FeatureTypes[0].Name,
            Count = 3,
        }, ct);

        foreach (var feature in wfsResult.Features)
            Console.WriteLine($"  {feature.Id}");
```

Expected output:

```
=== WFS ===
WFS 2.0.0: 4 feature types
  parcels.1
  parcels.2
  parcels.3
```

## Step 7: Query through the shared abstraction

Every read/query protocol client also registers `IHonuaFeatureQueryClient`.
Inject `IEnumerable<IHonuaFeatureQueryClient>` when application code should
switch providers without changing query code:

> **Adding this using breaks Step 3 unless you alias.** `QueryFeaturesRequest` is declared
> in both `Honua.Sdk.Grpc.Models` (imported in Step 2) and
> `Honua.Sdk.Abstractions.Features`, so every bare use from Step 3 becomes `CS0104:
> ambiguous reference`. Pin the one you mean:
>
> ```csharp
> using QueryFeaturesRequest = Honua.Sdk.Grpc.Models.QueryFeaturesRequest;
> ```

```csharp
using Honua.Sdk.Abstractions.Features;
using QueryFeaturesRequest = Honua.Sdk.Grpc.Models.QueryFeaturesRequest;

public sealed class DemoWorker(
    IHonuaGrpcClient grpcClient,
    IHonuaAdminClient adminClient,
    IHonuaGeocodingClient geocodingClient,
    IHonuaWfsClient wfsClient,
    IEnumerable<IHonuaFeatureQueryClient> featureQueryClients,
    IHostApplicationLifetime lifetime) : BackgroundService
```

Then add:

```csharp
        // --- 7. Shared feature query abstraction ---
        Console.WriteLine("\n=== Shared Query ===");

        var ogc = featureQueryClients.Single(c => c.ProviderName == "ogc-features");
        var page = await ogc.QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { CollectionId = "parks" },
            Filter = "status = 'open'",
            FilterLanguage = FeatureFilterLanguage.Cql2Text,
            OutFields = ["name", "status"],
            Limit = 3,
        }, ct);

        foreach (var feature in page.Features)
            Console.WriteLine($"  {feature.Id}");
```

To keep provider-specific source identifiers out of call sites, wrap the
selected client in a source descriptor:

```csharp
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "parks",
                Protocol = FeatureProtocolIds.OgcFeatures,
                Locator = new SourceLocator { CollectionId = "parks" }
            },
            ogc,
            editClient: ogc as IHonuaFeatureEditClient,
            nativeClient: ogc);

        var ids = await source.QueryObjectIdsAsync(new SourceQuery
        {
            Where = "status = 'open'",
            FilterLanguage = FeatureFilterLanguage.Cql2Text,
            Limit = 3,
        }, ct);
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
