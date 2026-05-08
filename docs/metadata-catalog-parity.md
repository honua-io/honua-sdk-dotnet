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
| STAC asset cataloging | Deferred child issue `honua-sdk-dotnet#147` | STAC protocol identifiers exist in shared SDK semantics, but this repo does not yet expose a concrete STAC client. Keep STAC separate from Records because STAC item/search semantics and asset links are not identical to OGC Records. |
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

## Parity Notes

- Records is standards-facing catalog discovery; it should link to concrete
  protocol resources instead of replacing FeatureServer, OGC Features, WFS, or
  scene metadata clients.
- Admin catalog remains the operator view. It can expose private metadata and
  control-plane resources that a public Records catalog should not expose.
- STAC parity is explicitly not hidden inside the Records client. The linked
  child issue is `honua-sdk-dotnet#147`, pending the server contract and
  cross-SDK semantics from `honua-io/honua-server#955`.
- Keep future endpoint additions aligned with `honua-io/honua-server#955` so
  JS, Python, and .NET SDKs can share names, paging behavior, error behavior,
  auth forwarding, and fixtures.
