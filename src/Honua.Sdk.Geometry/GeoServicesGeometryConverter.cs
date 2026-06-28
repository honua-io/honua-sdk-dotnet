// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text;
using System.Text.Json;
using NetTopologySuite;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Converts between GeoServices JSON geometry objects and NetTopologySuite geometries.
/// </summary>
public static class GeoServicesGeometryConverter
{
    private static readonly GeometryFactory DefaultGeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory();

    /// <summary>
    /// Reads a GeoServices JSON geometry from a JSON element.
    /// </summary>
    /// <param name="geometry">The GeoServices geometry object.</param>
    /// <param name="geometryFactory">Optional factory used to construct the geometry.</param>
    /// <returns>The parsed geometry.</returns>
    public static NetTopologySuite.Geometries.Geometry ReadGeometry(
        JsonElement geometry,
        GeometryFactory? geometryFactory = null)
    {
        if (geometry.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("GeoServices geometry must be a JSON object.");
        }

        var factory = geometryFactory ?? ResolveGeometryFactory(geometry);
        var hasZ = ReadBoolean(geometry, "hasZ");
        var hasM = ReadBoolean(geometry, "hasM");

        if (TryReadNumber(geometry, "x", out var x) &&
            TryReadNumber(geometry, "y", out var y))
        {
            var z = TryReadNumber(geometry, "z", out var pointZ) ? pointZ : Coordinate.NullOrdinate;
            var m = TryReadNumber(geometry, "m", out var pointM) ? pointM : Coordinate.NullOrdinate;
            return factory.CreatePoint(CreateCoordinate(x, y, z, m));
        }

        if (TryReadNumber(geometry, "xmin", out var minX) &&
            TryReadNumber(geometry, "ymin", out var minY) &&
            TryReadNumber(geometry, "xmax", out var maxX) &&
            TryReadNumber(geometry, "ymax", out var maxY))
        {
            return factory.ToGeometry(new Envelope(minX, maxX, minY, maxY));
        }

        if (geometry.TryGetProperty("points", out var points))
        {
            return ReadMultiPoint(points, factory, hasZ, hasM);
        }

        if (geometry.TryGetProperty("paths", out var paths))
        {
            return ReadPaths(paths, factory, hasZ, hasM);
        }

        if (geometry.TryGetProperty("rings", out var rings))
        {
            return ReadRings(rings, factory, hasZ, hasM);
        }

        throw new JsonException("GeoServices geometry type could not be determined.");
    }

    /// <summary>
    /// Writes a geometry as a GeoServices JSON element.
    /// </summary>
    /// <param name="geometry">The geometry to write.</param>
    /// <param name="spatialReference">Optional spatial reference to emit on the wire object.</param>
    /// <returns>The GeoServices geometry object.</returns>
    public static JsonElement WriteGeometry(
        NetTopologySuite.Geometries.Geometry geometry,
        HonuaSpatialReference? spatialReference = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteGeometry(writer, geometry, spatialReference);
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Writes a geometry as a GeoServices JSON string.
    /// </summary>
    /// <param name="geometry">The geometry to write.</param>
    /// <param name="spatialReference">Optional spatial reference to emit on the wire object.</param>
    /// <returns>The GeoServices geometry object.</returns>
    public static string WriteGeometryString(
        NetTopologySuite.Geometries.Geometry geometry,
        HonuaSpatialReference? spatialReference = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            WriteGeometry(writer, geometry, spatialReference);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static GeometryFactory ResolveGeometryFactory(JsonElement geometry)
    {
        var spatialReference = ReadSpatialReference(geometry);
        return spatialReference?.Wkid is int wkid && wkid > 0
            ? DefaultGeometryFactory.WithSRID(wkid)
            : DefaultGeometryFactory;
    }

    private static HonuaSpatialReference? ReadSpatialReference(JsonElement geometry)
    {
        if (!geometry.TryGetProperty("spatialReference", out var spatialReference) ||
            spatialReference.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryReadNumber(spatialReference, "wkid", out var wkid))
        {
            var latestWkid = TryReadNumber(spatialReference, "latestWkid", out var latest)
                ? (int?)checked((int)latest)
                : null;
            return HonuaSpatialReference.FromWkid(checked((int)wkid), latestWkid);
        }

        if (spatialReference.TryGetProperty("wkt", out var wktElement) &&
            wktElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(wktElement.GetString()))
        {
            return HonuaSpatialReference.FromWkt(wktElement.GetString()!);
        }

        return null;
    }

    private static MultiPoint ReadMultiPoint(
        JsonElement points,
        GeometryFactory factory,
        bool hasZ,
        bool hasM)
    {
        EnsureArray(points, "points");

        var geometries = new List<Point>();
        foreach (var point in points.EnumerateArray())
        {
            geometries.Add(factory.CreatePoint(ReadCoordinate(point, hasZ, hasM)));
        }

        return factory.CreateMultiPoint(geometries.ToArray());
    }

    private static NetTopologySuite.Geometries.Geometry ReadPaths(
        JsonElement paths,
        GeometryFactory factory,
        bool hasZ,
        bool hasM)
    {
        EnsureArray(paths, "paths");

        var lines = new List<LineString>();
        foreach (var path in paths.EnumerateArray())
        {
            lines.Add(factory.CreateLineString(ReadCoordinates(path, hasZ, hasM)));
        }

        return lines.Count == 1
            ? lines[0]
            : factory.CreateMultiLineString(lines.ToArray());
    }

    private static NetTopologySuite.Geometries.Geometry ReadRings(
        JsonElement rings,
        GeometryFactory factory,
        bool hasZ,
        bool hasM)
    {
        EnsureArray(rings, "rings");

        var shells = new List<PolygonShell>();
        var holeRings = new List<LinearRing>();
        foreach (var ring in rings.EnumerateArray())
        {
            var linearRing = factory.CreateLinearRing(CloseRing(ReadCoordinates(ring, hasZ, hasM)));

            // Esri JSON uses clockwise-outer ring orientation: clockwise (NOT CCW)
            // rings are outer shells and counter-clockwise rings are holes. Classify
            // strictly by orientation/signed area rather than ring position, so a
            // leading hole is not mistakenly promoted to a shell and a later shell
            // keeps its hole.
            if (Orientation.IsCCW(linearRing.CoordinateSequence))
            {
                holeRings.Add(linearRing);
            }
            else
            {
                shells.Add(new PolygonShell(linearRing));
            }
        }

        if (shells.Count == 0)
        {
            // No clockwise (outer) rings were found. Rather than discarding the
            // geometry, treat every counter-clockwise ring as a shell so the
            // coordinates survive round-tripping.
            if (holeRings.Count == 0)
            {
                return factory.CreatePolygon();
            }

            shells.AddRange(holeRings.Select(ring => new PolygonShell(ring)));
            holeRings.Clear();
        }

        foreach (var holeRing in holeRings)
        {
            AssignHole(shells, holeRing, factory);
        }

        if (shells.Count == 1)
        {
            return factory.CreatePolygon(shells[0].Shell, shells[0].Holes.ToArray());
        }

        var polygons = shells
            .Select(shell => factory.CreatePolygon(shell.Shell, shell.Holes.ToArray()))
            .Cast<Polygon>()
            .ToArray();
        return factory.CreateMultiPolygon(polygons);
    }

    private static void AssignHole(
        IReadOnlyList<PolygonShell> shells,
        LinearRing holeRing,
        GeometryFactory factory)
    {
        var holeInteriorPoint = factory.CreatePolygon(holeRing).InteriorPoint;
        PolygonShell? containingShell = null;
        var containingShellArea = double.PositiveInfinity;

        foreach (var shell in shells)
        {
            var shellPolygon = factory.CreatePolygon(shell.Shell);
            if (shellPolygon.Covers(holeInteriorPoint) &&
                shellPolygon.Area < containingShellArea)
            {
                containingShell = shell;
                containingShellArea = shellPolygon.Area;
            }
        }

        (containingShell ?? shells[0]).Holes.Add(holeRing);
    }

    private static Coordinate[] ReadCoordinates(JsonElement coordinates, bool hasZ, bool hasM)
    {
        EnsureArray(coordinates, "coordinates");

        var result = new List<Coordinate>();
        foreach (var coordinate in coordinates.EnumerateArray())
        {
            result.Add(ReadCoordinate(coordinate, hasZ, hasM));
        }

        return result.ToArray();
    }

    private static Coordinate ReadCoordinate(JsonElement coordinate, bool hasZ, bool hasM)
    {
        EnsureArray(coordinate, "coordinate");

        var values = new List<double?>();
        foreach (var value in coordinate.EnumerateArray())
        {
            values.Add(value.ValueKind == JsonValueKind.Null ? null : value.GetDouble());
        }

        if (values.Count < 2)
        {
            throw new JsonException("A coordinate must contain at least X and Y ordinates.");
        }

        var inferredHasZ = hasZ || values.Count >= 4 || (values.Count == 3 && !hasM);
        var inferredHasM = hasM || values.Count >= 4;
        var z = inferredHasZ && values.Count > 2 && values[2].HasValue
            ? values[2]!.Value
            : Coordinate.NullOrdinate;
        var mIndex = inferredHasZ ? 3 : 2;
        var m = inferredHasM && values.Count > mIndex && values[mIndex].HasValue
            ? values[mIndex]!.Value
            : Coordinate.NullOrdinate;

        return CreateCoordinate(values[0]!.Value, values[1]!.Value, z, m);
    }

    private static Coordinate[] CloseRing(Coordinate[] coordinates)
    {
        if (coordinates.Length == 0 ||
            coordinates[0].Equals2D(coordinates[^1]))
        {
            return coordinates;
        }

        var closed = new Coordinate[coordinates.Length + 1];
        Array.Copy(coordinates, closed, coordinates.Length);
        closed[^1] = CreateCoordinate(
            coordinates[0].X,
            coordinates[0].Y,
            coordinates[0].Z,
            coordinates[0].M);
        return closed;
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

    private static bool ReadBoolean(JsonElement geometry, string propertyName)
    {
        return geometry.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.True;
    }

    private static bool TryReadNumber(JsonElement geometry, string propertyName, out double value)
    {
        if (geometry.TryGetProperty(propertyName, out var element) &&
            element.ValueKind == JsonValueKind.Number)
        {
            value = element.GetDouble();
            return true;
        }

        value = 0;
        return false;
    }

    private static void EnsureArray(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"GeoServices {name} value must be an array.");
        }
    }

    private static void WriteGeometry(
        Utf8JsonWriter writer,
        NetTopologySuite.Geometries.Geometry geometry,
        HonuaSpatialReference? spatialReference)
    {
        writer.WriteStartObject();

        switch (geometry)
        {
            case Point point:
                WritePoint(writer, point);
                break;
            case MultiPoint multiPoint:
                WriteMultiPoint(writer, multiPoint);
                break;
            case LineString lineString:
                WritePaths(writer, [lineString]);
                break;
            case MultiLineString multiLineString:
                WritePaths(writer, multiLineString.Geometries.OfType<LineString>());
                break;
            case Polygon polygon:
                WriteRings(writer, [polygon]);
                break;
            case MultiPolygon multiPolygon:
                WriteRings(writer, multiPolygon.Geometries.OfType<Polygon>());
                break;
            default:
                throw new NotSupportedException($"GeoServices JSON does not support {geometry.GeometryType} geometries.");
        }

        WriteSpatialReference(writer, spatialReference, geometry);
        writer.WriteEndObject();
    }

    private static void WritePoint(Utf8JsonWriter writer, Point point)
    {
        writer.WriteNumber("x", point.X);
        writer.WriteNumber("y", point.Y);

        var sequence = point.CoordinateSequence;
        if (sequence.Count == 0)
        {
            return;
        }

        var z = sequence.GetOrdinate(0, Ordinate.Z);
        if (!double.IsNaN(z))
        {
            writer.WriteNumber("z", z);
        }

        var m = sequence.GetOrdinate(0, Ordinate.M);
        if (!double.IsNaN(m))
        {
            writer.WriteNumber("m", m);
        }
    }

    private static void WriteMultiPoint(Utf8JsonWriter writer, MultiPoint multiPoint)
    {
        var hasZ = HasOrdinate((NetTopologySuite.Geometries.Geometry)multiPoint, Ordinate.Z);
        var hasM = HasOrdinate((NetTopologySuite.Geometries.Geometry)multiPoint, Ordinate.M);
        WriteDimensionality(writer, hasZ, hasM);
        writer.WritePropertyName("points");
        writer.WriteStartArray();
        foreach (Point point in multiPoint.Geometries)
        {
            WriteCoordinate(writer, point.CoordinateSequence, 0, hasZ, hasM);
        }

        writer.WriteEndArray();
    }

    private static void WritePaths(Utf8JsonWriter writer, IEnumerable<LineString> lines)
    {
        var lineList = lines.ToList();
        var hasZ = HasOrdinate(lineList, Ordinate.Z);
        var hasM = HasOrdinate(lineList, Ordinate.M);
        WriteDimensionality(writer, hasZ, hasM);
        writer.WritePropertyName("paths");
        writer.WriteStartArray();
        foreach (var line in lineList)
        {
            WriteCoordinateSequence(writer, line.CoordinateSequence, hasZ, hasM);
        }

        writer.WriteEndArray();
    }

    private static void WriteRings(Utf8JsonWriter writer, IEnumerable<Polygon> polygons)
    {
        var polygonList = polygons.ToList();
        var hasZ = HasOrdinate(polygonList, Ordinate.Z);
        var hasM = HasOrdinate(polygonList, Ordinate.M);
        WriteDimensionality(writer, hasZ, hasM);
        writer.WritePropertyName("rings");
        writer.WriteStartArray();
        foreach (var polygon in polygonList)
        {
            // Esri JSON requires clockwise exterior rings and counter-clockwise holes,
            // and the orientation-aware reader (ReadRings) classifies rings strictly by
            // orientation. NTS geometry decoded from GeoJSON (RFC 7946) carries a CCW
            // exterior, so emit each ring in the orientation Esri expects rather than
            // whatever the source carried; otherwise a strict Esri consumer (or this
            // SDK's own reader) would mis-demote the exterior ring of a multi-ring
            // polygon to a hole.
            WriteRingInEsriOrientation(writer, polygon.ExteriorRing, wantClockwise: true, hasZ, hasM);
            for (var index = 0; index < polygon.NumInteriorRings; index++)
            {
                WriteRingInEsriOrientation(writer, polygon.GetInteriorRingN(index), wantClockwise: false, hasZ, hasM);
            }
        }

        writer.WriteEndArray();
    }

    private static void WriteRingInEsriOrientation(
        Utf8JsonWriter writer,
        LineString ring,
        bool wantClockwise,
        bool hasZ,
        bool hasM)
    {
        var sequence = ring.CoordinateSequence;
        var isCcw = Orientation.IsCCW(sequence);

        // Reverse only when the current orientation does not match the Esri requirement:
        // an exterior ring must be clockwise (not CCW); a hole must be counter-clockwise.
        if (wantClockwise == isCcw)
        {
            sequence = ((LineString)ring.Reverse()).CoordinateSequence;
        }

        WriteCoordinateSequence(writer, sequence, hasZ, hasM);
    }

    private static void WriteCoordinateSequence(
        Utf8JsonWriter writer,
        CoordinateSequence sequence,
        bool hasZ,
        bool hasM)
    {
        writer.WriteStartArray();
        for (var index = 0; index < sequence.Count; index++)
        {
            WriteCoordinate(writer, sequence, index, hasZ, hasM);
        }

        writer.WriteEndArray();
    }

    private static void WriteCoordinate(
        Utf8JsonWriter writer,
        CoordinateSequence sequence,
        int index,
        bool hasZ,
        bool hasM)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(sequence.GetX(index));
        writer.WriteNumberValue(sequence.GetY(index));

        if (hasZ)
        {
            WriteOptionalNumber(writer, sequence.GetOrdinate(index, Ordinate.Z));
        }

        if (hasM)
        {
            WriteOptionalNumber(writer, sequence.GetOrdinate(index, Ordinate.M));
        }

        writer.WriteEndArray();
    }

    private static void WriteOptionalNumber(Utf8JsonWriter writer, double value)
    {
        if (double.IsNaN(value))
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteNumberValue(value);
    }

    private static bool HasOrdinate(
        IEnumerable<NetTopologySuite.Geometries.Geometry> geometries,
        Ordinate ordinate)
    {
        foreach (var geometry in geometries)
        {
            if (HasOrdinate(geometry, ordinate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOrdinate(NetTopologySuite.Geometries.Geometry geometry, Ordinate ordinate)
    {
        foreach (var coordinate in geometry.Coordinates)
        {
            var value = ordinate == Ordinate.Z ? coordinate.Z : coordinate.M;
            if (!double.IsNaN(value))
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteDimensionality(Utf8JsonWriter writer, bool hasZ, bool hasM)
    {
        if (hasZ)
        {
            writer.WriteBoolean("hasZ", true);
        }

        if (hasM)
        {
            writer.WriteBoolean("hasM", true);
        }
    }

    private static void WriteSpatialReference(
        Utf8JsonWriter writer,
        HonuaSpatialReference? spatialReference,
        NetTopologySuite.Geometries.Geometry geometry)
    {
        var reference = spatialReference ?? (geometry.SRID > 0
            ? HonuaSpatialReference.FromWkid(geometry.SRID)
            : null);
        if (reference is null)
        {
            return;
        }

        writer.WritePropertyName("spatialReference");
        writer.WriteStartObject();
        if (reference.Wkid is int wkid)
        {
            writer.WriteNumber("wkid", wkid);
        }

        if (reference.LatestWkid is int latestWkid)
        {
            writer.WriteNumber("latestWkid", latestWkid);
        }

        if (!string.IsNullOrWhiteSpace(reference.Wkt))
        {
            writer.WriteString("wkt", reference.Wkt);
        }

        writer.WriteEndObject();
    }

    private sealed class PolygonShell(LinearRing shell)
    {
        public LinearRing Shell { get; } = shell;

        public List<LinearRing> Holes { get; } = [];
    }
}
