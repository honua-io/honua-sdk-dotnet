// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Catalogs.RecordsTests;

public sealed class TolerantNumberMatchedTests
{
    [Fact]
    public void Deserialize_NumberMatchedUnknown_ReturnsNull()
    {
        const string json = """
            { "type": "FeatureCollection", "numberMatched": "unknown", "numberReturned": 0, "features": [] }
            """;

        var collection = JsonSerializer.Deserialize(json, OgcRecordsJsonContext.Default.OgcRecordCollection);

        Assert.NotNull(collection);
        Assert.Null(collection!.NumberMatched);
    }

    [Fact]
    public void Deserialize_NumberMatchedNumeric_ReturnsValue()
    {
        const string json = """
            { "type": "FeatureCollection", "numberMatched": 13, "numberReturned": 1, "features": [] }
            """;

        var collection = JsonSerializer.Deserialize(json, OgcRecordsJsonContext.Default.OgcRecordCollection);

        Assert.NotNull(collection);
        Assert.Equal(13, collection!.NumberMatched);
    }
}
