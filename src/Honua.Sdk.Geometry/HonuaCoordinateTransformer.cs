// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using NetTopologySuite.Geometries;
using ProjNet;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using ProjNet.IO.CoordinateSystems;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Applies ProjNET coordinate transforms to NetTopologySuite geometries.
/// </summary>
public sealed class HonuaCoordinateTransformer
{
    private readonly CoordinateSystemServices? coordinateSystemServices;
    private readonly CoordinateTransformationFactory coordinateTransformationFactory = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaCoordinateTransformer"/> class.
    /// </summary>
    /// <param name="coordinateSystemServices">Optional ProjNET coordinate system catalog.</param>
    public HonuaCoordinateTransformer(CoordinateSystemServices? coordinateSystemServices = null)
    {
        // ProjNET's default catalog blocks on background initialization during lookup, which
        // deadlocks single-threaded browser runtimes. The built-in systems are resolved below.
        this.coordinateSystemServices = coordinateSystemServices;
    }

    /// <summary>
    /// Transforms a single coordinate between spatial references.
    /// </summary>
    /// <param name="coordinate">The coordinate to transform.</param>
    /// <param name="source">The source spatial reference.</param>
    /// <param name="target">The target spatial reference.</param>
    /// <returns>The transformed coordinate.</returns>
    public Coordinate Transform(
        Coordinate coordinate,
        HonuaSpatialReference source,
        HonuaSpatialReference target)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var mathTransform = CreateMathTransform(source, target);
        var (x, y) = mathTransform.Transform(coordinate.X, coordinate.Y);
        return CreateCoordinate(x, y, coordinate.Z, coordinate.M);
    }

    /// <summary>
    /// Transforms all coordinates in a geometry between spatial references.
    /// </summary>
    /// <param name="geometry">The geometry to transform.</param>
    /// <param name="source">The source spatial reference.</param>
    /// <param name="target">The target spatial reference.</param>
    /// <returns>A transformed geometry copy.</returns>
    public NetTopologySuite.Geometries.Geometry Transform(
        NetTopologySuite.Geometries.Geometry geometry,
        HonuaSpatialReference source,
        HonuaSpatialReference target)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var mathTransform = CreateMathTransform(source, target);
        var transformed = geometry.Copy();
        transformed.Apply(new TransformCoordinateSequenceFilter(mathTransform));
        if (target.Wkid is int targetWkid)
        {
            transformed.SRID = targetWkid;
        }

        return transformed;
    }

    /// <summary>
    /// Creates the ProjNET math transform for the supplied spatial references.
    /// </summary>
    /// <param name="source">The source spatial reference.</param>
    /// <param name="target">The target spatial reference.</param>
    /// <returns>The math transform.</returns>
    public MathTransform CreateMathTransform(HonuaSpatialReference source, HonuaSpatialReference target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var sourceSystem = ResolveCoordinateSystem(source);
        var targetSystem = ResolveCoordinateSystem(target);
        var transformation = coordinateSystemServices?.CreateTransformation(sourceSystem, targetSystem)
            ?? coordinateTransformationFactory.CreateFromCoordinateSystems(sourceSystem, targetSystem)
            ?? throw new InvalidOperationException(
                $"ProjNET could not create a transformation from {source.Identifier} to {target.Identifier}.");
        return transformation.MathTransform;
    }

    private CoordinateSystem ResolveCoordinateSystem(HonuaSpatialReference spatialReference)
    {
        if (!string.IsNullOrWhiteSpace(spatialReference.Wkt))
        {
            return CoordinateSystemWktReader.Parse(spatialReference.Wkt) as CoordinateSystem ??
                throw new InvalidOperationException(
                    $"Spatial reference {spatialReference.Identifier} WKT did not resolve to a coordinate system.");
        }

        if (spatialReference.Wkid is int wkid)
        {
            var coordinateSystem = coordinateSystemServices?.GetCoordinateSystem(wkid);
            if (coordinateSystem is not null)
            {
                return coordinateSystem;
            }

            if (wkid is 4326 or 3857)
            {
                return ResolveKnownEpsg(wkid);
            }
        }

        if (spatialReference is { Authority: not null, Code: int code })
        {
            var coordinateSystem = coordinateSystemServices?.GetCoordinateSystem(spatialReference.Authority, code);
            if (coordinateSystem is not null)
            {
                return coordinateSystem;
            }

            if (spatialReference.Authority.Equals("EPSG", StringComparison.OrdinalIgnoreCase) &&
                code is 4326 or 3857)
            {
                return ResolveKnownEpsg(code);
            }
        }

        throw new InvalidOperationException(
            $"Spatial reference {spatialReference.Identifier} cannot be resolved by ProjNET.");
    }

    private static CoordinateSystem ResolveKnownEpsg(int code)
    {
        return code switch
        {
            4326 => GeographicCoordinateSystem.WGS84,
            3857 => ProjectedCoordinateSystem.WebMercator,
            _ => throw new InvalidOperationException($"EPSG:{code} is not available in the ProjNET catalog.")
        };
    }

    private static Coordinate CreateCoordinate(double x, double y, double z, double m)
    {
        var hasZ = !double.IsNaN(z);
        var hasM = !double.IsNaN(m);
        return (hasZ, hasM) switch
        {
            (true, true) => new CoordinateZM(x, y, z, m),
            (true, false) => new CoordinateZ(x, y, z),
            (false, true) => new CoordinateM(x, y, m),
            _ => new Coordinate(x, y)
        };
    }

    private sealed class TransformCoordinateSequenceFilter(MathTransform mathTransform) : ICoordinateSequenceFilter
    {
        public bool Done => false;

        public bool GeometryChanged => true;

        public void Filter(CoordinateSequence seq, int i)
        {
            var (x, y) = mathTransform.Transform(seq.GetX(i), seq.GetY(i));
            seq.SetOrdinate(i, Ordinate.X, x);
            seq.SetOrdinate(i, Ordinate.Y, y);
        }
    }
}
