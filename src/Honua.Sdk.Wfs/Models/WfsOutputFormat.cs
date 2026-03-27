// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Wfs.Models;

/// <summary>
/// Common WFS output formats.
/// </summary>
public enum WfsOutputFormat
{
    /// <summary>GeoJSON (application/geo+json).</summary>
    GeoJson,

    /// <summary>GML 3.2 (application/gml+xml; version=3.2).</summary>
    Gml32,

    /// <summary>GML 3.1 (text/xml; subtype=gml/3.1.1).</summary>
    Gml31,

    /// <summary>CSV (text/csv).</summary>
    Csv,
}

/// <summary>
/// Extension methods for <see cref="WfsOutputFormat"/>.
/// </summary>
public static class WfsOutputFormatExtensions
{
    /// <summary>
    /// Returns the MIME type string for the given output format.
    /// </summary>
    public static string ToMediaType(this WfsOutputFormat format) => format switch
    {
        WfsOutputFormat.GeoJson => "application/geo+json",
        WfsOutputFormat.Gml32 => "application/gml+xml; version=3.2",
        WfsOutputFormat.Gml31 => "text/xml; subtype=gml/3.1.1",
        WfsOutputFormat.Csv => "text/csv",
        _ => "application/geo+json",
    };
}
