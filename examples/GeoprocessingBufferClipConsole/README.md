# Buffer and clip with OGC API Processes

This sample runs `geometry.buffer` on a GeoJSON point near Honolulu, then feeds
the returned polygon into `overlay.clip` to clip an inline example feature layer.
Both stages call a real Honua Server. A missing server, unsupported process,
failed job, invalid output, timeout, or cancellation fails the run.

```bash
export HONUA_BASE_URL=https://your-honua-server
export HONUA_API_KEY=your-api-key           # if required by your deployment
# export HONUA_BEARER_TOKEN=your-token      # alternative authentication
dotnet run --project examples/GeoprocessingBufferClipConsole
```

The target must advertise and execute `geometry.buffer` and `overlay.clip`,
accept GeoJSON in WKB-typed OGC inputs, and return raw results. These server
contracts are tracked by [honua-server#3267](https://github.com/honua-io/honua-server/issues/3267)
and [honua-server#3268](https://github.com/honua-io/honua-server/issues/3268).
Authenticated remote targets require HTTPS. Loopback HTTP supports local development.

The point uses EPSG:4326 and a **planar 0.01-degree** buffer. Distances use input
coordinate units; this is not a meter or geodesic buffer. The concrete buffer
input is named `wkb` even when its value is GeoJSON; it also requires `srid`
and `distance`.

`overlay.clip` accepts `input` and `clip` as inline
`data:application/geo+json;base64,...` FeatureCollections and preserves input
attributes. The sample wraps the actual returned buffer Feature as a
FeatureCollection. `geometry.clip` has different semantics: its
`targetWkb`/`clipEnvelopeWkb` inputs clip one geometry to the envelope of another,
so it would not clip the layer to the buffer polygon.

Each execution has a two-minute elapsed deadline covering discovery, submission,
polling, and results. `ExecuteSyncAsync` omits `Prefer` when the description
advertises `sync-execute`; otherwise it sends `Prefer: respond-async` and waits
for a successful terminal job if `async-execute` is advertised. Descriptions
without a supported execution mode fail before submission. It submits each stage once. Ctrl+C or a local
timeout stops waiting and leaves admitted server jobs available for inspection;
failures print the observed job ID when available.

The sample requests raw GeoJSON. Current document results instead contain
qualified inline values such as `{"outputFeatureLayer":{"value":{...},"mediaType":"application/geo+json"}}`;
they are not artifact-URI manifests. After both stages succeed, the sample saves
`buffer.geojson` and `clip.geojson` in a unique `buffer-clip-*` directory and prints
their real local `file:` URIs, media types, and any observed server job IDs.
It does not invent server artifact URLs.

CI compiles this sample with warnings as errors. HTTP fixture tests verify SDK
behavior; they do not qualify a live deployment or an installed published NuGet
package. A live server run and published-package receipt remain separate evidence.
