# Geofencing

`Honua.Sdk.Geometry` includes host-neutral geofence evaluation over
NetTopologySuite geometries. The SDK handles the portable data model and
evaluation rules; apps still own device sensor acquisition, background
permissions, native scheduling, notifications, and UI/map display.

Core types:

- `HonuaGeofenceDefinition` declares a geofence ID, NTS boundary geometry,
  optional planar buffer distance, optional proximity distance, and optional
  `SourceDescriptor` / `SourceQuery` metadata for source-backed geofences.
- `HonuaGeofencePosition` wraps a point sample, timestamp, and optional track ID.
- `HonuaGeofenceEvaluator` prepares the geofence geometries and evaluates
  current positions, synchronous position sequences, async position streams, or
  typed feature stream events from `IHonuaFeatureStreamClient`.
- `HonuaGeofenceEvaluationState` tracks per-geofence/per-track state so streams
  can report enter, exit, approach, and depart transitions.

Buffer and proximity distances are planar. If a geofence uses EPSG:4326
coordinates, supply `PlanarGeometryAnalysisOptions.AnalysisSpatialReference`
before using distance-based evaluation. This mirrors the planar analysis policy:
the SDK will not silently treat degrees as meters.

```csharp
using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 3857);
var fence = new HonuaGeofenceDefinition
{
    GeofenceId = "yard",
    Geometry = factory.CreatePolygon(
    [
        new Coordinate(0, 0),
        new Coordinate(10, 0),
        new Coordinate(10, 10),
        new Coordinate(0, 10),
        new Coordinate(0, 0)
    ]),
    BufferDistance = 2,
    ProximityDistance = 5
};

var evaluator = new HonuaGeofenceEvaluator([fence]);
var state = new HonuaGeofenceEvaluationState();

var result = evaluator.Evaluate(new HonuaGeofencePosition
{
    Location = factory.CreatePoint(new Coordinate(11, 5)),
    TrackId = "truck-1"
}, state).Single();
```

`HonuaGeofenceEvaluator` uses prepared NTS geometries for repeated predicate
evaluation. Use one evaluator instance per active geofence set and reuse a
`HonuaGeofenceEvaluationState` for each logical position stream.

Feature streams can be evaluated with the SDK's normalized stream contract. The
feature-stream overload rejects duplicate and stale sequence events by default
through `FeatureStreamEventProcessor`; pass a shared processor when cursor state
must survive reconnects or multiple evaluation loops.

```csharp
await foreach (var evaluation in evaluator.EvaluateFeatureStreamAsync(
    streamClient.SubscribeAsync(subscription, ct),
    featureEvent => HonuaGeofenceEvaluator.CreatePositionFromFeatureEvent(featureEvent),
    state,
    cancellationToken: ct))
{
    // Dispatch or persist the host-specific location event here.
}
```

`CreatePositionFromFeatureEvent` reads GeoServices or GeoJSON point geometry
from insert/update events. For providers that encode positions in attributes or
custom payloads, pass a custom selector function instead.
