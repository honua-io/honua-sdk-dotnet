# API reference

## Hosted reference

The full DocFX-generated API reference is published to GitHub Pages at
<https://honua-io.github.io/honua-sdk-dotnet/>. Each merge to `trunk` rebuilds
and redeploys the site via `.github/workflows/docs.yml`; if the link is not
yet live, it will start serving after the first push-to-`trunk` of this
workflow.

DocFX URLs follow the convention `api/<Namespace>.<Type>.html` (for example,
`api/Honua.Sdk.Grpc.IHonuaGrpcClient.html`). The deep links throughout this
document use that form so a fresh reader can jump straight from a name they
see in code into the generated reference.

The SDK ships full XML documentation for every public type and member. There
are three ways to browse the reference, from lightest to richest:

## 1. From the IDE (no setup)

Every published NuGet package includes its `.xml` documentation file and a
`.snupkg` symbols package, with [SourceLink](https://github.com/dotnet/sourcelink)
metadata baked into the PDBs by `Directory.Build.props`. Once you reference a
Honua SDK package, hover over any type or method in Visual Studio / Rider /
VS Code (with the C# extension) to see the doc summary; "Go to definition" /
F12 will source-step straight into the matching GitHub commit.

## 2. Build a local DocFX site (one-off)

`docs/docfx.json` is a minimal DocFX 2 project configured to scrape XML docs
from every `src/Honua.Sdk.*` build output. After building the solution at
least once:

```bash
dotnet tool update --global docfx
dotnet build Honua.Sdk.sln --configuration Release
docfx docs/docfx.json --serve
```

DocFX prints a local URL (`http://localhost:8080` by default) you can open
in a browser to browse the full API reference, with rendered XML doc
summaries, parameter docs, and per-package navigation.

## 3. Browse the source on GitHub

Each per-package README on
[nuget.org](https://www.nuget.org/profiles/honua) links back to the
`src/Honua.Sdk.<X>/` directory on GitHub. Browsing the source there gives
you the full surface plus complete XML doc blocks. Both INSTALL.md and
each per-package README link to the exact `trunk` directory.

## Repository conventions

- Every public type and member carries an XML `<summary>` block.
  `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is set in
  each `src/Honua.Sdk.*/*.csproj`, so the `.xml` ships in the NuGet package.
- `TreatWarningsAsErrors=true` means missing-doc warnings fail the build,
  enforcing the convention.
- The cancellation parameter is always named `cancellationToken` (FDG /
  CA1068 convention). The base address property is always `BaseAddress`
  (`Uri`) across REST and gRPC. The SDK never falls back to a localhost
  default; callers must set `BaseAddress` explicitly.

## Common entry points (cheat sheet)

The table below maps the canonical client interface to the package that
owns it and a DocFX deep link. Inject the interface in your services after
the matching `AddHonua*` extension registers it.

| Interface | Package | DocFX |
|---|---|---|
| `IHonuaGrpcClient` | `Honua.Sdk.Grpc` | [api/Honua.Sdk.Grpc.IHonuaGrpcClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Grpc.IHonuaGrpcClient.html) |
| `IHonuaAdminClient` | `Honua.Sdk.Admin` | [api/Honua.Sdk.Admin.IHonuaAdminClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Admin.IHonuaAdminClient.html) |
| `IHonuaCatalogClient` | `Honua.Sdk.Admin` (`Honua.Sdk.Admin.Catalog`) | [api/Honua.Sdk.Admin.Catalog.IHonuaCatalogClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Admin.Catalog.IHonuaCatalogClient.html) |
| `IHonuaGeocodingClient` | `Honua.Sdk.Admin` (`Honua.Sdk.Admin.Geocoding`) | [api/Honua.Sdk.Admin.Geocoding.IHonuaGeocodingClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Admin.Geocoding.IHonuaGeocodingClient.html) |
| `IHonuaStacClient` | `Honua.Sdk.Catalogs` (`Honua.Sdk.Catalogs.Stac`) | [api/Honua.Sdk.Catalogs.Stac.IHonuaStacClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Catalogs.Stac.IHonuaStacClient.html) |
| `IHonuaOgcFeaturesClient` | `Honua.Sdk.OgcFeatures` | [api/Honua.Sdk.OgcFeatures.IHonuaOgcFeaturesClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.OgcFeatures.IHonuaOgcFeaturesClient.html) |
| `IHonuaWfsClient` | `Honua.Sdk.OgcFeatures` (`Honua.Sdk.OgcFeatures.Wfs`) | [api/Honua.Sdk.OgcFeatures.Wfs.IHonuaWfsClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.OgcFeatures.Wfs.IHonuaWfsClient.html) |
| `IHonuaOgcRecordsClient` | `Honua.Sdk.Catalogs` (`Honua.Sdk.Catalogs.Records`) | [api/Honua.Sdk.Catalogs.Records.IHonuaOgcRecordsClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Catalogs.Records.IHonuaOgcRecordsClient.html) |
| `IHonuaSceneClient` | `Honua.Sdk.Scenes` (interface in `Honua.Sdk.Abstractions.Scenes`) | [api/Honua.Sdk.Abstractions.Scenes.IHonuaSceneClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Abstractions.Scenes.IHonuaSceneClient.html) |
| `IHonuaSpecClient` | `Honua.Sdk.Spec` | [api/Honua.Sdk.Spec.IHonuaSpecClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Spec.IHonuaSpecClient.html) |
| `IHonuaStudioReportsClient` | `Honua.Sdk.Studio` | [api/Honua.Sdk.Studio.IHonuaStudioReportsClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Studio.IHonuaStudioReportsClient.html) |
| `IHonuaProcessesClient` | `Honua.Sdk.Processes` | [api/Honua.Sdk.Processes.IHonuaProcessesClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Processes.IHonuaProcessesClient.html) |
| `IHonuaProcessGrpcClient` | `Honua.Sdk.Grpc` | [api/Honua.Sdk.Grpc.IHonuaProcessGrpcClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Grpc.IHonuaProcessGrpcClient.html) |
| `IHonuaFeatureServerClient` | `Honua.Sdk.GeoServices` | [api/Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerClient.html) |
| `IHonuaRoutingClient` | `Honua.Sdk.GeoServices` (interface in `Honua.Sdk.Abstractions.Routing`) | [api/Honua.Sdk.Abstractions.Routing.IHonuaRoutingClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Abstractions.Routing.IHonuaRoutingClient.html) |
| `IHonuaFeatureQueryClient` | `Honua.Sdk.Abstractions` | [api/Honua.Sdk.Abstractions.Features.IHonuaFeatureQueryClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Abstractions.Features.IHonuaFeatureQueryClient.html) |
| `IHonuaFeatureStreamClient` | `Honua.Sdk.Abstractions` | [api/Honua.Sdk.Abstractions.Features.IHonuaFeatureStreamClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Abstractions.Features.IHonuaFeatureStreamClient.html) |
| `IReplicaSyncClient` | `Honua.Sdk.Offline` (interface in `Honua.Sdk.Abstractions.Offline`) | [api/Honua.Sdk.Abstractions.Offline.IReplicaSyncClient.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Abstractions.Offline.IReplicaSyncClient.html) |

## Package map (14 sub-packages + the meta package)

Each row links to the package README, the canonical types you reach for
first, and the DocFX namespace landing page.

| Package | README | Canonical types | DocFX namespace |
|---|---|---|---|
| `Honua.Sdk` (umbrella meta-package) | [src/Honua.Sdk/README.md](../src/Honua.Sdk/README.md) | `HonuaSdkOptions`, `AddHonua(...)` | [api/Honua.Sdk.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.html) |
| `Honua.Sdk.Abstractions` | [src/Honua.Sdk.Abstractions/README.md](../src/Honua.Sdk.Abstractions/README.md) | `IHonuaFeatureQueryClient`, `IHonuaFeatureEditClient`, `IHonuaFeatureAttachmentClient`, `IHonuaFeatureStreamClient`, `SourceDescriptor`, `SourceQuery`, `HonuaConsoleShellDescriptor`, `HonuaEnvironmentProfile`, `HonuaAnalysisReport`, `HonuaAnalysisResultPackage`, `HonuaException`, `HonuaConfigurationException`, `HonuaPluginManifest` | [api/Honua.Sdk.Abstractions.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Abstractions.html) |
| `Honua.Sdk.Offline` | [src/Honua.Sdk.Offline/README.md](../src/Honua.Sdk.Offline/README.md) | `OfflineDownloadPlanner`, `OfflineSyncEngine`, `ReplicaSyncClient`, `OfflinePackageManifest`, `OfflineSourceDescriptor` (last two live in `Honua.Sdk.Abstractions.Offline`) | [api/Honua.Sdk.Offline.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Offline.html) |
| `Honua.Sdk.Grpc` | [src/Honua.Sdk.Grpc/README.md](../src/Honua.Sdk.Grpc/README.md) | `IHonuaGrpcClient`, `IHonuaProcessGrpcClient`, `HonuaGrpcClient`, `HonuaGrpcClientOptions`, `QueryFeaturesRequest`, `QueryFeaturesResponse`, `ApplyEditsRequest` | [api/Honua.Sdk.Grpc.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Grpc.html) |
| `Honua.Sdk.Admin` | [src/Honua.Sdk.Admin/README.md](../src/Honua.Sdk.Admin/README.md) | `IHonuaAdminClient` (composes 17 sub-interfaces), `IHonuaAdminRolesClient`, `IHonuaAdminUsersClient`, `IHonuaAdminAlertsClient`, `IHonuaAdminFeatureEventsClient`, `IHonuaAdminStreamingOperationsClient`, `IHonuaCatalogClient`, `IHonuaGeocodingClient`, `HonuaAdminClientOptions` | [api/Honua.Sdk.Admin.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Admin.html) |
| `Honua.Sdk.Processes` | [src/Honua.Sdk.Processes/README.md](../src/Honua.Sdk.Processes/README.md) | `IHonuaProcessesClient`, `HonuaProcessesClientOptions`, `HonuaProcessList`, `HonuaProcessExecuteRequest`, `HonuaProcessExecuteInputs`, `HonuaProcessJobStatus`, `HonuaProcessResults` | [api/Honua.Sdk.Processes.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Processes.html) |
| `Honua.Sdk.Spec` | [src/Honua.Sdk.Spec/README.md](../src/Honua.Sdk.Spec/README.md) | `IHonuaSpecClient`, `HonuaSpecClientOptions`, `SpecDocumentRequest`, `SpecValidateRequest`, `SpecApplyStream`, `HonuaSpecArtifact` | [api/Honua.Sdk.Spec.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Spec.html) |
| `Honua.Sdk.Studio` | [src/Honua.Sdk.Studio/README.md](../src/Honua.Sdk.Studio/README.md) | `IHonuaStudioReportsClient`, `HonuaStudioReportsClient`, `HonuaStudioClientOptions`, `HonuaRenderedReport`, `HonuaStudioApiException`, `HonuaStudioContractException` | [api/Honua.Sdk.Studio.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Studio.html) |
| `Honua.Sdk.ConsoleShare` | [src/Honua.Sdk.ConsoleShare/README.md](../src/Honua.Sdk.ConsoleShare/README.md) | `IHonuaConsoleShareClient`, `IHonuaConsoleShareExportClient`, `IHonuaConsoleShareOpenDataClient`, `HonuaConsoleShareClient`, `HonuaConsoleShareClientOptions` | [api/Honua.Sdk.ConsoleShare.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.ConsoleShare.html) |
| `Honua.Sdk.Field` | [src/Honua.Sdk.Field/README.md](../src/Honua.Sdk.Field/README.md) | `FormDefinition`, `FormField`, `FieldRecord`, `FormValidator`, `FormValidationResult` | [api/Honua.Sdk.Field.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Field.html) |
| `Honua.Sdk.Geometry` | [src/Honua.Sdk.Geometry/README.md](../src/Honua.Sdk.Geometry/README.md) | `HonuaSpatialReference`, `HonuaCoordinateTransformer`, `HonuaPlanarGeometryAnalyzer`, `HonuaGeofenceEvaluator`, `GeometryText`, `GeoJsonGeometryConverter` | [api/Honua.Sdk.Geometry.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Geometry.html) |
| `Honua.Sdk.GeoServices` | [src/Honua.Sdk.GeoServices/README.md](../src/Honua.Sdk.GeoServices/README.md) | `IHonuaFeatureServerClient`, `IHonuaFeatureServerEditClient`, `IHonuaRoutingClient`, `HonuaGeoServicesClientOptions` | [api/Honua.Sdk.GeoServices.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.GeoServices.html) |
| `Honua.Sdk.Scenes` | [src/Honua.Sdk.Scenes/README.md](../src/Honua.Sdk.Scenes/README.md) | `IHonuaSceneClient`, `HonuaSceneClient`, `HonuaSceneClientOptions`, `HonuaSceneListRequest`, `HonuaSceneResolveRequest` | [api/Honua.Sdk.Scenes.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Scenes.html) |
| `Honua.Sdk.OgcFeatures` | [src/Honua.Sdk.OgcFeatures/README.md](../src/Honua.Sdk.OgcFeatures/README.md) | `IHonuaOgcFeaturesClient`, `IHonuaOgcFeaturesEditClient`, `IHonuaOgcFeaturesPatchClient`, `IHonuaWfsClient`, `OgcItemsParams`, `OgcFeatureCollection`, `OgcQueryables` | [api/Honua.Sdk.OgcFeatures.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.OgcFeatures.html) |
| `Honua.Sdk.Catalogs` | [src/Honua.Sdk.Catalogs/README.md](../src/Honua.Sdk.Catalogs/README.md) | `IHonuaStacClient`, `StacSearchQuery`, `StacSearchRequest`, `StacItemsQuery`, `StacItemCollection`, `IHonuaOgcRecordsClient`, `OgcRecordsQuery`, `OgcRecord`, `OgcRecordsCollection` | [api/Honua.Sdk.Catalogs.html](https://honua-io.github.io/honua-sdk-dotnet/api/Honua.Sdk.Catalogs.html) |

## Worked example: resolve a client from DI

A fresh reader should be able to copy this verbatim. It is the shortest path
from "I have a `BaseAddress`" to "I have a typed client". Replace the URL
with your Honua server.

```csharp
// requires a running Honua server at this URL
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.Grpc.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHonuaGrpc(options =>
{
    options.BaseAddress = new Uri("https://localhost:5001");
});

using var host = builder.Build();
var grpc = host.Services.GetRequiredService<IHonuaGrpcClient>();

var response = await grpc.QueryFeaturesAsync(
    new QueryFeaturesRequest
    {
        ServiceId = "parks",
        LayerId = 0,
        ResultRecordCount = 5,
    },
    cancellationToken: default);

Console.WriteLine($"Returned {response.Features.Count} features.");
```

The same pattern applies to every other package: pull in `Honua.Sdk.<X>`,
call `AddHonua<X>(o => o.BaseAddress = new Uri(...))`, then resolve the
canonical interface listed in the cheat sheet above.

## How it composes

- The 13 sub-packages share contracts from `Honua.Sdk.Abstractions`. If a
  type lives there (for example, `SourceDescriptor`, `IHonuaSceneClient`,
  `IReplicaSyncClient`), the surface is the same regardless of which
  transport package implements it. See [architecture.md](architecture.md)
  for the layering rationale.
- Authentication is shared via `IHonuaAuthenticationOptions` (every
  `Honua<X>ClientOptions` implements it). See
  [authentication.md](authentication.md) for the token flows.
- Errors are normalized into `HonuaException` and
  `HonuaConfigurationException` from `Honua.Sdk.Abstractions`. See
  [troubleshooting.md](troubleshooting.md) for diagnostic surfaces.

## Pitfalls

- The `Honua.Sdk.Abstractions.Internal.*` namespace is for SDK-internal use
  even when type-public; no compatibility guarantee. Do not import it.
- The vendored `geospatial-grpc` generated code (`Geospatial.V1.*` /
  `Honua.V1.*` under the `obj/` build outputs) is consumed but not part of
  the SDK's stable surface — use the typed wrappers in
  `Honua.Sdk.Grpc.Models` instead.
- `BaseAddress` is required. Calling `AddHonua*` without setting it throws
  `HonuaConfigurationException` at first use rather than silently falling
  back to `localhost`.
- Disposing a DI-resolved `IHonuaGrpcClient` tears down the gRPC channel
  for every other consumer in the container. Let DI manage the lifetime;
  the interface intentionally does not extend `IDisposable`.

## See also

- [quickstart.md](quickstart.md) — end-to-end "hello features" with the
  packages above.
- [authentication.md](authentication.md) — token flows and the shared
  `IHonuaAuthenticationOptions` contract.
- [architecture.md](architecture.md) — layering, abstractions, and where
  each package sits.
- [troubleshooting.md](troubleshooting.md) — error-string lookup table for
  `HonuaException` and friends.
- [client-behavior.md](client-behavior.md) — retries, timeouts, paging.
