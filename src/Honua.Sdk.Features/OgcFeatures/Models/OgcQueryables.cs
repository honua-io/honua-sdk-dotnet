// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Features.OgcFeatures.Models;

/// <summary>
/// Queryable properties for an OGC collection (JSON Schema object).
/// </summary>
public sealed class OgcQueryables
{
    /// <summary>The JSON Schema type (typically "object").</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Title of the queryables schema.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Description of the queryables schema.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Property definitions as a JSON Schema properties object.</summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, JsonElement>? Properties { get; init; }

    /// <summary>Required property names.</summary>
    [JsonPropertyName("required")]
    public IReadOnlyList<string>? Required { get; init; }
}
