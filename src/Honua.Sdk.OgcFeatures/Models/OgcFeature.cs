// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.OgcFeatures.Models;

/// <summary>
/// A single GeoJSON feature from an OGC API Features response.
/// Properties are represented as <see cref="JsonElement"/> for AOT safety.
/// Access values via <c>feature.Properties["name"].GetString()</c> etc.
/// </summary>
public sealed class OgcFeature
{
    /// <summary>The GeoJSON type (always "Feature").</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "Feature";

    /// <summary>The feature identifier.</summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; init; }

    /// <summary>Feature geometry as a raw JSON element (GeoJSON format).</summary>
    [JsonPropertyName("geometry")]
    public JsonElement? Geometry { get; init; }

    /// <summary>Feature properties as a dictionary of JSON elements.</summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, JsonElement>? Properties { get; init; }

    /// <summary>Navigation links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcLink>? Links { get; init; }
}
