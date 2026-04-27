// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.GeoServices.FeatureServer.Models;

/// <summary>
/// A single feature from a FeatureServer query response.
/// Attributes are represented as <see cref="JsonElement"/> for AOT safety.
/// Access values via <c>feature.Attributes["name"].GetString()</c> etc.
/// </summary>
public sealed class FeatureServerFeature
{
    /// <summary>Feature attributes as a dictionary of JSON elements.</summary>
    [JsonPropertyName("attributes")]
    public Dictionary<string, JsonElement>? Attributes { get; init; }

    /// <summary>Feature geometry as a raw JSON element (Esri JSON format).</summary>
    [JsonPropertyName("geometry")]
    public JsonElement? Geometry { get; init; }
}
