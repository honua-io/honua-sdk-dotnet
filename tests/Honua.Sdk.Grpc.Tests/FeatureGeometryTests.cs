// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry;
using Honua.Sdk.Grpc.Conversion;
using Honua.Sdk.Grpc.Models;
using NetTopologySuite.Geometries;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Tests;

public class FeatureGeometryTests
{
    private static readonly GeometryFactory Factory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    [Fact]
    public void ConvertFeature_SurfacesProtoNativeNtsPoint_WithZAndM()
    {
        var protoFeature = new Proto.Feature
        {
            Id = 7,
            Geometry = new Proto.Geometry
            {
                Point = new Proto.PointGeometry
                {
                    X = -157.8583,
                    Y = 21.3069,
                    Z = 12.5,
                    M = 4.25,
                },
            },
        };

        var feature = ProtoAdapter.ConvertFeature(protoFeature);

        // The gRPC path surfaces NTS directly (no serialize-then-reparse).
        Assert.NotNull(feature.NtsGeometry);
        var point = Assert.IsType<Point>(feature.ToGeometry());
        Assert.Equal(-157.8583, point.X, 6);
        Assert.Equal(21.3069, point.Y, 6);
        Assert.Equal(12.5, point.Coordinate.Z);
        Assert.Equal(4.25, point.Coordinate.M);

        // The Esri JSON dictionary is still populated for backward compatibility.
        Assert.NotNull(feature.Geometry);
    }

    [Fact]
    public void ConvertFeature_LineString_PreservesOrderAndMeasure()
    {
        var path = new Proto.CoordinateSequence();
        path.Coords.Add(new Proto.Coordinate { X = -158, Y = 21, M = 1 });
        path.Coords.Add(new Proto.Coordinate { X = -157, Y = 22, M = 2 });
        var protoFeature = new Proto.Feature
        {
            Id = 1,
            Geometry = new Proto.Geometry { Polyline = new Proto.PolylineGeometry { Paths = { path } } },
        };

        var line = Assert.IsType<LineString>(ProtoAdapter.ConvertFeature(protoFeature).ToGeometry());

        Assert.Equal(2, line.NumPoints);
        Assert.Equal(-158, line.GetCoordinateN(0).X);
        Assert.Equal(1, line.GetCoordinateN(0).M);
        Assert.Equal(2, line.GetCoordinateN(1).M);
    }

    [Fact]
    public void ConvertFeature_Polygon_PreservesRingOrientation()
    {
        var polygon = new Proto.PolygonGeometry();
        polygon.Rings.Add(CreateRing((0, 0), (0, 10), (10, 10), (10, 0), (0, 0)));
        polygon.Rings.Add(CreateRing((2, 2), (8, 2), (8, 8), (2, 8), (2, 2)));
        var protoFeature = new Proto.Feature
        {
            Id = 2,
            Geometry = new Proto.Geometry { Polygon = polygon },
        };

        var result = Assert.IsType<Polygon>(ProtoAdapter.ConvertFeature(protoFeature).ToGeometry());

        // The proto path maps ring[0] to the shell and the remaining rings to holes by position.
        Assert.Equal(1, result.NumInteriorRings);
        Assert.Equal(5, result.ExteriorRing.NumPoints);
        Assert.Equal(64, result.Area, 3); // 100 (10x10 shell) - 36 (6x6 hole)
    }

    [Fact]
    public void ToGeometry_ReadsEsriJsonDictionary_WhenNoNtsCarried()
    {
        var feature = new Feature
        {
            Id = 3,
            Geometry = new Dictionary<string, object?>
            {
                ["x"] = -122.4,
                ["y"] = 37.8,
            },
        };

        var point = Assert.IsType<Point>(feature.ToGeometry());
        Assert.Equal(-122.4, point.X, 6);
        Assert.Equal(37.8, point.Y, 6);
    }

    [Fact]
    public void ToGeometry_ReturnsNull_WhenNoGeometry()
    {
        Assert.Null(new Feature { Id = 4 }.ToGeometry());
    }

    [Fact]
    public void WithGeometry_RoundTripsThroughApplyEditsProto()
    {
        var original = Factory.CreatePolygon(Factory.CreateLinearRing(
        [
            new Coordinate(0, 0),
            new Coordinate(0, 10),
            new Coordinate(10, 10),
            new Coordinate(10, 0),
            new Coordinate(0, 0),
        ]));

        var feature = Feature.WithGeometry(
            original,
            new Dictionary<string, object?> { ["name"] = "parcel" },
            id: 99,
            spatialReference: HonuaSpatialReference.Wgs84);

        Assert.Same(original, feature.NtsGeometry);
        Assert.NotNull(feature.Geometry);

        // The NTS-native write path converts straight to the proto.
        var protoFeature = ProtoAdapter.ConvertFeatureToProto(feature);
        Assert.Equal(99, protoFeature.Id);
        Assert.Equal(Proto.Geometry.ShapeOneofCase.Polygon, protoFeature.Geometry.ShapeCase);

        var roundTripped = Assert.IsType<Polygon>(GrpcGeometryConverter.ReadGeometry(protoFeature.Geometry));
        Assert.True(roundTripped.EqualsTopologically(original));
    }

    [Fact]
    public void ApplyEditsRequest_AcceptsNtsFeatures()
    {
        var point = Factory.CreatePoint(new Coordinate(-100, 40));
        var request = new ApplyEditsRequest
        {
            ServiceId = "svc",
            LayerId = 0,
            Adds = [Feature.WithGeometry(point, new Dictionary<string, object?> { ["k"] = 1 })],
        };

        var proto = ProtoAdapter.ToProtoApplyEditsRequest(request);

        Assert.Single(proto.Adds);
        var added = GrpcGeometryConverter.ReadGeometry(proto.Adds[0].Geometry);
        var addedPoint = Assert.IsType<Point>(added);
        Assert.Equal(-100, addedPoint.X);
        Assert.Equal(40, addedPoint.Y);
    }

    private static Proto.CoordinateSequence CreateRing(params (double X, double Y)[] coordinates)
    {
        var sequence = new Proto.CoordinateSequence();
        foreach (var (x, y) in coordinates)
        {
            sequence.Coords.Add(new Proto.Coordinate { X = x, Y = y });
        }

        return sequence;
    }
}
