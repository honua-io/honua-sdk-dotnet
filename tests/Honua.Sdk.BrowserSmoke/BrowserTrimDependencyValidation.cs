// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;

namespace Honua.Sdk.BrowserSmoke;

internal static class BrowserTrimDependencyValidation
{
    private const string ExpectedFeatureName = "trimmed-browser-smoke";
    private static readonly BrowserTrimJsonContext JsonContext = CreateJsonContext();

    public static Point RoundTripGeoJsonFeature(Point point)
    {
        var attributes = new AttributesTable();
        attributes.Add("name", ExpectedFeatureName);
        var featureCollection = new FeatureCollection
        {
            new Feature(point, attributes),
        };

        var json = JsonSerializer.Serialize(featureCollection, JsonContext.FeatureCollection);
        var roundTrip = JsonSerializer.Deserialize(json, JsonContext.FeatureCollection)
            ?? throw new InvalidOperationException("NTS GeoJSON feature collection deserialized as null.");
        if (roundTrip.Count != 1 || roundTrip[0].Geometry is not Point roundTrippedPoint)
        {
            throw new InvalidOperationException("NTS GeoJSON feature collection did not preserve its point feature.");
        }
        if (roundTrip[0].Attributes is not JsonElementAttributesTable roundTrippedAttributes ||
            !roundTrippedAttributes.TryGetJsonObjectPropertyValue<string>(
                "name",
                JsonContext.Options,
                out var roundTrippedName) ||
            !string.Equals(roundTrippedName, ExpectedFeatureName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("NTS GeoJSON feature collection did not preserve its name attribute.");
        }

        return roundTrippedPoint;
    }

    private static BrowserTrimJsonContext CreateJsonContext()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new GeoJsonConverterFactory());
        return new BrowserTrimJsonContext(options);
    }
}

[JsonSerializable(typeof(FeatureCollection))]
internal sealed partial class BrowserTrimJsonContext : JsonSerializerContext
{
}
