// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Wfs.Models;

/// <summary>
/// A bounding box for spatial queries.
/// </summary>
public sealed class WfsBoundingBox
{
    /// <summary>Minimum X (longitude) coordinate.</summary>
    public required double MinX { get; init; }

    /// <summary>Minimum Y (latitude) coordinate.</summary>
    public required double MinY { get; init; }

    /// <summary>Maximum X (longitude) coordinate.</summary>
    public required double MaxX { get; init; }

    /// <summary>Maximum Y (latitude) coordinate.</summary>
    public required double MaxY { get; init; }

    /// <summary>
    /// Optional CRS URI (e.g. urn:ogc:def:crs:EPSG::4326).
    /// Coordinates are always encoded in longitude/latitude (CRS84) axis order regardless
    /// of the CRS specified here. If your WFS server strictly enforces CRS-native axis
    /// order, construct coordinates accordingly or omit this property to default to CRS84.
    /// </summary>
    public string? Crs { get; init; }

    /// <summary>
    /// Formats the bounding box as a WFS BBOX parameter value.
    /// </summary>
    internal string ToQueryValue()
    {
        var value = FormattableString.Invariant($"{MinX},{MinY},{MaxX},{MaxY}");
        return Crs is not null ? $"{value},{Crs}" : value;
    }
}
