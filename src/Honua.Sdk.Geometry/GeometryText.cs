// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using NetTopologySuite.IO;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Reads and writes common text and binary geometry representations.
/// </summary>
public static class GeometryText
{
    /// <summary>
    /// Reads a Well-Known Text geometry.
    /// </summary>
    /// <param name="wkt">The WKT payload.</param>
    /// <returns>The parsed geometry.</returns>
    public static NetTopologySuite.Geometries.Geometry ReadWkt(string wkt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wkt);

        return new WKTReader().Read(wkt);
    }

    /// <summary>
    /// Writes a geometry as Well-Known Text.
    /// </summary>
    /// <param name="geometry">The geometry to write.</param>
    /// <returns>The WKT payload.</returns>
    public static string WriteWkt(NetTopologySuite.Geometries.Geometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        return new WKTWriter().Write(geometry);
    }

    /// <summary>
    /// Reads a Well-Known Binary geometry.
    /// </summary>
    /// <param name="wkb">The WKB payload.</param>
    /// <returns>The parsed geometry.</returns>
    public static NetTopologySuite.Geometries.Geometry ReadWkb(byte[] wkb)
    {
        ArgumentNullException.ThrowIfNull(wkb);

        return new WKBReader().Read(wkb);
    }

    /// <summary>
    /// Writes a geometry as Well-Known Binary.
    /// </summary>
    /// <param name="geometry">The geometry to write.</param>
    /// <param name="byteOrder">The byte order to use.</param>
    /// <param name="includeSrid">Whether to include the geometry SRID in the WKB payload.</param>
    /// <returns>The WKB payload.</returns>
    public static byte[] WriteWkb(
        NetTopologySuite.Geometries.Geometry geometry,
        ByteOrder byteOrder = ByteOrder.LittleEndian,
        bool includeSrid = false)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        return new WKBWriter(byteOrder, includeSrid).Write(geometry);
    }
}
