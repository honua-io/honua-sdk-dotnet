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

    private static readonly HonuaGrpcGeometryWriteOptions DefaultWriteOptions = new();

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
        return ReadGeometry(
            geometry,
            new HonuaGrpcGeometryReadOptions
            {
                SpatialReference = spatialReference,
                GeometryFactory = geometryFactory,
            });
    }

    /// <summary>Reads a Geospatial gRPC geometry message using the specified decoding options.</summary>
    public static NetTopologySuite.Geometries.Geometry ReadGeometry(
        Proto.Geometry geometry,
        HonuaGrpcGeometryReadOptions options)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(options);

        var factory = options.GeometryFactory ?? ResolveGeometryFactory(options.SpatialReference);
        var transform = options.CoordinateTransform;
        return geometry.ShapeCase switch
        {
            Proto.Geometry.ShapeOneofCase.Point => ReadPoint(geometry.Point, factory),
            Proto.Geometry.ShapeOneofCase.MultiPoint => ReadMultiPoint(geometry.MultiPoint, factory),
            Proto.Geometry.ShapeOneofCase.Polyline => ReadPolyline(geometry.Polyline, factory, transform),
            Proto.Geometry.ShapeOneofCase.Polygon => ReadPolygon(geometry.Polygon, factory, transform),
            Proto.Geometry.ShapeOneofCase.MultiPolygon => ReadMultiPolygon(geometry.MultiPolygon, factory, transform),
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
        return WriteGeometry(geometry, DefaultWriteOptions);
    }

    /// <summary>Writes a NetTopologySuite geometry as a Geospatial gRPC geometry message using the specified encoding options.</summary>
    public static Proto.Geometry WriteGeometry(
        NetTopologySuite.Geometries.Geometry geometry,
        HonuaGrpcGeometryWriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(options);

        return geometry switch
        {
            Point point => new Proto.Geometry { Point = WritePoint(point) },
            MultiPoint multiPoint => new Proto.Geometry { MultiPoint = WriteMultiPoint(multiPoint) },
            LineString lineString => new Proto.Geometry { Polyline = WritePolyline([lineString], options) },
            MultiLineString multiLineString => new Proto.Geometry
            {
                Polyline = WritePolyline(multiLineString.Geometries.OfType<LineString>(), options),
            },
            Polygon polygon => new Proto.Geometry { Polygon = WritePolygon(polygon, options) },
            MultiPolygon multiPolygon => new Proto.Geometry { MultiPolygon = WriteMultiPolygon(multiPolygon, options) },
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

        var result = ReadHorizontalSpatialReference(spatialReference);
        if (result is null)
        {
            return null;
        }

        return ApplyVerticalSpatialReference(result, spatialReference);
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

        if (spatialReference.VcsWkid is int vcsWkid)
        {
            proto.VcsWkid = vcsWkid;
        }

        if (spatialReference.LatestVcsWkid is int latestVcsWkid)
        {
            proto.LatestVcsWkid = latestVcsWkid;
        }

        return proto;
    }

    private static HonuaSpatialReference? ReadHorizontalSpatialReference(Proto.SpatialReference spatialReference)
    {
        if (!string.IsNullOrWhiteSpace(spatialReference.Wkt))
        {
            var wkid = spatialReference.Wkid > 0 ? spatialReference.Wkid : (int?)null;
            var latestWkid = spatialReference.LatestWkid > 0
                ? spatialReference.LatestWkid
                : (int?)null;
            return HonuaSpatialReference.FromWktWithLatestWkid(
                spatialReference.Wkt,
                wkid is null ? null : "EPSG",
                wkid,
                latestWkid);
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

    private static HonuaSpatialReference ApplyVerticalSpatialReference(
        HonuaSpatialReference horizontal,
        Proto.SpatialReference proto)
    {
        if (proto.VcsWkid > 0)
        {
            return horizontal.WithVerticalWkid(
                proto.VcsWkid,
                proto.LatestVcsWkid > 0 ? proto.LatestVcsWkid : null);
        }

        return proto.LatestVcsWkid > 0
            ? horizontal.WithVerticalWkid(proto.LatestVcsWkid)
            : horizontal;
    }

    private static GeometryFactory ResolveGeometryFactory(HonuaSpatialReference? spatialReference)
    {
        return spatialReference?.Wkid is int wkid && wkid > 0
            ? DefaultGeometryFactory.WithSRID(wkid)
            : DefaultGeometryFactory;
    }

    private static Point ReadPoint(Proto.PointGeometry point, GeometryFactory factory)
    {
        var presence = new CoordinateSequenceCodec.OrdinatePresence(point.HasZ, point.HasM);
        var values = new CoordinateSequenceCodec.OrdinateValues(point.X, point.Y, point.Z, point.M);
        return factory.CreatePoint(CoordinateSequenceCodec.CreateCoordinate(presence, values));
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
        GeometryFactory factory,
        Proto.CoordinateTransform? transform)
    {
        var lines = polyline.Paths
            .Select(path => factory.CreateLineString(CoordinateSequenceCodec.Read(path, transform)))
            .ToArray();
        return lines.Length == 1
            ? lines[0]
            : factory.CreateMultiLineString(lines);
    }

    private static Polygon ReadPolygon(
        Proto.PolygonGeometry polygon,
        GeometryFactory factory,
        Proto.CoordinateTransform? transform)
    {
        if (polygon.Rings.Count == 0)
        {
            return factory.CreatePolygon();
        }

        var shell = factory.CreateLinearRing(CloseRing(CoordinateSequenceCodec.Read(polygon.Rings[0], transform)));
        var holes = polygon.Rings
            .Skip(1)
            .Select(ring => factory.CreateLinearRing(CloseRing(CoordinateSequenceCodec.Read(ring, transform))))
            .ToArray();
        return factory.CreatePolygon(shell, holes);
    }

    private static MultiPolygon ReadMultiPolygon(
        Proto.MultiPolygonGeometry multiPolygon,
        GeometryFactory factory,
        Proto.CoordinateTransform? transform)
    {
        var polygons = multiPolygon.Polygons
            .Select(polygon => ReadPolygon(polygon, factory, transform))
            .ToArray();
        return factory.CreateMultiPolygon(polygons);
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

    private static Proto.PolylineGeometry WritePolyline(
        IEnumerable<LineString> lines,
        HonuaGrpcGeometryWriteOptions options)
    {
        var proto = new Proto.PolylineGeometry();
        foreach (var line in lines)
        {
            proto.Paths.Add(CoordinateSequenceCodec.Write(line.CoordinateSequence, options));
        }

        return proto;
    }

    private static Proto.PolygonGeometry WritePolygon(
        Polygon polygon,
        HonuaGrpcGeometryWriteOptions options)
    {
        var proto = new Proto.PolygonGeometry();
        if (polygon.IsEmpty)
        {
            return proto;
        }

        proto.Rings.Add(CoordinateSequenceCodec.Write(polygon.ExteriorRing.CoordinateSequence, options));
        for (var index = 0; index < polygon.NumInteriorRings; index++)
        {
            proto.Rings.Add(CoordinateSequenceCodec.Write(polygon.GetInteriorRingN(index).CoordinateSequence, options));
        }

        return proto;
    }

    private static Proto.MultiPolygonGeometry WriteMultiPolygon(
        MultiPolygon multiPolygon,
        HonuaGrpcGeometryWriteOptions options)
    {
        var proto = new Proto.MultiPolygonGeometry();
        foreach (Polygon polygon in multiPolygon.Geometries)
        {
            proto.Polygons.Add(WritePolygon(polygon, options));
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

    private static Coordinate[] CloseRing(Coordinate[] coordinates)
    {
        if (coordinates.Length == 0 ||
            coordinates[0].Equals2D(coordinates[^1]))
        {
            return coordinates;
        }

        var closed = new Coordinate[coordinates.Length + 1];
        Array.Copy(coordinates, closed, coordinates.Length);
        var presence = new CoordinateSequenceCodec.OrdinatePresence(
            !double.IsNaN(coordinates[0].Z),
            !double.IsNaN(coordinates[0].M));
        var values = new CoordinateSequenceCodec.OrdinateValues(
            coordinates[0].X,
            coordinates[0].Y,
            coordinates[0].Z,
            coordinates[0].M);
        closed[^1] = CoordinateSequenceCodec.CreateCoordinate(presence, values);
        return closed;
    }
}
