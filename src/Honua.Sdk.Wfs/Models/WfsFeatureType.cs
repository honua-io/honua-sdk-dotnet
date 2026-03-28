// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Wfs.Models;

/// <summary>
/// Describes a feature type advertised by a WFS service.
/// </summary>
public sealed class WfsFeatureType
{
    /// <summary>Qualified name of the feature type.</summary>
    public string Name { get; init; } = "";

    /// <summary>Human-readable title.</summary>
    public string? Title { get; init; }

    /// <summary>Human-readable description.</summary>
    public string? Abstract { get; init; }

    /// <summary>Default coordinate reference system URI.</summary>
    public string? DefaultCrs { get; init; }

    /// <summary>Additional supported coordinate reference systems.</summary>
    public IReadOnlyList<string> OtherCrs { get; init; } = [];

    /// <summary>Supported output formats for this feature type.</summary>
    public IReadOnlyList<string> OutputFormats { get; init; } = [];

    /// <summary>Lower corner of the WGS84 bounding box (longitude, latitude).</summary>
    public (double X, double Y)? LowerCorner { get; init; }

    /// <summary>Upper corner of the WGS84 bounding box (longitude, latitude).</summary>
    public (double X, double Y)? UpperCorner { get; init; }
}
