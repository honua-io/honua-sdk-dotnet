# Honua.Sdk.ConsoleShare

Browser-safe Console Share client and shared contracts for Honua Console hosts
(Blazor Web and optional .NET MAUI Blazor Hybrid). Wraps the server Console Share
access, public-link, and embed-token lifecycle surface so Console hosts consume
server-owned share contracts without duplicating DTOs.

## Install

Install directly when a host only needs Console Share access and link/embed
controls.

Honua SDK packages are currently published to the authenticated GitHub Packages
feed only — nuget.org publishing is planned but not yet available. One-time
setup: configure the feed with a GitHub **classic** PAT that has the
`read:packages` scope, then install with `--source honua`. Full setup (CI,
package source mapping): [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

```bash
dotnet nuget add source https://nuget.pkg.github.com/honua-io/index.json \
  --name honua --username YOUR_GITHUB_USERNAME --password YOUR_CLASSIC_PAT \
  --store-password-in-clear-text
dotnet add package Honua.Sdk.ConsoleShare --source honua
```

```csharp
using Honua.Sdk.ConsoleShare.Extensions;

builder.Services.AddHonuaConsoleShare(o =>
{
    o.BaseAddress = new Uri("https://your-honua-server");
    o.BearerTokenProvider = tokenProvider.GetAccessTokenAsync;
});
```

Use `IHonuaConsoleShareClient` to read a share detail, update access, validate a
dependency closure before a visibility change, and manage the public-link and
embed-token lifecycle. Use `IHonuaConsoleShareExportClient` (register with
`AddHonuaConsoleShareExport`) to manage scheduled Share export definitions and
runs and to read Share traffic projections. Use `IHonuaConsoleShareOpenDataClient`
(register with `AddHonuaConsoleShareOpenData`) to author open-data pages, evaluate
open-data eligibility, preview DCAT/data.json exports, drive STAC publish/unpublish,
and read the anonymous open-data, DCAT, Schema.org, and STAC projections. The share
contracts live in `Honua.Sdk.Abstractions` (namespace
`Honua.Sdk.Abstractions.Console.Share`) so browser and native hosts share one DTO set.

## REST surface

The client targets the Console Share surface under `/api/v1/console/shares`.

| SDK method | HTTP contract |
|---|---|
| `GetShareAsync` | `GET /api/v1/console/shares/{shareId}` |
| `UpdateAccessAsync` | `PUT /api/v1/console/shares/{shareId}/access` |
| `ValidateDependencyClosureAsync` | `POST /api/v1/console/shares/{shareId}/access/validate` |
| `CreatePublicLinkAsync` | `PUT /api/v1/console/shares/{shareId}/public-link` |
| `RevokePublicLinkAsync` | `DELETE /api/v1/console/shares/{shareId}/public-link` |
| `CreateEmbedTokenAsync` | `PUT /api/v1/console/shares/{shareId}/embed-token` |
| `RevokeEmbedTokenAsync` | `DELETE /api/v1/console/shares/{shareId}/embed-token` |

`IHonuaConsoleShareExportClient` targets the Share export and traffic admin
surface under `/api/v1/admin/share`.

| SDK method | HTTP contract |
|---|---|
| `ListExportDefinitionsAsync` | `GET /api/v1/admin/share/exports` |
| `CreateExportDefinitionAsync` | `POST /api/v1/admin/share/exports` |
| `GetExportDefinitionAsync` | `GET /api/v1/admin/share/exports/{exportId}` |
| `UpdateExportDefinitionAsync` | `PUT /api/v1/admin/share/exports/{exportId}` |
| `DeleteExportDefinitionAsync` | `DELETE /api/v1/admin/share/exports/{exportId}` |
| `TriggerExportAsync` | `POST /api/v1/admin/share/exports/{exportId}/trigger` |
| `PauseExportAsync` | `POST /api/v1/admin/share/exports/{exportId}/pause` |
| `ResumeExportAsync` | `POST /api/v1/admin/share/exports/{exportId}/resume` |
| `ListExportRunsAsync` | `GET /api/v1/admin/share/exports/{exportId}/runs` |
| `GetExportRunAsync` | `GET /api/v1/admin/share/exports/{exportId}/runs/{runId}` |
| `GetTrafficSummaryAsync` | `GET /api/v1/admin/share/traffic` |
| `GetTrafficSeriesAsync` | `GET /api/v1/admin/share/traffic/series` |
| `GetItemTrafficSummaryAsync` | `GET /api/v1/admin/services/{serviceName}/layers/{layerId}/share/traffic` |
| `GetItemTrafficSeriesAsync` | `GET /api/v1/admin/services/{serviceName}/layers/{layerId}/share/traffic/series` |

`IHonuaConsoleShareOpenDataClient` targets the authenticated open-data publication
surface under `/api/v1/console/content/{id}/open-data` and the anonymous open-data
read surface under `/api/v1/open-data`.

| SDK method | HTTP contract |
|---|---|
| `GetPageAsync` | `GET /api/v1/console/content/{id}/open-data` |
| `UpdatePageAsync` | `PUT /api/v1/console/content/{id}/open-data` |
| `GetEligibilityAsync` | `GET /api/v1/console/content/{id}/open-data/eligibility` |
| `PreviewDcatAsync` | `GET /api/v1/console/content/{id}/open-data/dcat` |
| `GetStacPublicationAsync` | `GET /api/v1/console/content/{id}/open-data/stac` |
| `PublishStacAsync` | `POST /api/v1/console/content/{id}/open-data/stac/publish` |
| `UnpublishStacAsync` | `DELETE /api/v1/console/content/{id}/open-data/stac` |
| `GetPublicDatasetAsync` | `GET /api/v1/open-data/datasets/{id}` |
| `GetPublicDataJsonAsync` | `GET /api/v1/open-data/datasets/{id}/data.json` |
| `GetPublicSchemaOrgAsync` | `GET /api/v1/open-data/datasets/{id}/schema.org` |
| `GetPublicStacCatalogAsync` | `GET /api/v1/open-data/stac` |
| `GetPublicStacCollectionAsync` | `GET /api/v1/open-data/stac/collections/{collectionId}` |
| `GetPublicStacItemAsync` | `GET /api/v1/open-data/stac/collections/{collectionId}/items/{itemId}` |

The authenticated open-data endpoints and the anonymous dataset read return data
inside the server's `{ "success", "data", ... }` envelope; the client unwraps the
`data` payload. The anonymous data.json, Schema.org, and STAC reads return
standards-shaped documents directly.

`GetShareAsync` returns `HonuaShareItemDetail` (the share summary plus grants and
any active public link / embed token) deserialized from the **unwrapped** server
JSON. All request and response bodies resolve through a source-generated JSON
context, so serialization is trimming/AOT-safe.

```csharp
var detail = await share.GetShareAsync(shareId, cancellationToken);

if (detail.Item.Visibility != HonuaShareVisibility.Public)
{
    var closure = await share.ValidateDependencyClosureAsync(
        shareId,
        new HonuaShareAccessUpdate { Visibility = HonuaShareVisibility.Public },
        cancellationToken);

    if (closure.Valid)
    {
        await share.UpdateAccessAsync(
            shareId,
            new HonuaShareAccessUpdate { Visibility = HonuaShareVisibility.Public },
            cancellationToken);

        var link = await share.CreatePublicLinkAsync(
            shareId,
            new HonuaPublicLinkRequest { Enabled = true },
            cancellationToken);

        Console.WriteLine(link.Url);
    }
}
```

## Errors

Non-success HTTP statuses throw `HonuaConsoleShareApiException` (preserving
`StatusCode`, problem `Title`/`Detail`, and the raw `ResponseBody`) — including
anonymous/forbidden states surfaced as `401`/`403` problem documents. Successful
responses whose body does not satisfy the share contract throw
`HonuaConsoleShareContractException`.

## Scope

This package wraps the Console Share access, public-link, and embed-token
lifecycle surface, the Share export-definition / export-run and Share-traffic
admin surface, and the open-data / DCAT / STAC publication surface (authenticated
page authoring, eligibility, DCAT preview, and STAC publish/unpublish, plus the
anonymous open-data, DCAT, Schema.org, and STAC read projections).

## Documentation

- [Console client contracts](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/console-client-contracts.md)
- [Client behavior](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/client-behavior.md)
- [Browser / WASM support](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/browser-wasm-support.md)
