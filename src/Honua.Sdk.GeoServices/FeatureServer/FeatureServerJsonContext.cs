// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.GeoServices.FeatureServer.Models;

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>
/// Source-generated JSON serializer context for FeatureServer types (AOT-compatible).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(FeatureServerServiceInfo))]
[JsonSerializable(typeof(FeatureServerLayerInfo))]
[JsonSerializable(typeof(FeatureServerQueryResponse))]
[JsonSerializable(typeof(FeatureServerEditResponse))]
[JsonSerializable(typeof(FeatureServerAttachmentQueryResponse))]
[JsonSerializable(typeof(FeatureServerAttachmentEditResponse))]
[JsonSerializable(typeof(FeatureServerFeature[]))]
[JsonSerializable(typeof(FeatureServerStatisticDefinition[]))]
[JsonSerializable(typeof(FeatureServerValidateSqlResponse))]
[JsonSerializable(typeof(GeoServicesErrorResponse))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(FeatureServerEnvelope))]
internal sealed partial class FeatureServerJsonContext : JsonSerializerContext
{
}

internal sealed class FeatureServerEnvelope
{
    [JsonPropertyName("xmin")]
    public double MinX { get; init; }

    [JsonPropertyName("ymin")]
    public double MinY { get; init; }

    [JsonPropertyName("xmax")]
    public double MaxX { get; init; }

    [JsonPropertyName("ymax")]
    public double MaxY { get; init; }
}
