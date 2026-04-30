# Geometry Analysis

`Honua.Sdk.Geometry` exposes local geometry helpers backed by
NetTopologySuite and ProjNet. Honua-specific code stays limited to adapters,
projection policy, and diagnostics.

## Planar Analysis

Use `HonuaPlanarGeometryAnalyzer` for planar NTS operations:

- distance, length, and area measurements;
- centroid, buffer, simplify, intersection, and envelope operations;
- containment, cover, intersection, and overlap predicates;
- nearest point pair and nearest vertex lookups.

Measurements are returned in the active coordinate units. If a geometry is in
EPSG:4326, measurement methods throw by default because planar degree
measurements are usually wrong. Supply `AnalysisSpatialReference` to project
with ProjNet before analysis.

```csharp
var length = HonuaPlanarGeometryAnalyzer.MeasureLength(line, new PlanarGeometryAnalysisOptions
{
    AnalysisSpatialReference = HonuaSpatialReference.WebMercator
});
```

Constructive operations that receive an analysis spatial reference return
geometries in that analysis coordinate space. Transform them back with
`HonuaCoordinateTransformer` when the caller needs source coordinates.

## Geodesic Behavior

The SDK does not pretend that NTS planar math is geodesic math. True geodesic
operations should come from a dedicated geodesy implementation or a server-side
analysis endpoint. Until that exists in the SDK, use the planar analyzer only
when the coordinate space is appropriate for the operation.
