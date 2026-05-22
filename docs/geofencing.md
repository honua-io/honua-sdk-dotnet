# Geofencing

`Honua.Sdk.Geometry` includes a host-neutral geofence evaluator over
NetTopologySuite geometries. The SDK owns the portable data model
(boundary geometries, buffer/proximity distances, source-backed metadata)
and the evaluation rules that turn position samples into enter/exit/
approach/depart transitions. Apps still own device sensor acquisition,
background permissions, native scheduling, notifications, and any UI or
map display. Reach for this evaluator when you have point samples
(from a device, a tracking feed, a normalized feature stream, or a test
fixture) and a set of polygonal fences, and you want a single typed
pipeline that handles state transitions and is safe to share with mobile
and web hosts.

## Types you'll touch

All types live in `Honua.Sdk.Geometry` (file:
[src/Honua.Sdk.Geometry/HonuaGeofenceEvaluator.cs](../src/Honua.Sdk.Geometry/HonuaGeofenceEvaluator.cs)).

| Type | Role |
|---|---|
| `HonuaGeofenceDefinition` | Declares a fence: `GeofenceId` (required), `Geometry` (required NTS geometry), `BufferDistance` (planar), `ProximityDistance` (planar, optional), optional `Source` / `SourceQuery` for source-backed fences, and free-form `Metadata`. |
| `HonuaGeofencePosition` | A single sample: `Location` (NTS `Point`), optional `Timestamp`, optional `TrackId` (separate tracks maintain separate transition state). |
| `HonuaGeofenceStatus` | `Outside`, `Inside`, `Proximity`. |
| `HonuaGeofenceTransition` | `None`, `Entered`, `Exited`, `Approached`, `Departed`. |
| `HonuaGeofenceEvaluation` | Per-position, per-fence result: `Definition`, `Position`, `GeofenceId`, `Status`, `Transition`, `Distance` (planar; `0` when inside). |
| `HonuaGeofenceEvaluationState` | Mutable per-`(GeofenceId, TrackId)` state. `Entries` snapshot for diagnostics; `Clear()` resets. |
| `HonuaGeofenceStateEntry` | Snapshot entry returned by `HonuaGeofenceEvaluationState.Entries`. |
| `HonuaGeofenceEvaluator` | Prepares NTS geometries once and evaluates a single position, a sync sequence, an async stream, or a `FeatureStreamEvent` stream. |

`HonuaGeofenceEvaluator` accepts the same
`PlanarGeometryAnalysisOptions` as `HonuaPlanarGeometryAnalyzer` (see
[geometry-analysis.md](geometry-analysis.md)) so projection policy is
consistent across the package.

## Evaluation contract

For each `(position, geofence)` pair the evaluator:

1. Projects the position into the analysis coordinate space if
   `AnalysisSpatialReference` is set (otherwise it uses the position's
   `SRID`).
2. Tests whether the prepared geofence geometry **covers** the projected
   point.
3. If not inside, computes the planar distance from the point to the
   active (post-buffer) boundary.
4. Maps `(inside, distance, proximityDistance)` to `HonuaGeofenceStatus`:
   - `inside` → `Inside`
   - `distance <= ProximityDistance` → `Proximity`
   - otherwise → `Outside`
5. Updates `HonuaGeofenceEvaluationState` for `(GeofenceId, TrackId)` and
   emits the resulting `HonuaGeofenceTransition`.

The transition table is symmetric: `Outside`/`Proximity` → `Inside`
emits `Entered`; `Inside` → `Outside`/`Proximity` emits `Exited`;
`Outside` → `Proximity` emits `Approached`; `Proximity` → `Outside`
emits `Departed`. The first observation of a track emits `Entered` (when
the initial status is `Inside`) or `Approached` (when it is `Proximity`),
and `None` otherwise.

Buffer and proximity distances are planar. If a geofence uses EPSG:4326
coordinates, supply
`PlanarGeometryAnalysisOptions.AnalysisSpatialReference` before using
distance-based evaluation. This mirrors the planar-analysis policy: the
SDK will not silently treat degrees as metres.

## Worked example: single position, single fence

```csharp
using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

// Build a fence in Web Mercator (SRID 3857) so buffer/proximity distances
// are in metres without any extra projection.
var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 3857);
var fence = new HonuaGeofenceDefinition
{
    GeofenceId = "yard",
    Geometry = factory.CreatePolygon(new[]
    {
        new Coordinate(0, 0),
        new Coordinate(10, 0),
        new Coordinate(10, 10),
        new Coordinate(0, 10),
        new Coordinate(0, 0),
    }),
    BufferDistance = 2,        // expand the boundary by 2 m
    ProximityDistance = 5,     // anything within 5 m of the buffered edge is "Proximity"
};

var evaluator = new HonuaGeofenceEvaluator(new[] { fence });
var state = new HonuaGeofenceEvaluationState();

var position = new HonuaGeofencePosition
{
    Location = factory.CreatePoint(new Coordinate(11, 5)),
    TrackId = "truck-1",
    Timestamp = DateTimeOffset.UtcNow,
};

var result = evaluator.Evaluate(position, state).Single();
Console.WriteLine($"{result.GeofenceId}: status={result.Status} " +
                  $"transition={result.Transition} dist={result.Distance:F2}");
// Example output: yard: status=Proximity transition=Approached dist=1.00
```

Reuse one `HonuaGeofenceEvaluator` per active fence set and one
`HonuaGeofenceEvaluationState` per logical position stream. Prepared NTS
geometries amortise the cost of repeated `Covers` calls.

## Worked example: project EPSG:4326 positions before evaluation

```csharp
using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
var fence = new HonuaGeofenceDefinition
{
    GeofenceId = "park",
    Geometry = factory.CreatePolygon(new[]
    {
        new Coordinate(-122.42, 37.77),
        new Coordinate(-122.41, 37.77),
        new Coordinate(-122.41, 37.78),
        new Coordinate(-122.42, 37.78),
        new Coordinate(-122.42, 37.77),
    }),
    BufferDistance = 50,       // 50 m in the analysis SR
    ProximityDistance = 25,
};

var options = new PlanarGeometryAnalysisOptions
{
    AnalysisSpatialReference = HonuaSpatialReference.WebMercator,
};

var evaluator = new HonuaGeofenceEvaluator(new[] { fence }, options);
var state = new HonuaGeofenceEvaluationState();

var sample = new HonuaGeofencePosition
{
    Location = factory.CreatePoint(new Coordinate(-122.415, 37.775)),
    TrackId = "device-42",
};

var evaluation = evaluator.Evaluate(sample, state).Single();
```

## Worked example: feature stream with dwell logic

`EvaluateFeatureStreamAsync` rejects duplicate and stale sequence events
through `FeatureStreamEventProcessor`. Pass a shared processor when the
cursor state must survive reconnects or multiple evaluation loops.

```csharp
// requires a running Honua server at this URL
using System.Threading;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Geometry;

// streamClient resolved from DI elsewhere (IHonuaFeatureStreamClient).
// fence and evaluator built as in the previous example.

CancellationToken cancellationToken = default;
var subscription = new FeatureStreamSubscribeRequest { /* configure per IHonuaFeatureStreamClient */ };
var state = new HonuaGeofenceEvaluationState();
var dwell = new Dictionary<(string GeofenceId, string TrackId), DateTimeOffset>();
TimeSpan dwellThreshold = TimeSpan.FromMinutes(5);

await foreach (var evaluation in evaluator.EvaluateFeatureStreamAsync(
    streamClient.SubscribeAsync(subscription, cancellationToken),
    featureEvent => HonuaGeofenceEvaluator.CreatePositionFromFeatureEvent(featureEvent),
    state,
    cancellationToken: cancellationToken))
{
    var key = (evaluation.GeofenceId, evaluation.Position.TrackId ?? "");
    var ts = evaluation.Position.Timestamp ?? DateTimeOffset.UtcNow;

    if (evaluation.Transition == HonuaGeofenceTransition.Entered)
    {
        dwell[key] = ts;
    }
    else if (evaluation.Transition == HonuaGeofenceTransition.Exited &&
             dwell.TryGetValue(key, out var enteredAt))
    {
        if (ts - enteredAt >= dwellThreshold)
        {
            // Dispatch the host-specific dwell event here.
        }

        dwell.Remove(key);
    }
}
```

`CreatePositionFromFeatureEvent` reads GeoServices or GeoJSON point
geometry from `FeatureStreamEventKind.Insert` / `Update` events and uses
`FeatureId` (falling back to `ObjectId`) as the `TrackId`. For providers
that encode positions in attributes or a custom payload, pass your own
selector function instead.

## How it composes

- The evaluator reuses the projection policy in
  `PlanarGeometryAnalysisOptions`, so geometry tooling stays consistent
  with [geometry-analysis.md](geometry-analysis.md).
- Source-backed geofences carry an optional `SourceDescriptor` /
  `SourceQuery` so a host can refresh the boundary from any
  `Honua.Sdk.Abstractions.Features` client (see
  [source-facade.md](source-facade.md)).
- Feature-stream evaluation plugs directly into
  `IHonuaFeatureStreamClient` (see
  [feature-edits.md](feature-edits.md) for the stream contract).
- Hosts that need offline evaluation can pair the evaluator with
  [offline-sync-core.md](offline-sync-core.md) to keep fence definitions
  in sync.

## Pitfalls

- Constructing `HonuaGeofenceEvaluator` with no definitions throws
  `ArgumentException`: `"At least one geofence definition is required."`
- Negative or non-finite `BufferDistance` / `ProximityDistance` throw at
  construction (`ArgumentOutOfRangeException` /
  `"Value must be finite."`). Validate user input before passing it in.
- An empty boundary geometry throws `ArgumentException`:
  `"Geometry must not be empty."`
- Using EPSG:4326 fences with a `BufferDistance` or `ProximityDistance`
  but no `AnalysisSpatialReference` throws the same planar-policy error
  the analyzer raises: `"Planar measurements on EPSG:4326 coordinates are
  disabled by default; supply AnalysisSpatialReference for projection or
  set AllowGeographicMeasurements."`
- Sharing a single `HonuaGeofenceEvaluationState` across unrelated
  position streams produces incorrect transitions because the state is
  keyed on `(GeofenceId, TrackId)` only. Give each track a distinct
  `TrackId`, or give each stream its own state.

## See also

- [src/Honua.Sdk.Geometry/README.md](../src/Honua.Sdk.Geometry/README.md)
  — package overview and install snippet.
- [geometry-analysis.md](geometry-analysis.md) — the planar analyzer this
  evaluator is built on.
- [feature-edits.md](feature-edits.md) — `IHonuaFeatureStreamClient` and
  the `FeatureStreamEvent` shape consumed by
  `EvaluateFeatureStreamAsync`.
- [source-facade.md](source-facade.md) — `SourceDescriptor` /
  `SourceQuery` carried on `HonuaGeofenceDefinition`.
- [authentication.md](authentication.md) — auth flow for the feature
  stream clients that feed the evaluator.
- [troubleshooting.md](troubleshooting.md) — error-string lookup.
