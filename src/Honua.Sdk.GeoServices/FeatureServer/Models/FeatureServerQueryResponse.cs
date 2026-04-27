// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.GeoServices.FeatureServer.Models;

/// <summary>
/// Response from a FeatureServer query operation.
/// </summary>
public sealed class FeatureServerQueryResponse
{
    /// <summary>The object ID field name.</summary>
    [JsonPropertyName("objectIdFieldName")]
    public string? ObjectIdFieldName { get; init; }

    /// <summary>The spatial reference of the result.</summary>
    [JsonPropertyName("spatialReference")]
    public FeatureServerSpatialReference? SpatialReference { get; init; }

    /// <summary>Field definitions for the returned features.</summary>
    [JsonPropertyName("fields")]
    public IReadOnlyList<FeatureServerField>? Fields { get; init; }

    /// <summary>The returned features.</summary>
    [JsonPropertyName("features")]
    public IReadOnlyList<FeatureServerFeature>? Features { get; init; }

    /// <summary>Whether the server has more records than returned.</summary>
    [JsonPropertyName("exceededTransferLimit")]
    public bool ExceededTransferLimit { get; init; }

    /// <summary>Total count of matching features (when returnCountOnly is true).</summary>
    [JsonPropertyName("count")]
    public long? Count { get; init; }

    /// <summary>Array of object IDs (when returnIdsOnly is true).</summary>
    [JsonPropertyName("objectIds")]
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>Extent of matching features (when returnExtentOnly is true).</summary>
    [JsonPropertyName("extent")]
    public FeatureServerExtent? Extent { get; init; }
}
