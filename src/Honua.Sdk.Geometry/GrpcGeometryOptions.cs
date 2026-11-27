// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using NetTopologySuite.Geometries;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Geometry;

/// <summary>Coordinate sequence wire encodings supported by Geospatial gRPC.</summary>
public enum HonuaGrpcCoordinateEncoding
{
    /// <summary>Per-vertex Coordinate submessages.</summary>
    CoordinateMessages = 0,

    /// <summary>Packed interleaved IEEE-754 double ordinates.</summary>
    PackedOrdinates = 1,

    /// <summary>Quantized delta-encoded sint64 coordinates paired with a CoordinateTransform.</summary>
    QuantizedDelta = 2,
}

/// <summary>Options controlling how Geospatial gRPC geometry messages are decoded.</summary>
public sealed record HonuaGrpcGeometryReadOptions
{
    /// <summary>Optional spatial reference used to set the geometry SRID.</summary>
    public HonuaSpatialReference? SpatialReference { get; init; }

    /// <summary>Optional geometry factory used to construct the geometry.</summary>
    public GeometryFactory? GeometryFactory { get; init; }

    /// <summary>Optional quantization transform used to decode delta-encoded coordinates.</summary>
    public Proto.CoordinateTransform? CoordinateTransform { get; init; }
}

/// <summary>Options controlling how NetTopologySuite geometries are encoded as Geospatial gRPC messages.</summary>
public sealed record HonuaGrpcGeometryWriteOptions
{
    /// <summary>Coordinate sequence wire encoding to emit for paths and rings.</summary>
    public HonuaGrpcCoordinateEncoding CoordinateEncoding { get; init; } = HonuaGrpcCoordinateEncoding.CoordinateMessages;

    /// <summary>Quantization transform required when writing quantized delta coordinates.</summary>
    public Proto.CoordinateTransform? CoordinateTransform { get; init; }
}
