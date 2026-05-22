// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Catalogs.Stac.Models;

/// <summary>
/// Metadata for a STAC collection.
/// </summary>
public sealed class StacCollection
{
    /// <summary>The STAC object type.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>The STAC version advertised by the collection.</summary>
    [JsonPropertyName("stac_version")]
    public string? StacVersion { get; init; }

    /// <summary>STAC extension URIs advertised by the collection.</summary>
    [JsonPropertyName("stac_extensions")]
    public IReadOnlyList<string>? StacExtensions { get; init; }

    /// <summary>Collection identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Collection title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Collection description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Collection license expression or identifier.</summary>
    [JsonPropertyName("license")]
    public string? License { get; init; }

    /// <summary>Collection keywords.</summary>
    [JsonPropertyName("keywords")]
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>Spatial and temporal collection extent.</summary>
    [JsonPropertyName("extent")]
    public StacExtent? Extent { get; init; }

    /// <summary>Navigation links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<StacLink>? Links { get; init; }

    /// <summary>Collection assets, when advertised.</summary>
    [JsonPropertyName("assets")]
    public IReadOnlyDictionary<string, StacAsset>? Assets { get; init; }

    /// <summary>Item asset definitions, when advertised by the STAC Item Assets extension.</summary>
    [JsonPropertyName("item_assets")]
    public IReadOnlyDictionary<string, StacAsset>? ItemAssets { get; init; }

    /// <summary>STAC summaries object preserved as raw JSON values.</summary>
    [JsonPropertyName("summaries")]
    public Dictionary<string, JsonElement>? Summaries { get; init; }

    /// <summary>Provider entries preserved as raw JSON for STAC extension flexibility.</summary>
    [JsonPropertyName("providers")]
    public IReadOnlyList<JsonElement>? Providers { get; init; }

    /// <summary>Additional server-specific collection properties.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>
/// STAC collections endpoint response.
/// </summary>
internal sealed class StacCollectionsResponse
{
    [JsonPropertyName("collections")]
    public List<StacCollection>? Collections { get; set; }

    [JsonPropertyName("links")]
    public IReadOnlyList<StacLink>? Links { get; set; }
}
