# Honua.Sdk

Umbrella / meta package for the Honua .NET SDK.

Installing this one package brings in every other `Honua.Sdk.*` package and
exposes a single `AddHonua(o => o.BaseAddress = ...)` DI extension that fans
the shared cross-cutting configuration (base address, authentication, retry /
timeout, primary HTTP handler) out across every enabled sub-package.

```bash
dotnet add package Honua.Sdk
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

After that, every enabled client (`IHonuaGrpcClient`, `IHonuaAdminClient`,
`IHonuaOgcFeaturesClient`, `IHonuaWfsClient`, `IHonuaGeocodingClient`, plus
the shared `IHonuaFeatureQueryClient` / `IHonuaFeatureEditClient` /
`IHonuaFeatureAttachmentClient` abstractions) is available for injection.

## Module opt-in flags

The umbrella registers the core query / edit / admin trio by default. Flip the
`Use*` flags to enable the more situational sub-packages or to opt out of any
default module:

| Flag | Default | Registers |
|---|---|---|
| `UseGrpc` | `true` | `IHonuaGrpcClient` (gRPC FeatureService) |
| `UseAdmin` | `true` | `IHonuaAdminClient` + `IHonuaCatalogClient` (REST control plane) |
| `UseGeocoding` | `true` | `IHonuaGeocodingClient` + `IHonuaBatchGeocodingClient` |
| `UseOgcFeatures` | `true` | `IHonuaOgcFeaturesClient` |
| `UseWfs` | `true` | `IHonuaWfsClient` (ships inside Honua.Sdk.OgcFeatures) |
| `UseGeoServices` | `false` | `IHonuaFeatureServerClient` |
| `UseRouting` | `false` | `IHonuaRoutingClient` (NAServer) |
| `UseScenes` | `false` | `IHonuaSceneClient` |
| `UseSpec` | `false` | `IHonuaSpecClient` (validate / plan / apply) |
| `UseStac` | `false` | `IHonuaStacClient` |
| `UseOgcRecords` | `false` | `IHonuaOgcRecordsClient` |

```csharp
builder.Services.AddHonua(o =>
{
    o.BaseAddress = new Uri("https://your-honua-server");

    // Enable everything situational the app needs:
    o.UseStac       = true;
    o.UseOgcRecords = true;
    o.UseScenes     = true;

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
dotnet add package Honua.Sdk.Grpc       
dotnet add package Honua.Sdk.Admin      
dotnet add package Honua.Sdk.OgcFeatures
```

```csharp
builder.Services.AddHonuaGrpc       (o => o.BaseAddress = serverUri);
builder.Services.AddHonuaAdmin      (o => o.BaseAddress = serverUri);
builder.Services.AddHonuaOgcFeatures(o => o.BaseAddress = serverUri);
```

The umbrella is purely a build-time aggregator. It does not duplicate any of
the code that lives in a sub-package — it just calls into them.

See the [repo README](https://github.com/honua-io/honua-sdk-dotnet) and
[INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md)
for the full catalog of packages and the per-package guides.
