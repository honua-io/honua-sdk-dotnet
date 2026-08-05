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
| `Honua.Sdk.GeoServices` | GeoServices FeatureServer read/query/edit client. Also exposes NAServer Routing (`IHonuaRoutingClient`, `AddHonuaRouting`) and the ImageServer raster client (`IHonuaRasterDataClient`, `AddHonuaImageServer` -- raster metadata, coverage statistics, windowed reads) using the same options/auth handler. |
| `Honua.Sdk.Scenes` | Scene metadata, endpoint resolution, and offline scene package contracts |
| `Honua.Sdk.OgcFeatures` | OGC API Features read/query client plus WFS 2.0 read surface (GetCapabilities, GetFeature, DescribeFeatureType) |
| `Honua.Sdk.Catalogs` | OGC API Records + STAC catalog client (landing pages, conformance, collections, item / record search and paging) |
| `Honua.Sdk.Cli` | Global .NET tool providing the support-safe, schema-pinned `honua doctor` diagnostic emitter and read-only replay |

## Prerequisites

- .NET 10.0 SDK or later
- A running Honua Server instance

## Install from GitHub Packages (current channel)

All Honua .NET SDK releases — stable `1.x` and prerelease alike — are currently
published to the authenticated Honua GitHub Packages feed
(`https://nuget.pkg.github.com/honua-io/index.json`) only. Nothing is on
nuget.org yet, so a bare `dotnet add package Honua.Sdk*` fails with `NU1101`;
see [Planned: nuget.org](#planned-nugetorg-not-yet-available) below. Dry runs
publish to neither feed; maintainers can inspect their package artifacts on the
corresponding GitHub Actions run.

### 1. Authenticate to the feed

The GitHub Packages NuGet endpoint requires authentication even for public
packages. Create a GitHub **classic** personal access token with the
`read:packages` scope (the NuGet endpoint does not accept fine-grained tokens),
then add the source:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/honua-io/index.json" \
  --name honua \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text
```

`--store-password-in-clear-text` is required on Linux and macOS, where NuGet
cannot encrypt stored passwords; it writes the PAT into your user-level
`NuGet.config` in plain text. Keep the token scoped to `read:packages` only,
and on shared or CI machines prefer an environment-substituted `NuGet.config`
committed next to your solution instead:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="honua" value="https://nuget.pkg.github.com/honua-io/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <honua>
      <add key="Username" value="%GITHUB_USERNAME%" />
      <add key="ClearTextPassword" value="%GITHUB_TOKEN%" />
    </honua>
  </packageSourceCredentials>
</configuration>
```

If your `NuGet.config` uses `<packageSourceMapping>` (this repository's own
`NuGet.config` does), the Honua patterns — including the `Geospatial.Grpc`
protocol dependency — must map to the Honua source or restore will fail even
with credentials in place:

```xml
<packageSourceMapping>
  <packageSource key="nuget.org">
    <package pattern="*" />
  </packageSource>
  <packageSource key="honua">
    <package pattern="Honua.Sdk" />
    <package pattern="Honua.Sdk.*" />
    <package pattern="Geospatial.Grpc" />
  </packageSource>
</packageSourceMapping>
```

To restore this repository itself, its `NuGet.config` already defines the feed
as `github-honua`; supply your credentials with:

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

### 2. Install packages

Pick the packages that match your transport / workload:

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

### 3. Install the CLI tool

Install `Honua.Sdk.Cli` as a global .NET tool rather than an application
dependency. `dotnet tool install` does not read a repository `NuGet.config`,
so the feed must be passed explicitly with `--add-source`, and the feed
credentials must already be configured for that source URL (step 1 above
stores them in your user-level NuGet config):

```bash
dotnet tool install --global Honua.Sdk.Cli \
  --add-source https://nuget.pkg.github.com/honua-io/index.json
honua doctor --help
```

All SDK packages share one package version from `Directory.Build.props`.
Release tags use `dotnet-sdk-v<PackageVersion>`, for example
`dotnet-sdk-v1.0.0`. See [Release and NuGet Publishing](docs/release.md)
for the publish workflow and versioning rules.

## Planned: nuget.org (not yet available)

Stable release tags will additionally publish to nuget.org once the
`Geospatial.Grpc` protocol dependency has a stable public release there;
publishing is wired into the release workflow but deliberately deferred until
then. Once live, the default nuget.org source will be sufficient and no
Honua-specific feed configuration will be needed. Until that happens, use the
GitHub Packages instructions above for every version, stable or prerelease.

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
