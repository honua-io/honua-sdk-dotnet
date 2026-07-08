// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Sdk.Geometry;
using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Tests;

public class GeoJsonGeometryConverterTests
{
    [Fact]
    public void ReadGeometry_ParsesPoint()
    {
        using var document = JsonDocument.Parse(
            """{"type":"Point","coordinates":[-157.8583,21.3069]}""");

        var geometry = GeoJsonGeometryConverter.ReadGeometry(document.RootElement);

        var point = Assert.IsType<Point>(geometry);
        Assert.Equal(-157.8583, point.X, precision: 4);
        Assert.Equal(21.3069, point.Y, precision: 4);
    }

    [Fact]
    public void ReadGeometry_CachesSerializerOptionsForGeometryFactoryReference()
    {
        var cacheField = typeof(GeoJsonGeometryConverter).GetField(
            "SerializerOptionsByGeometryFactory",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(cacheField);
        var cache = Assert.IsType<ConditionalWeakTable<GeometryFactory, JsonSerializerOptions>>(cacheField.GetValue(null));
        var factory = new GeometryFactory(new PrecisionModel(), 3857);

        Assert.False(cache.TryGetValue(factory, out _));

        var firstGeometry = GeoJsonGeometryConverter.ReadGeometry(
            """{"type":"Point","coordinates":[1,2]}""",
            factory);
        Assert.Equal(3857, firstGeometry.SRID);
        Assert.True(cache.TryGetValue(factory, out var firstOptions));

        _ = GeoJsonGeometryConverter.ReadGeometry(
            """{"type":"Point","coordinates":[3,4]}""",
            factory);

        Assert.True(cache.TryGetValue(factory, out var secondOptions));
        Assert.Same(firstOptions, secondOptions);
    }

    [Fact]
    public void WriteGeometry_WritesPolygon()
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var polygon = factory.CreatePolygon(
            [
                new Coordinate(-158, 21),
                new Coordinate(-157, 21),
                new Coordinate(-157, 22),
                new Coordinate(-158, 21),
            ]);

        var json = GeoJsonGeometryConverter.WriteGeometry(polygon);

        Assert.Equal("Polygon", json.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Array, json.GetProperty("coordinates").ValueKind);
    }
}
