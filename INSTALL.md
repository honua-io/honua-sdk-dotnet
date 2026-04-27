# Installing the Honua .NET SDK

## Packages

| Package | Description |
|---------|-------------|
| `Honua.Sdk.Admin` | Admin client for managing services, layers, and configuration |
| `Honua.Sdk.Grpc` | gRPC client for `FeatureService` queries and edits |
| `Honua.Sdk.Wfs` | WFS 2.0 read/query client for GetCapabilities, GetFeature, DescribeFeatureType |

## Prerequisites

- .NET 10.0 SDK or later
- A running Honua Server instance

## Install via NuGet

```bash
# gRPC client (most common)
dotnet add package Honua.Sdk.Grpc --prerelease

# Admin client
dotnet add package Honua.Sdk.Admin --prerelease

# WFS 2.0 client
dotnet add package Honua.Sdk.Wfs --prerelease
```

## Install from GitHub Packages (pre-release)

Add the Honua GitHub Packages source:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/honua-io/index.json" \
  --name honua \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT
```

Then install:

```bash
dotnet add package Honua.Sdk.Grpc --prerelease --source honua
```

## Quick Start

```csharp
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Grpc.Models;

// Register in DI
builder.Services.AddHonuaGrpc(options =>
{
    options.Address = "https://your-honua-server.com";
});

// Use in a service
public class MyService(IHonuaGrpcClient client)
{
    public async Task<IReadOnlyList<Feature>> GetFeaturesAsync(int layerId)
    {
        var response = await client.QueryFeaturesAsync(new QueryFeaturesRequest
        {
            ServiceId = "my-service",
            LayerId = layerId,
            ReturnGeometry = true,
        });

        return response.Features;
    }
}
```

## Version Policy

- **Pre-release** (`-alpha.*`, `-beta.*`): Published to GitHub Packages on every tag
- **Stable** (`1.0.0+`): Published to NuGet.org after validation

All packages follow [Semantic Versioning](https://semver.org/). Major versions are coordinated across all Honua SDKs.

## Server Compatibility Baseline

`Honua.Sdk.Admin` currently requires Honua Server
`HonuaAdminCompatibility.MinimumSupportedServerVersion` or newer and a minimum
server release channel baseline of `preview`. `CheckCompatibilityAsync()`
evaluates that server against `GET /api/v1/admin/capabilities`, including
control-plane API major `1` and base path `/api/v1/admin`, and also surfaces
coarse feature flags for metadata and manifest workflows.

Typical startup flow:

```csharp
using Honua.Sdk.Admin;

var compatibility = await adminClient.CheckCompatibilityAsync();

if (!compatibility.IsSupported)
{
    throw new InvalidOperationException(
        compatibility.UnsupportedReason ??
        "The connected Honua Server is not supported by this SDK.");
}

if (compatibility.Features.ManifestExport)
{
    var manifest = await adminClient.GetManifestAsync();
}
```

The same compatibility gate is the first remote step in the
[Admin Bootstrap Console](examples/AdminBootstrapConsole/README.md) sample
before any connection, publish, or service mutation.

Use `GetCapabilitiesAsync()` directly when you need the raw compatibility
metadata, including `releaseChannel`, `metadataSchemas`, and the
`manifestDryRun` / `manifestPrune` feature flags.

See [docs/compatibility.md](docs/compatibility.md) for the full server matrix
and the CI package API compatibility gate used before publish.

## Authentication Transport

When `ApiKey` or `BearerToken` is configured on the Admin or gRPC clients, the
SDK only sends those credentials over HTTPS. The only HTTP exception is
loopback / `localhost` for local development, which is the path used by the
admin bootstrap sample against local Docker Compose defaults.
