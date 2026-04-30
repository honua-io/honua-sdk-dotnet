// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using System.Globalization;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Definition for a host-neutral geofence.
/// </summary>
public sealed class HonuaGeofenceDefinition
{
    /// <summary>
    /// Stable geofence identifier.
    /// </summary>
    public required string GeofenceId { get; init; }

    /// <summary>
    /// Boundary geometry for the geofence.
    /// </summary>
    public required NtsGeometry Geometry { get; init; }

    /// <summary>
    /// Optional planar buffer distance added to the boundary before evaluation.
    /// </summary>
    public double BufferDistance { get; init; }

    /// <summary>
    /// Optional planar distance outside the boundary that reports proximity.
    /// </summary>
    public double? ProximityDistance { get; init; }

    /// <summary>
    /// Optional source descriptor used when the geofence came from a feature source.
    /// </summary>
    public SourceDescriptor? Source { get; init; }

    /// <summary>
    /// Optional source query used to retrieve or refresh source-backed geofence features.
    /// </summary>
    public SourceQuery? SourceQuery { get; init; }

    /// <summary>
    /// Optional application metadata carried through evaluations.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Position sample evaluated against geofences.
/// </summary>
public sealed class HonuaGeofencePosition
{
    /// <summary>
    /// Location point to evaluate.
    /// </summary>
    public required Point Location { get; init; }

    /// <summary>
    /// Optional timestamp for stream evaluation.
    /// </summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>
    /// Optional track or device identifier. Separate tracks maintain separate transition state.
    /// </summary>
    public string? TrackId { get; init; }
}

/// <summary>
/// Geofence status for a position sample.
/// </summary>
public enum HonuaGeofenceStatus
{
    /// <summary>
    /// The position is outside the boundary and outside the proximity distance.
    /// </summary>
    Outside,

    /// <summary>
    /// The position is inside or on the geofence boundary.
    /// </summary>
    Inside,

    /// <summary>
    /// The position is outside the boundary but inside the proximity distance.
    /// </summary>
    Proximity,
}

/// <summary>
/// Transition detected between previous and current geofence status.
/// </summary>
public enum HonuaGeofenceTransition
{
    /// <summary>
    /// No transition was detected.
    /// </summary>
    None,

    /// <summary>
    /// The position moved from outside/proximity to inside.
    /// </summary>
    Entered,

    /// <summary>
    /// The position moved from inside to outside/proximity.
    /// </summary>
    Exited,

    /// <summary>
    /// The position moved from outside to proximity.
    /// </summary>
    Approached,

    /// <summary>
    /// The position moved from proximity to outside.
    /// </summary>
    Departed,
}

/// <summary>
/// Result of evaluating one position against one geofence.
/// </summary>
public sealed class HonuaGeofenceEvaluation
{
    /// <summary>
    /// Geofence definition used for the evaluation.
    /// </summary>
    public required HonuaGeofenceDefinition Definition { get; init; }

    /// <summary>
    /// Position sample used for the evaluation.
    /// </summary>
    public required HonuaGeofencePosition Position { get; init; }

    /// <summary>
    /// Geofence identifier copied from <see cref="Definition"/>.
    /// </summary>
    public required string GeofenceId { get; init; }

    /// <summary>
    /// Evaluated geofence status.
    /// </summary>
    public HonuaGeofenceStatus Status { get; init; }

    /// <summary>
    /// Transition from the previous known status for this track and geofence.
    /// </summary>
    public HonuaGeofenceTransition Transition { get; init; }

    /// <summary>
    /// Planar distance from the position to the active geofence boundary. Inside positions return zero.
    /// </summary>
    public double Distance { get; init; }
}

/// <summary>
/// Snapshot entry for geofence stream state.
/// </summary>
public sealed class HonuaGeofenceStateEntry
{
    /// <summary>
    /// Geofence identifier.
    /// </summary>
    public required string GeofenceId { get; init; }

    /// <summary>
    /// Track identifier, when one was provided by the position sample.
    /// </summary>
    public string? TrackId { get; init; }

    /// <summary>
    /// Last known status for this track and geofence.
    /// </summary>
    public HonuaGeofenceStatus Status { get; init; }
}

/// <summary>
/// Mutable transition state for geofence stream evaluation.
/// </summary>
public sealed class HonuaGeofenceEvaluationState
{
    private readonly Dictionary<StateKey, HonuaGeofenceStatus> statuses = new();

    /// <summary>
    /// Gets a snapshot of tracked geofence statuses.
    /// </summary>
    public IReadOnlyList<HonuaGeofenceStateEntry> Entries
        => statuses
            .Select(entry => new HonuaGeofenceStateEntry
            {
                GeofenceId = entry.Key.GeofenceId,
                TrackId = string.IsNullOrEmpty(entry.Key.TrackId) ? null : entry.Key.TrackId,
                Status = entry.Value
            })
            .ToArray();

    /// <summary>
    /// Clears all tracked state.
    /// </summary>
    public void Clear()
        => statuses.Clear();

    internal HonuaGeofenceTransition Update(string geofenceId, string? trackId, HonuaGeofenceStatus status)
    {
        var key = new StateKey(geofenceId, trackId ?? string.Empty);
        statuses.TryGetValue(key, out var previous);
        var hasPrevious = statuses.ContainsKey(key);
        statuses[key] = status;

        return hasPrevious
            ? GetTransition(previous, status)
            : GetInitialTransition(status);
    }

    private static HonuaGeofenceTransition GetInitialTransition(HonuaGeofenceStatus status)
        => status switch
        {
            HonuaGeofenceStatus.Inside => HonuaGeofenceTransition.Entered,
            HonuaGeofenceStatus.Proximity => HonuaGeofenceTransition.Approached,
            _ => HonuaGeofenceTransition.None
        };

    private static HonuaGeofenceTransition GetTransition(
        HonuaGeofenceStatus previous,
        HonuaGeofenceStatus current)
        => (previous, current) switch
        {
            (HonuaGeofenceStatus.Inside, HonuaGeofenceStatus.Outside) => HonuaGeofenceTransition.Exited,
            (HonuaGeofenceStatus.Inside, HonuaGeofenceStatus.Proximity) => HonuaGeofenceTransition.Exited,
            (HonuaGeofenceStatus.Outside, HonuaGeofenceStatus.Inside) => HonuaGeofenceTransition.Entered,
            (HonuaGeofenceStatus.Proximity, HonuaGeofenceStatus.Inside) => HonuaGeofenceTransition.Entered,
            (HonuaGeofenceStatus.Outside, HonuaGeofenceStatus.Proximity) => HonuaGeofenceTransition.Approached,
            (HonuaGeofenceStatus.Proximity, HonuaGeofenceStatus.Outside) => HonuaGeofenceTransition.Departed,
            _ => HonuaGeofenceTransition.None
        };

    private readonly record struct StateKey(string GeofenceId, string TrackId);
}

/// <summary>
/// NTS-backed geofence evaluator for current positions and position streams.
/// </summary>
public sealed class HonuaGeofenceEvaluator
{
    private readonly PreparedGeofence[] geofences;
    private readonly PlanarGeometryAnalysisOptions? options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaGeofenceEvaluator"/> class.
    /// </summary>
    /// <param name="definitions">Geofence definitions to evaluate.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    public HonuaGeofenceEvaluator(
        IEnumerable<HonuaGeofenceDefinition> definitions,
        PlanarGeometryAnalysisOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        this.options = options;
        geofences = definitions.Select(definition => PrepareDefinition(definition, options)).ToArray();
        if (geofences.Length == 0)
        {
            throw new ArgumentException("At least one geofence definition is required.", nameof(definitions));
        }
    }

    /// <summary>
    /// Evaluates one position against every geofence.
    /// </summary>
    /// <param name="position">Position to evaluate.</param>
    /// <param name="state">Optional transition state. When omitted, transitions are <see cref="HonuaGeofenceTransition.None"/>.</param>
    /// <returns>Evaluation results for each geofence.</returns>
    public IReadOnlyList<HonuaGeofenceEvaluation> Evaluate(
        HonuaGeofencePosition position,
        HonuaGeofenceEvaluationState? state = null)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(position.Location);

        var point = PreparePosition(position.Location);
        var evaluations = new List<HonuaGeofenceEvaluation>(geofences.Length);

        foreach (var geofence in geofences)
        {
            EnsureCompatibleKnownSrids(geofence.ActiveGeometry, point, options);

            var inside = geofence.PreparedGeometry.Covers(point);
            var distance = inside ? 0 : geofence.ActiveGeometry.Distance(point);
            var status = GetStatus(inside, distance, geofence.Definition.ProximityDistance);
            var transition = state?.Update(geofence.Definition.GeofenceId, position.TrackId, status) ??
                HonuaGeofenceTransition.None;

            evaluations.Add(new HonuaGeofenceEvaluation
            {
                Definition = geofence.Definition,
                Position = position,
                GeofenceId = geofence.Definition.GeofenceId,
                Status = status,
                Transition = transition,
                Distance = distance
            });
        }

        return evaluations;
    }

    /// <summary>
    /// Evaluates a sequence of positions using a shared transition state.
    /// </summary>
    /// <param name="positions">Position sequence.</param>
    /// <param name="state">Optional state object. A new state is created when omitted.</param>
    /// <returns>Evaluation results in position order.</returns>
    public IEnumerable<HonuaGeofenceEvaluation> Evaluate(
        IEnumerable<HonuaGeofencePosition> positions,
        HonuaGeofenceEvaluationState? state = null)
    {
        ArgumentNullException.ThrowIfNull(positions);

        var runningState = state ?? new HonuaGeofenceEvaluationState();
        foreach (var position in positions)
        {
            foreach (var evaluation in Evaluate(position, runningState))
            {
                yield return evaluation;
            }
        }
    }

    /// <summary>
    /// Evaluates an asynchronous position stream using a shared transition state.
    /// </summary>
    /// <param name="positions">Asynchronous position stream.</param>
    /// <param name="state">Optional state object. A new state is created when omitted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Evaluation results in stream order.</returns>
    public async IAsyncEnumerable<HonuaGeofenceEvaluation> EvaluateAsync(
        IAsyncEnumerable<HonuaGeofencePosition> positions,
        HonuaGeofenceEvaluationState? state = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(positions);

        var runningState = state ?? new HonuaGeofenceEvaluationState();
        await foreach (var position in positions.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            foreach (var evaluation in Evaluate(position, runningState))
            {
                yield return evaluation;
            }
        }
    }

    /// <summary>
    /// Evaluates normalized feature stream events using a shared transition state and sequence rejection.
    /// </summary>
    /// <param name="featureEvents">Feature stream events to evaluate.</param>
    /// <param name="positionSelector">Maps accepted feature stream events to position samples. Return <see langword="null"/> to skip an event.</param>
    /// <param name="state">Optional state object. A new state is created when omitted.</param>
    /// <param name="sequenceProcessor">
    /// Optional stream sequence processor. A new processor is created when omitted so duplicate and stale events are rejected by default.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Evaluation results in accepted stream-event order.</returns>
    public async IAsyncEnumerable<HonuaGeofenceEvaluation> EvaluateFeatureStreamAsync(
        IAsyncEnumerable<FeatureStreamEvent> featureEvents,
        Func<FeatureStreamEvent, HonuaGeofencePosition?> positionSelector,
        HonuaGeofenceEvaluationState? state = null,
        FeatureStreamEventProcessor? sequenceProcessor = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(featureEvents);
        ArgumentNullException.ThrowIfNull(positionSelector);

        var runningState = state ?? new HonuaGeofenceEvaluationState();
        var processor = sequenceProcessor ?? new FeatureStreamEventProcessor();
        await foreach (var featureEvent in featureEvents.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (!IsPositionEvent(featureEvent))
            {
                continue;
            }

            var sequenceResult = processor.Process(featureEvent);
            if (!sequenceResult.Accepted)
            {
                continue;
            }

            var position = positionSelector(featureEvent);
            if (position is null)
            {
                continue;
            }

            foreach (var evaluation in Evaluate(position, runningState))
            {
                yield return evaluation;
            }
        }
    }

    /// <summary>
    /// Creates a position sample from a point feature stream event.
    /// </summary>
    /// <param name="featureEvent">Feature stream event carrying point geometry.</param>
    /// <param name="geometryFactory">Optional geometry factory used when the stream geometry omits a spatial reference.</param>
    /// <returns>A position sample, or <see langword="null"/> when the event does not carry position geometry.</returns>
    public static HonuaGeofencePosition? CreatePositionFromFeatureEvent(
        FeatureStreamEvent featureEvent,
        GeometryFactory? geometryFactory = null)
    {
        ArgumentNullException.ThrowIfNull(featureEvent);

        if (!IsPositionEvent(featureEvent) ||
            featureEvent.Geometry is not { } geometry ||
            geometry.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        var point = ReadPointGeometry(geometry, geometryFactory);
        return new HonuaGeofencePosition
        {
            Location = point,
            Timestamp = featureEvent.Timestamp,
            TrackId = featureEvent.FeatureId ?? featureEvent.ObjectId?.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static PreparedGeofence PrepareDefinition(
        HonuaGeofenceDefinition definition,
        PlanarGeometryAnalysisOptions? options)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.GeofenceId);
        ArgumentNullException.ThrowIfNull(definition.Geometry);
        EnsureNotEmpty(definition.Geometry, nameof(definition.Geometry));
        EnsureFinite(definition.BufferDistance, nameof(definition.BufferDistance));
        ArgumentOutOfRangeException.ThrowIfNegative(definition.BufferDistance);

        if (definition.ProximityDistance is { } proximityDistance)
        {
            EnsureFinite(proximityDistance, nameof(definition.ProximityDistance));
            ArgumentOutOfRangeException.ThrowIfNegative(proximityDistance);
        }

        var requiresPlanarMeasurement = definition.BufferDistance > 0 || definition.ProximityDistance > 0;
        var activeGeometry = ProjectForAnalysis(definition.Geometry, options);
        EnsureMeasurementCoordinatePolicy(activeGeometry, options, requiresPlanarMeasurement);

        if (definition.BufferDistance > 0)
        {
            activeGeometry = activeGeometry.Buffer(definition.BufferDistance);
            EnsureNotEmpty(activeGeometry, nameof(definition.Geometry));
        }

        return new PreparedGeofence(
            definition,
            activeGeometry,
            PreparedGeometryFactory.Prepare(activeGeometry));
    }

    private static HonuaGeofenceStatus GetStatus(bool inside, double distance, double? proximityDistance)
    {
        if (inside)
        {
            return HonuaGeofenceStatus.Inside;
        }

        return proximityDistance is not null && distance <= proximityDistance
            ? HonuaGeofenceStatus.Proximity
            : HonuaGeofenceStatus.Outside;
    }

    private Point PreparePosition(Point location)
    {
        EnsureNotEmpty(location, nameof(location));
        var projected = ProjectForAnalysis(location, options);
        return projected as Point ??
            throw new InvalidOperationException("Projected geofence position did not remain a point geometry.");
    }

    private static bool IsPositionEvent(FeatureStreamEvent featureEvent)
        => featureEvent.Kind is FeatureStreamEventKind.Insert or FeatureStreamEventKind.Update;

    private static Point ReadPointGeometry(JsonElement geometry, GeometryFactory? geometryFactory)
    {
        var parsed = IsGeoJsonGeometry(geometry)
            ? GeoJsonGeometryConverter.ReadGeometry(geometry, geometryFactory)
            : GeoServicesGeometryConverter.ReadGeometry(geometry, geometryFactory);

        return parsed as Point ??
            throw new InvalidOperationException("Feature stream geometry must be a point for geofence position evaluation.");
    }

    private static bool IsGeoJsonGeometry(JsonElement geometry)
        => geometry.ValueKind == JsonValueKind.Object &&
           geometry.TryGetProperty("type", out var type) &&
           type.ValueKind == JsonValueKind.String;

    private static NtsGeometry ProjectForAnalysis(
        NtsGeometry geometry,
        PlanarGeometryAnalysisOptions? options)
    {
        if (options?.AnalysisSpatialReference is not { } target)
        {
            return geometry;
        }

        var source = ResolveSourceSpatialReference(geometry, options)
            ?? throw new InvalidOperationException(
                "SourceSpatialReference or geometry SRID is required when AnalysisSpatialReference is supplied.");

        if (AreEquivalent(source, target))
        {
            return geometry;
        }

        var transformer = options.CoordinateTransformer ?? new HonuaCoordinateTransformer();
        return transformer.Transform(geometry, source, target);
    }

    private static void EnsureCompatibleKnownSrids(
        NtsGeometry first,
        NtsGeometry second,
        PlanarGeometryAnalysisOptions? options)
    {
        if (options?.AnalysisSpatialReference is not null ||
            first.SRID <= 0 ||
            second.SRID <= 0 ||
            first.SRID == second.SRID)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Geometry SRIDs differ ({first.SRID} and {second.SRID}); supply an analysis spatial reference.");
    }

    private static void EnsureMeasurementCoordinatePolicy(
        NtsGeometry geometry,
        PlanarGeometryAnalysisOptions? options,
        bool requirePlanarMeasurement)
    {
        if (!requirePlanarMeasurement ||
            options?.AnalysisSpatialReference is not null ||
            options?.AllowGeographicMeasurements == true)
        {
            return;
        }

        var source = ResolveSourceSpatialReference(geometry, options);
        if (source?.Wkid == 4326)
        {
            throw new InvalidOperationException(
                "Planar geofence distances on EPSG:4326 coordinates are disabled by default; supply AnalysisSpatialReference for projection or set AllowGeographicMeasurements.");
        }
    }

    private static HonuaSpatialReference? ResolveSourceSpatialReference(
        NtsGeometry geometry,
        PlanarGeometryAnalysisOptions? options)
    {
        if (options?.SourceSpatialReference is { } source)
        {
            return source;
        }

        return geometry.SRID > 0 ? HonuaSpatialReference.FromWkid(geometry.SRID) : null;
    }

    private static bool AreEquivalent(
        HonuaSpatialReference first,
        HonuaSpatialReference second)
    {
        if (first.Wkid is int firstWkid && second.Wkid is int secondWkid)
        {
            return firstWkid == secondWkid;
        }

        if (!string.IsNullOrWhiteSpace(first.Authority) &&
            !string.IsNullOrWhiteSpace(second.Authority) &&
            first.Code is int firstCode &&
            second.Code is int secondCode)
        {
            return firstCode == secondCode &&
                first.Authority.Equals(second.Authority, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(first.Wkt) || !string.IsNullOrWhiteSpace(second.Wkt))
        {
            return string.Equals(first.Wkt, second.Wkt, StringComparison.Ordinal);
        }

        return first.Identifier.Equals(second.Identifier, StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureNotEmpty(NtsGeometry geometry, string parameterName)
    {
        if (geometry.IsEmpty)
        {
            throw new ArgumentException("Geometry must not be empty.", parameterName);
        }
    }

    private static void EnsureFinite(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite.");
        }
    }

    private sealed record PreparedGeofence(
        HonuaGeofenceDefinition Definition,
        NtsGeometry ActiveGeometry,
        IPreparedGeometry PreparedGeometry);
}
