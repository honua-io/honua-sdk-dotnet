// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using NetTopologySuite;
using NetTopologySuite.Geometries;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Geometry;

/// <summary>
/// Converts between Geospatial gRPC geometry messages and NetTopologySuite geometries.
/// </summary>
public static class GrpcGeometryConverter
{
    private static readonly GeometryFactory DefaultGeometryFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory();

    /// <summary>
    /// Reads a Geospatial gRPC geometry message.
    /// </summary>
    /// <param name="geometry">The gRPC geometry message.</param>
    /// <param name="spatialReference">Optional spatial reference used to set the geometry SRID.</param>
    /// <param name="geometryFactory">Optional factory used to construct the geometry.</param>
    /// <returns>The parsed geometry.</returns>
    public static NetTopologySuite.Geometries.Geometry ReadGeometry(
        Proto.Geometry geometry,
        HonuaSpatialReference? spatialReference = null,
        GeometryFactory? geometryFactory = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        var factory = geometryFactory ?? ResolveGeometryFactory(spatialReference);
        return geometry.ShapeCase switch
        {
            Proto.Geometry.ShapeOneofCase.Point => ReadPoint(geometry.Point, factory),
            Proto.Geometry.ShapeOneofCase.MultiPoint => ReadMultiPoint(geometry.MultiPoint, factory),
            Proto.Geometry.ShapeOneofCase.Polyline => ReadPolyline(geometry.Polyline, factory),
            Proto.Geometry.ShapeOneofCase.Polygon => ReadPolygon(geometry.Polygon, factory),
            Proto.Geometry.ShapeOneofCase.MultiPolygon => ReadMultiPolygon(geometry.MultiPolygon, factory),
            _ => throw new ArgumentException("gRPC geometry did not contain a supported shape.", nameof(geometry)),
        };
    }

    /// <summary>
    /// Writes a NetTopologySuite geometry as a Geospatial gRPC geometry message.
    /// </summary>
    /// <param name="geometry">The geometry to write.</param>
    /// <returns>The gRPC geometry message.</returns>
    public static Proto.Geometry WriteGeometry(NetTopologySuite.Geometries.Geometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        return geometry switch
        {
            Point point => new Proto.Geometry { Point = WritePoint(point) },
            MultiPoint multiPoint => new Proto.Geometry { MultiPoint = WriteMultiPoint(multiPoint) },
            LineString lineString => new Proto.Geometry { Polyline = WritePolyline([lineString]) },
            MultiLineString multiLineString => new Proto.Geometry
            {
                Polyline = WritePolyline(multiLineString.Geometries.OfType<LineString>()),
            },
            Polygon polygon => new Proto.Geometry { Polygon = WritePolygon(polygon) },
            MultiPolygon multiPolygon => new Proto.Geometry { MultiPolygon = WriteMultiPolygon(multiPolygon) },
            _ => throw new NotSupportedException(
                $"Geospatial gRPC does not support {geometry.GeometryType} geometries."),
        };
    }

    /// <summary>
    /// Reads a Geospatial gRPC spatial reference message.
    /// </summary>
    /// <param name="spatialReference">The gRPC spatial reference message.</param>
    /// <returns>The parsed spatial reference, or null when the proto message is empty.</returns>
    public static HonuaSpatialReference? ReadSpatialReference(Proto.SpatialReference? spatialReference)
    {
        if (spatialReference is null ||
            (spatialReference.Wkid <= 0 &&
             spatialReference.LatestWkid <= 0 &&
             string.IsNullOrWhiteSpace(spatialReference.Wkt)))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(spatialReference.Wkt))
        {
            return spatialReference.Wkid > 0
                ? HonuaSpatialReference.FromWkt(spatialReference.Wkt, "EPSG", spatialReference.Wkid)
                : HonuaSpatialReference.FromWkt(spatialReference.Wkt);
        }

        if (spatialReference.Wkid > 0)
        {
            return HonuaSpatialReference.FromWkid(
                spatialReference.Wkid,
                spatialReference.LatestWkid > 0 ? spatialReference.LatestWkid : null);
        }

        return spatialReference.LatestWkid > 0
            ? HonuaSpatialReference.FromWkid(spatialReference.LatestWkid)
            : null;
    }

    /// <summary>
    /// Writes a spatial reference as a Geospatial gRPC spatial reference message.
    /// </summary>
    /// <param name="spatialReference">The spatial reference to write.</param>
    /// <returns>The gRPC spatial reference message, or null when no spatial reference is supplied.</returns>
    public static Proto.SpatialReference? WriteSpatialReference(HonuaSpatialReference? spatialReference)
    {
        if (spatialReference is null)
        {
            return null;
        }

        var proto = new Proto.SpatialReference();
        if (spatialReference.Wkid is int wkid)
        {
            proto.Wkid = wkid;
        }

        if (spatialReference.LatestWkid is int latestWkid)
        {
            proto.LatestWkid = latestWkid;
        }

        if (!string.IsNullOrWhiteSpace(spatialReference.Wkt))
        {
            proto.Wkt = spatialReference.Wkt;
        }

        return proto;
    }

    private static GeometryFactory ResolveGeometryFactory(HonuaSpatialReference? spatialReference)
    {
        return spatialReference?.Wkid is int wkid && wkid > 0
            ? DefaultGeometryFactory.WithSRID(wkid)
            : DefaultGeometryFactory;
    }

    private static Point ReadPoint(Proto.PointGeometry point, GeometryFactory factory)
    {
        return factory.CreatePoint(CreateCoordinate(point.X, point.Y, point.HasZ, point.Z, point.HasM, point.M));
    }

    private static MultiPoint ReadMultiPoint(Proto.MultiPointGeometry multiPoint, GeometryFactory factory)
    {
        var points = multiPoint.Points
            .Select(point => ReadPoint(point, factory))
            .ToArray();
        return factory.CreateMultiPoint(points);
    }

    private static NetTopologySuite.Geometries.Geometry ReadPolyline(
        Proto.PolylineGeometry polyline,
        GeometryFactory factory)
    {
        var lines = polyline.Paths
            .Select(path => factory.CreateLineString(ReadCoordinateSequence(path)))
            .ToArray();
        return lines.Length == 1
            ? lines[0]
            : factory.CreateMultiLineString(lines);
    }

    private static Polygon ReadPolygon(Proto.PolygonGeometry polygon, GeometryFactory factory)
    {
        if (polygon.Rings.Count == 0)
        {
            return factory.CreatePolygon();
        }

        var shell = factory.CreateLinearRing(CloseRing(ReadCoordinateSequence(polygon.Rings[0])));
        var holes = polygon.Rings
            .Skip(1)
            .Select(ring => factory.CreateLinearRing(CloseRing(ReadCoordinateSequence(ring))))
            .ToArray();
        return factory.CreatePolygon(shell, holes);
    }

    private static MultiPolygon ReadMultiPolygon(Proto.MultiPolygonGeometry multiPolygon, GeometryFactory factory)
    {
        var polygons = multiPolygon.Polygons
            .Select(polygon => ReadPolygon(polygon, factory))
            .ToArray();
        return factory.CreateMultiPolygon(polygons);
    }

    private static Coordinate[] ReadCoordinateSequence(Proto.CoordinateSequence sequence)
    {
        return sequence.Coords
            .Select(coordinate => CreateCoordinate(
                coordinate.X,
                coordinate.Y,
                coordinate.HasZ,
                coordinate.Z,
                coordinate.HasM,
                coordinate.M))
            .ToArray();
    }

    private static Proto.PointGeometry WritePoint(Point point)
    {
        if (point.IsEmpty || point.CoordinateSequence.Count == 0)
        {
            throw new NotSupportedException("Geospatial gRPC does not support empty point geometries.");
        }

        var sequence = point.CoordinateSequence;
        var proto = new Proto.PointGeometry
        {
            X = sequence.GetX(0),
            Y = sequence.GetY(0),
        };
        WriteOptionalOrdinates(proto, sequence, 0);
        return proto;
    }

    private static Proto.MultiPointGeometry WriteMultiPoint(MultiPoint multiPoint)
    {
        var proto = new Proto.MultiPointGeometry();
        foreach (Point point in multiPoint.Geometries)
        {
            proto.Points.Add(WritePoint(point));
        }

        return proto;
    }

    private static Proto.PolylineGeometry WritePolyline(IEnumerable<LineString> lines)
    {
        var proto = new Proto.PolylineGeometry();
        foreach (var line in lines)
        {
            proto.Paths.Add(WriteCoordinateSequence(line.CoordinateSequence));
        }

        return proto;
    }

    private static Proto.PolygonGeometry WritePolygon(Polygon polygon)
    {
        var proto = new Proto.PolygonGeometry();
        if (polygon.IsEmpty)
        {
            return proto;
        }

        proto.Rings.Add(WriteCoordinateSequence(polygon.ExteriorRing.CoordinateSequence));
        for (var index = 0; index < polygon.NumInteriorRings; index++)
        {
            proto.Rings.Add(WriteCoordinateSequence(polygon.GetInteriorRingN(index).CoordinateSequence));
        }

        return proto;
    }

    private static Proto.MultiPolygonGeometry WriteMultiPolygon(MultiPolygon multiPolygon)
    {
        var proto = new Proto.MultiPolygonGeometry();
        foreach (Polygon polygon in multiPolygon.Geometries)
        {
            proto.Polygons.Add(WritePolygon(polygon));
        }

        return proto;
    }

    private static Proto.CoordinateSequence WriteCoordinateSequence(CoordinateSequence sequence)
    {
        var proto = new Proto.CoordinateSequence();
        for (var index = 0; index < sequence.Count; index++)
        {
            var coordinate = new Proto.Coordinate
            {
                X = sequence.GetX(index),
                Y = sequence.GetY(index),
            };
            WriteOptionalOrdinates(coordinate, sequence, index);
            proto.Coords.Add(coordinate);
        }

        return proto;
    }

    private static void WriteOptionalOrdinates(
        Proto.PointGeometry point,
        CoordinateSequence sequence,
        int index)
    {
        var z = sequence.GetOrdinate(index, Ordinate.Z);
        if (!double.IsNaN(z))
        {
            point.Z = z;
        }

        var m = sequence.GetOrdinate(index, Ordinate.M);
        if (!double.IsNaN(m))
        {
            point.M = m;
        }
    }

    private static void WriteOptionalOrdinates(
        Proto.Coordinate coordinate,
        CoordinateSequence sequence,
        int index)
    {
        var z = sequence.GetOrdinate(index, Ordinate.Z);
        if (!double.IsNaN(z))
        {
            coordinate.Z = z;
        }

        var m = sequence.GetOrdinate(index, Ordinate.M);
        if (!double.IsNaN(m))
        {
            coordinate.M = m;
        }
    }

    private static Coordinate CreateCoordinate(
        double x,
        double y,
        bool hasZ,
        double z,
        bool hasM,
        double m)
    {
        return (hasZ, hasM) switch
        {
            (true, true) => new CoordinateZM(x, y, z, m),
            (true, false) => new CoordinateZ(x, y, z),
            (false, true) => new CoordinateM(x, y, m),
            _ => new Coordinate(x, y),
        };
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
            !double.IsNaN(coordinates[0].Z),
            coordinates[0].Z,
            !double.IsNaN(coordinates[0].M),
            coordinates[0].M);
        return closed;
    }
}
