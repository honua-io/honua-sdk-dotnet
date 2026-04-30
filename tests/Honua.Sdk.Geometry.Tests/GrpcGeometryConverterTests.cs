// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Geometry.Tests;

public class GrpcGeometryConverterTests
{
    [Fact]
    public void ReadGeometry_PointPreservesZAndM()
    {
        var proto = new Proto.Geometry
        {
            Point = new Proto.PointGeometry
            {
                X = -157.8583,
                Y = 21.3069,
                Z = 12.5,
                M = 4.25,
            },
        };

        var geometry = GrpcGeometryConverter.ReadGeometry(proto, HonuaSpatialReference.Wgs84);

        var point = Assert.IsType<Point>(geometry);
        Assert.Equal(4326, point.SRID);
        Assert.Equal(12.5, point.Coordinate.Z);
        Assert.Equal(4.25, point.Coordinate.M);
    }

    [Fact]
    public void WriteGeometry_LineStringPreservesMeasure()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var line = factory.CreateLineString(
            [
                new CoordinateM(-158, 21, 1),
                new CoordinateM(-157, 22, 2),
            ]);

        var proto = GrpcGeometryConverter.WriteGeometry(line);

        Assert.Equal(Proto.Geometry.ShapeOneofCase.Polyline, proto.ShapeCase);
        var coordinate = proto.Polyline.Paths[0].Coords[0];
        Assert.False(coordinate.HasZ);
        Assert.True(coordinate.HasM);
        Assert.Equal(1, coordinate.M);
    }

    [Fact]
    public void ReadGeometry_PolygonPreservesInteriorRings()
    {
        var polygon = new Proto.PolygonGeometry();
        polygon.Rings.Add(CreateSequence(
            (0, 0),
            (0, 10),
            (10, 10),
            (10, 0),
            (0, 0)));
        polygon.Rings.Add(CreateSequence(
            (2, 2),
            (8, 2),
            (8, 8),
            (2, 8),
            (2, 2)));
        var proto = new Proto.Geometry { Polygon = polygon };

        var geometry = GrpcGeometryConverter.ReadGeometry(proto);

        var result = Assert.IsType<Polygon>(geometry);
        Assert.Equal(1, result.NumInteriorRings);
    }

    [Fact]
    public void WriteGeometry_MultiPolygonPreservesPolygonBoundaries()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var first = factory.CreatePolygon(
            factory.CreateLinearRing(
                [
                    new Coordinate(0, 0),
                    new Coordinate(0, 10),
                    new Coordinate(10, 10),
                    new Coordinate(10, 0),
                    new Coordinate(0, 0),
                ]),
            [
                factory.CreateLinearRing(
                    [
                        new Coordinate(2, 2),
                        new Coordinate(8, 2),
                        new Coordinate(8, 8),
                        new Coordinate(2, 8),
                        new Coordinate(2, 2),
                    ]),
            ]);
        var second = factory.CreatePolygon(
            [
                new Coordinate(20, 0),
                new Coordinate(20, 10),
                new Coordinate(30, 10),
                new Coordinate(30, 0),
                new Coordinate(20, 0),
            ]);
        var multiPolygon = factory.CreateMultiPolygon([first, second]);

        var proto = GrpcGeometryConverter.WriteGeometry(multiPolygon);

        Assert.Equal(Proto.Geometry.ShapeOneofCase.MultiPolygon, proto.ShapeCase);
        Assert.Equal(2, proto.MultiPolygon.Polygons.Count);
        Assert.Equal(2, proto.MultiPolygon.Polygons[0].Rings.Count);
        Assert.Single(proto.MultiPolygon.Polygons[1].Rings);
    }

    [Fact]
    public void WriteGeometry_EmptyPolygonWritesNoRings()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var polygon = factory.CreatePolygon();

        var proto = GrpcGeometryConverter.WriteGeometry(polygon);

        Assert.Equal(Proto.Geometry.ShapeOneofCase.Polygon, proto.ShapeCase);
        Assert.Empty(proto.Polygon.Rings);
    }

    [Fact]
    public void SpatialReference_RoundTripsWkid()
    {
        var proto = GrpcGeometryConverter.WriteSpatialReference(HonuaSpatialReference.FromWkid(3857));

        var spatialReference = GrpcGeometryConverter.ReadSpatialReference(proto);

        Assert.NotNull(spatialReference);
        Assert.Equal(3857, spatialReference.Wkid);
        Assert.Equal(3857, spatialReference.LatestWkid);
    }

    [Fact]
    public void ReadSpatialReference_WktPreservesLatestWkid()
    {
        var proto = new Proto.SpatialReference
        {
            Wkid = 102100,
            LatestWkid = 3857,
            Wkt = "PROJCS[\"WGS_1984_Web_Mercator_Auxiliary_Sphere\"]",
        };

        var spatialReference = GrpcGeometryConverter.ReadSpatialReference(proto);

        Assert.NotNull(spatialReference);
        Assert.Equal(102100, spatialReference.Wkid);
        Assert.Equal(3857, spatialReference.LatestWkid);
        Assert.Equal(proto.Wkt, spatialReference.Wkt);
    }

    [Fact]
    public void WriteGeometry_GeometryCollectionThrows()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var collection = factory.CreateGeometryCollection([]);

        Assert.Throws<NotSupportedException>(() => GrpcGeometryConverter.WriteGeometry(collection));
    }

    private static Proto.CoordinateSequence CreateSequence(params (double X, double Y)[] values)
    {
        var sequence = new Proto.CoordinateSequence();
        foreach (var value in values)
        {
            sequence.Coords.Add(new Proto.Coordinate { X = value.X, Y = value.Y });
        }

        return sequence;
    }
}
