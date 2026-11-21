// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using NetTopologySuite.Geometries;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Geometry;

internal static class CoordinateSequenceCodec
{
    private readonly record struct CoordinateLayout(int Stride, bool HasZ, bool HasM);

    internal readonly record struct OrdinatePresence(bool HasZ, bool HasM);

    internal readonly record struct OrdinateValues(double X, double Y, double Z, double M);

    private sealed class TransformAxes
    {
        public double ScaleX { get; init; }
        public double ScaleY { get; init; }
        public double ScaleZ { get; init; }
        public double ScaleM { get; init; }
        public double TranslateX { get; init; }
        public double TranslateY { get; init; }
        public double TranslateZ { get; init; }
        public double TranslateM { get; init; }
    }

    private struct QuantizedAccumulator
    {
        public long X;
        public long Y;
        public long Z;
        public long M;
    }

    public static Coordinate[] Read(
        Proto.CoordinateSequence sequence,
        Proto.CoordinateTransform? transform)
    {
        if (sequence.Coords.Count > 0)
        {
            return ReadCoordinateMessages(sequence);
        }

        if (sequence.Ordinates.Count > 0)
        {
            return ReadPackedOrdinates(sequence);
        }

        if (sequence.DeltaCoords.Count > 0)
        {
            return ReadDeltaCoords(sequence, transform);
        }

        return [];
    }

    public static Proto.CoordinateSequence Write(
        CoordinateSequence sequence,
        HonuaGrpcGeometryWriteOptions options)
    {
        return options.CoordinateEncoding switch
        {
            HonuaGrpcCoordinateEncoding.CoordinateMessages => WriteCoordinateMessages(sequence),
            HonuaGrpcCoordinateEncoding.PackedOrdinates => WritePackedOrdinates(sequence),
            HonuaGrpcCoordinateEncoding.QuantizedDelta => WriteDeltaCoords(sequence, options.CoordinateTransform),
            _ => throw new ArgumentOutOfRangeException(
                nameof(options),
                options.CoordinateEncoding,
                "Unsupported coordinate encoding."),
        };
    }

    public static Coordinate CreateCoordinate(OrdinatePresence presence, OrdinateValues values)
    {
        return (presence.HasZ, presence.HasM) switch
        {
            (true, true) => new CoordinateZM(values.X, values.Y, values.Z, values.M),
            (true, false) => new CoordinateZ(values.X, values.Y, values.Z),
            (false, true) => new CoordinateM(values.X, values.Y, values.M),
            _ => new Coordinate(values.X, values.Y),
        };
    }

    private static Coordinate[] ReadCoordinateMessages(Proto.CoordinateSequence sequence)
    {
        var coordinates = new Coordinate[sequence.Coords.Count];
        for (var i = 0; i < sequence.Coords.Count; i++)
        {
            var c = sequence.Coords[i];
            coordinates[i] = CreateCoordinate(
                new OrdinatePresence(c.HasZ, c.HasM),
                new OrdinateValues(c.X, c.Y, c.Z, c.M));
        }

        return coordinates;
    }

    private static Coordinate[] ReadPackedOrdinates(Proto.CoordinateSequence sequence)
    {
        var layout = ResolveLayout(sequence);
        var values = sequence.Ordinates;
        ValidateDivisibleCount(values.Count, layout.Stride, "ordinates");

        var count = values.Count / layout.Stride;
        var coordinates = new Coordinate[count];
        var presence = new OrdinatePresence(layout.HasZ, layout.HasM);
        var mOffset = layout.HasZ ? 3 : 2;
        for (var i = 0; i < count; i++)
        {
            var baseIndex = i * layout.Stride;
            var z = layout.HasZ ? values[baseIndex + 2] : Coordinate.NullOrdinate;
            var m = layout.HasM ? values[baseIndex + mOffset] : Coordinate.NullOrdinate;
            var ordinates = new OrdinateValues(values[baseIndex], values[baseIndex + 1], z, m);
            coordinates[i] = CreateCoordinate(presence, ordinates);
        }

        return coordinates;
    }

    private static Coordinate[] ReadDeltaCoords(
        Proto.CoordinateSequence sequence,
        Proto.CoordinateTransform? transform)
    {
        var axes = ResolveTransformAxes(transform);
        var layout = ResolveLayout(sequence);
        var deltas = sequence.DeltaCoords;
        ValidateDivisibleCount(deltas.Count, layout.Stride, "delta_coords");

        var count = deltas.Count / layout.Stride;
        var coordinates = new Coordinate[count];
        var presence = new OrdinatePresence(layout.HasZ, layout.HasM);
        var mOffset = layout.HasZ ? 3 : 2;
        var acc = new QuantizedAccumulator();
        for (var i = 0; i < count; i++)
        {
            var baseIndex = i * layout.Stride;
            acc.X = checked(acc.X + deltas[baseIndex]);
            acc.Y = checked(acc.Y + deltas[baseIndex + 1]);
            var x = acc.X * axes.ScaleX + axes.TranslateX;
            var y = acc.Y * axes.ScaleY + axes.TranslateY;
            var z = layout.HasZ ? (acc.Z = checked(acc.Z + deltas[baseIndex + 2])) * axes.ScaleZ + axes.TranslateZ : Coordinate.NullOrdinate;
            var m = layout.HasM ? (acc.M = checked(acc.M + deltas[baseIndex + mOffset])) * axes.ScaleM + axes.TranslateM : Coordinate.NullOrdinate;
            coordinates[i] = CreateCoordinate(presence, new OrdinateValues(x, y, z, m));
        }

        return coordinates;
    }

    private static Proto.CoordinateSequence WriteCoordinateMessages(CoordinateSequence sequence)
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

    private static Proto.CoordinateSequence WritePackedOrdinates(CoordinateSequence sequence)
    {
        var layout = DetectLayout(sequence);
        var proto = CreateHeader(layout);
        for (var i = 0; i < sequence.Count; i++)
        {
            var values = ReadOrdinates(sequence, i);
            proto.Ordinates.Add(values.X);
            proto.Ordinates.Add(values.Y);
            if (layout.HasZ)
            {
                proto.Ordinates.Add(double.IsNaN(values.Z) ? 0.0 : values.Z);
            }

            if (layout.HasM)
            {
                proto.Ordinates.Add(double.IsNaN(values.M) ? 0.0 : values.M);
            }
        }

        return proto;
    }

    private sealed class DeltaEncodeState(
        Proto.CoordinateSequence target,
        TransformAxes axes,
        CoordinateLayout layout)
    {
        public Proto.CoordinateSequence Target { get; } = target;
        public TransformAxes Axes { get; } = axes;
        public CoordinateLayout Layout { get; } = layout;
        public long PrevX { get; set; }
        public long PrevY { get; set; }
        public long PrevZ { get; set; }
        public long PrevM { get; set; }
    }

    private static Proto.CoordinateSequence WriteDeltaCoords(
        CoordinateSequence sequence,
        Proto.CoordinateTransform? transform)
    {
        var axes = ResolveTransformAxes(transform);
        var layout = DetectLayout(sequence);
        ValidateNonZeroScales(axes, layout);

        var state = new DeltaEncodeState(CreateHeader(layout), axes, layout);
        for (var i = 0; i < sequence.Count; i++)
        {
            AppendQuantizedVertex(state, ReadOrdinates(sequence, i));
        }

        return state.Target;
    }

    private static void AppendQuantizedVertex(DeltaEncodeState state, OrdinateValues values)
    {
        var qx = Quantize(values.X, state.Axes.TranslateX, state.Axes.ScaleX);
        var qy = Quantize(values.Y, state.Axes.TranslateY, state.Axes.ScaleY);
        state.Target.DeltaCoords.Add(checked(qx - state.PrevX));
        state.Target.DeltaCoords.Add(checked(qy - state.PrevY));
        state.PrevX = qx;
        state.PrevY = qy;

        if (state.Layout.HasZ)
        {
            var qz = Quantize(double.IsNaN(values.Z) ? 0.0 : values.Z, state.Axes.TranslateZ, state.Axes.ScaleZ);
            state.Target.DeltaCoords.Add(checked(qz - state.PrevZ));
            state.PrevZ = qz;
        }

        if (state.Layout.HasM)
        {
            var qm = Quantize(double.IsNaN(values.M) ? 0.0 : values.M, state.Axes.TranslateM, state.Axes.ScaleM);
            state.Target.DeltaCoords.Add(checked(qm - state.PrevM));
            state.PrevM = qm;
        }
    }

    private static Proto.CoordinateSequence CreateHeader(CoordinateLayout layout)
        => new()
        {
            Stride = (uint)layout.Stride,
            HasZ = layout.HasZ,
            HasM = layout.HasM,
        };

    private static CoordinateLayout ResolveLayout(Proto.CoordinateSequence sequence)
    {
        return sequence.Stride switch
        {
            0 => new CoordinateLayout(
                2 + (sequence.HasZ ? 1 : 0) + (sequence.HasM ? 1 : 0),
                sequence.HasZ,
                sequence.HasM),
            2 when !sequence.HasZ && !sequence.HasM => new CoordinateLayout(2, false, false),
            3 when sequence.HasZ && sequence.HasM => throw new ArgumentException(
                "CoordinateSequence stride 3 cannot specify both has_z and has_m.",
                nameof(sequence)),
            3 when sequence.HasM => new CoordinateLayout(3, false, true),
            3 => new CoordinateLayout(3, true, false),
            4 => new CoordinateLayout(4, true, true),
            _ => throw new ArgumentException(
                $"CoordinateSequence stride {sequence.Stride} is invalid for has_z={sequence.HasZ}, has_m={sequence.HasM}.",
                nameof(sequence)),
        };
    }

    private static CoordinateLayout DetectLayout(CoordinateSequence sequence)
    {
        var hasZ = HasAnyOrdinate(sequence, Ordinate.Z);
        var hasM = HasAnyOrdinate(sequence, Ordinate.M);
        var stride = 2 + (hasZ ? 1 : 0) + (hasM ? 1 : 0);
        return new CoordinateLayout(stride, hasZ, hasM);
    }

    private static bool HasAnyOrdinate(CoordinateSequence sequence, Ordinate ordinate)
    {
        for (var i = 0; i < sequence.Count; i++)
        {
            if (!double.IsNaN(sequence.GetOrdinate(i, ordinate)))
            {
                return true;
            }
        }

        return false;
    }

    private static OrdinateValues ReadOrdinates(CoordinateSequence sequence, int index)
    {
        return new OrdinateValues(
            sequence.GetX(index),
            sequence.GetY(index),
            sequence.GetOrdinate(index, Ordinate.Z),
            sequence.GetOrdinate(index, Ordinate.M));
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

    private static TransformAxes ResolveTransformAxes(Proto.CoordinateTransform? transform)
    {
        if (transform is null)
        {
            throw new ArgumentException(
                "A CoordinateTransform is required for quantized delta coordinates.",
                nameof(transform));
        }

        var axes = new TransformAxes
        {
            ScaleX = transform.ScaleX,
            ScaleY = transform.ScaleY,
            ScaleZ = transform.HasScaleZ ? transform.ScaleZ : 1.0,
            ScaleM = transform.HasScaleM ? transform.ScaleM : 1.0,
            TranslateX = transform.TranslateX,
            TranslateY = transform.TranslateY,
            TranslateZ = transform.HasTranslateZ ? transform.TranslateZ : 0.0,
            TranslateM = transform.HasTranslateM ? transform.TranslateM : 0.0,
        };

        if (!double.IsFinite(axes.ScaleX) || !double.IsFinite(axes.ScaleY) ||
            !double.IsFinite(axes.ScaleZ) || !double.IsFinite(axes.ScaleM) ||
            !double.IsFinite(axes.TranslateX) || !double.IsFinite(axes.TranslateY) ||
            !double.IsFinite(axes.TranslateZ) || !double.IsFinite(axes.TranslateM))
        {
            throw new ArgumentException("CoordinateTransform parameters must be finite numbers.", nameof(transform));
        }

        return axes;
    }

    private static void ValidateNonZeroScales(TransformAxes axes, CoordinateLayout layout)
    {
        if (axes.ScaleX == 0.0 || axes.ScaleY == 0.0 ||
            (layout.HasZ && axes.ScaleZ == 0.0) ||
            (layout.HasM && axes.ScaleM == 0.0))
        {
            throw new ArgumentOutOfRangeException(nameof(axes), "CoordinateTransform scale values must be non-zero.");
        }
    }

    private static long Quantize(double world, double translate, double scale)
    {
        var scaled = (world - translate) / scale;
        if (!double.IsFinite(scaled))
        {
            throw new ArgumentOutOfRangeException(nameof(world), "Quantized coordinate value is not finite.");
        }

        return checked((long)Math.Round(scaled, MidpointRounding.AwayFromZero));
    }

    private static void ValidateDivisibleCount(int count, int stride, string fieldName)
    {
        if (count % stride != 0)
        {
            throw new ArgumentException(
                $"CoordinateSequence {fieldName} count ({count}) is not divisible by stride ({stride}).");
        }
    }
}
