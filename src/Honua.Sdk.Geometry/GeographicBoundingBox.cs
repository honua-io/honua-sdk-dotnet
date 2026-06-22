// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Represents a WGS84 geographic bounding box defined by longitude and latitude extents
/// expressed in decimal degrees.
/// </summary>
/// <remarks>
/// This type is intentionally CRS-free; coordinates are always interpreted as EPSG:4326
/// (WGS84) longitude/latitude. For a CRS-aware bounding box that supports arbitrary
/// spatial references, use <see cref="FeatureBoundingBox"/>.
/// </remarks>
public sealed record GeographicBoundingBox
{
    /// <summary>
    /// The canonical WGS84 CRS identifier used when converting to <see cref="FeatureBoundingBox"/>.
    /// </summary>
    public const string Wgs84CrsIdentifier = "EPSG:4326";

    /// <summary>
    /// Initializes a new instance of the <see cref="GeographicBoundingBox"/> record, validating
    /// that all coordinates are finite and within the WGS84 ranges, and that the southern
    /// boundary is strictly less than the northern boundary.
    /// </summary>
    /// <param name="minLongitude">Western boundary in decimal degrees, within [-180, 180].</param>
    /// <param name="minLatitude">Southern boundary in decimal degrees, within [-90, 90].</param>
    /// <param name="maxLongitude">Eastern boundary in decimal degrees, within [-180, 180].</param>
    /// <param name="maxLatitude">Northern boundary in decimal degrees, within [-90, 90].</param>
    /// <remarks>
    /// A box whose <paramref name="minLongitude"/> is greater than its <paramref name="maxLongitude"/>
    /// is interpreted as an extent that crosses the antimeridian (±180°), spanning eastward from
    /// the western edge across 180° to the eastern edge. See <see cref="CrossesAntimeridian"/>.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when any coordinate is not finite, when a longitude falls outside [-180, 180],
    /// when a latitude falls outside [-90, 90], or when <paramref name="minLatitude"/> is not
    /// strictly less than <paramref name="maxLatitude"/>.
    /// </exception>
    public GeographicBoundingBox(
        double minLongitude,
        double minLatitude,
        double maxLongitude,
        double maxLatitude)
    {
        ValidateFinite(minLongitude, nameof(minLongitude));
        ValidateFinite(minLatitude, nameof(minLatitude));
        ValidateFinite(maxLongitude, nameof(maxLongitude));
        ValidateFinite(maxLatitude, nameof(maxLatitude));

        ValidateLongitudeRange(minLongitude, nameof(minLongitude));
        ValidateLongitudeRange(maxLongitude, nameof(maxLongitude));
        ValidateLatitudeRange(minLatitude, nameof(minLatitude));
        ValidateLatitudeRange(maxLatitude, nameof(maxLatitude));

        // Longitude ordering is intentionally NOT enforced: minLongitude > maxLongitude denotes
        // an antimeridian-crossing extent (e.g. west=170, east=-170 spans the Pacific across 180°).

        if (!(minLatitude < maxLatitude))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{nameof(minLatitude)} ({minLatitude}) must be strictly less than {nameof(maxLatitude)} ({maxLatitude})."),
                nameof(maxLatitude));
        }

        MinLongitude = minLongitude;
        MinLatitude = minLatitude;
        MaxLongitude = maxLongitude;
        MaxLatitude = maxLatitude;
    }

    /// <summary>
    /// Gets the western boundary in decimal degrees.
    /// </summary>
    public double MinLongitude { get; }

    /// <summary>
    /// Gets the southern boundary in decimal degrees.
    /// </summary>
    public double MinLatitude { get; }

    /// <summary>
    /// Gets the eastern boundary in decimal degrees.
    /// </summary>
    public double MaxLongitude { get; }

    /// <summary>
    /// Gets the northern boundary in decimal degrees.
    /// </summary>
    public double MaxLatitude { get; }

    /// <summary>
    /// Gets a value indicating whether this bounding box crosses the antimeridian (±180°),
    /// which is the case when <see cref="MinLongitude"/> is greater than <see cref="MaxLongitude"/>.
    /// When <see langword="true"/>, the longitudinal extent wraps eastward from
    /// <see cref="MinLongitude"/> across 180° to <see cref="MaxLongitude"/>.
    /// </summary>
    public bool CrossesAntimeridian => MinLongitude > MaxLongitude;

    /// <summary>
    /// Gets the longitudinal span of this bounding box in decimal degrees, accounting for
    /// antimeridian wrapping. For a non-crossing box this is <c>MaxLongitude - MinLongitude</c>;
    /// for an antimeridian-crossing box it is <c>(180 - MinLongitude) + (MaxLongitude - (-180))</c>.
    /// </summary>
    public double LongitudeSpan => CrossesAntimeridian
        ? (360.0 - MinLongitude) + MaxLongitude
        : MaxLongitude - MinLongitude;

    /// <summary>
    /// Determines whether the supplied WGS84 coordinate lies on or within this bounding box,
    /// accounting for antimeridian wrapping.
    /// </summary>
    /// <param name="longitude">Longitude in decimal degrees.</param>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <returns>
    /// <see langword="true"/> when the coordinate is on or inside the box; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public bool Contains(double longitude, double latitude)
    {
        if (latitude < MinLatitude || latitude > MaxLatitude)
        {
            return false;
        }

        // For an antimeridian-crossing box the valid longitude band is the union of
        // [MinLongitude, 180] and [-180, MaxLongitude].
        return CrossesAntimeridian
            ? longitude >= MinLongitude || longitude <= MaxLongitude
            : longitude >= MinLongitude && longitude <= MaxLongitude;
    }

    /// <summary>
    /// Determines whether this bounding box intersects another WGS84 bounding box. Boxes that
    /// share only an edge are considered to intersect.
    /// </summary>
    /// <param name="other">The other bounding box to test against.</param>
    /// <returns>
    /// <see langword="true"/> when the two boxes overlap or touch; otherwise
    /// <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
    public bool Intersects(GeographicBoundingBox other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (MinLatitude > other.MaxLatitude || MaxLatitude < other.MinLatitude)
        {
            return false;
        }

        return LongitudeBandsOverlap(this, other);
    }

    /// <summary>
    /// Tests whether the longitude bands of two boxes overlap, accounting for antimeridian
    /// wrapping on either box. A crossing box is treated as the union of two non-crossing
    /// half-bands split at ±180°.
    /// </summary>
    private static bool LongitudeBandsOverlap(GeographicBoundingBox a, GeographicBoundingBox b)
    {
        foreach (var (aMin, aMax) in LongitudeBands(a))
        {
            foreach (var (bMin, bMax) in LongitudeBands(b))
            {
                if (aMin <= bMax && aMax >= bMin)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Decomposes a box's longitude extent into one or two non-wrapping [min, max] bands.
    /// </summary>
    private static IEnumerable<(double Min, double Max)> LongitudeBands(GeographicBoundingBox box)
    {
        if (box.CrossesAntimeridian)
        {
            yield return (box.MinLongitude, 180.0);
            yield return (-180.0, box.MaxLongitude);
        }
        else
        {
            yield return (box.MinLongitude, box.MaxLongitude);
        }
    }

    /// <summary>
    /// Creates a CRS-aware <see cref="FeatureBoundingBox"/> representation of this geographic
    /// bounding box, tagged with the canonical WGS84 CRS identifier.
    /// </summary>
    /// <returns>A <see cref="FeatureBoundingBox"/> with <c>Crs = "EPSG:4326"</c>.</returns>
    public FeatureBoundingBox ToFeatureBoundingBox()
    {
        return new FeatureBoundingBox
        {
            MinX = MinLongitude,
            MinY = MinLatitude,
            MaxX = MaxLongitude,
            MaxY = MaxLatitude,
            Crs = Wgs84CrsIdentifier,
        };
    }

    /// <summary>
    /// Creates a <see cref="GeographicBoundingBox"/> from a CRS-aware
    /// <see cref="FeatureBoundingBox"/>, ensuring the source CRS is WGS84.
    /// </summary>
    /// <param name="bbox">The CRS-aware bounding box to convert.</param>
    /// <returns>An equivalent WGS84 <see cref="GeographicBoundingBox"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bbox"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="bbox"/> declares a non-WGS84 CRS, or when its coordinate
    /// extents are invalid.
    /// </exception>
    public static GeographicBoundingBox FromFeatureBoundingBox(FeatureBoundingBox bbox)
    {
        ArgumentNullException.ThrowIfNull(bbox);

        if (!IsWgs84(bbox.Crs))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"FeatureBoundingBox CRS '{bbox.Crs}' is not WGS84 (EPSG:4326)."),
                nameof(bbox));
        }

        return new GeographicBoundingBox(bbox.MinX, bbox.MinY, bbox.MaxX, bbox.MaxY);
    }

    private static bool IsWgs84(string? crs)
    {
        if (string.IsNullOrWhiteSpace(crs))
        {
            // A missing CRS is treated as WGS84 by convention for geographic data.
            return true;
        }

        var trimmed = crs.Trim();
        if (trimmed.Equals("EPSG:4326", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("urn:ogc:def:crs:EPSG::4326", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("http://www.opengis.net/def/crs/EPSG/0/4326", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("http://www.opengis.net/def/crs/OGC/1.3/CRS84", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("urn:ogc:def:crs:OGC:1.3:CRS84", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("CRS84", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("4326", StringComparison.Ordinal))
        {
            return true;
        }

        return HonuaSpatialReference.TryParse(trimmed, out var spatialReference)
            && (spatialReference.Wkid == 4326 || spatialReference.LatestWkid == 4326);
    }

    private static void ValidateFinite(double value, string paramName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentException($"{paramName} must be a finite number.", paramName);
        }
    }

    private static void ValidateLongitudeRange(double value, string paramName)
    {
        if (value < -180.0 || value > 180.0)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{paramName} ({value}) must be within [-180, 180]."),
                paramName);
        }
    }

    private static void ValidateLatitudeRange(double value, string paramName)
    {
        if (value < -90.0 || value > 90.0)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{paramName} ({value}) must be within [-90, 90]."),
                paramName);
        }
    }
}
