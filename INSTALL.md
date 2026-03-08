# Installing the Honua .NET SDK

## Packages

| Package | Description |
|---------|-------------|
| `Honua.Sdk.Admin` | Admin client for managing services, layers, and configuration |
| `Honua.Sdk.Grpc` | gRPC client for `FeatureService` queries and edits |

## Prerequisites

- .NET 10.0 SDK or later
- A running Honua Server instance

## Install via NuGet

```bash
# gRPC client (most common)
dotnet add package Honua.Sdk.Grpc --prerelease

# Admin client
dotnet add package Honua.Sdk.Admin --prerelease
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

// Register in DI
builder.Services.AddHonuaGrpcClient(options =>
{
    options.BaseUri = new Uri("https://your-honua-server.com");
});

// Use in a service
public class MyService(HonuaFeatureClient client)
{
    public async Task<IReadOnlyList<Feature>> GetFeaturesAsync(int layerId)
    {
        return await client.QueryFeaturesAsync(new QueryFeaturesRequest
        {
            ServiceId = "my-service",
            LayerId = layerId,
            ReturnGeometry = true,
        });
    }
}
```

## Version Policy

- **Pre-release** (`-alpha.*`, `-beta.*`): Published to GitHub Packages on every tag
- **Stable** (`1.0.0+`): Published to NuGet.org after validation

All packages follow [Semantic Versioning](https://semver.org/). Major versions are coordinated across all Honua SDKs.
