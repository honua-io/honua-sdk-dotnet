// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.OgcFeatures;

namespace Honua.Sdk.OgcFeatures.Tests.Models;

public sealed class TolerantNumberMatchedTests
{
    [Fact]
    public void Deserialize_NumberMatchedUnknown_ReturnsNull()
    {
        const string json = """
            { "type": "FeatureCollection", "numberMatched": "unknown", "numberReturned": 0, "features": [] }
            """;

        var collection = JsonSerializer.Deserialize(json, OgcFeaturesJsonContext.Default.OgcFeatureCollection);

        Assert.NotNull(collection);
        Assert.Null(collection!.NumberMatched);
        Assert.Equal(0, collection.NumberReturned);
    }

    [Fact]
    public void Deserialize_NumberMatchedNumeric_ReturnsValue()
    {
        const string json = """
            { "type": "FeatureCollection", "numberMatched": 42, "numberReturned": 1, "features": [] }
            """;

        var collection = JsonSerializer.Deserialize(json, OgcFeaturesJsonContext.Default.OgcFeatureCollection);

        Assert.NotNull(collection);
        Assert.Equal(42, collection!.NumberMatched);
    }

    [Fact]
    public void Deserialize_NumberMatchedNumericString_ReturnsValue()
    {
        const string json = """
            { "type": "FeatureCollection", "numberMatched": "42", "numberReturned": 1, "features": [] }
            """;

        var collection = JsonSerializer.Deserialize(json, OgcFeaturesJsonContext.Default.OgcFeatureCollection);

        Assert.NotNull(collection);
        Assert.Equal(42, collection!.NumberMatched);
    }
}
