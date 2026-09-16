# GeoServices source-import certification

This directory certifies lossless ArcGIS service/layer import through the
**installed** `Honua.Sdk.GeoServices` package against a live ArcGIS REST source,
for [honua-sdk-dotnet#341](https://github.com/honua-io/honua-sdk-dotnet/issues/341).

It is not part of `Honua.Sdk.sln` and never builds against SDK source: the
consumer references the package by version, restores it from nuget.org only, and
proves the loaded assembly is byte-identical to the one inside the restored
package, whose SHA-512 must equal the nuget.org catalog `packageHash`.

## Layout

| Path | Purpose |
| --- | --- |
| `fixture/source-sites.v1.sql` | Versioned source fixture (`source-import-fixture.v1`). Every literal makes a lossy decode observable. |
| `fixture/expected.v1.json` | Oracle derived by hand from the fixture SQL, plus released cells with reasons. |
| `consumer/` | Installed-package consumer; isolated from the repository build and package sources. |
| `run.sh` | Boots the digest-pinned candidate, publishes the fixture, runs the consumer and the negative control. |
| `evidence/<pin>/<package>/` | Receipts, logs and source wire captures from a run. |
| `source-inventory.md` | Every importer source operation and wire field mapped to the SDK API and the cell that proves it. |

## What a run does

1. Boots the pinned `honua-server` image with PostGIS, Redis and a throwaway
   key-ring certificate. It places a Caddy sidecar in front of the same server to
   provide four source variants:
   - TLS (`generateToken` issues tokens only over HTTPS);
   - an HTTP Basic-protected source;
   - a source that ignores `resultOffset`;
   - a throttled source (429 with `Retry-After`).
2. Seeds `fixture/source-sites.v1.sql` and publishes it through the supported
   admin API. A coded-value domain, an alias and `timeInfo` are authored through
   the admin field and layer-metadata operations.
3. Captures the source's own service, layer and query JSON under `wire/`.
4. Restores `Honua.Sdk.GeoServices` and runs every cell.
5. Runs again with a corrupted oracle: `big_counter` 2^53 + 1 is replaced by the
   double-rounded 2^53. That run must fail `data.int64-precision`, or `run.sh`
   exits 2.

## Cells

Data cells compare decoded values with the hand-derived oracle. Metadata cells
serialise the SDK's typed model back to JSON and require every member of the
source's wire JSON to survive with an equal value. A model that parses a layer
but drops `drawingInfo` fails with the exact lost JSON paths; recognition alone
never passes.

| Group | Cells | Acceptance criterion |
| --- | --- | --- |
| `package.*` | published bytes | Normal versioned `PackageReference` |
| `discovery.*`, `schema.*` | layers, tables, object ID field, field types, coded-value domain | Service/layer/table discovery and schema |
| `metadata.*` | service/layer/field lossless round-trip, drawingInfo, relationships, timeInfo, pagination capabilities | Preserve metadata |
| `data.*`, `geometry.*` | count/IDs, int64, GUID, null versus empty, temporal, numeric, null geometry, Z/M | Preserve values |
| `paging.*` | transfer limit, offset pages, ID batches, both on an offset-ignoring source | Termination, stable ID batching, no duplicates or omissions |
| `auth.*`, `errors.*`, `transport.*` | API key, token, bearer, basic, rejections, HTTP-200 error envelopes, injected handler, cancellation, streaming, 429 `Retry-After` | Credentials, transport, errors |

A cell the pinned candidate cannot express through a supported authoring path is
listed under `released` in the oracle, with the reason. Released cells are
reported as `released`, never as `pass`.

## Running

```bash
WORK_DIR=/path/on/real/disk/cert341 \
OUT_DIR=certification/geoservices-source-import/evidence/2cc2213/published-1.8.0 \
SDK_PACKAGE_VERSION=1.8.0 \
certification/geoservices-source-import/run.sh
```

The defaults are the published 1.8.0 package and the imaged trunk nightly
`nightly-2cc2213` (`sha256:61e06ef3…`). Set `SERVER_IMAGE` and
`SERVER_SOURCE_SHA` to certify against a newer nightly.

Requirements are Docker, the .NET 10 SDK, `jq`, `openssl` and network access to
ghcr.io and nuget.org. `WORK_DIR` must be on a disk Docker can bind-mount.

To check unpublished SDK source before a release, pack the packages into a
folder and set `LOCAL_PACKAGE_DIR` together with the packed
`SDK_PACKAGE_VERSION`. That run restores the local packages instead of
nuget.org, and its receipt records the local source. It is **not** a
published-bytes receipt and cannot close #341.

## Results

| Evidence | Package bytes | Server | pass / fail / released |
| --- | --- | --- | --- |
| `evidence/548b7a5/published-1.7.0` | nuget.org 1.7.0 | `548b7a5` (`sha256:29974ee7…`) | 29 / 10 / 3 |
| `evidence/548b7a5/unpublished-1.7.1-cert341` | local pack of #372 (not published) | `548b7a5` | 39 / 0 / 3 |
| `evidence/2cc2213/published-1.8.0` | nuget.org 1.8.0, nupkg SHA-512 `m5wSCMoU…pkaQw==`, assembly SHA-256 `b276ee31…` | `nightly-2cc2213` (`sha256:61e06ef3…`, dbSchema 120) | **39 / 0 / 3** |

The negative control failed `data.int64-precision` in every run, as required.
