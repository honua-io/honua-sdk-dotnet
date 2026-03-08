# Honua .NET SDK

Official .NET client libraries for [Honua](https://github.com/honua-io/honua-server) --
an open-source geospatial feature server. The SDK provides typed clients for
querying features over gRPC, managing services through the Admin REST API, and
geocoding addresses.

## Packages

| Package | Description |
|---------|-------------|
| **Honua.Sdk.Grpc** | gRPC client for `FeatureService` -- typed queries, streaming, spatial filters |
| **Honua.Sdk.Admin** | Admin REST client -- services, layers, connections, styles, metadata |
| *Geocoding* (in Admin) | Forward/reverse geocoding and autocomplete via `IHonuaGeocodingClient` |

## Install

```bash
dotnet add package Honua.Sdk.Grpc --prerelease
dotnet add package Honua.Sdk.Admin --prerelease
```

Pre-release builds are also available from
[GitHub Packages](INSTALL.md#install-from-github-packages-pre-release).

## Quick usage

Register the clients with dependency injection and query features:

```csharp
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Admin.Extensions;

// Register clients
builder.Services.AddHonuaGrpc(o => o.Address = "https://localhost:5001");
builder.Services.AddHonuaAdmin(o => o.BaseAddress = new Uri("https://localhost:5001"));
builder.Services.AddHonuaGeocoding(o => o.BaseAddress = new Uri("https://localhost:5001"));

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

## Repository layout

```
src/
  Honua.Sdk.Grpc/          gRPC client package
  Honua.Sdk.Admin/          Admin + Geocoding client package
tests/
  Honua.Sdk.Grpc.Tests/     gRPC client tests
  Honua.Sdk.Admin.Tests/    Admin client tests
examples/
  FieldDataCollection/      .NET MAUI field-data-collection app
docs/
  quickstart.md             5-minute quickstart tutorial
```

## Documentation

- **[Quickstart](docs/quickstart.md)** -- build a console app that queries
  features, lists services, and geocodes an address in 5 minutes
- **[INSTALL.md](INSTALL.md)** -- NuGet and GitHub Packages setup, version
  policy
- **[Field Data Collection](examples/FieldDataCollection/)** -- full MAUI
  example with offline sync and map views
- **[gRPC vs Forms](examples/FieldDataCollection/GRPC_FORMS_COMPARISON.md)**
  -- choosing between gRPC queries and XForms for data collection

## License

[Apache 2.0](LICENSE)
