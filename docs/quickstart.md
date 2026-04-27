# 5-Minute Quickstart: Query Features from a Console App

## What You'll Build

A .NET console app that connects to a Honua server, queries geospatial features
over gRPC, queries via OGC WFS 2.0, queries FeatureServer and OGC API Features
through a shared abstraction, lists services through the Admin REST API, and
forward-geocodes an address -- all printed to the console.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- A running Honua server (default: `https://localhost:5001`)

## Step 1: Create project and install (30 seconds)

```bash
dotnet new console -n HonuaDemo
cd HonuaDemo
dotnet add package Honua.Sdk.Grpc --prerelease
dotnet add package Honua.Sdk.Abstractions --prerelease
dotnet add package Honua.Sdk.Admin --prerelease
dotnet add package Honua.Sdk.Wfs --prerelease
dotnet add package Honua.Sdk.GeoServices --prerelease
dotnet add package Honua.Sdk.OgcFeatures --prerelease
dotnet add package Microsoft.Extensions.Hosting
```

This pulls in the SDK packages and the Generic Host for dependency injection.

## Step 2: Configure the client with DI (60 seconds)

Replace the contents of `Program.cs` with the following. The Generic Host wires
up the gRPC, Admin, Geocoding, WFS, GeoServices FeatureServer, and OGC API
Features clients so they can be injected anywhere.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.Wfs.Extensions;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.OgcFeatures.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// gRPC client -- used for feature queries
builder.Services.AddHonuaGrpc(options =>
{
    options.Address = "https://localhost:5001";
});

// Admin REST client -- used for service management
builder.Services.AddHonuaAdmin(options =>
{
    options.BaseAddress = new Uri("https://localhost:5001");
});

// Geocoding client -- shares the Admin base address and auth
builder.Services.AddHonuaGeocoding(options =>
{
    options.BaseAddress = new Uri("https://localhost:5001");
});

// WFS 2.0 client -- OGC feature queries
builder.Services.AddHonuaWfs(options =>
{
    options.BaseAddress = new Uri("https://localhost:5001");
});

// GeoServices FeatureServer client
builder.Services.AddHonuaFeatureServer(options =>
{
    options.BaseAddress = new Uri("https://localhost:5001");
});

// OGC API Features client
builder.Services.AddHonuaOgcFeatures(options =>
{
    options.BaseAddress = new Uri("https://localhost:5001");
});

builder.Services.AddHostedService<DemoWorker>();

var app = builder.Build();
await app.RunAsync();
```

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
            new ForwardGeocodeOptions { MaxResults = 3 },
            ct);

        foreach (var result in candidates)
        {
            Console.WriteLine($"  {result.Address}");
            Console.WriteLine($"    lat={result.Latitude:F6}, lon={result.Longitude:F6}, " +
                              $"score={result.Score}");
        }
```

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
using Honua.Sdk.Wfs;
using Honua.Sdk.Wfs.Models;

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

```csharp
using Honua.Sdk.Abstractions.Features;

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
- **[INSTALL.md](../INSTALL.md)** -- package sources, GitHub Packages setup,
  and version policy
- **[Field Data Collection example](../examples/FieldDataCollection/)** -- a
  full .NET MAUI reference app with offline sync, forms, and map views
- **[gRPC vs Forms comparison](../examples/FieldDataCollection/GRPC_FORMS_COMPARISON.md)**
  -- when to use gRPC queries versus OpenRosa/XForms for data collection
