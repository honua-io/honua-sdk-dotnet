// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Catalogs.Serialization;

namespace Honua.Sdk.Catalogs.Stac.Models;

/// <summary>
/// Single STAC item. Items are GeoJSON features with STAC-specific collection,
/// asset, version, and extension members.
/// </summary>
public sealed class StacItem
{
    /// <summary>The GeoJSON type.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "Feature";

    /// <summary>The STAC item identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>The collection identifier that owns the item.</summary>
    [JsonPropertyName("collection")]
    public string? Collection { get; init; }

    /// <summary>The STAC version advertised by the item.</summary>
    [JsonPropertyName("stac_version")]
    public string? StacVersion { get; init; }

    /// <summary>STAC extension URIs advertised by the item.</summary>
    [JsonPropertyName("stac_extensions")]
    public IReadOnlyList<string>? StacExtensions { get; init; }

    /// <summary>The item geometry or footprint as raw GeoJSON.</summary>
    [JsonPropertyName("geometry")]
    public JsonElement? Geometry { get; init; }

    /// <summary>Item bounding box as [minLon, minLat, maxLon, maxLat], or 3D equivalent.</summary>
    [JsonPropertyName("bbox")]
    public IReadOnlyList<double>? Bbox { get; init; }

    /// <summary>Item properties as raw JSON elements for STAC extension flexibility.</summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, JsonElement>? Properties { get; init; }

    /// <summary>Navigation and related-resource links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<StacLink>? Links { get; init; }

    /// <summary>Assets associated with this item, keyed by asset role or name.</summary>
    [JsonPropertyName("assets")]
    public IReadOnlyDictionary<string, StacAsset>? Assets { get; init; }

    /// <summary>Additional server-specific or extension item members.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>
/// STAC asset metadata for a downloadable or previewable item resource.
/// </summary>
public sealed class StacAsset
{
    /// <summary>The asset URI.</summary>
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;

    /// <summary>The asset media type.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>A human-readable asset title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>A human-readable asset description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>STAC asset roles, such as data, thumbnail, overview, or metadata.</summary>
    [JsonPropertyName("roles")]
    public IReadOnlyList<string>? Roles { get; init; }

    /// <summary>Additional server-specific or extension asset members.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>
/// GeoJSON feature collection returned by STAC item and search endpoints.
/// </summary>
public sealed class StacItemCollection
{
    /// <summary>The GeoJSON type.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "FeatureCollection";

    /// <summary>The STAC items in this page.</summary>
    [JsonPropertyName("features")]
    public IReadOnlyList<StacItem>? Features { get; init; }

    /// <summary>Alias for <see cref="Features"/> using STAC item terminology.</summary>
    [JsonIgnore]
    public IReadOnlyList<StacItem>? Items => Features;

    /// <summary>Total number of items matching the query when reported by the server.</summary>
    [JsonPropertyName("numberMatched")]
    [JsonConverter(typeof(TolerantNullableInt64Converter))]
    public long? NumberMatched { get; init; }

    /// <summary>Number of items returned in this page.</summary>
    [JsonPropertyName("numberReturned")]
    public int? NumberReturned { get; init; }

    /// <summary>Navigation links including <c>next</c> for paging.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<StacLink>? Links { get; init; }

    /// <summary>STAC context metadata, when advertised by the server.</summary>
    [JsonPropertyName("context")]
    public Dictionary<string, JsonElement>? Context { get; init; }

    /// <summary>Additional server-specific or extension collection members.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
