# 5-Minute Quickstart: Query Features from a Console App

## What You'll Build

A .NET console app that connects to a Honua server, queries geospatial features
over gRPC, lists services through the Admin REST API, and forward-geocodes an
address -- all printed to the console.

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- A running Honua server (default: `https://localhost:5001`)

## Step 1: Create project and install (30 seconds)

```bash
dotnet new console -n HonuaDemo
cd HonuaDemo
dotnet add package Honua.Sdk.Grpc --prerelease
dotnet add package Honua.Sdk.Admin --prerelease
dotnet add package Microsoft.Extensions.Hosting
```

This pulls in both SDK packages and the Generic Host for dependency injection.

## Step 2: Configure the client with DI (60 seconds)

Replace the contents of `Program.cs` with the following. The Generic Host wires
up the gRPC, Admin, and Geocoding clients so they can be injected anywhere.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Admin.Extensions;

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

## What's Next

- **[INSTALL.md](../INSTALL.md)** -- package sources, GitHub Packages setup,
  and version policy
- **[Field Data Collection example](../examples/FieldDataCollection/)** -- a
  full .NET MAUI app with offline sync, forms, and map views
- **[gRPC vs Forms comparison](../examples/FieldDataCollection/GRPC_FORMS_COMPARISON.md)**
  -- when to use gRPC queries versus OpenRosa/XForms for data collection
