// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Represents a spatial reference by WKID, authority/code, or WKT.
/// </summary>
public sealed record HonuaSpatialReference
{
    private HonuaSpatialReference(
        string? authority,
        int? code,
        int? wkid,
        int? latestWkid,
        string? wkt)
    {
        Authority = authority;
        Code = code;
        Wkid = wkid;
        LatestWkid = latestWkid;
        Wkt = wkt;
    }

    /// <summary>
    /// Gets the WGS84 longitude/latitude spatial reference.
    /// </summary>
    public static HonuaSpatialReference Wgs84 { get; } = FromWkid(4326);

    /// <summary>
    /// Gets the Web Mercator spatial reference.
    /// </summary>
    public static HonuaSpatialReference WebMercator { get; } = FromWkid(3857);

    /// <summary>
    /// Gets the spatial reference authority, such as EPSG.
    /// </summary>
    public string? Authority { get; }

    /// <summary>
    /// Gets the authority code.
    /// </summary>
    public int? Code { get; }

    /// <summary>
    /// Gets the well-known ID when one is available.
    /// </summary>
    public int? Wkid { get; }

    /// <summary>
    /// Gets the latest well-known ID when one is available.
    /// </summary>
    public int? LatestWkid { get; }

    /// <summary>
    /// Gets the Well-Known Text definition when one is available.
    /// </summary>
    public string? Wkt { get; }

    /// <summary>
    /// Gets the stable display identifier for the spatial reference.
    /// </summary>
    public string Identifier
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Authority) && Code is int code)
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"{Authority}:{code}");
            }

            if (Wkid is int wkid)
            {
                return string.Create(CultureInfo.InvariantCulture, $"WKID:{wkid}");
            }

            return string.IsNullOrWhiteSpace(Wkt) ? "UNKNOWN" : "WKT";
        }
    }

    /// <summary>
    /// Creates a spatial reference from a well-known ID.
    /// </summary>
    /// <param name="wkid">The well-known ID.</param>
    /// <param name="latestWkid">Optional latest well-known ID.</param>
    /// <returns>The spatial reference.</returns>
    public static HonuaSpatialReference FromWkid(int wkid, int? latestWkid = null)
    {
        if (wkid <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wkid), wkid, "WKID must be greater than zero.");
        }

        if (latestWkid is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latestWkid),
                latestWkid,
                "Latest WKID must be greater than zero.");
        }

        return new HonuaSpatialReference("EPSG", wkid, wkid, latestWkid ?? wkid, null);
    }

    /// <summary>
    /// Creates a spatial reference from an authority/code pair.
    /// </summary>
    /// <param name="authority">The coordinate reference system authority.</param>
    /// <param name="code">The authority code.</param>
    /// <returns>The spatial reference.</returns>
    public static HonuaSpatialReference FromAuthorityCode(string authority, int code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        if (code <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Authority code must be greater than zero.");
        }

        var normalizedAuthority = authority.Trim();
        var wkid = normalizedAuthority.Equals("EPSG", StringComparison.OrdinalIgnoreCase)
            ? code
            : (int?)null;
        return new HonuaSpatialReference(normalizedAuthority, code, wkid, wkid, null);
    }

    /// <summary>
    /// Creates a spatial reference from Well-Known Text.
    /// </summary>
    /// <param name="wkt">The WKT coordinate system definition.</param>
    /// <param name="authority">Optional authority name.</param>
    /// <param name="code">Optional authority code.</param>
    /// <returns>The spatial reference.</returns>
    public static HonuaSpatialReference FromWkt(string wkt, string? authority = null, int? code = null)
    {
        return FromWktWithLatestWkid(wkt, authority, code, latestWkid: null);
    }

    internal static HonuaSpatialReference FromWktWithLatestWkid(
        string wkt,
        string? authority,
        int? code,
        int? latestWkid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wkt);
        if (authority is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(authority);
        }

        if (code is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Authority code must be greater than zero.");
        }

        if (latestWkid is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latestWkid),
                latestWkid,
                "Latest WKID must be greater than zero.");
        }

        var normalizedAuthority = authority?.Trim();
        var wkid = normalizedAuthority is not null &&
            normalizedAuthority.Equals("EPSG", StringComparison.OrdinalIgnoreCase)
            ? code
            : null;
        return new HonuaSpatialReference(normalizedAuthority, code, wkid, latestWkid ?? wkid, wkt.Trim());
    }

    /// <summary>
    /// Parses a common CRS identifier such as EPSG:4326, WKID:3857, or an OGC CRS URI.
    /// </summary>
    /// <param name="value">The CRS identifier.</param>
    /// <returns>The parsed spatial reference.</returns>
    public static HonuaSpatialReference Parse(string value)
    {
        if (TryParse(value, out var spatialReference))
        {
            return spatialReference;
        }

        throw new FormatException($"Spatial reference '{value}' is not a supported CRS identifier.");
    }

    /// <summary>
    /// Tries to parse a common CRS identifier.
    /// </summary>
    /// <param name="value">The CRS identifier.</param>
    /// <param name="spatialReference">The parsed spatial reference when parsing succeeds.</param>
    /// <returns>True when parsing succeeds.</returns>
    public static bool TryParse(
        string? value,
        [NotNullWhen(true)] out HonuaSpatialReference? spatialReference)
    {
        spatialReference = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var wkid))
        {
            if (wkid <= 0)
            {
                return false;
            }

            spatialReference = FromWkid(wkid);
            return true;
        }

        if (TryParseAuthorityCode(trimmed, out spatialReference))
        {
            return true;
        }

        if (TryParseOgcIdentifier(trimmed, out spatialReference))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Converts the spatial reference to an OGC CRS identifier when an authority/code pair is available.
    /// </summary>
    /// <returns>The OGC CRS identifier, or null when the spatial reference is WKT-only.</returns>
    public string? ToOgcCrsIdentifier()
    {
        return !string.IsNullOrWhiteSpace(Authority) && Code is int code
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"http://www.opengis.net/def/crs/{Authority}/0/{code}")
            : null;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Identifier;
    }

    private static bool TryParseAuthorityCode(
        string value,
        [NotNullWhen(true)] out HonuaSpatialReference? spatialReference)
    {
        spatialReference = null;
        var parts = value.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            !string.IsNullOrWhiteSpace(parts[0]) &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var code))
        {
            if (code <= 0)
            {
                return false;
            }

            spatialReference = parts[0].Equals("WKID", StringComparison.OrdinalIgnoreCase)
                ? FromWkid(code)
                : FromAuthorityCode(parts[0], code);
            return true;
        }

        return false;
    }

    private static bool TryParseOgcIdentifier(
        string value,
        [NotNullWhen(true)] out HonuaSpatialReference? spatialReference)
    {
        spatialReference = null;
        if (value.StartsWith("urn:ogc:def:crs:", StringComparison.OrdinalIgnoreCase))
        {
            var parts = value.Split(':', StringSplitOptions.TrimEntries);
            if (parts.Length >= 7 &&
                int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var urnCode) &&
                urnCode > 0)
            {
                spatialReference = FromAuthorityCode(parts[4], urnCode);
                return true;
            }
        }

        if (value.Contains("/def/crs/", StringComparison.OrdinalIgnoreCase))
        {
            var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var crsIndex = Array.FindIndex(parts, part => part.Equals("crs", StringComparison.OrdinalIgnoreCase));
            if (crsIndex >= 0 &&
                parts.Length > crsIndex + 3 &&
                int.TryParse(parts[^1], NumberStyles.None, CultureInfo.InvariantCulture, out var uriCode) &&
                uriCode > 0)
            {
                spatialReference = FromAuthorityCode(parts[crsIndex + 1], uriCode);
                return true;
            }
        }

        return false;
    }
}
