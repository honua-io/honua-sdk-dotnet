// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;
using Honua.Sdk.OgcFeatures.Conversion;

namespace Honua.Sdk.OgcFeatures.Models;

/// <summary>
/// A GeoJSON FeatureCollection from an OGC API Features items response.
/// </summary>
public sealed class OgcFeatureCollection
{
    /// <summary>The GeoJSON type (always "FeatureCollection").</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "FeatureCollection";

    /// <summary>The features in this page.</summary>
    [JsonPropertyName("features")]
    public IReadOnlyList<OgcFeature>? Features { get; init; }

    /// <summary>Total number of features matching the query (server-reported).</summary>
    [JsonPropertyName("numberMatched")]
    [JsonConverter(typeof(TolerantNullableInt64Converter))]
    public long? NumberMatched { get; init; }

    /// <summary>Number of features returned in this page.</summary>
    [JsonPropertyName("numberReturned")]
    public int? NumberReturned { get; init; }

    /// <summary>Navigation links including "next" for paging.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcLink>? Links { get; init; }
}
