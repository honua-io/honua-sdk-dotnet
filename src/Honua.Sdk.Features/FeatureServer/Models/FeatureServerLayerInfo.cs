// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Features.FeatureServer.Models;

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

    /// <summary>Whether the layer supports statistics.</summary>
    [JsonPropertyName("supportsStatistics")]
    public bool SupportsStatistics { get; init; }

    /// <summary>Whether the layer supports advanced queries.</summary>
    [JsonPropertyName("supportsAdvancedQueries")]
    public bool SupportsAdvancedQueries { get; init; }
}
