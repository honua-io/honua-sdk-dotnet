# Honua.Sdk

Umbrella / meta package for the Honua .NET SDK.

Installing this one package brings in every other `Honua.Sdk.*` package and
exposes a single `AddHonua(o => o.BaseAddress = ...)` DI extension that fans
the shared cross-cutting configuration (base address, authentication, retry /
timeout, primary HTTP handler) out across every enabled sub-package.

Honua SDK packages are currently published to the authenticated GitHub Packages
feed only — nuget.org publishing is planned but not yet available. One-time
setup: configure the feed with a GitHub **classic** PAT that has the
`read:packages` scope, then install with `--source honua`. Full setup (CI,
package source mapping): [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

```bash
dotnet nuget add source https://nuget.pkg.github.com/honua-io/index.json \
  --name honua --username YOUR_GITHUB_USERNAME --password YOUR_CLASSIC_PAT \
  --store-password-in-clear-text
dotnet add package Honua.Sdk --source honua
```

```csharp
using Honua.Sdk;

builder.Services.AddHonua(o =>
{
    o.BaseAddress    = new Uri("https://your-honua-server");
    o.BearerToken    = jwt;                  // shared across every enabled client
    // o.BearerTokenProvider = ct => ...;   // also forwarded
});
```

After that, the common default clients (`IHonuaGrpcClient`,
`IHonuaProcessGrpcClient`, `IHonuaAdminClient`, `IHonuaOgcFeaturesClient`,
`IHonuaProcessesClient`, `IHonuaWfsClient`, `IHonuaGeocodingClient`, plus the
shared `IHonuaFeatureQueryClient` / `IHonuaFeatureEditClient` /
`IHonuaFeatureAttachmentClient` abstractions) are available for injection.
Opt-in modules such as Studio and ConsoleShare are available after enabling
their `Use*` flag.

## Module opt-in flags

The umbrella registers the common default query, edit, admin, geocoding,
process, and WFS clients by default. Flip the `Use*` flags to enable the more
situational sub-packages or to opt out of any default module:

| Flag | Default | Registers |
|---|---|---|
| `UseGrpc` | `true` | `IHonuaGrpcClient` (gRPC FeatureService) + `IHonuaProcessGrpcClient` (native ProcessService) |
| `UseAdmin` | `true` | `IHonuaAdminClient` + `IHonuaCatalogClient` (REST control plane) |
| `UseGeocoding` | `true` | `IHonuaGeocodingClient` + `IHonuaBatchGeocodingClient` |
| `UseOgcFeatures` | `true` | `IHonuaOgcFeaturesClient` |
| `UseProcesses` | `true` | `IHonuaProcessesClient` (OGC API Processes REST) |
| `UseWfs` | `true` | `IHonuaWfsClient` (ships inside Honua.Sdk.OgcFeatures) |
| `UseGeoServices` | `false` | `IHonuaFeatureServerClient` |
| `UseRouting` | `false` | `IHonuaRoutingClient` (NAServer) |
| `UseImageServer` | `false` | ImageServer raster client + provider-neutral `IHonuaRasterDataClient` (raster metadata, coverage statistics, windowed reads) |
| `UseScenes` | `false` | `IHonuaSceneClient` |
| `UseSpec` | `false` | `IHonuaSpecClient` (validate / plan / apply / artifacts) |
| `UseStac` | `false` | `IHonuaStacClient` |
| `UseOgcRecords` | `false` | `IHonuaOgcRecordsClient` |
| `UseStudio` | `false` | `IHonuaStudioReportsClient` (analysis report retrieve / render) |
| `UseConsoleShare` | `false` | `IHonuaConsoleShareClient` (share detail and access policy) |
| `UseGeoprocessingProfile` | `false` | `IHonuaFeatureGateway` + the GeoServices FeatureServer client it routes attachments and time/having queries to |

### Geoprocessing (GP) feature profile

The workhorse gRPC `FeatureService` exposes only
`QueryFeatures`/`QueryFeaturesStream`/`ApplyEdits`: it has **no attachment RPCs**
and **no provider-neutral time-filter or grouped-statistics `having` contract**.
A GP tool that reads features over gRPC therefore used to hit
`NotSupportedException` the moment it touched media or a time-aware / summary
query.

Enable `UseGeoprocessingProfile` to register the unified `IHonuaFeatureGateway`
(plus the GeoServices FeatureServer client it needs). The gateway routes each
operation to a capable provider: attachments resolve over GeoServices even when
features stream over gRPC, and temporal / `having` queries transparently fall
back from gRPC to a time/having-capable provider. Every feature query client also
exposes `QueryCapabilities` (`SupportsTimeFilter`, `SupportsHaving`,
`SupportsStatistics`, `SupportsGroupBy`) so a tool can pick a provider up front
instead of relying on routing.

```csharp
builder.Services.AddHonua(o =>
{
    o.BaseAddress = new Uri("https://your-honua-server");
    o.UseGeoprocessingProfile = true; // gateway + GeoServices, no NotSupportedException

    // later, in a GP tool — attachments and temporal queries just work:
    // var gateway = sp.GetRequiredService<IHonuaFeatureGateway>();
    // var media   = await gateway.ListAttachmentsAsync(listRequest);
    // var summary = await gateway.QueryAsync(temporalStatsRequest);
});
```

```csharp
builder.Services.AddHonua(o =>
{
    o.BaseAddress = new Uri("https://your-honua-server");

    // Enable everything situational the app needs:
    o.UseStac       = true;
    o.UseOgcRecords = true;
    o.UseScenes     = true;
    o.UseStudio     = true;
    o.UseConsoleShare = true;

    // Or trim a default off if the app does not need it:
    o.UseGeocoding  = false;
});
```

Setting every `Use*` flag to `false` is treated as misconfiguration and throws
`HonuaConfigurationException` at registration time.

## Want narrower dependencies?

The per-package `AddHonua*` extensions remain available and unchanged. Use
them directly if you want to depend on only one (or a few) of the sub-packages
without pulling in the rest:

```bash
dotnet add package Honua.Sdk.Grpc --source honua
dotnet add package Honua.Sdk.Admin --source honua
dotnet add package Honua.Sdk.OgcFeatures --source honua
dotnet add package Honua.Sdk.Processes --source honua
dotnet add package Honua.Sdk.Studio --source honua
dotnet add package Honua.Sdk.ConsoleShare --source honua
```

```csharp
builder.Services.AddHonuaGrpc       (o => o.BaseAddress = serverUri);
builder.Services.AddHonuaAdmin      (o => o.BaseAddress = serverUri);
builder.Services.AddHonuaOgcFeatures(o => o.BaseAddress = serverUri);
builder.Services.AddHonuaProcesses  (o => o.BaseAddress = serverUri);
builder.Services.AddHonuaStudio     (o => o.BaseAddress = serverUri);
builder.Services.AddHonuaConsoleShare(o => o.BaseAddress = serverUri);
```

The umbrella is purely a build-time aggregator. It does not duplicate any of
the code that lives in a sub-package — it just calls into them.

See the [repo README](https://github.com/honua-io/honua-sdk-dotnet) and
[INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md)
for the full catalog of packages and the per-package guides.
