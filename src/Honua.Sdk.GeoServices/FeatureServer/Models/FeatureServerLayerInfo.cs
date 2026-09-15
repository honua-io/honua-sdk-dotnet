// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.GeoServices.FeatureServer.Models;

/// <summary>
/// Detailed metadata for a single FeatureServer layer.
/// </summary>
public sealed class FeatureServerLayerInfo
{
    /// <summary>Layer ID.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>Layer name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Layer description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Geometry type (e.g., "esriGeometryPoint").</summary>
    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    /// <summary>The spatial reference of the layer.</summary>
    [JsonPropertyName("spatialReference")]
    public FeatureServerSpatialReference? SpatialReference { get; init; }

    /// <summary>The full extent of the layer.</summary>
    [JsonPropertyName("extent")]
    public FeatureServerExtent? Extent { get; init; }

    /// <summary>Field definitions for the layer.</summary>
    [JsonPropertyName("fields")]
    public IReadOnlyList<FeatureServerField>? Fields { get; init; }

    /// <summary>The name of the object ID field.</summary>
    [JsonPropertyName("objectIdField")]
    public string? ObjectIdField { get; init; }

    /// <summary>The name of the global ID field.</summary>
    [JsonPropertyName("globalIdField")]
    public string? GlobalIdField { get; init; }

    /// <summary>Maximum number of records returned per query.</summary>
    [JsonPropertyName("maxRecordCount")]
    public int MaxRecordCount { get; init; }

    /// <summary>Capabilities supported by the layer.</summary>
    [JsonPropertyName("capabilities")]
    public string? Capabilities { get; init; }

    /// <summary>Whether the layer advertises attachment support.</summary>
    [JsonPropertyName("hasAttachments")]
    public bool HasAttachments { get; init; }

    /// <summary>Whether the layer supports statistics.</summary>
    [JsonPropertyName("supportsStatistics")]
    public bool SupportsStatistics { get; init; }

    /// <summary>Whether the layer supports advanced queries.</summary>
    [JsonPropertyName("supportsAdvancedQueries")]
    public bool SupportsAdvancedQueries { get; init; }

    /// <summary>Time metadata advertised by the layer.</summary>
    [JsonPropertyName("timeInfo")]
    public FeatureServerTimeInfo? TimeInfo { get; init; }

    /// <summary>Layer type (e.g., "Feature Layer" or "Table").</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Whether the layer stores Z values; <see langword="null"/> when the source omits it.</summary>
    [JsonPropertyName("hasZ")]
    public bool? HasZ { get; init; }

    /// <summary>Whether the layer stores M values; <see langword="null"/> when the source omits it.</summary>
    [JsonPropertyName("hasM")]
    public bool? HasM { get; init; }

    /// <summary>Whether the layer honours <c>resultOffset</c>; <see langword="null"/> when the source omits it.</summary>
    [JsonPropertyName("supportsPagination")]
    public bool? SupportsPagination { get; init; }

    /// <summary>The provider-native advanced query capabilities.</summary>
    [JsonPropertyName("advancedQueryCapabilities")]
    public JsonElement? AdvancedQueryCapabilities { get; init; }

    /// <summary>The provider-native renderer, transparency and labeling metadata.</summary>
    [JsonPropertyName("drawingInfo")]
    public JsonElement? DrawingInfo { get; init; }

    /// <summary>The provider-native relationship classes, including key fields.</summary>
    [JsonPropertyName("relationships")]
    public JsonElement? Relationships { get; init; }

    /// <summary>The field that holds the subtype/type code.</summary>
    [JsonPropertyName("typeIdField")]
    public string? TypeIdField { get; init; }

    /// <summary>The provider-native feature types (legacy subtypes).</summary>
    [JsonPropertyName("types")]
    public JsonElement? Types { get; init; }

    /// <summary>The provider-native subtype definitions.</summary>
    [JsonPropertyName("subtypes")]
    public JsonElement? Subtypes { get; init; }

    /// <summary>
    /// Every source member not modelled by a typed property, preserved verbatim so metadata
    /// survives import without loss.
    /// </summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>
/// Time metadata advertised by a FeatureServer layer.
/// </summary>
public sealed class FeatureServerTimeInfo
{
    /// <summary>Start time field name.</summary>
    [JsonPropertyName("startTimeField")]
    public string? StartTimeField { get; init; }

    /// <summary>End time field name.</summary>
    [JsonPropertyName("endTimeField")]
    public string? EndTimeField { get; init; }

    /// <summary>Track ID field name.</summary>
    [JsonPropertyName("trackIdField")]
    public string? TrackIdField { get; init; }

    /// <summary>Provider time reference payload.</summary>
    [JsonPropertyName("timeReference")]
    public JsonElement? TimeReference { get; init; }

    /// <summary>The provider-native time extent (start and end epoch milliseconds).</summary>
    [JsonPropertyName("timeExtent")]
    public JsonElement? TimeExtent { get; init; }

    /// <summary>
    /// Every source member not modelled by a typed property, preserved verbatim so metadata
    /// survives import without loss.
    /// </summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json source generation requires a setter for JsonExtensionData.")]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}
