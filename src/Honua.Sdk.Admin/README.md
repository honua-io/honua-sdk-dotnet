# Honua.Sdk.Admin

REST client for the Honua Admin API: service settings, metadata resources and
manifests, secure database connections, published layers, styles, identity
providers, RBAC, users, license, observability, deploy plans, alert rules,
feature-event replay, and streaming subscriber operations. Also ships the
portal-style Catalog client and the GeoServices-compatible Geocoding client.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

Honua SDK packages are currently published to the authenticated GitHub Packages
feed only — nuget.org publishing is planned but not yet available. One-time
setup: configure the feed with a GitHub **classic** PAT that has the
`read:packages` scope, then install with `--source honua`. Full setup (CI,
package source mapping): [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

```bash
dotnet nuget add source https://nuget.pkg.github.com/honua-io/index.json \
  --name honua --username YOUR_GITHUB_USERNAME --password YOUR_CLASSIC_PAT \
  --store-password-in-clear-text
dotnet add package Honua.Sdk.Admin --source honua
```

## Quick usage

```csharp
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Extensions;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddHonuaAdmin(o => o.BaseAddress = new Uri("https://your-honua-server"));
var provider = services.BuildServiceProvider();

var client = provider.GetRequiredService<IHonuaAdminClient>();

var compatibility = await client.CheckCompatibilityAsync(cancellationToken);
var summaries = await client.ListServicesAsync(cancellationToken);
foreach (var service in summaries)
{
    var settings = await client.GetServiceSettingsAsync(service.ServiceName, cancellationToken);
    Console.WriteLine($"{service.ServiceName}: {string.Join(',', settings.EnabledProtocols)}");
}
```

For application code, prefer the narrow sub-interface that matches the
workflow instead of depending on the aggregate `IHonuaAdminClient`.
`AddHonuaAdmin` registers all sub-interfaces against the same underlying
`HonuaAdminClient`.

```csharp
using Honua.Sdk.Admin.Models;

var users = provider.GetRequiredService<IHonuaAdminUsersClient>();
var permissions = await users.GetEffectivePermissionsAsync(
    "user-operator",
    cancellationToken);

var alerts = provider.GetRequiredService<IHonuaAdminAlertsClient>();
var rules = await alerts.ListAlertRulesAsync(
    serviceId: "parcels",
    layerId: 1,
    cancellationToken);

var events = provider.GetRequiredService<IHonuaAdminFeatureEventsClient>();
var replay = await events.ReplayFeatureEventsAsync(
    new FeatureEventReplayQuery { Cursor = 1001, Limit = 100 },
    cancellationToken);
```

## GeoServices/migration import lifecycle

Discover an ArcGIS service, queue a single-layer import, and wait for it to
reach a terminal status without ever risking a duplicate import on retry:

```csharp
using Honua.Sdk.Admin.Models;

var admin = provider.GetRequiredService<IHonuaAdminClient>();

var discovered = await admin.DiscoverGeoservicesServiceAsync(new GeoservicesDiscoverRequest
{
    ServiceUrl = "https://gis.example.com/arcgis/rest/services/Parcels/FeatureServer",
    Credentials = new GeoservicesCredentialDescriptor
    {
        Mode = "token",
        AccessTokenSecretReference = "secret://arcgis/token"
    }
});

var job = await admin.StartGeoservicesImportAsync(new GeoservicesStartImportRequest
{
    ServiceUrl = discovered.ServiceUrl,
    LayerId = discovered.Layers[0].Id,
    TableName = "parcels",
    TargetSrid = 4326,
    AutoPublish = true
});

// Reconnects to the job by polling status only; safe to call again after a
// client restart because it never re-invokes StartGeoservicesImportAsync.
var terminal = await admin.WaitForGeoservicesImportJobAsync(job.JobId);

if (terminal.Status is GeoservicesImportStatus.NeedsReview)
{
    var findings = terminal.ReconciliationArtifact!.Reasons;
    Console.WriteLine($"Import needs review: {string.Join("; ", findings)}");
}
```

Import several dependency-ordered layers as one resumable batch, and poll its
rolled-up status plus per-child job ids:

```csharp
var batch = await admin.StartMigrationBatchAsync(new MigrationBatchStartRequest
{
    SourceKind = "arcgis-geoservices-rest",
    Layers =
    [
        new MigrationBatchLayerSpec
        {
            SourceResourceId = "res:parcels",
            ServiceUrl = discovered.ServiceUrl,
            LayerId = 0,
            TableName = "parcels"
        },
        new MigrationBatchLayerSpec
        {
            SourceResourceId = "res:zoning",
            ServiceUrl = discovered.ServiceUrl,
            LayerId = 1,
            TableName = "zoning",
            DependsOn = ["res:parcels"]
        }
    ]
});

var status = await admin.GetMigrationBatchAsync(batch.BatchId);
foreach (var child in status.Children)
{
    Console.WriteLine($"{child.SourceResourceId}: {child.Status} (job {child.JobId})");
}
```

Independently, retrieve a migration run's signed reconciliation scorecard once
the run (recorded separately by the migration engine) completes:

```csharp
var scorecard = await admin.GetMigrationRunReconciliationScorecardAsync(runId);
Console.WriteLine($"Data reconciliation verdict: {scorecard.Verdict}");
```

## Console control-plane contracts

The Admin package includes the stable Console P0 control-plane surface used by
Blazor Web and MAUI hosts:

| Workflow | Interface |
|---|---|
| Service, layer, protocol, connection, style, manifest, deploy, and compatibility operations | `IHonuaAdminClient` or the existing narrow service-specific interfaces |
| Route guard roles and permission grants | `IHonuaAdminRolesClient` |
| User list, role assignment, deprovisioning, and effective permissions | `IHonuaAdminUsersClient` |
| Alert zones and alert rules | `IHonuaAdminAlertsClient` |
| Feature-change replay pages | `IHonuaAdminFeatureEventsClient` |
| Streaming subscriber list and disconnect | `IHonuaAdminStreamingOperationsClient` |

Most Admin endpoints use the server's `ApiResponse<T>` envelope and are
unwrapped before returning typed SDK models. Table discovery returns raw
`TableDiscoveryResponse`, and feature-event replay returns raw
`FeatureEventReplayResponse`, because those current server endpoints are not
envelope-wrapped. Admin requests are emitted as camelCase JSON, while response
binding is case-insensitive so the server's current PascalCase replay payload
(`Events`, `NextCursor`, `HasMore`, and event fields) binds to
`FeatureEventReplayResponse`. Non-success HTTP statuses throw
`HonuaAdminApiException`; successful responses that fail the expected contract
throw `HonuaAdminOperationException`.

### Catalog and Geocoding

```csharp
using Honua.Sdk.Admin.Catalog;
using Honua.Sdk.Admin.Geocoding;

services.AddHonuaCatalog(o => o.BaseAddress = new Uri("https://your-honua-server"));
services.AddHonuaGeocoding(o => o.BaseAddress = new Uri("https://your-honua-server"));

var catalog = provider.GetRequiredService<IHonuaCatalogClient>();
var hits = await catalog.SearchAsync(
    new CatalogQueryOptions { Query = "parcels", Limit = 25 },
    cancellationToken);

var geocoder = provider.GetRequiredService<IHonuaGeocodingClient>();
var candidates = await geocoder.ForwardGeocodeAsync(
    "1600 Amphitheatre Pkwy, Mountain View, CA",
    options: null,
    cancellationToken);
```

### Raster import (write/output)

`IHonuaAdminRasterImportClient` (part of `IHonuaAdminClient`) uploads a raster into
PostGIS via the admin import endpoint. This is the write half of the raster
geoprocessing round-trip; the read half is the read-only `IHonuaRasterDataClient`
(`ReadWindowAsync`) in `Honua.Sdk.GeoServices`.

```csharp
var admin = provider.GetRequiredService<IHonuaAdminClient>();

await using var tiff = File.OpenRead("output.tif");
var result = await admin.ImportRasterAsync(new RasterImportRequest
{
    Content = tiff,
    FileName = "output.tif",
    LayerId = 7,
    Name = "GP result",
    Srid = 4326,
}, cancellationToken);

var formats = await admin.GetSupportedRasterFormatsAsync(cancellationToken);
```

## Server compatibility

`IHonuaAdminClient.CheckCompatibilityAsync` reports whether the connected Honua
Server matches this SDK baseline. See
[compatibility.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/compatibility.md)
for the policy and supported version matrix.

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Console client contracts](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/console-client-contracts.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Metadata catalog parity](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/metadata-catalog-parity.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
