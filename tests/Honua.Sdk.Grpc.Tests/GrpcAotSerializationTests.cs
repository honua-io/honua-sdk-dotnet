// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Grpc.Conversion;
using Honua.Sdk.Grpc.Models;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Grpc.Tests;

public sealed class GrpcAotSerializationTests
{
    [Fact]
    public void SerializeDictionaryToElement_WritesNestedGeoServicesGeometryAndSkipsNullsWhenRequested()
    {
        var geometry = new Dictionary<string, object?>
        {
            ["rings"] = new List<object?>
            {
                new double[][] { [0, 0], [10, 0], [10, 10], [0, 10], [0, 0] }
            },
            ["spatialReference"] = new Dictionary<string, object?> { ["wkid"] = 4326, ["wkt"] = null },
            ["unused"] = null
        };

        var element = GrpcJsonSerializer.SerializeDictionaryToElement(geometry, skipNullProperties: true);

        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.False(element.TryGetProperty("unused", out _));
        Assert.True(element.TryGetProperty("spatialReference", out var sr));
        Assert.Equal(4326, sr.GetProperty("wkid").GetInt32());
        Assert.False(sr.TryGetProperty("wkt", out _));
        Assert.Equal(1, element.GetProperty("rings").GetArrayLength());
    }

    [Fact]
    public void SerializeValueToElement_HandlesScalarTypesDatesAndByteArrays()
    {
        var guid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var dateOnly = new DateOnly(2026, 11, 26);

        Assert.Equal(JsonValueKind.Null, GrpcJsonSerializer.SerializeValueToElement(null).ValueKind);
        Assert.Equal("hello", GrpcJsonSerializer.SerializeValueToElement("hello").GetString());
        Assert.True(GrpcJsonSerializer.SerializeValueToElement(true).GetBoolean());
        Assert.Equal(42L, GrpcJsonSerializer.SerializeValueToElement(42L).GetInt64());
        Assert.Equal(3.5, GrpcJsonSerializer.SerializeValueToElement(3.5).GetDouble());
        Assert.Equal("2026-11-26", GrpcJsonSerializer.SerializeValueToElement(dateOnly).GetString());
        Assert.Equal(guid, GrpcJsonSerializer.SerializeValueToElement(guid).GetGuid());
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), GrpcJsonSerializer.SerializeValueToElement(new byte[] { 1, 2, 3 }).GetString());
    }

    [Fact]
    public void FeatureToGeometry_RoundTripsGeoServicesDictionaryWithoutReflection()
    {
        var feature = new Feature
        {
            Id = 99,
            Geometry = new Dictionary<string, object?>
            {
                ["x"] = -156.3319,
                ["y"] = 20.7984,
                ["spatialReference"] = new Dictionary<string, object?> { ["wkid"] = 4326 }
            }
        };

        var point = Assert.IsType<Point>(feature.ToGeometry());

        Assert.Equal(-156.3319, point.X, 4);
        Assert.Equal(20.7984, point.Y, 4);
        Assert.Equal(4326, point.SRID);
    }
}
