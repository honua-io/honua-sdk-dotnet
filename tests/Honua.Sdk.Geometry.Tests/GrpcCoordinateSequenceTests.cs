// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Geometry.Tests;

public class GrpcCoordinateSequenceTests
{
    private static readonly GeometryFactory Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    [Fact]
    public void PackedOrdinates_2DLineString_RoundTrips()
    {
        var line = Factory.CreateLineString(
        [
            new Coordinate(-157.8583, 21.3069),
            new Coordinate(-157.8000, 21.2750),
            new Coordinate(-157.7500, 21.2600),
        ]);

        var proto = GrpcGeometryConverter.WriteGeometry(
            line,
            new HonuaGrpcGeometryWriteOptions
            {
                CoordinateEncoding = HonuaGrpcCoordinateEncoding.PackedOrdinates,
            });

        var path = Assert.Single(proto.Polyline.Paths);
        Assert.Empty(path.Coords);
        Assert.Equal(2u, path.Stride);
        Assert.False(path.HasZ);
        Assert.False(path.HasM);
        Assert.Equal(6, path.Ordinates.Count);

        var decoded = Assert.IsType<LineString>(
            GrpcGeometryConverter.ReadGeometry(proto, HonuaSpatialReference.Wgs84));
        Assert.Equal(4326, decoded.SRID);
        Assert.Equal(3, decoded.NumPoints);
        Assert.Equal(-157.8583, decoded.GetCoordinateN(0).X, 6);
        Assert.Equal(21.3069, decoded.GetCoordinateN(0).Y, 6);
        Assert.True(double.IsNaN(decoded.GetCoordinateN(0).Z));
        Assert.True(double.IsNaN(decoded.GetCoordinateN(0).M));
    }

    [Fact]
    public void PackedOrdinates_3DXYZ_And_3DXYM_DisambiguatedByFlags()
    {
        var xyzLine = Factory.CreateLineString(
        [
            new CoordinateZ(10.0, 20.0, 100.5),
            new CoordinateZ(11.0, 21.0, 105.25),
        ]);
        var xymLine = Factory.CreateLineString(
        [
            new CoordinateM(10.0, 20.0, 1.5),
            new CoordinateM(11.0, 21.0, 2.75),
        ]);

        var writeOptions = new HonuaGrpcGeometryWriteOptions
        {
            CoordinateEncoding = HonuaGrpcCoordinateEncoding.PackedOrdinates,
        };
        var protoXyz = GrpcGeometryConverter.WriteGeometry(xyzLine, writeOptions);
        var protoXym = GrpcGeometryConverter.WriteGeometry(xymLine, writeOptions);

        Assert.Equal(3u, protoXyz.Polyline.Paths[0].Stride);
        Assert.True(protoXyz.Polyline.Paths[0].HasZ);
        Assert.False(protoXyz.Polyline.Paths[0].HasM);

        Assert.Equal(3u, protoXym.Polyline.Paths[0].Stride);
        Assert.False(protoXym.Polyline.Paths[0].HasZ);
        Assert.True(protoXym.Polyline.Paths[0].HasM);

        var decodedXyz = Assert.IsType<LineString>(GrpcGeometryConverter.ReadGeometry(protoXyz));
        Assert.Equal(100.5, decodedXyz.GetCoordinateN(0).Z);
        Assert.True(double.IsNaN(decodedXyz.GetCoordinateN(0).M));

        var decodedXym = Assert.IsType<LineString>(GrpcGeometryConverter.ReadGeometry(protoXym));
        Assert.True(double.IsNaN(decodedXym.GetCoordinateN(0).Z));
        Assert.Equal(1.5, decodedXym.GetCoordinateN(0).M);
        Assert.Equal(2.75, decodedXym.GetCoordinateN(1).M);
    }

    [Fact]
    public void PackedOrdinates_4DXYZM_AndLegacyStride3Fallback_DecodeCorrectly()
    {
        var zmLine = Factory.CreateLineString(
        [
            new CoordinateZM(1.0, 2.0, 3.0, 4.0),
            new CoordinateZM(5.0, 6.0, 7.0, 8.0),
        ]);

        var protoZm = GrpcGeometryConverter.WriteGeometry(
            zmLine,
            new HonuaGrpcGeometryWriteOptions
            {
                CoordinateEncoding = HonuaGrpcCoordinateEncoding.PackedOrdinates,
            });
        var decodedZm = Assert.IsType<LineString>(GrpcGeometryConverter.ReadGeometry(protoZm));
        Assert.Equal(4u, protoZm.Polyline.Paths[0].Stride);
        Assert.True(protoZm.Polyline.Paths[0].HasZ);
        Assert.True(protoZm.Polyline.Paths[0].HasM);
        Assert.Equal(3.0, decodedZm.GetCoordinateN(0).Z);
        Assert.Equal(4.0, decodedZm.GetCoordinateN(0).M);
        Assert.Equal(7.0, decodedZm.GetCoordinateN(1).Z);
        Assert.Equal(8.0, decodedZm.GetCoordinateN(1).M);

        var legacySequence = new Proto.CoordinateSequence { Stride = 3 };
        legacySequence.Ordinates.AddRange([0.0, 1.0, 50.0, 2.0, 3.0, 60.0]);
        var legacyProto = new Proto.Geometry
        {
            Polyline = new Proto.PolylineGeometry { Paths = { legacySequence } },
        };
        var decodedLegacy = Assert.IsType<LineString>(GrpcGeometryConverter.ReadGeometry(legacyProto));
        Assert.Equal(50.0, decodedLegacy.GetCoordinateN(0).Z);
        Assert.True(double.IsNaN(decodedLegacy.GetCoordinateN(0).M));
    }

    [Fact]
    public void QuantizedDeltaCoords_2DPolygonWithHole_RoundTripsWithinQuantizationTolerance()
    {
        var polygon = Factory.CreatePolygon(
            Factory.CreateLinearRing(
            [
                new Coordinate(-158.0, 21.0),
                new Coordinate(-157.5, 21.0),
                new Coordinate(-157.5, 21.5),
                new Coordinate(-158.0, 21.5),
                new Coordinate(-158.0, 21.0),
            ]),
            [
                Factory.CreateLinearRing(
                [
                    new Coordinate(-157.9, 21.1),
                    new Coordinate(-157.6, 21.1),
                    new Coordinate(-157.6, 21.4),
                    new Coordinate(-157.9, 21.4),
                    new Coordinate(-157.9, 21.1),
                ]),
            ]);

        var transform = new Proto.CoordinateTransform
        {
            ScaleX = 1e-5,
            ScaleY = 1e-5,
            TranslateX = -158.0,
            TranslateY = 21.0,
        };

        var proto = GrpcGeometryConverter.WriteGeometry(
            polygon,
            new HonuaGrpcGeometryWriteOptions
            {
                CoordinateEncoding = HonuaGrpcCoordinateEncoding.QuantizedDelta,
                CoordinateTransform = transform,
            });

        Assert.Equal(2, proto.Polygon.Rings.Count);
        Assert.Empty(proto.Polygon.Rings[0].Coords);
        Assert.Empty(proto.Polygon.Rings[0].Ordinates);
        Assert.Equal(10, proto.Polygon.Rings[0].DeltaCoords.Count);
        Assert.Equal([0L, 0L, 50000L, 0L, 0L, 50000L, -50000L, 0L, 0L, -50000L], proto.Polygon.Rings[0].DeltaCoords);

        var decoded = Assert.IsType<Polygon>(GrpcGeometryConverter.ReadGeometry(
            proto,
            new HonuaGrpcGeometryReadOptions
            {
                SpatialReference = HonuaSpatialReference.Wgs84,
                CoordinateTransform = transform,
            }));

        Assert.Equal(4326, decoded.SRID);
        Assert.Equal(1, decoded.NumInteriorRings);
        Assert.Equal(-157.5, decoded.ExteriorRing.GetCoordinateN(1).X, 5);
        Assert.Equal(21.5, decoded.ExteriorRing.GetCoordinateN(2).Y, 5);
        Assert.Equal(-157.9, decoded.GetInteriorRingN(0).GetCoordinateN(0).X, 5);
        Assert.Equal(21.4, decoded.GetInteriorRingN(0).GetCoordinateN(2).Y, 5);
    }

    [Fact]
    public void QuantizedDeltaCoords_3DXYM_And_4DXYZM_RoundTripWithAxisTransforms()
    {
        var transform = new Proto.CoordinateTransform
        {
            ScaleX = 0.01,
            ScaleY = 0.01,
            ScaleZ = 0.1,
            ScaleM = 0.5,
            TranslateX = 100.0,
            TranslateY = 200.0,
            TranslateZ = 10.0,
            TranslateM = 5.0,
        };

        var xymLine = Factory.CreateLineString(
        [
            new CoordinateM(100.25, 200.50, 6.5),
            new CoordinateM(100.10, 200.80, 9.0),
        ]);
        var zmLine = Factory.CreateLineString(
        [
            new CoordinateZM(100.25, 200.50, 12.3, 6.5),
            new CoordinateZM(100.10, 200.80, 15.8, 9.0),
        ]);

        var writeOptions = new HonuaGrpcGeometryWriteOptions
        {
            CoordinateEncoding = HonuaGrpcCoordinateEncoding.QuantizedDelta,
            CoordinateTransform = transform,
        };
        var readOptions = new HonuaGrpcGeometryReadOptions
        {
            CoordinateTransform = transform,
        };

        var decodedXym = Assert.IsType<LineString>(
            GrpcGeometryConverter.ReadGeometry(GrpcGeometryConverter.WriteGeometry(xymLine, writeOptions), readOptions));
        Assert.True(double.IsNaN(decodedXym.GetCoordinateN(0).Z));
        Assert.Equal(6.5, decodedXym.GetCoordinateN(0).M, 4);
        Assert.Equal(9.0, decodedXym.GetCoordinateN(1).M, 4);

        var decodedZm = Assert.IsType<LineString>(
            GrpcGeometryConverter.ReadGeometry(GrpcGeometryConverter.WriteGeometry(zmLine, writeOptions), readOptions));
        Assert.Equal(100.10, decodedZm.GetCoordinateN(1).X, 4);
        Assert.Equal(200.80, decodedZm.GetCoordinateN(1).Y, 4);
        Assert.Equal(15.8, decodedZm.GetCoordinateN(1).Z, 4);
        Assert.Equal(9.0, decodedZm.GetCoordinateN(1).M, 4);
    }

    [Fact]
    public void CoordinateSequence_InvalidInputs_ThrowDescriptiveExceptions()
    {
        var badStride = new Proto.CoordinateSequence { Stride = 3, HasZ = true, HasM = true };
        badStride.Ordinates.AddRange([0, 1, 2, 3, 4, 5]);
        Assert.Throws<ArgumentException>(() => GrpcGeometryConverter.ReadGeometry(
            new Proto.Geometry { Polyline = new Proto.PolylineGeometry { Paths = { badStride } } }));

        var misaligned = new Proto.CoordinateSequence { Stride = 2 };
        misaligned.Ordinates.AddRange([0, 1, 2]);
        Assert.Throws<ArgumentException>(() => GrpcGeometryConverter.ReadGeometry(
            new Proto.Geometry { Polyline = new Proto.PolylineGeometry { Paths = { misaligned } } }));

        var deltaWithoutTransform = new Proto.CoordinateSequence { Stride = 2 };
        deltaWithoutTransform.DeltaCoords.AddRange([10, 20, 5, -5]);
        Assert.Throws<ArgumentException>(() => GrpcGeometryConverter.ReadGeometry(
            new Proto.Geometry { Polyline = new Proto.PolylineGeometry { Paths = { deltaWithoutTransform } } }));

        var line = Factory.CreateLineString([new Coordinate(0, 0), new Coordinate(1, 1)]);
        Assert.Throws<ArgumentOutOfRangeException>(() => GrpcGeometryConverter.WriteGeometry(
            line,
            new HonuaGrpcGeometryWriteOptions
            {
                CoordinateEncoding = HonuaGrpcCoordinateEncoding.QuantizedDelta,
                CoordinateTransform = new Proto.CoordinateTransform { ScaleX = 0, ScaleY = 1 },
            }));
    }

    [Fact]
    public void SpatialReference_VerticalWkid_RoundTripsThroughProto()
    {
        var compound = HonuaSpatialReference.FromWkid(4326).WithVerticalWkid(5773, 5773);

        var proto = GrpcGeometryConverter.WriteSpatialReference(compound);
        Assert.NotNull(proto);
        Assert.Equal(4326, proto.Wkid);
        Assert.Equal(5773, proto.VcsWkid);
        Assert.Equal(5773, proto.LatestVcsWkid);

        var roundTripped = GrpcGeometryConverter.ReadSpatialReference(proto);
        Assert.NotNull(roundTripped);
        Assert.Equal(4326, roundTripped.Wkid);
        Assert.Equal(5773, roundTripped.VcsWkid);
        Assert.Equal(5773, roundTripped.LatestVcsWkid);

        Assert.Throws<ArgumentOutOfRangeException>(() => HonuaSpatialReference.Wgs84.WithVerticalWkid(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => HonuaSpatialReference.Wgs84.WithVerticalWkid(5703, -1));
    }
}
