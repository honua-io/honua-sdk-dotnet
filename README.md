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
using Honua.Sdk.Grpc.Models;
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

## Admin bootstrap flow

For the canonical runnable sample app for this repo's bootstrap and publish
operator flow, see
[examples/AdminBootstrapConsole](examples/AdminBootstrapConsole/).

- `CheckCompatibilityAsync()` is the first remote call. It validates server
  version `0.1.0` or newer, release channel `preview` or newer, control-plane
  API major `1`, and base path `/api/v1/admin`.
- Existing connections are reused only when the configured name also matches
  host, port, database, username, and SSL settings. Same-name connections that
  point somewhere else fail fast.
- Existing layers are reused only when the configured service and source table
  match. The sample enables the layer and union-adds `Grpc` to the current
  enabled protocol list instead of replacing it.
- Publishing requires discovery metadata for the geometry column, geometry
  type, SRID, and a single primary key.
- Verification uses a bounded `QueryFeaturesAsync()` request with
  `Where = "1=1"`, `ReturnGeometry = false`, `ResultRecordCount = 3`,
  `OrderBy = primary key`, and `OutFields` selected from discovery metadata.

## Repository layout

```
src/
  Honua.Sdk.Grpc/          gRPC client package (query, stream, edit)
  Honua.Sdk.Admin/          Admin + Geocoding client package
  Honua.Sdk.Wfs/           WFS 2.0 read/query client package
tests/
  Honua.Sdk.Grpc.Tests/     gRPC client tests (42 tests)
  Honua.Sdk.Admin.Tests/    Admin + Geocoding tests (81 tests)
  Honua.Sdk.Wfs.Tests/      WFS client tests (52 tests)
examples/
  AdminBootstrapConsole/     Canonical console sample for admin bootstrap + gRPC verification
  FieldDataCollection/      Advanced .NET MAUI reference app (not the primary onboarding sample)
docs/
  quickstart.md             5-minute quickstart tutorial
  staging-integration.md    Staging CI inputs, evidence, and troubleshooting
```

## Documentation

- **[Admin Bootstrap Console](examples/AdminBootstrapConsole/)** -- the
  canonical sample app for this repo; bootstrap a PostGIS table with
  `Honua.Sdk.Admin`, preserve existing protocols while enabling `Grpc`, verify
  it with a bounded `Honua.Sdk.Grpc` query, and troubleshoot the exact error
  surfaces returned by the sample
- **[Quickstart](docs/quickstart.md)** -- build a console app that queries
  features via gRPC and WFS, lists services, and geocodes an address in 5 minutes
- **[Staging Integration Guide](docs/staging-integration.md)** -- staging
  environment inputs, CI evidence artifacts, common failures, and bounded
  follow-on tickets for shared staging ownership
- **[INSTALL.md](INSTALL.md)** -- NuGet and GitHub Packages setup, version
  policy and server compatibility baseline
- **[Field Data Collection](examples/FieldDataCollection/)** -- full MAUI
  reference app with offline sync and map views

## License

[Apache 2.0](LICENSE)
