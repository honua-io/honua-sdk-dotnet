# Metadata And Catalog Reads

This repo exposes stable, non-UI metadata/catalog reads through protocol-specific
clients. The server-owned parity matrix is tracked in
`honua-io/honua-server#955`; this SDK maps the stable read surfaces without
inventing a separate catalog model for every protocol.

## Which Client To Use

| Need | SDK surface | Notes |
| --- | --- | --- |
| Public standards catalog discovery | `IHonuaOgcRecordsClient` in `Honua.Sdk.OgcRecords` | Use for landing, conformance, record collections, record search, record detail, paging, bbox/time/free-text filters, and raw JSON access. |
| Operator/control-plane discovery | `IHonuaCatalogClient` in `Honua.Sdk.Admin.Catalog` | Aggregates admin service summaries, FeatureServer metadata, metadata resources, groups, and saved source descriptors. Requires admin/control-plane auth. |
| STAC asset cataloging | `IHonuaStacClient` in `Honua.Sdk.Stac` | Use for STAC catalog discovery, collections, item pages, item detail, GET/POST search, asset links, bbox/time filters, fields projection, paging, and raw JSON access. |
| Migration source assessment | Admin migration models and client extensions in `Honua.Sdk.Admin` | Use for external ArcGIS/GeoServer inventory artifacts, compatibility assessments, manifests, and readiness evidence. |
| Exact protocol capabilities | Native protocol clients | Use `Honua.Sdk.GeoServices` for FeatureServer metadata, `Honua.Sdk.OgcFeatures` for collections/queryables/items, `Honua.Sdk.Wfs` for capabilities and DescribeFeatureType, and `Honua.Sdk.Scenes` for scene metadata. |

## OGC API Records Client

`Honua.Sdk.OgcRecords` is the public catalog client. It intentionally mirrors
the existing REST client conventions in this repo:

- `AddHonuaOgcRecords(...)` registers `IHonuaOgcRecordsClient`.
- Auth options match the other REST clients: static credentials, per-request
  providers, request-aware access token providers, diagnostics, primary handler
  factories, retries, and timeout validation.
- HTTP failures raise `HonuaOgcRecordsException`, preserving status,
  response body, and RFC 7807 problem details when present.
- `GetRecordsPagesAsync(...)` follows same-origin `next` links and rejects
  cross-origin next-page redirects.
- `GetRecordsJsonAsync(...)` and `GetRecordJsonAsync(...)` preserve raw JSON
  access for profile fields that are not yet promoted to typed properties.

```csharp
using Honua.Sdk.OgcRecords;
using Honua.Sdk.OgcRecords.Models;

var collections = await recordsClient.ListCollectionsAsync(ct);

var page = await recordsClient.SearchAsync(
    "default",
    new OgcRecordsQuery
    {
        Query = "parks",
        Types = ["service", "layer"],
        Bbox = [-158.4, 21.2, -157.6, 21.9],
        Datetime = "2026-05-01T00:00:00Z/..",
        Limit = 25
    },
    ct);

await foreach (var recordsPage in recordsClient.GetRecordsPagesAsync("default", ct: ct))
{
    foreach (var record in recordsPage.Records ?? [])
    {
        Console.WriteLine(record.Properties?["title"].GetString());
    }
}
```

## STAC Client

`Honua.Sdk.Stac` is the protocol-native asset catalog client added as the
child of `honua-sdk-dotnet#146` in `honua-sdk-dotnet#147`. It stays separate
from Records because STAC has catalog/collection/item/search semantics, asset
links, item assets, fields projection, and search-body behavior that are not
the same as OGC API Records records.

- `AddHonuaStac(...)` registers `IHonuaStacClient`.
- Auth, retry, timeout, diagnostics, and primary handler options match the
  other REST clients.
- `SearchAsync(StacSearchQuery)` uses GET `/stac/search`; `SearchAsync(StacSearchRequest)`
  POSTs JSON to `/stac/search`.
- `GetItemsPagesAsync(...)` and `SearchPagesAsync(...)` follow same-origin
  `next` links and reject cross-origin redirects.
- `GetItemsJsonAsync(...)`, `GetItemJsonAsync(...)`, and `SearchJsonAsync(...)`
  preserve raw JSON access for STAC extensions and profile-specific fields.

```csharp
using Honua.Sdk.Stac;
using Honua.Sdk.Stac.Models;

var collections = await stacClient.ListCollectionsAsync(ct);

var search = await stacClient.SearchAsync(
    new StacSearchQuery
    {
        Collections = ["imagery"],
        Bbox = [-158.4, 21.2, -157.6, 21.9],
        Datetime = "2026-05-01T00:00:00Z/..",
        Filter = "cloud_cover < 10",
        FilterLang = "cql2-text",
        Fields = new StacFields
        {
            Include = ["id", "properties.datetime"],
            Exclude = ["assets.thumbnail"]
        },
        Limit = 25
    },
    ct);

await foreach (var page in stacClient.SearchPagesAsync(new StacSearchRequest
{
    Collections = ["imagery"],
    Limit = 100
}, ct))
{
    foreach (var item in page.Features ?? [])
    {
        Console.WriteLine($"{item.Id}: {item.Assets?.Count ?? 0} assets");
    }
}
```

## Parity Notes

- Records is standards-facing catalog discovery; it should link to concrete
  protocol resources instead of replacing FeatureServer, OGC Features, WFS, or
  scene metadata clients.
- Admin catalog remains the operator view. It can expose private metadata and
  control-plane resources that a public Records catalog should not expose.
- STAC parity is explicitly not hidden inside the Records client. The linked
  child issue is `honua-sdk-dotnet#147`; this SDK surface follows the stable
  routes and cross-SDK semantics from `honua-io/honua-server#955`.
- Keep future endpoint additions aligned with `honua-io/honua-server#955` so
  JS, Python, and .NET SDKs can share names, paging behavior, error behavior,
  auth forwarding, and fixtures.
