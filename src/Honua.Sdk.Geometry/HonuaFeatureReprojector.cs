// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Nts = NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Applies client-side, on-the-fly reprojection to NetTopologySuite geometries returned by a query.
/// </summary>
/// <remarks>
/// Mirrors arcpy's on-the-fly projection: when a tool asks for an output CRS that differs from the
/// CRS the server actually returned, this helper reprojects the geometry locally with ProjNET via
/// <see cref="HonuaCoordinateTransformer"/> instead of trusting the server-honored output SR. The
/// transform is opt-in and a no-op (returns the input instance) when source and target match.
/// </remarks>
public sealed class HonuaFeatureReprojector
{
    private readonly HonuaCoordinateTransformer transformer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaFeatureReprojector"/> class.
    /// </summary>
    /// <param name="transformer">Optional coordinate transformer; a default ProjNET-backed one is used when omitted.</param>
    public HonuaFeatureReprojector(HonuaCoordinateTransformer? transformer = null)
    {
        this.transformer = transformer ?? new HonuaCoordinateTransformer();
    }

    /// <summary>
    /// Reprojects a single geometry from its returned spatial reference to the requested output CRS.
    /// </summary>
    /// <param name="geometry">The geometry to reproject.</param>
    /// <param name="returnedSpatialReference">The spatial reference the geometry is currently expressed in.</param>
    /// <param name="targetSpatialReference">The requested output spatial reference.</param>
    /// <returns>
    /// The geometry expressed in <paramref name="targetSpatialReference"/>. When the source and target
    /// are equivalent the input instance is returned unchanged.
    /// </returns>
    public Nts.Geometry Reproject(
        Nts.Geometry geometry,
        HonuaSpatialReference returnedSpatialReference,
        HonuaSpatialReference targetSpatialReference)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(returnedSpatialReference);
        ArgumentNullException.ThrowIfNull(targetSpatialReference);

        if (AreEquivalent(returnedSpatialReference, targetSpatialReference))
        {
            return geometry;
        }

        return transformer.Transform(geometry, returnedSpatialReference, targetSpatialReference);
    }

    /// <summary>
    /// Reprojects a sequence of geometries from their returned spatial reference to the requested CRS.
    /// </summary>
    /// <param name="geometries">The geometries to reproject (null entries are passed through).</param>
    /// <param name="returnedSpatialReference">The spatial reference the geometries are expressed in.</param>
    /// <param name="targetSpatialReference">The requested output spatial reference.</param>
    /// <returns>The reprojected geometries in the same order. A no-op when source and target match.</returns>
    public IReadOnlyList<Nts.Geometry?> Reproject(
        IEnumerable<Nts.Geometry?> geometries,
        HonuaSpatialReference returnedSpatialReference,
        HonuaSpatialReference targetSpatialReference)
    {
        ArgumentNullException.ThrowIfNull(geometries);
        ArgumentNullException.ThrowIfNull(returnedSpatialReference);
        ArgumentNullException.ThrowIfNull(targetSpatialReference);

        if (AreEquivalent(returnedSpatialReference, targetSpatialReference))
        {
            return geometries.ToList();
        }

        var transform = transformer.CreateMathTransform(returnedSpatialReference, targetSpatialReference);
        var targetWkid = targetSpatialReference.Wkid;

        var results = new List<Nts.Geometry?>();
        foreach (var geometry in geometries)
        {
            if (geometry is null)
            {
                results.Add(null);
                continue;
            }

            var copy = geometry.Copy();
            copy.Apply(new ReprojectingSequenceFilter(transform));
            if (targetWkid is int wkid)
            {
                copy.SRID = wkid;
            }

            results.Add(copy);
        }

        return results;
    }

    /// <summary>
    /// Determines whether a returned spatial reference already matches the requested output CRS,
    /// in which case no reprojection is required.
    /// </summary>
    /// <param name="returnedSpatialReference">The spatial reference the data is expressed in.</param>
    /// <param name="targetSpatialReference">The requested output spatial reference.</param>
    /// <returns><see langword="true"/> when no reprojection is needed.</returns>
    public static bool AreEquivalent(
        HonuaSpatialReference? returnedSpatialReference,
        HonuaSpatialReference? targetSpatialReference)
    {
        if (returnedSpatialReference is null || targetSpatialReference is null)
        {
            return false;
        }

        if (returnedSpatialReference.Wkid is int sourceWkid &&
            targetSpatialReference.Wkid is int targetWkid)
        {
            return sourceWkid == targetWkid;
        }

        if (returnedSpatialReference.LatestWkid is int sourceLatest &&
            targetSpatialReference.LatestWkid is int targetLatest)
        {
            return sourceLatest == targetLatest;
        }

        if (!string.IsNullOrWhiteSpace(returnedSpatialReference.Wkt) &&
            !string.IsNullOrWhiteSpace(targetSpatialReference.Wkt))
        {
            return string.Equals(
                returnedSpatialReference.Wkt,
                targetSpatialReference.Wkt,
                StringComparison.Ordinal);
        }

        return string.Equals(
            returnedSpatialReference.Identifier,
            targetSpatialReference.Identifier,
            StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ReprojectingSequenceFilter(
        ProjNet.CoordinateSystems.Transformations.MathTransform mathTransform)
        : Nts.ICoordinateSequenceFilter
    {
        public bool Done => false;

        public bool GeometryChanged => true;

        public void Filter(Nts.CoordinateSequence seq, int i)
        {
            var (x, y) = mathTransform.Transform(seq.GetX(i), seq.GetY(i));
            seq.SetOrdinate(i, Nts.Ordinate.X, x);
            seq.SetOrdinate(i, Nts.Ordinate.Y, y);
        }
    }
}
