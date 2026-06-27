// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Abstractions.Serialization;

namespace Honua.Sdk.Catalogs.Records.Models;

/// <summary>
/// A GeoJSON-compatible metadata record returned by OGC API Records.
/// </summary>
public sealed class OgcRecord
{
    /// <summary>The GeoJSON type.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "Feature";

    /// <summary>The record identifier.</summary>
    [JsonPropertyName("id")]
    public JsonElement? Id { get; init; }

    /// <summary>The record geometry or footprint as raw GeoJSON.</summary>
    [JsonPropertyName("geometry")]
    public JsonElement? Geometry { get; init; }

    /// <summary>Record properties as raw JSON elements for profile flexibility.</summary>
    [JsonPropertyName("properties")]
    public Dictionary<string, JsonElement>? Properties { get; init; }

    /// <summary>Navigation and related-resource links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcRecordsLink>? Links { get; init; }

    /// <summary>Additional server-specific record members.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>
/// A GeoJSON-compatible Records feature collection.
/// </summary>
public sealed class OgcRecordCollection
{
    /// <summary>The GeoJSON type.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "FeatureCollection";

    /// <summary>The records in this page.</summary>
    [JsonPropertyName("features")]
    public IReadOnlyList<OgcRecord>? Records { get; init; }

    /// <summary>Total number of records matching the query when reported by the server.</summary>
    [JsonPropertyName("numberMatched")]
    [JsonConverter(typeof(TolerantNullableInt64Converter))]
    public long? NumberMatched { get; init; }

    /// <summary>Number of records returned in this page.</summary>
    [JsonPropertyName("numberReturned")]
    public int? NumberReturned { get; init; }

    /// <summary>Navigation links including <c>next</c> for paging.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcRecordsLink>? Links { get; init; }

    /// <summary>Additional server-specific collection members.</summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
