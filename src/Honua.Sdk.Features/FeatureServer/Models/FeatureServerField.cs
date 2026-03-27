// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Features.FeatureServer.Models;

/// <summary>
/// A field definition from a FeatureServer layer.
/// </summary>
public sealed class FeatureServerField
{
    /// <summary>The field name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>The field type (e.g., "esriFieldTypeOID", "esriFieldTypeString").</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>The field alias (display name).</summary>
    [JsonPropertyName("alias")]
    public string? Alias { get; init; }

    /// <summary>Whether the field is nullable.</summary>
    [JsonPropertyName("nullable")]
    public bool Nullable { get; init; }

    /// <summary>Maximum length for string fields.</summary>
    [JsonPropertyName("length")]
    public int? Length { get; init; }

    /// <summary>Whether the field is editable.</summary>
    [JsonPropertyName("editable")]
    public bool Editable { get; init; }

    /// <summary>The default value for the field.</summary>
    [JsonPropertyName("defaultValue")]
    public JsonElement? DefaultValue { get; init; }
}
