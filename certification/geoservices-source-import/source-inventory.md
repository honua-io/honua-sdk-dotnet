# ArcGIS importer source inventory (#341 AC1)

This table lists every source operation and wire field that honua-server's importer
transport reads, mapped to the `Honua.Sdk.GeoServices` API that covers it. The importer
transport is `src/Honua.Core/Features/Migration/Services/ArcGisRestClient.cs` on
the manifest-pinned honua-server `87966c3f7b6c840ffc4d4da0b451714ab717b18a`
(rechecked 2026-09-26). The operation and wire-field denominator is unchanged
from the previous `2cc2213` inventory.

Status values:

- **typed 1.8.0**: a typed member in the published 1.8.0 package.
- **preserved 1.8.0**: round-trips losslessly through `AdditionalProperties` in 1.8.0 but
  has no typed member.
- **typed 1.9.0**: typed by honua-sdk-dotnet#379 and published in 1.9.0; also
  present in the certified 1.10.0 package. `package.typed-importer-metadata`
  directly asserts these members against an authored transport fixture, including
  subtype defaults, int64 defaults, WKT and the #383 field tri-state flags.

All rows are now available in published 1.10.0. Version labels below record when
each API first became available; they are not unresolved publication work.
The 1.10.0 consumer also exercises `GetServiceMetadataAsync` and
`GetLayerMetadataAsync` (published in 1.9.0 by #389): the
`metadata.raw-source-documents` live cell checks exact member presence and values
against independent source reads. These are the SDK-owned reads used by the newer
server importer adapter; no customer-authored HTTP is needed.

The certification cell or unit test that proves each row is named in the last column.

## Operations

| Importer operation | Request | SDK 1.8.0 API | Proof |
| --- | --- | --- | --- |
| `DiscoverServiceAsync` | `{root}?f=json` | `GetServiceInfoAsync` (FeatureServer or MapServer under any root via `ArcGisServiceRoot`) | `discovery.*`, `metadata.service.lossless-roundtrip`; `ArcGisSourceRootAddressingTests` |
| `GetLayerInfoAsync` | `{root}/{id}?f=json` | `GetLayerInfoAsync` | `metadata.layer.*`, `schema.*` |
| layer count enrichment, `QueryFeatureCountAsync` | `query?where=…&returnCountOnly=true` | `QueryCountAsync` | `data.count-and-ids` |
| `QueryObjectIdsAsync` | `query?where=…&returnIdsOnly=true` | `QueryIdsAsync` | `data.count-and-ids` |
| `QueryFeaturesAsync` (offset) | `query?…&returnGeometry=true&returnZ=true&returnM=true&resultOffset&resultRecordCount[&outSR]` | `QueryPagesAsync` with `ReturnZ`/`ReturnM`/`OutSR` | `paging.offset-pages`, `paging.offset-ignoring-source.offset-pages`, `geometry.z-m` |
| `QueryFeaturesAsync` (object IDs) | `query?…&objectIds=…` | `QueryAllFeaturesByObjectIdBatchesAsync` | `paging.object-id-batches`, `paging.offset-ignoring-source.object-id-batches` |
| `QueryAttachmentsAsync` | `queryAttachments?objectIds=…&returnUrl=false` | `QueryAttachmentsAsync` | released live (`data.attachments`); `ArcGisSourceRootAddressingTests` |
| `DownloadAttachmentAsync` | `{id}/{oid}/attachments/{attachmentId}` (headers-read stream) | `DownloadAttachmentAsync` | released live (`data.attachments`); `HonuaFeatureServerClientTests` |

## Transport behaviour

| Importer behaviour | SDK 1.8.0 | Proof |
| --- | --- | --- |
| Token/OAuth credential header, Basic credentials | `ArcGisSourceCredentialHandler` (token parameter, bearer, basic, per-request providers) | `auth.token`, `auth.bearer`, `auth.basic` |
| 401/403/498/499 classified as authentication failures | `HonuaFeatureServerException.StatusCode` / `GeoServicesErrorCode` | `auth.none-rejected`, `auth.invalid-token`, `auth.basic-rejected` |
| HTTP-200 `error` envelope becomes a failure with details | `HonuaFeatureServerException.Details` | `errors.http200-envelope.*` |
| Retry on 5xx/429 honouring `Retry-After` | `MaxRetryAttempts`, `HonuaFeatureServerException.RetryAfter` | `transport.retry-after-429` |
| Injected handler (pinned-DNS SSRF policy stays in the server) | injectable primary handler / `HttpClient` | `transport.injected-primary-handler` |
| Caller cancellation aborts discovery and paging | `CancellationToken` on every operation | `transport.cancellation` |
| Bounded source bodies (`MigrationHttpContentReader.DefaultMaxResponseBytes`) | `MaxResponseBytes`, `HonuaFeatureServerResponseTooLargeException` | `ArcGisSourceResponseLimitTests`; `transport.bounded-streaming` |

## Wire fields

| Response | Field | SDK member | Status |
| --- | --- | --- | --- |
| service | `serviceDescription`, `maxRecordCount`, `capabilities`, `supportedQueryFormats`, `spatialReference`, `layers[].id/name`, `tables` | `FeatureServerServiceInfo` | typed 1.8.0 |
| service | `description`, `currentVersion` | `Description`, `CurrentVersion` | preserved 1.8.0; typed 1.9.0 |
| layer | `id`, `name`, `description`, `type`, `geometryType`, `hasZ`, `hasM`, `supportsPagination`, `advancedQueryCapabilities.supportsPagination`, `maxRecordCount`, `hasAttachments`, `extent` (+`spatialReference.wkid`), `fields`, `drawingInfo`, `typeIdField`, `types`, `subtypes` | `FeatureServerLayerInfo` | typed 1.8.0 |
| layer | `minScale`, `maxScale`, `subtypeField`, `defaultSubtypeCode`, `attributeRules` | `MinScale`, `MaxScale`, `SubtypeField`, `DefaultSubtypeCode`, `AttributeRules` | preserved 1.8.0; typed 1.9.0 |
| field | `name`, `type`, `alias`, `length`, `nullable`, `domain` (+ `editable`, `defaultValue`) | `FeatureServerField` | typed 1.8.0 |
| count / IDs | `count`, `objectIds` | `QueryCountAsync`, `QueryIdsAsync` | typed 1.8.0 |
| features | `features[].attributes` (raw `JsonElement`: int64, GUID, null, dates exact), `features[].geometry` (raw, including Z/M and `curvePaths`/`curveRings`), `exceededTransferLimit`, `spatialReference` | `FeatureServerQueryResponse` | typed 1.8.0 |
| error | `error.code`, `error.message`, `error.details` | `HonuaFeatureServerException` | typed 1.8.0 |
| attachments | `attachmentGroups[].parentObjectId/parentGlobalId/attachmentInfos[].id/name/contentType/size/keywords` | `FeatureServerAttachmentModels` | typed 1.8.0 |

True-curve geometry is preserved verbatim as raw JSON. `GeoServicesGeometryConverter.ReadGeometry`
rejects it with a `JsonException` instead of linearising it (pinned by
`QueryAsync_TrueCurveGeometry_IsPreservedVerbatimAndNeverCoercedToLinearGeometry` in #379).
