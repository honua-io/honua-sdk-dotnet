// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Catalogs.Records.Models;

/// <summary>
/// Metadata for an OGC API Records collection.
/// </summary>
public sealed class OgcRecordsCollection
{
    /// <summary>Collection identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Collection title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Collection description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Type of items exposed by the collection, when advertised.</summary>
    [JsonPropertyName("itemType")]
    public string? ItemType { get; init; }

    /// <summary>Record type or profile advertised by the collection, when available.</summary>
    [JsonPropertyName("recordType")]
    public string? RecordType { get; init; }

    /// <summary>Spatial and temporal collection extent.</summary>
    [JsonPropertyName("extent")]
    public OgcRecordsExtent? Extent { get; init; }

    /// <summary>Navigation links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcRecordsLink>? Links { get; init; }

    /// <summary>Additional server-specific collection properties.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>
/// Collections endpoint response.
/// </summary>
internal sealed class OgcRecordsCollectionsResponse
{
    [JsonPropertyName("collections")]
    public List<OgcRecordsCollection>? Collections { get; set; }

    [JsonPropertyName("links")]
    public IReadOnlyList<OgcRecordsLink>? Links { get; set; }
}
