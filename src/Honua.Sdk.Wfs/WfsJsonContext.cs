// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Wfs.Models;

namespace Honua.Sdk.Wfs;

/// <summary>
/// Source-generated JSON serializer context for AOT-safe GeoJSON deserialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WfsGeoJsonFeatureCollection))]
[JsonSerializable(typeof(WfsGeoJsonFeature))]
[JsonSerializable(typeof(GeoJsonGeometry))]
internal sealed partial class WfsJsonContext : JsonSerializerContext { }

/// <summary>
/// Internal wire model for GeoJSON FeatureCollection responses from WFS.
/// </summary>
internal sealed class WfsGeoJsonFeatureCollection
{
    public string? Type { get; set; }
    public JsonElement NumberMatched { get; set; }
    public int? NumberReturned { get; set; }
    public List<WfsGeoJsonFeature>? Features { get; set; }

    public WfsFeatureCollection ToPublicModel()
    {
        long? matched = NumberMatched.ValueKind switch
        {
            JsonValueKind.Number => NumberMatched.GetInt64(),
            JsonValueKind.String when NumberMatched.GetString() != "unknown" =>
                long.TryParse(NumberMatched.GetString(), out var v) ? v : null,
            _ => null,
        };

        var features = Features?.Select(f => f.ToPublicModel()).ToList()
            ?? new List<WfsFeature>();

        return new WfsFeatureCollection
        {
            Features = features,
            NumberMatched = matched,
            NumberReturned = NumberReturned ?? features.Count,
        };
    }
}

/// <summary>
/// Internal wire model for a single GeoJSON Feature from WFS.
/// </summary>
internal sealed class WfsGeoJsonFeature
{
    public string? Type { get; set; }
    public JsonElement Id { get; set; }
    public GeoJsonGeometry? Geometry { get; set; }
    public Dictionary<string, JsonElement>? Properties { get; set; }

    public WfsFeature ToPublicModel()
    {
        var id = Id.ValueKind switch
        {
            JsonValueKind.String => Id.GetString(),
            JsonValueKind.Number => Id.GetRawText(),
            _ => null,
        };

        return new WfsFeature
        {
            Id = id,
            Geometry = Geometry,
            Properties = Properties is not null
                ? new Dictionary<string, JsonElement>(Properties)
                : new Dictionary<string, JsonElement>(),
        };
    }
}
