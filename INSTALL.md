# Installing the Honua .NET SDK

## Packages

| Package | Description |
|---------|-------------|
| `Honua.Sdk` | **Umbrella / meta** -- one install + one `AddHonua(o => o.BaseAddress = ...)` registers every enabled sub-package. Recommended starting point. The narrower per-package facades below remain available unchanged. |
| `Honua.Sdk.Abstractions` | Shared feature query/edit/stream abstractions implemented by provider-specific clients, Console shell/route/environment contracts, and browser-safe offline sync contracts (manifests, sync state, checkpoints, conflicts, storage) |
| `Honua.Sdk.Offline` | Provider-neutral offline push/pull planner and sync engine over the shared feature abstractions |
| `Honua.Sdk.Admin` | Admin client for managing services, layers, connections, styles, metadata, roles, users, alerts, observability, feature-event replay, and streaming subscriber operations. Also exposes Catalog discovery (`IHonuaCatalogClient`, `AddHonuaCatalog`) and Geocoding (`IHonuaGeocodingClient`, `AddHonuaGeocoding`) using the same options/auth handler. |
| `Honua.Sdk.Processes` | OGC API Processes REST client for process discovery, async job submission, polling, dismissal, results, and shared job models |
| `Honua.Sdk.Spec` | Spec workspace client for validate, plan, apply stream, cancel, and cached artifact retrieval |
| `Honua.Sdk.Studio` | Console Studio analysis-report read client -- retrieve the structured report envelope and render Markdown/HTML for completed jobs |
| `Honua.Sdk.ConsoleShare` | Console Share clients -- read share detail, update access, validate dependency closure, manage public-link / embed-token lifecycle, drive scheduled exports and traffic, and publish open-data (DCAT / STAC) |
| `Honua.Sdk.Field` | Field form, validation, calculated field, duplicate detection, and record workflow contracts |
| `Honua.Sdk.Grpc` | gRPC client for `FeatureService` queries/edits and native `ProcessService` job lifecycle access |
| `Honua.Sdk.Geometry` | NTS/ProjNet-backed geometry conversion, spatial references, projection, planar analysis, and geofence evaluation |
| `Honua.Sdk.GeoServices` | GeoServices FeatureServer read/query/edit client. Also exposes NAServer Routing (`IHonuaRoutingClient`, `AddHonuaRouting`) using the same options/auth handler. |
| `Honua.Sdk.Scenes` | Scene metadata, endpoint resolution, and offline scene package contracts |
| `Honua.Sdk.OgcFeatures` | OGC API Features read/query client plus WFS 2.0 read surface (GetCapabilities, GetFeature, DescribeFeatureType) |
| `Honua.Sdk.Catalogs` | OGC API Records + STAC catalog client (landing pages, conformance, collections, item / record search and paging) |

## Prerequisites

- .NET 10.0 SDK or later
- A running Honua Server instance

## Install stable releases from nuget.org

Stable release tags publish to nuget.org and require no Honua-specific feed
configuration once all public dependencies are available there. Until the first
public-feed release, use the GitHub Packages instructions below. Afterward, the
default NuGet source used by `dotnet` is sufficient.

```bash
# Umbrella / meta package -- one install brings in every Honua.Sdk.* package
# and exposes a single AddHonua(o => o.BaseAddress = ...) DI extension.
dotnet add package Honua.Sdk

# Or pick narrower packages individually:

# Shared read/query/edit/stream abstractions (lightweight; depend on this from libraries)
dotnet add package Honua.Sdk.Abstractions

# Provider-neutral offline sync planner and engine
# (Offline sync contracts now ship in Honua.Sdk.Abstractions.)
dotnet add package Honua.Sdk.Offline

# gRPC client (most common server transport)
dotnet add package Honua.Sdk.Grpc

# NTS/ProjNet-backed geometry, CRS, and geofence engine
dotnet add package Honua.Sdk.Geometry

# Admin / Catalog / Geocoding (REST) client
dotnet add package Honua.Sdk.Admin

# OGC API Processes REST client
dotnet add package Honua.Sdk.Processes

# Spec workspace client
dotnet add package Honua.Sdk.Spec

# Console Studio analysis-report read client
dotnet add package Honua.Sdk.Studio

# Console Share access, export, traffic, and open-data clients
dotnet add package Honua.Sdk.ConsoleShare

# Field form and record workflow contracts
dotnet add package Honua.Sdk.Field

# OGC / GeoServices read/query clients
# (WFS 2.0 surface now ships inside Honua.Sdk.OgcFeatures.)
dotnet add package Honua.Sdk.GeoServices
dotnet add package Honua.Sdk.OgcFeatures

# Metadata / catalog clients
dotnet add package Honua.Sdk.Scenes
dotnet add package Honua.Sdk.Catalogs
```

All SDK packages share one package version from `Directory.Build.props`.
Release tags use `dotnet-sdk-v<PackageVersion>`, for example
`dotnet-sdk-v1.0.0`. See [Release and NuGet Publishing](docs/release.md)
for the publish workflow and versioning rules.

## Install from GitHub Packages

Use GitHub Packages for prerelease builds that are not published to nuget.org.
Dry runs publish to neither feed; maintainers can inspect their package
artifacts on the corresponding GitHub Actions run.

Add the Honua GitHub Packages source:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/honua-io/index.json" \
  --name honua \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT
```

This repository also contains `NuGet.config` with a `github-honua` source used
by CI. To restore locally with that source name, run:

```bash
dotnet nuget update source github-honua \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text
```

The SDK gRPC and Geometry packages currently depend on the generated
`Geospatial.Grpc` protocol package from GitHub Packages; no sibling repo should
copy protocol source files to satisfy that dependency. The Geometry dependency
is retained for 1.x compatibility and is scheduled to move to a protocol
adapter package in the next major release.

Then install -- pick the packages that match your transport / workload:

```bash
# Umbrella / meta — easiest single install
dotnet add package Honua.Sdk --source honua

# Or pick narrower packages individually:
dotnet add package Honua.Sdk.Abstractions --source honua
dotnet add package Honua.Sdk.Offline --source honua
dotnet add package Honua.Sdk.Grpc --source honua
dotnet add package Honua.Sdk.Geometry --source honua
dotnet add package Honua.Sdk.Admin --source honua
dotnet add package Honua.Sdk.Processes --source honua
dotnet add package Honua.Sdk.Spec --source honua
dotnet add package Honua.Sdk.Studio --source honua
dotnet add package Honua.Sdk.ConsoleShare --source honua
dotnet add package Honua.Sdk.Field --source honua
dotnet add package Honua.Sdk.GeoServices --source honua
dotnet add package Honua.Sdk.Scenes --source honua
dotnet add package Honua.Sdk.OgcFeatures --source honua
dotnet add package Honua.Sdk.Catalogs --source honua
```

## Quick Start

```csharp
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Grpc.Models;

// Register in DI
builder.Services.AddHonuaGrpc(options =>
{
    options.BaseAddress = new Uri("https://your-honua-server.com");
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

- **Stable** (`1.0.0+`): All releases from `1.0.0` onward are stable and
  follow [Semantic Versioning](https://semver.org/). Breaking changes are
  gated behind a major version bump.
- **Pre-1.0 history**: Releases prior to `1.0.0` shipped as `0.1.x-alpha.*`
  on GitHub Packages and are retained for historical reference only. New
  consumers should track `1.0.0` or later.

Major versions are coordinated across all Honua SDKs.

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

When `ApiKey`, `BearerToken`, `ApiKeyProvider`, or `BearerTokenProvider` is
configured on any SDK client, the SDK only sends those credentials over HTTPS.
The only HTTP exception is loopback / `localhost` for local development, which
is the path used by the admin bootstrap sample against local Docker Compose
defaults.

Use credential providers for refresh, revocation, and key rotation:

```csharp
builder.Services.AddHonuaAdmin(o =>
{
    o.BaseAddress = new Uri("https://honua.example.com");
    o.BearerTokenProvider = ct => tokenCache.GetAccessTokenAsync(ct);
});
```

Providers run before each request or RPC. Returning null or an empty string
omits the credential header. Store secrets in your application-owned secure
store and see [docs/authentication.md](docs/authentication.md) for storage,
failure, and retry behavior.
