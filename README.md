# Honua .NET SDK

Official .NET client libraries for [Honua](https://github.com/honua-io/honua-server) --
an open-source geospatial feature server. The SDK provides typed clients for
querying and editing features over gRPC, querying via OGC WFS 2.0, managing
services through the Admin REST API, and geocoding addresses.

## Packages

| Package | Description |
|---------|-------------|
| **Honua.Sdk.Grpc** | gRPC client for `FeatureService` -- typed queries, streaming, edits, spatial filters |
| **Honua.Sdk.Admin** | Admin REST client -- services, layers, connections, styles, metadata |
| **Honua.Sdk.Wfs** | WFS 2.0 read/query client -- GetCapabilities, GetFeature (GeoJSON), DescribeFeatureType |
| *Geocoding* (in Admin) | Forward/reverse geocoding and autocomplete via `IHonuaGeocodingClient` |

## Install

```bash
dotnet add package Honua.Sdk.Grpc --prerelease
dotnet add package Honua.Sdk.Admin --prerelease
dotnet add package Honua.Sdk.Wfs --prerelease
```

Pre-release builds are also available from
[GitHub Packages](INSTALL.md#install-from-github-packages-pre-release).

## Quick usage

Register the clients with dependency injection and query features:

```csharp
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.Wfs.Extensions;

// Register clients
builder.Services.AddHonuaGrpc(o => o.Address = "https://localhost:5001");
builder.Services.AddHonuaAdmin(o => o.BaseAddress = new Uri("https://localhost:5001"));
builder.Services.AddHonuaGeocoding(o => o.BaseAddress = new Uri("https://localhost:5001"));
builder.Services.AddHonuaWfs(o => o.BaseAddress = new Uri("https://localhost:5001"));

// Query features (injected IHonuaGrpcClient)
var response = await grpcClient.QueryFeaturesAsync(new QueryFeaturesRequest
{
    ServiceId = "parks",
    LayerId = 0,
    Where = "status = 'open'",
    ReturnGeometry = true,
});

foreach (var feature in response.Features)
    Console.WriteLine($"{feature.Id}: {feature.Attributes["name"]}");
```

## Apply edits

The gRPC client supports feature edits (adds, updates, deletes):

```csharp
var response = await grpcClient.ApplyEditsAsync(new ApplyEditsRequest
{
    ServiceId = "parks",
    LayerId = 0,
    Adds = [new Feature { Attributes = new() { ["name"] = "New Park" } }],
    RollbackOnFailure = true,
});

Console.WriteLine($"Added: {response.AddResults.Count}");
```

## Streaming

Stream large result sets without buffering the entire response:

```csharp
await foreach (var page in grpcClient.QueryFeaturesStreamAsync(request))
{
    foreach (var feature in page.Features)
        Console.WriteLine(feature.Id);
}
```

## Retry

The gRPC and WFS clients retry automatically on transient failures with
exponential backoff and jitter. gRPC retries on `Unavailable` / `Internal`;
WFS retries on `429`, `502`, `503`. Configurable on both clients:

```csharp
builder.Services.AddHonuaGrpc(o =>
{
    o.Address = "https://localhost:5001";
    o.EnableRetry = true;       // default
    o.MaxRetryAttempts = 3;     // default, range 2-5
});

builder.Services.AddHonuaWfs(o =>
{
    o.BaseAddress = new Uri("https://localhost:5001");
    o.EnableRetry = true;       // default
    o.MaxRetryAttempts = 3;     // default, range 2-5
});
```

## WFS 2.0 queries

Query features via OGC WFS 2.0 with GeoJSON output:

```csharp
var caps = await wfsClient.GetCapabilitiesAsync();
Console.WriteLine($"WFS {caps.Version}: {caps.FeatureTypes.Count} feature types");

var result = await wfsClient.GetFeaturesAsync(new GetFeaturesRequest
{
    TypeNames = "parcels",
    Count = 10,
    Bbox = new WfsBoundingBox { MinX = -122.5, MinY = 37.5, MaxX = -122.0, MaxY = 38.0 },
});

foreach (var feature in result.Features)
    Console.WriteLine($"{feature.Id}: {feature.Properties["name"]}");
```

Auto-paginate large result sets with `IAsyncEnumerable`:

```csharp
await foreach (var feature in wfsClient.GetFeaturesAsyncEnumerable(new GetFeaturesRequest
{
    TypeNames = "parcels",
    Count = 100,
}))
{
    Console.WriteLine(feature.Id);
}
```

## Admin compatibility checks

`Honua.Sdk.Admin` validates a connected server against
`GET /api/v1/admin/capabilities`:

```csharp
var compatibility = await adminClient.CheckCompatibilityAsync();

if (!compatibility.IsSupported)
{
    throw new InvalidOperationException(
        $"Honua Server {compatibility.ServerVersion} is not supported. " +
        $"Minimum supported version: {compatibility.MinimumSupportedServerVersion}. " +
        $"{compatibility.UnsupportedReason}");
}
```

## Repository layout

```
src/
  Honua.Sdk.Grpc/          gRPC client package (query, stream, edit)
  Honua.Sdk.Admin/          Admin + Geocoding client package
  Honua.Sdk.Wfs/           WFS 2.0 read/query client package
tests/
  Honua.Sdk.Grpc.Tests/     gRPC client tests (42 tests)
  Honua.Sdk.Admin.Tests/    Admin + Geocoding tests (81 tests)
  Honua.Sdk.Wfs.Tests/      WFS client tests (46 tests)
examples/
  FieldDataCollection/      .NET MAUI field-data-collection app
docs/
  quickstart.md             5-minute quickstart tutorial
```

## Documentation

- **[Quickstart](docs/quickstart.md)** -- build a console app that queries
  features, lists services, and geocodes an address in 5 minutes
- **[INSTALL.md](INSTALL.md)** -- NuGet and GitHub Packages setup, version
  policy and server compatibility baseline
- **[Field Data Collection](examples/FieldDataCollection/)** -- full MAUI
  example with offline sync and map views

## License

[Apache 2.0](LICENSE)
