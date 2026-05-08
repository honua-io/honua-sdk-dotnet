// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Stac.Models;

/// <summary>
/// STAC landing page response.
/// </summary>
public sealed class StacLandingPage
{
    /// <summary>The STAC object type.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The STAC catalog identifier.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>The STAC version advertised by the catalog.</summary>
    [JsonPropertyName("stac_version")]
    public string? StacVersion { get; init; }

    /// <summary>STAC extension URIs advertised by the catalog.</summary>
    [JsonPropertyName("stac_extensions")]
    public IReadOnlyList<string>? StacExtensions { get; init; }

    /// <summary>Landing page title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Landing page description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Navigation links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<StacLink>? Links { get; init; }

    /// <summary>Conformance class URIs advertised on landing pages that include them.</summary>
    [JsonPropertyName("conformsTo")]
    public IReadOnlyList<string>? ConformsTo { get; init; }

    /// <summary>Additional server-specific landing-page properties.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
