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
    /// Initializes a new instance of the <see cref="GeographicBoundingBox"/> record.
    /// </summary>
    /// <param name="minLongitude">Western boundary in decimal degrees.</param>
    /// <param name="minLatitude">Southern boundary in decimal degrees.</param>
    /// <param name="maxLongitude">Eastern boundary in decimal degrees.</param>
    /// <param name="maxLatitude">Northern boundary in decimal degrees.</param>
    /// <remarks>
    /// Longitude bounds may be supplied with <paramref name="minLongitude"/> greater than
    /// <paramref name="maxLongitude"/> to describe an extent that crosses the antimeridian
    /// (±180°); for example, a Pacific extent of <c>min = 170°, max = -170°</c> spans the
    /// 20° band straddling the date line. Equal longitude bounds describe a zero-width box
    /// and are rejected. Latitude is not periodic, so the southern bound must remain strictly
    /// less than the northern bound.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when any coordinate is not finite, when any coordinate is outside its valid
    /// range (longitude ±180°, latitude ±90°), when <paramref name="minLongitude"/> equals
    /// <paramref name="maxLongitude"/>, or when <paramref name="minLatitude"/> is not strictly
    /// less than <paramref name="maxLatitude"/>.
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

        ValidateLongitude(minLongitude, nameof(minLongitude));
        ValidateLongitude(maxLongitude, nameof(maxLongitude));
        ValidateLatitude(minLatitude, nameof(minLatitude));
        ValidateLatitude(maxLatitude, nameof(maxLatitude));

        // Longitude is periodic: minLongitude > maxLongitude denotes an antimeridian-crossing
        // extent and is allowed. Only a zero-width longitude span is rejected.
        if (minLongitude == maxLongitude)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{nameof(minLongitude)} ({minLongitude}) must not equal {nameof(maxLongitude)} ({maxLongitude})."),
                nameof(maxLongitude));
        }

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
    /// i.e. whether <see cref="MinLongitude"/> is greater than <see cref="MaxLongitude"/>.
    /// </summary>
    public bool IsAntimeridianCrossing => MinLongitude > MaxLongitude;

    /// <summary>
    /// Determines whether the supplied WGS84 coordinate lies on or within this bounding box.
    /// </summary>
    /// <param name="longitude">Longitude in decimal degrees.</param>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <returns>
    /// <see langword="true"/> when the coordinate is on or inside the box; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public bool Contains(double longitude, double latitude)
    {
        var longitudeInside = IsAntimeridianCrossing
            ? longitude >= MinLongitude || longitude <= MaxLongitude
            : longitude >= MinLongitude && longitude <= MaxLongitude;

        return longitudeInside
            && latitude >= MinLatitude
            && latitude <= MaxLatitude;
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

        var latitudeOverlaps = MinLatitude <= other.MaxLatitude
            && MaxLatitude >= other.MinLatitude;
        return latitudeOverlaps && LongitudeOverlaps(other);
    }

    private bool LongitudeOverlaps(GeographicBoundingBox other)
    {
        // For antimeridian-crossing boxes the longitude span is the union of
        // [MinLongitude, 180] and [-180, MaxLongitude], so the simple min/max overlap
        // test does not apply. Decompose into non-crossing segments and test each pair.
        var self = ToLongitudeSegments();
        var others = other.ToLongitudeSegments();
        foreach (var (selfMin, selfMax) in self)
        {
            foreach (var (otherMin, otherMax) in others)
            {
                if (selfMin <= otherMax && selfMax >= otherMin)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private (double Min, double Max)[] ToLongitudeSegments()
    {
        return IsAntimeridianCrossing
            ? [(MinLongitude, 180.0), (-180.0, MaxLongitude)]
            : [(MinLongitude, MaxLongitude)];
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

    private static void ValidateLongitude(double value, string paramName)
    {
        if (value is < -180.0 or > 180.0)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{paramName} ({value}) must be between -180 and 180 degrees."),
                paramName);
        }
    }

    private static void ValidateLatitude(double value, string paramName)
    {
        if (value is < -90.0 or > 90.0)
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{paramName} ({value}) must be between -90 and 90 degrees."),
                paramName);
        }
    }
}
