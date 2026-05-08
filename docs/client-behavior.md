# Client Behavior

This page documents the cross-cutting behavior that applies to the Honua SDK
clients: timeouts, retries, errors, pagination, and current endpoint coverage.
Runtime support is tracked separately in
[Browser And WebAssembly Support](browser-wasm-support.md).

## Timeouts and cancellation

Every DI-registered client exposes a `Timeout` option. The default is 100
seconds, matching the .NET `HttpClient` default. The value must be greater than
10 milliseconds and less than 24 hours.

```csharp
builder.Services.AddHonuaGrpc(options =>
{
    options.Address = "https://localhost:5001";
    options.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHonuaWfs(options =>
{
    options.BaseAddress = new Uri("https://localhost:5001");
    options.Timeout = TimeSpan.FromSeconds(30);
});
```

For HTTP clients, `Timeout` is applied to the underlying `HttpClient`. When
automatic retry is enabled, it is also applied to the standard resilience
pipeline as both the total request timeout and the per-attempt timeout. For
gRPC, `Timeout` is converted to a per-call deadline. All public async methods
also accept a `CancellationToken`; use it for caller-driven cancellation.

## Retries

Retries are enabled by default and can be disabled per client with
`EnableRetry = false`. `MaxRetryAttempts` is clamped to the supported range of
2 to 5.

| Client family | Retried failures |
|---------------|------------------|
| gRPC | `QueryFeatures` and `QueryFeaturesStream` retries on `Unavailable`, `Internal` |
| Admin, Geocoding, Spec, WFS, GeoServices, OGC API Features | Safe HTTP methods (`GET`, `HEAD`, `OPTIONS`, `TRACE`) retry on `429`, `502`, `503` |

Write operations such as Admin mutations, FeatureServer `applyEdits`, and OGC
API Features create/update/delete calls are not retried by the default policy.
Retrying writes should be an explicit application decision because a server may
apply a mutation before returning a transient failure.

Authentication failures, validation errors, unsupported operations, parser
failures, and application-level protocol errors are not retried by the SDK.

## Error handling

Each package exposes protocol-specific exception types so callers can catch the
right failure surface without parsing strings.

| Package | Exception | Notes |
|---------|-----------|-------|
| `Honua.Sdk.Admin` | `HonuaAdminApiException` | Non-success HTTP status codes. Includes status code and response body. |
| `Honua.Sdk.Admin` | `HonuaAdminOperationException` | Successful HTTP responses that fail the expected Admin contract, such as null envelopes or compatibility failures. |
| `Honua.Sdk.Spec` | `HonuaSpecException` | Non-success spec REST responses, including structured problem-details payloads. |
| `Honua.Sdk.Wfs` | `HonuaWfsException` | HTTP failures, OGC `ExceptionReport` responses, and content-format mismatches. Includes the OGC exception code when available. |
| `Honua.Sdk.GeoServices` | `HonuaFeatureServerException` | HTTP failures and GeoServices JSON error envelopes, including 200 responses that carry an error payload. |
| `Honua.Sdk.Scenes` | `HonuaSceneException` | HTTP failures, invalid scene JSON, malformed scene contracts, and missing required scene capabilities. |
| `Honua.Sdk.OgcFeatures` | `HonuaOgcFeaturesException` | HTTP failures, JSON contract failures, and rejected cross-origin next-page links. |
| `Honua.Sdk.OgcRecords` | `HonuaOgcRecordsException` | HTTP failures, RFC 7807 problem-details payloads, JSON contract failures, and rejected cross-origin next-page links. |
| `Honua.Sdk.Grpc` | `HonuaGrpcException` | Wraps `RpcException` and preserves the gRPC status code. |

`ArgumentNullException`, `ArgumentException`, `InvalidOperationException`, and
`NotSupportedException` are used for local input/configuration problems before
or instead of a remote request.

## Migration source scans

`Honua.Sdk.Admin` exposes the migration scanner through
`HonuaAdminClient.ScanMigrationSourceAsync()`. The method posts
`MigrationInventoryScanRequest` to `/api/v1/admin/import/scan` and returns the
raw `MigrationSourceInventoryArtifact`; this endpoint does not use the usual
Admin `success/data` envelope.

Pass `exportJson: true` to request `/api/v1/admin/import/scan?export=json`.
The server still returns the same source inventory artifact shape, but may send
it as an indented JSON attachment for migration project repositories.

Treat the returned artifact as a planning document. `200 OK` only means the
server produced an artifact; callers must inspect
`scanCompleteness.status` and `overallCompatibility.level`. A successful HTTP
response can still carry `scanCompleteness.status = "failed"` when discovery
could not complete cleanly.

## Pagination

Native clients expose provider-specific pagination helpers, and read clients
also implement `IHonuaFeatureQueryClient.QueryPagesAsync` from
`Honua.Sdk.Abstractions` for provider-neutral paging.

| Client | Pagination behavior |
|--------|---------------------|
| WFS | `GetFeaturesAsyncEnumerable` advances `STARTINDEX`; shared queries advance `FeatureQueryRequest.Offset`. Paging stops when `numberMatched` is reached or a page is empty, with a 100-page safety limit. |
| GeoServices FeatureServer | `QueryPagesAsync` advances `resultOffset` while the server reports `exceededTransferLimit`. |
| OGC API Features | `GetItemsPagesAsync` follows same-origin `rel=next` links. Cross-origin next links are rejected. |
| gRPC | `QueryFeaturesStreamAsync` returns server-streamed pages until `IsLastPage`; `QueryPagesAsync` maps those pages to the shared abstraction. |

## Endpoint coverage

Current typed endpoint coverage is:

| Package | Covered surfaces |
|---------|------------------|
| `Honua.Sdk.Abstractions` | Shared feature query/edit/attachment/stream/source contracts, routing contracts, scene contracts, and host-neutral plugin manifests. |
| `Honua.Sdk.Admin` | Service listing/settings/protocols, catalog discovery, MapServer/access/time/layer metadata settings, metadata resources and manifests, version/capabilities/compatibility/config, secure connections/encryption, layer publishing/table discovery/styles, migration source scans and artifacts, observability, migrations, deploy preflight/plans/operations, and geocoding. |
| `Honua.Sdk.Spec` | Spec validation, plan compilation, apply SSE event streaming, and apply cancellation over `/v1/spec/*`. |
| `Honua.Sdk.Grpc` | Feature query, streaming feature query, and feature edits. |
| `Honua.Sdk.Wfs` | `GetCapabilities`, `DescribeFeatureType`, `GetFeature`, feature count via hits, custom output handlers, and auto-pagination. |
| `Honua.Sdk.GeoServices` | FeatureServer service/layer metadata, query, feature by object ID, count, IDs, extent, statistics, SQL validation, raw query, auto-pagination, layer edit capabilities, and applyEdits/add/update/delete feature edits. |
| `Honua.Sdk.Scenes` | Scene list, scene metadata detail, render endpoint resolution, access envelopes, attribution metadata, and offline scene package manifest parsing/validation. |
| `Honua.Sdk.Field` | Provider-neutral form definitions, source-schema-to-form mapping, field validation, visibility rules, calculated fields, duplicate detection contracts, and record workflow transitions. No transport or display behavior. |
| `Honua.Sdk.Geometry` | NTS/ProjNet-backed geometry conversion, CRS parsing/projection, planar geometry analysis helpers, and host-neutral geofence evaluation. |
| `Honua.Sdk.OgcFeatures` | Landing page, conformance, collections, collection details, queryables, items, item by ID, raw item responses, next-link pagination, and create/update/patch/delete edits. |
| `Honua.Sdk.OgcRecords` | Records landing page, conformance, record collections, collection details, record search, record detail, raw JSON access, query filters, and same-origin next-link pagination. |

Shared read queries are available through `IHonuaFeatureQueryClient` for gRPC,
WFS, GeoServices FeatureServer, and OGC API Features. Shared feature edit
capabilities are available through `IHonuaFeatureEditClient`; gRPC,
GeoServices FeatureServer, and OGC API Features currently advertise write
support, while WFS reports unsupported edit capabilities with a reason.
OGC API Features additionally supports shared RFC 7396 JSON Merge Patch edit
payloads through `FeatureEditRequest.Patches` and native
`IHonuaOgcFeaturesPatchClient.PatchItemAsync()`.

Shared real-time feed contracts are available through
`IHonuaFeatureStreamClient` in `Honua.Sdk.Abstractions`. The SDK normalizes
connect, reconnect, heartbeat, subscribe, unsubscribe, insert, update, and
delete envelopes with source IDs, feature IDs, timestamps, geometry, attributes,
sequence numbers, and sequence tokens. `FeatureStreamEventProcessor` rejects
duplicate and stale sequence events, and `FeatureStreamEventBuffer` provides
bounded backpressure behavior. Concrete server transport wiring remains tied to
`honua-server#339` and `honua-server#692`.

`Honua.Sdk.Abstractions` also exposes the source-oriented facade used for
cross-provider application code: `SourceDescriptor`, `SourceLocator`,
`SourceQuery`, `IHonuaSource`, and `HonuaSource`. It wraps the existing
query/edit interfaces, normalizes protocol aliases such as
`geoservices-featureserver` to `geoservices-feature-service`, and keeps native
clients available through `IHonuaSource.Protocol<TClient>()`.

`Honua.Sdk.Abstractions.Plugins` exposes the SDK-owned plugin contract surface:
`HonuaPluginManifest`, declared permissions, compatibility and edition gates,
capability flags, safe configuration envelopes, and non-UI extension point
descriptors for field validators, calculated fields, data transformers, and
workflow hooks. Host repos own runtime loading, UI registration, sandboxing,
code signing, marketplace behavior, and map/display integration. See
[Plugin Contracts](plugin-contracts.md).

Source descriptor discovery is provider-aware. `IHonuaSource.GetDescriptorAsync`
returns enriched `SourceDescriptor` metadata when the backing client supports
`IHonuaFeatureDescriptorClient`: GeoServices maps FeatureServer layer metadata,
OGC API Features combines collection metadata with queryables, WFS combines
capabilities with DescribeFeatureType, and gRPC derives schema from query
metadata plus an extent-only query until the proto adds a dedicated schema RPC.

Portal/catalog discovery is available through `IHonuaCatalogClient` in
`Honua.Sdk.Admin.Catalog`. It aggregates existing Honua Server admin service
summaries, FeatureServer service/layer metadata, and metadata resources into
searchable service, layer, group, and saved source descriptor items. Groups and
saved source descriptors are metadata-resource backed until the server exposes
first-class typed catalog endpoints tracked by
`honua-server#869`; display documents remain out of SDK scope.

Public standards catalog discovery is available through
`IHonuaOgcRecordsClient` in `Honua.Sdk.OgcRecords`. Use it for OGC API Records
landing/conformance, record collection discovery, record search/detail, and raw
JSON access. Keep STAC, admin catalog, migration inventory, and protocol-native
metadata separate; see [Metadata And Catalog Reads](metadata-catalog-parity.md).

Shared query support is intentionally provider-aware. GeoServices FeatureServer
maps provider-neutral time filters, statistics, group-by, and having clauses to
native query parameters. GeoServices FeatureServer and gRPC map explicit
geometry spatial filters with CRS and spatial relationship values; simple bbox
filters remain available as the cross-provider envelope path. gRPC maps
statistics and group-by through the geospatial proto, but rejects time filters
and having clauses until those contracts exist in the proto. OGC API Features
maps time filters to `datetime` and rejects provider-neutral statistics,
group-by, having, and explicit geometry spatial filters. WFS rejects
provider-neutral time, statistics, and explicit geometry spatial-filter facets
until there is a dedicated shared mapping.

Local geometry analysis lives in `Honua.Sdk.Geometry`. `HonuaPlanarGeometryAnalyzer`
wraps NetTopologySuite for planar distance, area, length, centroid, buffer,
simplify, intersection, containment, overlap, nearest point, nearest vertex, and
envelope operations. ProjNet projection is opt-in through
`PlanarGeometryAnalysisOptions.AnalysisSpatialReference`; planar measurements on
EPSG:4326 coordinates throw by default so callers do not accidentally treat
degrees as meters. True geodesic behavior remains separate from the planar NTS
surface.

Host-neutral geofencing also lives in `Honua.Sdk.Geometry`.
`HonuaGeofenceEvaluator` evaluates current positions and position streams
against NTS boundary geometries with optional planar buffers, proximity
distances, source-query metadata, prepared geometry predicates, and per-track
enter/exit/approach/depart state. It can also consume normalized
`FeatureStreamEvent` sequences, rejecting duplicate or stale stream events before
evaluation. Device sensors, background permissions, notifications, map display,
and platform scheduling remain host-owned. See [Geofencing](geofencing.md).
