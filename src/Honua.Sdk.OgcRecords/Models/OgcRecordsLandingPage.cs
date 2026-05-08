// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.OgcRecords.Models;

/// <summary>
/// OGC API Records landing page response.
/// </summary>
public sealed class OgcRecordsLandingPage
{
    /// <summary>Landing page title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Landing page description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Navigation links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcRecordsLink>? Links { get; init; }

    /// <summary>Additional server-specific landing-page properties.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
