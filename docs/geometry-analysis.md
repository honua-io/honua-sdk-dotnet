# Geometry analysis

`Honua.Sdk.Geometry` is the local, in-process geometry layer of the SDK.
It wraps NetTopologySuite (NTS) for planar predicates, measurements, and
constructive operations, and ProjNET for coordinate transforms. Reach for
this package when you need to compute distances, areas, buffers,
intersections, or containment without round-tripping to the server, or
when you need to convert between GeoJSON, WKT, WKB, and NTS
`Geometry` instances. Honua-specific code stays limited to adapters,
projection policy, and diagnostics: there is no custom geometry kernel.

## Types you'll touch

All types live in `Honua.Sdk.Geometry`. See the
[package README](../src/Honua.Sdk.Geometry/README.md) for install notes.

| Type | Role |
|---|---|
| `HonuaSpatialReference` | Immutable record naming an SR by WKID, authority/code, or WKT. Constants: `HonuaSpatialReference.Wgs84` (EPSG:4326) and `HonuaSpatialReference.WebMercator` (EPSG:3857). Factory: `FromWkid(int, int? latestWkid = null)`. |
| `HonuaCoordinateTransformer` | ProjNET-backed transformer. `Transform(Coordinate, source, target)` and `Transform(Geometry, source, target)`. Copies the geometry and updates `SRID` when the target has a WKID. |
| `HonuaPlanarGeometryAnalyzer` | Static planar predicates and measurements over NTS geometries. |
| `PlanarGeometryAnalysisOptions` | `SourceSpatialReference`, `AnalysisSpatialReference`, `CoordinateTransformer`, `AllowGeographicMeasurements`. |
| `NearestGeometryPointPair` | `FirstPoint`, `SecondPoint`, `Distance` returned by `FindNearestPoints`. |
| `NearestGeometryVertex` | `Vertex`, `CoordinateIndex`, `Distance` returned by `FindNearestVertex`. |
| `GeometryText` | Static WKT/WKB conversion: `ReadWkt`, `WriteWkt`, `ReadWkb`, `WriteWkb`. |
| `GeoJsonGeometryConverter` | Static GeoJSON conversion: `ReadGeometry(JsonElement)`, `ReadGeometry(string)`, `WriteGeometry`, `WriteGeometryString`. |
| `GeoServicesGeometryConverter` | Esri GeoServices JSON geometry conversion (mirrors the GeoJSON converter). |
| `GrpcGeometryConverter` | Conversion between NTS geometries and the gRPC `Geometry` message. |
| `GeographicBoundingBox` | Validated `(west, south, east, north)` envelope for catalog payloads. |

## Planar analysis

`HonuaPlanarGeometryAnalyzer` is the entry point for every planar
operation. Each method accepts an optional `PlanarGeometryAnalysisOptions`
that controls the source SR (when the geometry has no `SRID`), the
analysis SR to project into before computing, and the transformer to use.

Measurements (`MeasureDistance`, `MeasureLength`, `MeasureArea`, `Buffer`,
`Simplify`, `FindNearestPoints`, `FindNearestVertex`) require a planar
coordinate space. If the geometry is in EPSG:4326 and neither
`AnalysisSpatialReference` nor `AllowGeographicMeasurements` is supplied,
the analyzer throws `InvalidOperationException`:
`"Planar measurements on EPSG:4326 coordinates are disabled by default; supply AnalysisSpatialReference for projection or set AllowGeographicMeasurements."`

Predicates (`Contains`, `Covers`, `Intersects`, `Overlaps`) and
non-measurement operations (`GetCentroid`, `GetEnvelope`,
`GetEnvelopeGeometry`, `Intersect`) do not require a planar SR, but two
geometries with conflicting known SRIDs and no `AnalysisSpatialReference`
will throw:
`"Geometry SRIDs differ ({first} and {second}); supply an analysis spatial reference."`

Constructive operations that receive an `AnalysisSpatialReference` return
geometries in that analysis coordinate space. Transform them back with
`HonuaCoordinateTransformer` when the caller needs source coordinates.

## Worked example: measure, contain, intersect

```csharp
using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

// Build a polygon in WGS84, then project to Web Mercator for measurement.
var wgs84Factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
var ring = wgs84Factory.CreatePolygon(new[]
{
    new Coordinate(-122.42, 37.77),
    new Coordinate(-122.41, 37.77),
    new Coordinate(-122.41, 37.78),
    new Coordinate(-122.42, 37.78),
    new Coordinate(-122.42, 37.77),
});

var options = new PlanarGeometryAnalysisOptions
{
    AnalysisSpatialReference = HonuaSpatialReference.WebMercator,
};

double areaSquareMeters = HonuaPlanarGeometryAnalyzer.MeasureArea(ring, options);
double perimeterMeters  = HonuaPlanarGeometryAnalyzer.MeasureLength(ring, options);

// Containment and intersection do not require an analysis SR by themselves,
// but supplying one keeps both operands in the same coordinate space.
var probe = wgs84Factory.CreatePoint(new Coordinate(-122.415, 37.775));
bool inside       = HonuaPlanarGeometryAnalyzer.Contains(ring, probe, options);
bool overlapsRing = HonuaPlanarGeometryAnalyzer.Intersects(ring, probe, options);

// Distance from a point outside the polygon to the boundary, in metres.
var elsewhere = wgs84Factory.CreatePoint(new Coordinate(-122.40, 37.79));
double distanceMeters = HonuaPlanarGeometryAnalyzer.MeasureDistance(ring, elsewhere, options);

Console.WriteLine($"area={areaSquareMeters:F1} m², perim={perimeterMeters:F1} m, " +
                  $"inside={inside}, intersects={overlapsRing}, dist={distanceMeters:F1} m");
```

## Worked example: convert GeoJSON, WKT, and WKB

```csharp
using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

// GeoJSON in, NTS Geometry out.
const string geoJson = """
    { "type": "Point", "coordinates": [-122.42, 37.77] }
    """;
Geometry point = GeoJsonGeometryConverter.ReadGeometry(geoJson);

// NTS Geometry to WKT and back.
string wkt = GeometryText.WriteWkt(point);          // "POINT (-122.42 37.77)"
Geometry roundTripped = GeometryText.ReadWkt(wkt);

// NTS Geometry to WKB (little-endian, no SRID prefix) and back.
byte[] wkb = GeometryText.WriteWkb(point);
Geometry fromWkb = GeometryText.ReadWkb(wkb);

// And GeoJSON out.
string outGeoJson = GeoJsonGeometryConverter.WriteGeometryString(roundTripped);
```

## Worked example: explicit coordinate transform

```csharp
using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

var transformer = new HonuaCoordinateTransformer();
var wgs84Factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
var sourcePoint = wgs84Factory.CreatePoint(new Coordinate(-122.42, 37.77));

Geometry projected = transformer.Transform(
    sourcePoint,
    HonuaSpatialReference.Wgs84,
    HonuaSpatialReference.WebMercator);

// projected.SRID is now 3857; the returned instance is a copy.
```

## How it composes

- `HonuaGeofenceEvaluator` (see [geofencing.md](geofencing.md)) is built on
  top of `HonuaPlanarGeometryAnalyzer` and shares
  `PlanarGeometryAnalysisOptions`.
- The Grpc, GeoServices, OgcFeatures, and Stac clients all return
  geometries that you can hand directly to the analyzer; use the matching
  converter (`GrpcGeometryConverter`, `GeoServicesGeometryConverter`,
  `GeoJsonGeometryConverter`) to get an NTS `Geometry`.
- For server-side analysis (true geodesic math, large datasets, or
  pre-indexed catalogs), prefer the relevant client in
  `Honua.Sdk.GeoServices` or `Honua.Sdk.Grpc` rather than projecting and
  measuring locally.

## Pitfalls

- The SDK does not pretend that NTS planar math is geodesic math. True
  geodesic operations should come from a dedicated geodesy implementation
  or a server-side analysis endpoint. Use the planar analyzer only when
  the coordinate space is appropriate for the operation.
- Calling `MeasureDistance`/`MeasureLength`/`MeasureArea`/`Buffer` on a
  geometry whose SRID resolves to EPSG:4326 without supplying
  `AnalysisSpatialReference` throws by design. The error string is quoted
  above; the fix is either to project (`AnalysisSpatialReference =
  HonuaSpatialReference.WebMercator` for global work, or a local
  projection for accuracy) or to set `AllowGeographicMeasurements = true`
  when you actually want degree-based math.
- `FindNearestPoints` and `FindNearestVertex` throw `ArgumentException`
  (`"Geometry must not be empty."`) on an empty `Geometry`. Filter empties
  before calling.
- `Buffer`/`Simplify`/`MeasureDistance` reject non-finite (`NaN`,
  infinity) distance arguments with `ArgumentOutOfRangeException`
  (`"Value must be finite."`). Validate user input before passing it in.
- `HonuaCoordinateTransformer.Transform(Geometry, ...)` returns a new
  geometry; it does not mutate the input. The output's `SRID` is set to
  the target's WKID when one is available, but if you supplied a
  WKT-only target the SRID is left unchanged.

## See also

- [src/Honua.Sdk.Geometry/README.md](../src/Honua.Sdk.Geometry/README.md)
  — package overview and install snippet.
- [geofencing.md](geofencing.md) — the geofence evaluator built on this
  package.
- [authentication.md](authentication.md) — auth flow for the server-side
  geometry endpoints you might call instead of computing locally.
- [troubleshooting.md](troubleshooting.md) — error-string lookup for the
  measurement-policy and SRID-mismatch exceptions above.
- [architecture.md](architecture.md) — where Geometry sits in the SDK
  layering.
