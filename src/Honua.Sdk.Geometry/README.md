# Honua.Sdk.Geometry

NetTopologySuite-backed geometry and CRS helpers used across the Honua .NET SDK:
spatial-reference parsing (`HonuaSpatialReference`), ProjNet coordinate transforms
(`HonuaCoordinateTransformer`), planar analysis (`HonuaPlanarGeometryAnalyzer`:
distance, length, area, buffer, simplify, nearest points), geofence evaluation,
and GeoJSON / EsriJSON / GML / gRPC geometry converters.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

```bash
dotnet add package Honua.Sdk.Geometry
```

## Quick usage

```csharp
using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

var factory = new GeometryFactory(new PrecisionModel(), srid: 4326);
var boulder = factory.CreatePoint(new Coordinate(-105.2705, 40.0150));
var denver = factory.CreatePoint(new Coordinate(-104.9903, 39.7392));

// Project both points to Web Mercator for a planar measurement.
var transformer = new HonuaCoordinateTransformer();
var options = new PlanarGeometryAnalysisOptions
{
    SourceSpatialReference = HonuaSpatialReference.Wgs84,
    AnalysisSpatialReference = HonuaSpatialReference.WebMercator,
    CoordinateTransformer = transformer,
};

var meters = HonuaPlanarGeometryAnalyzer.MeasureDistance(boulder, denver, options);
Console.WriteLine($"Distance: {meters:F0} m (Web Mercator planar)");
```

## Documentation

- [Quickstart](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/quickstart.md)
- [Authentication](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/authentication.md)
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Geometry analysis](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/geometry-analysis.md)
- [Geofencing](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/geofencing.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
