// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Tests;

public sealed class HonuaGeofenceEvaluatorTests
{
    private static readonly GeometryFactory ProjectedFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 3857);

    private static readonly GeometryFactory Wgs84Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    [Fact]
    public void Evaluate_BufferedDefinition_ReturnsInsideProximityAndOutsideStatuses()
    {
        var evaluator = new HonuaGeofenceEvaluator(
        [
            CreateDefinition(bufferDistance: 2, proximityDistance: 5)
        ]);

        var inside = evaluator.Evaluate(CreatePosition(11, 5)).Single();
        var proximity = evaluator.Evaluate(CreatePosition(14, 5)).Single();
        var outside = evaluator.Evaluate(CreatePosition(25, 5)).Single();

        Assert.Equal(HonuaGeofenceStatus.Inside, inside.Status);
        Assert.Equal(0, inside.Distance);
        Assert.Equal(HonuaGeofenceStatus.Proximity, proximity.Status);
        Assert.InRange(proximity.Distance, 1.9, 2.1);
        Assert.Equal(HonuaGeofenceStatus.Outside, outside.Status);
    }

    [Fact]
    public void Evaluate_WithState_ReturnsEnterExitAndProximityTransitions()
    {
        var evaluator = new HonuaGeofenceEvaluator(
        [
            CreateDefinition(proximityDistance: 5)
        ]);
        var state = new HonuaGeofenceEvaluationState();

        var outside = evaluator.Evaluate(CreatePosition(25, 5, trackId: "truck-1"), state).Single();
        var approach = evaluator.Evaluate(CreatePosition(14, 5, trackId: "truck-1"), state).Single();
        var enter = evaluator.Evaluate(CreatePosition(5, 5, trackId: "truck-1"), state).Single();
        var exit = evaluator.Evaluate(CreatePosition(25, 5, trackId: "truck-1"), state).Single();

        Assert.Equal(HonuaGeofenceTransition.None, outside.Transition);
        Assert.Equal(HonuaGeofenceTransition.Approached, approach.Transition);
        Assert.Equal(HonuaGeofenceTransition.Entered, enter.Transition);
        Assert.Equal(HonuaGeofenceTransition.Exited, exit.Transition);
        Assert.Contains(state.Entries, entry =>
            entry.GeofenceId == "yard" &&
            entry.TrackId == "truck-1" &&
            entry.Status == HonuaGeofenceStatus.Outside);
    }

    [Fact]
    public void Evaluate_PositionSequence_UsesSharedTransitionState()
    {
        var evaluator = new HonuaGeofenceEvaluator(
        [
            CreateDefinition(proximityDistance: 5)
        ]);

        var transitions = evaluator.Evaluate(
            [
                CreatePosition(14, 5),
                CreatePosition(5, 5),
                CreatePosition(14, 5),
                CreatePosition(25, 5)
            ])
            .Select(evaluation => evaluation.Transition)
            .ToArray();

        Assert.Equal(
            [
                HonuaGeofenceTransition.Approached,
                HonuaGeofenceTransition.Entered,
                HonuaGeofenceTransition.Exited,
                HonuaGeofenceTransition.Departed
            ],
            transitions);
    }

    [Fact]
    public async Task EvaluateAsync_PositionStream_UsesSharedTransitionState()
    {
        var evaluator = new HonuaGeofenceEvaluator(
        [
            CreateDefinition(proximityDistance: 5)
        ]);

        var results = new List<HonuaGeofenceEvaluation>();
        await foreach (var evaluation in evaluator.EvaluateAsync(CreatePositionStream()))
        {
            results.Add(evaluation);
        }

        Assert.Equal(HonuaGeofenceTransition.Approached, results[0].Transition);
        Assert.Equal(HonuaGeofenceTransition.Entered, results[1].Transition);
    }

    [Fact]
    public async Task EvaluateFeatureStreamAsync_RejectsDuplicateAndStaleEvents()
    {
        var evaluator = new HonuaGeofenceEvaluator(
        [
            CreateDefinition(proximityDistance: 5)
        ]);

        var results = new List<HonuaGeofenceEvaluation>();
        await foreach (var evaluation in evaluator.EvaluateFeatureStreamAsync(
            CreateFeatureEventStream(
            [
                FeatureEvent(1, x: 14, y: 5),
                FeatureEvent(1, x: 5, y: 5),
                FeatureEvent(0, x: 5, y: 5),
                FeatureEvent(2, x: 5, y: 5)
            ]),
            featureEvent => HonuaGeofenceEvaluator.CreatePositionFromFeatureEvent(featureEvent)))
        {
            results.Add(evaluation);
        }

        Assert.Equal(2, results.Count);
        Assert.Equal(HonuaGeofenceTransition.Approached, results[0].Transition);
        Assert.Equal(HonuaGeofenceTransition.Entered, results[1].Transition);
    }

    [Fact]
    public void CreatePositionFromFeatureEvent_ReadsGeoJsonPoint()
    {
        var featureEvent = FeatureEvent(
            sequenceNumber: 1,
            geometry: Json("""{"type":"Point","coordinates":[14,5]}"""));

        var position = HonuaGeofenceEvaluator.CreatePositionFromFeatureEvent(
            featureEvent,
            ProjectedFactory);

        Assert.NotNull(position);
        Assert.Equal(14, position.Location.X);
        Assert.Equal(5, position.Location.Y);
        Assert.Equal("truck-1", position.TrackId);
    }

    [Fact]
    public async Task EvaluateFeatureStreamAsync_ObservesCancellation()
    {
        var evaluator = new HonuaGeofenceEvaluator(
        [
            CreateDefinition(proximityDistance: 5)
        ]);
        using var cts = new CancellationTokenSource();
        await using var enumerator = evaluator.EvaluateFeatureStreamAsync(
            CreateCancellableFeatureEventStream(cts.Token),
            featureEvent => HonuaGeofenceEvaluator.CreatePositionFromFeatureEvent(featureEvent),
            cancellationToken: cts.Token)
            .GetAsyncEnumerator(cts.Token);

        Assert.True(await enumerator.MoveNextAsync());
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await enumerator.MoveNextAsync().AsTask());
    }

    [Fact]
    public void Definition_CarriesSourceQueryMetadata()
    {
        var definition = CreateDefinition(
            source: new SourceDescriptor
            {
                Id = "yards",
                Protocol = FeatureProtocolIds.OgcFeatures,
                Locator = new SourceLocator { CollectionId = "yards" }
            },
            sourceQuery: new SourceQuery
            {
                Where = "status = 'active'",
                ReturnGeometry = true
            });
        var evaluator = new HonuaGeofenceEvaluator([definition]);

        var evaluation = evaluator.Evaluate(CreatePosition(5, 5)).Single();

        Assert.Equal("yards", evaluation.Definition.Source?.Id);
        Assert.Equal("status = 'active'", evaluation.Definition.SourceQuery?.Where);
    }

    [Fact]
    public void Evaluate_ProjectsWgs84GeofenceBeforeBufferedEvaluation()
    {
        var definition = new HonuaGeofenceDefinition
        {
            GeofenceId = "honolulu",
            Geometry = Wgs84Factory.CreatePoint(new Coordinate(-157.8583, 21.3069)),
            BufferDistance = 250
        };
        var evaluator = new HonuaGeofenceEvaluator([definition], new PlanarGeometryAnalysisOptions
        {
            AnalysisSpatialReference = HonuaSpatialReference.WebMercator
        });

        var evaluation = evaluator.Evaluate(new HonuaGeofencePosition
        {
            Location = Wgs84Factory.CreatePoint(new Coordinate(-157.8583, 21.3079))
        }).Single();

        Assert.Equal(HonuaGeofenceStatus.Inside, evaluation.Status);
    }

    [Fact]
    public void Evaluate_RejectsGeographicDistanceWithoutProjection()
    {
        var definition = new HonuaGeofenceDefinition
        {
            GeofenceId = "honolulu",
            Geometry = Wgs84Factory.CreatePoint(new Coordinate(-157.8583, 21.3069)),
            BufferDistance = 250
        };

        var exception = Assert.Throws<InvalidOperationException>(() => new HonuaGeofenceEvaluator([definition]));

        Assert.Contains("EPSG:4326", exception.Message, StringComparison.Ordinal);
    }

    private static HonuaGeofenceDefinition CreateDefinition(
        double bufferDistance = 0,
        double? proximityDistance = null,
        SourceDescriptor? source = null,
        SourceQuery? sourceQuery = null)
        => new()
        {
            GeofenceId = "yard",
            Geometry = ProjectedFactory.CreatePolygon(
            [
                new Coordinate(0, 0),
                new Coordinate(10, 0),
                new Coordinate(10, 10),
                new Coordinate(0, 10),
                new Coordinate(0, 0)
            ]),
            BufferDistance = bufferDistance,
            ProximityDistance = proximityDistance,
            Source = source,
            SourceQuery = sourceQuery,
            Metadata = new Dictionary<string, string> { ["kind"] = "operations-yard" }
        };

    private static HonuaGeofencePosition CreatePosition(double x, double y, string? trackId = null)
        => new()
        {
            Location = ProjectedFactory.CreatePoint(new Coordinate(x, y)),
            TrackId = trackId,
            Timestamp = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero)
        };

    private static async IAsyncEnumerable<HonuaGeofencePosition> CreatePositionStream()
    {
        yield return CreatePosition(14, 5);
        await Task.Yield();
        yield return CreatePosition(5, 5);
    }

    private static async IAsyncEnumerable<FeatureStreamEvent> CreateFeatureEventStream(
        IEnumerable<FeatureStreamEvent> events)
    {
        foreach (var featureEvent in events)
        {
            yield return featureEvent;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<FeatureStreamEvent> CreateCancellableFeatureEventStream(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return FeatureEvent(1, x: 14, y: 5);
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
    }

    private static FeatureStreamEvent FeatureEvent(long sequenceNumber, double x, double y)
        => FeatureEvent(sequenceNumber, Json($$"""
            {
              "x": {{x}},
              "y": {{y}},
              "spatialReference": { "wkid": 3857 }
            }
            """));

    private static FeatureStreamEvent FeatureEvent(long sequenceNumber, JsonElement geometry)
        => new()
        {
            SubscriptionId = "positions",
            Source = new FeatureSource { ServiceId = "fleet", LayerId = 0 },
            Kind = FeatureStreamEventKind.Update,
            FeatureId = "truck-1",
            Timestamp = new DateTimeOffset(2026, 4, 30, 12, 0, 0, TimeSpan.Zero),
            SequenceNumber = sequenceNumber,
            Geometry = geometry
        };

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
