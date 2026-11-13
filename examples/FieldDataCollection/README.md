# Archived field data collection reference

> **Native application path:** [gRPC for live data and edits; PMTiles, GeoParquet and COG through the CDN](../../docs/native-data-paths.md). Start there for browser gRPC-Web setup, signed private URLs and regeneration after changes. OGC API and GeoServices REST examples below are compatibility workflows.

This directory retains source from an earlier .NET MAUI field collection app:
map and record views, form parsing, location services, and offline sync sketches.
It depends on the former `Honua.Mobile.Core` API.

`FieldDataCollection.csproj` builds only `ReferenceSampleMarker.cs` as a
non-packable .NET library. The MAUI files are reference assets; the project does
not build or run them. A successful solution or CI build therefore validates
the marker, not a mobile application.

For runnable examples using the current SDK, see:

- [FieldFormConsole](../FieldFormConsole/README.md) for form validation and
  calculated fields.
- [OfflineConflictConsole](../OfflineConflictConsole/README.md) for offline
  sync conflicts and resolution.
- [AdminBootstrapConsole](../AdminBootstrapConsole/README.md) for publishing
  and querying a layer.

Mobile development belongs in [honua-mobile](https://github.com/honua-io/honua-mobile).
The retained files are licensed under the repository's [Apache 2.0 license](../../LICENSE).
