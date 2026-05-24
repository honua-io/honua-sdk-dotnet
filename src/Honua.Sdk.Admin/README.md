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

```bash
dotnet add package Honua.Sdk.Admin
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
    layerId: 0,
    cancellationToken);

var events = provider.GetRequiredService<IHonuaAdminFeatureEventsClient>();
var replay = await events.ReplayFeatureEventsAsync(
    new FeatureEventReplayQuery { Cursor = 1001, Limit = 100 },
    cancellationToken);
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
unwrapped before returning typed SDK models. Feature-event replay returns the
raw replay page shape. Non-success HTTP statuses throw
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
