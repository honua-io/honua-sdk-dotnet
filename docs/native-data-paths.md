---
type: guide
title: "Start with gRPC and cloud-native files"
description: "Use gRPC for authenticated live data and edits, and PMTiles, GeoParquet and COG through the CDN for public and read-heavy data."
---
# Start with gRPC and cloud-native files

Build new Honua applications and finish customer migrations on the native path:
**gRPC for authenticated, live and edited data**, plus **cloud-native files for
public and read-heavy data**. The shared protobuf contract lives in
[geospatial-grpc](https://github.com/honua-io/geospatial-grpc).
This follows the founder direction recorded in [ADR-0087](https://github.com/honua-io/honua-server/pull/7555).

## Live queries and edits in .NET

Start with the [60-second gRPC hello](quickstart.md#60-second-hello-features)
using `Honua.Sdk.Grpc`. `HonuaGrpcClient` implements feature queries, streaming
and `ApplyEditsAsync`; use the native gRPC implementation of the shared feature
abstractions for new read/write applications. Configure HTTPS/TLS and the
deployment's native gRPC listener for authenticated remote connections; local
Compose h2c is a development configuration. See [authentication](authentication.md)
and [feature edits](feature-edits.md).

`Honua.Sdk.Grpc` is **not supported in browser/WASM**; preserve the
[browser support matrix](browser-wasm-support.md). Browser applications use the
JavaScript gRPC-Web setup below, or a .NET service with native gRPC behind a
browser-facing application boundary. Installing the .NET package does not add
browser gRPC-Web support.

## Browser setup: gRPC-Web

Browsers use gRPC-Web rather than a native HTTP/2 gRPC channel. The JavaScript
SDK uses the Connect libraries with the **gRPC-Web transport**; this does not
require or imply support for the separate Connect protocol.

```bash
npm install @honua/sdk-js @bufbuild/protobuf@^2 @connectrpc/connect@^2 @connectrpc/connect-web@^2
```

Configure the deployment's HTTPS gRPC-Web endpoint and allow the application's
origin, authorization headers and gRPC-Web response headers in its CORS policy.
Use a short-lived user access token from your sign-in flow; never ship an admin
key in a browser bundle. Replace these deployment-specific values before running:

```ts doc-test=compile
import { HonuaClient } from "@honua/sdk-js/honua";

const client = new HonuaClient({
  baseUrl: "https://your-honua-server.example",
  transport: "grpc-web",
  bearerToken: "<short-lived-user-access-token>",
});
const page = await client.queryFeatures({
  serviceId: "parcels",
  layerId: 0,
  where: "1=1",
  outFields: ["*"],
  returnGeometry: true,
  resultRecordCount: 10,
});
console.log(page.features);
```

The endpoint must actually expose gRPC-Web; an ordinary REST endpoint or an
HTTP/2-only native gRPC listener is insufficient. Configure token renewal through
the SDK's auth provider for a long-running session. Setting `transport` explicitly
matters: the JavaScript client's default remains REST for existing applications.

## Public files and private snapshots

Ordinary gRPC calls are POST requests and are not CDN-cacheable. Publish public,
read-heavy data as versioned files, then use a format-aware reader that requests
only the needed byte ranges through the CDN:

| Data | Native file | Reader |
| --- | --- | --- |
| Map tiles | PMTiles | PMTiles-capable map renderer |
| Vector extracts and analytics | GeoParquet | GeoParquet / Arrow tooling |
| Raster imagery | Cloud Optimized GeoTIFF (COG) | COG-aware raster reader |

Configure object storage and the CDN to support HTTP range requests (including
`206 Partial Content` and `Content-Range`), preserve the file bytes, and allow
browser CORS access where needed. A plain full-file download is still possible;
partial reads depend on the reader and the file's layout.

For private files, authorize the user first and return a short-lived **signed
URL** scoped to the permitted object. The reader uses that URL for its range
requests. Keep private responses out of shared public caches unless the CDN
enforces the same authorization; configure signing, cache keys and expiry with
the deployment operator. Refresh the signed URL when it expires, including
during multi-request reads, and do not put it in logs or source control.

After edits, regenerate the affected files and publish a new immutable version
(for example a generation-specific object key). Update the application's catalog
or manifest only after publication succeeds; acquire a new signed URL for that
version. A signed URL controls access, not freshness. An old file does not change
when the live layer changes. Keep gRPC as the source for current authorized
reads and writes while a new snapshot is being built.

Use a format-aware PMTiles viewer, GeoParquet/Arrow analytics reader or COG raster
reader in your application host. `Honua.Sdk.Grpc` is the live client, not a file
decoder; this guide does not claim that the .NET SDK supplies those readers.

## Compatibility during coexistence

OGC API and GeoServices REST clients remain supported for third-party tools and
existing Esri clients that cannot yet migrate. Their examples demonstrate
**compatibility**, not the target architecture for a new Honua application.
Keep them during coexistence, then move application data access to gRPC and
cloud-native files. Control-plane administration and capabilities outside the
feature data path retain their own documented contracts.
