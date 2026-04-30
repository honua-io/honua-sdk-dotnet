// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Distance;
using NetTopologySuite.Simplify;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace Honua.Sdk.Geometry;

/// <summary>
/// NTS-backed planar geometry analysis helpers.
/// </summary>
public static class HonuaPlanarGeometryAnalyzer
{
    /// <summary>
    /// Measures the planar distance between two geometries.
    /// </summary>
    /// <param name="first">The first geometry.</param>
    /// <param name="second">The second geometry.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The planar distance in the active analysis coordinate units.</returns>
    public static double MeasureDistance(
        NtsGeometry first,
        NtsGeometry second,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var (preparedFirst, preparedSecond) = PrepareBinary(first, second, options, requirePlanarMeasurement: true);
        return preparedFirst.Distance(preparedSecond);
    }

    /// <summary>
    /// Measures the planar length of a geometry.
    /// </summary>
    /// <param name="geometry">The geometry to measure.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The planar length in the active analysis coordinate units.</returns>
    public static double MeasureLength(
        NtsGeometry geometry,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var prepared = PrepareUnary(geometry, options, requirePlanarMeasurement: true);
        return prepared.Length;
    }

    /// <summary>
    /// Measures the planar area of a geometry.
    /// </summary>
    /// <param name="geometry">The geometry to measure.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The planar area in squared active analysis coordinate units.</returns>
    public static double MeasureArea(
        NtsGeometry geometry,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var prepared = PrepareUnary(geometry, options, requirePlanarMeasurement: true);
        return prepared.Area;
    }

    /// <summary>
    /// Gets the planar centroid of a geometry.
    /// </summary>
    /// <param name="geometry">The geometry to analyze.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The centroid point in the active analysis coordinate space.</returns>
    public static Point GetCentroid(
        NtsGeometry geometry,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var prepared = PrepareUnary(geometry, options, requirePlanarMeasurement: false);
        return prepared.Centroid;
    }

    /// <summary>
    /// Buffers a geometry with the NTS planar buffer operation.
    /// </summary>
    /// <param name="geometry">The geometry to buffer.</param>
    /// <param name="distance">The buffer distance in the active analysis coordinate units.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The buffered geometry in the active analysis coordinate space.</returns>
    public static NtsGeometry Buffer(
        NtsGeometry geometry,
        double distance,
        PlanarGeometryAnalysisOptions? options = null)
    {
        EnsureFinite(distance, nameof(distance));
        var prepared = PrepareUnary(geometry, options, requirePlanarMeasurement: true);
        return prepared.Buffer(distance);
    }

    /// <summary>
    /// Simplifies a geometry with NTS topology-preserving simplification.
    /// </summary>
    /// <param name="geometry">The geometry to simplify.</param>
    /// <param name="distanceTolerance">The simplification tolerance in the active analysis coordinate units.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The simplified geometry in the active analysis coordinate space.</returns>
    public static NtsGeometry Simplify(
        NtsGeometry geometry,
        double distanceTolerance,
        PlanarGeometryAnalysisOptions? options = null)
    {
        EnsureFinite(distanceTolerance, nameof(distanceTolerance));
        ArgumentOutOfRangeException.ThrowIfNegative(distanceTolerance);
        var prepared = PrepareUnary(geometry, options, requirePlanarMeasurement: true);
        return TopologyPreservingSimplifier.Simplify(prepared, distanceTolerance);
    }

    /// <summary>
    /// Intersects two geometries with the NTS planar overlay operation.
    /// </summary>
    /// <param name="first">The first geometry.</param>
    /// <param name="second">The second geometry.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The intersection geometry in the active analysis coordinate space.</returns>
    public static NtsGeometry Intersect(
        NtsGeometry first,
        NtsGeometry second,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var (preparedFirst, preparedSecond) = PrepareBinary(first, second, options, requirePlanarMeasurement: false);
        return preparedFirst.Intersection(preparedSecond);
    }

    /// <summary>
    /// Determines whether one geometry contains another in planar coordinates.
    /// </summary>
    /// <param name="container">The possible containing geometry.</param>
    /// <param name="candidate">The candidate contained geometry.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>True when the container contains the candidate.</returns>
    public static bool Contains(
        NtsGeometry container,
        NtsGeometry candidate,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var (preparedContainer, preparedCandidate) = PrepareBinary(
            container,
            candidate,
            options,
            requirePlanarMeasurement: false);
        return preparedContainer.Contains(preparedCandidate);
    }

    /// <summary>
    /// Determines whether one geometry covers another in planar coordinates.
    /// </summary>
    /// <param name="container">The possible covering geometry.</param>
    /// <param name="candidate">The candidate covered geometry.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>True when the container covers the candidate.</returns>
    public static bool Covers(
        NtsGeometry container,
        NtsGeometry candidate,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var (preparedContainer, preparedCandidate) = PrepareBinary(
            container,
            candidate,
            options,
            requirePlanarMeasurement: false);
        return preparedContainer.Covers(preparedCandidate);
    }

    /// <summary>
    /// Determines whether two geometries intersect in planar coordinates.
    /// </summary>
    /// <param name="first">The first geometry.</param>
    /// <param name="second">The second geometry.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>True when the geometries intersect.</returns>
    public static bool Intersects(
        NtsGeometry first,
        NtsGeometry second,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var (preparedFirst, preparedSecond) = PrepareBinary(first, second, options, requirePlanarMeasurement: false);
        return preparedFirst.Intersects(preparedSecond);
    }

    /// <summary>
    /// Determines whether two geometries overlap in planar coordinates.
    /// </summary>
    /// <param name="first">The first geometry.</param>
    /// <param name="second">The second geometry.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>True when the geometries overlap.</returns>
    public static bool Overlaps(
        NtsGeometry first,
        NtsGeometry second,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var (preparedFirst, preparedSecond) = PrepareBinary(first, second, options, requirePlanarMeasurement: false);
        return preparedFirst.Overlaps(preparedSecond);
    }

    /// <summary>
    /// Gets the envelope of a geometry.
    /// </summary>
    /// <param name="geometry">The geometry to analyze.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The envelope in the active analysis coordinate space.</returns>
    public static Envelope GetEnvelope(
        NtsGeometry geometry,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var prepared = PrepareUnary(geometry, options, requirePlanarMeasurement: false);
        return new Envelope(prepared.EnvelopeInternal);
    }

    /// <summary>
    /// Gets the envelope geometry of a geometry.
    /// </summary>
    /// <param name="geometry">The geometry to analyze.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The envelope geometry in the active analysis coordinate space.</returns>
    public static NtsGeometry GetEnvelopeGeometry(
        NtsGeometry geometry,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var prepared = PrepareUnary(geometry, options, requirePlanarMeasurement: false);
        return prepared.Envelope;
    }

    /// <summary>
    /// Finds the nearest points between two geometries.
    /// </summary>
    /// <param name="first">The first geometry.</param>
    /// <param name="second">The second geometry.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The nearest point pair in the active analysis coordinate space.</returns>
    public static NearestGeometryPointPair FindNearestPoints(
        NtsGeometry first,
        NtsGeometry second,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var (preparedFirst, preparedSecond) = PrepareBinary(first, second, options, requirePlanarMeasurement: true);
        EnsureNotEmpty(preparedFirst, nameof(first));
        EnsureNotEmpty(preparedSecond, nameof(second));

        var coordinates = DistanceOp.NearestPoints(preparedFirst, preparedSecond);
        var firstPoint = preparedFirst.Factory.CreatePoint(coordinates[0].Copy());
        var secondPoint = preparedSecond.Factory.CreatePoint(coordinates[1].Copy());

        return new NearestGeometryPointPair
        {
            FirstPoint = firstPoint,
            SecondPoint = secondPoint,
            Distance = firstPoint.Distance(secondPoint)
        };
    }

    /// <summary>
    /// Finds the nearest vertex in a geometry to a target geometry.
    /// </summary>
    /// <param name="geometry">The geometry whose vertices are searched.</param>
    /// <param name="target">The target geometry.</param>
    /// <param name="options">Optional projection and coordinate policy.</param>
    /// <returns>The nearest vertex in the active analysis coordinate space.</returns>
    public static NearestGeometryVertex FindNearestVertex(
        NtsGeometry geometry,
        NtsGeometry target,
        PlanarGeometryAnalysisOptions? options = null)
    {
        var (preparedGeometry, preparedTarget) = PrepareBinary(geometry, target, options, requirePlanarMeasurement: true);
        EnsureNotEmpty(preparedGeometry, nameof(geometry));
        EnsureNotEmpty(preparedTarget, nameof(target));

        var coordinates = preparedGeometry.Coordinates;
        if (coordinates.Length == 0)
        {
            throw new ArgumentException("Geometry does not contain any coordinates.", nameof(geometry));
        }

        var nearestIndex = 0;
        var nearestPoint = preparedGeometry.Factory.CreatePoint(coordinates[0].Copy());
        var nearestDistance = nearestPoint.Distance(preparedTarget);

        for (var index = 1; index < coordinates.Length; index++)
        {
            var point = preparedGeometry.Factory.CreatePoint(coordinates[index].Copy());
            var distance = point.Distance(preparedTarget);
            if (distance < nearestDistance)
            {
                nearestIndex = index;
                nearestPoint = point;
                nearestDistance = distance;
            }
        }

        return new NearestGeometryVertex
        {
            Vertex = nearestPoint,
            CoordinateIndex = nearestIndex,
            Distance = nearestDistance
        };
    }

    private static NtsGeometry PrepareUnary(
        NtsGeometry geometry,
        PlanarGeometryAnalysisOptions? options,
        bool requirePlanarMeasurement)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        var prepared = ProjectForAnalysis(geometry, options);
        EnsureMeasurementCoordinatePolicy(prepared, options, requirePlanarMeasurement);
        return prepared;
    }

    private static (NtsGeometry First, NtsGeometry Second) PrepareBinary(
        NtsGeometry first,
        NtsGeometry second,
        PlanarGeometryAnalysisOptions? options,
        bool requirePlanarMeasurement)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        EnsureCompatibleKnownSrids(first, second, options);

        var preparedFirst = ProjectForAnalysis(first, options);
        var preparedSecond = ProjectForAnalysis(second, options);
        EnsureMeasurementCoordinatePolicy(preparedFirst, options, requirePlanarMeasurement);
        EnsureMeasurementCoordinatePolicy(preparedSecond, options, requirePlanarMeasurement);
        return (preparedFirst, preparedSecond);
    }

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
                "Planar measurements on EPSG:4326 coordinates are disabled by default; supply AnalysisSpatialReference for projection or set AllowGeographicMeasurements.");
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
}
