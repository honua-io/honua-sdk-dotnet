// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Features.FeatureServer.Models;

/// <summary>
/// Service-level metadata from a FeatureServer endpoint.
/// </summary>
public sealed class FeatureServerServiceInfo
{
    /// <summary>Service description.</summary>
    [JsonPropertyName("serviceDescription")]
    public string? ServiceDescription { get; init; }

    /// <summary>Whether the service supports query operations.</summary>
    [JsonPropertyName("hasVersionedData")]
    public bool HasVersionedData { get; init; }

    /// <summary>Maximum number of records returned per query.</summary>
    [JsonPropertyName("maxRecordCount")]
    public int MaxRecordCount { get; init; }

    /// <summary>Supported query formats.</summary>
    [JsonPropertyName("supportedQueryFormats")]
    public string? SupportedQueryFormats { get; init; }

    /// <summary>Capabilities supported by the service.</summary>
    [JsonPropertyName("capabilities")]
    public string? Capabilities { get; init; }

    /// <summary>The spatial reference of the service.</summary>
    [JsonPropertyName("spatialReference")]
    public FeatureServerSpatialReference? SpatialReference { get; init; }

    /// <summary>Initial extent of the service.</summary>
    [JsonPropertyName("initialExtent")]
    public FeatureServerExtent? InitialExtent { get; init; }

    /// <summary>Full extent of the service.</summary>
    [JsonPropertyName("fullExtent")]
    public FeatureServerExtent? FullExtent { get; init; }

    /// <summary>Layers in the service.</summary>
    [JsonPropertyName("layers")]
    public IReadOnlyList<FeatureServerLayerSummary>? Layers { get; init; }
}

/// <summary>
/// Summary information about a layer in a FeatureServer service.
/// </summary>
public sealed class FeatureServerLayerSummary
{
    /// <summary>Layer ID.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>Layer name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
