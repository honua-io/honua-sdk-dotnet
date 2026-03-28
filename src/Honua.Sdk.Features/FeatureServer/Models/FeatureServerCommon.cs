// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Features.FeatureServer.Models;

/// <summary>
/// A spatial reference definition.
/// </summary>
public sealed class FeatureServerSpatialReference
{
    /// <summary>Well-known ID of the spatial reference.</summary>
    [JsonPropertyName("wkid")]
    public int Wkid { get; init; }

    /// <summary>Latest well-known ID of the spatial reference.</summary>
    [JsonPropertyName("latestWkid")]
    public int LatestWkid { get; init; }
}

/// <summary>
/// A spatial extent envelope.
/// </summary>
public sealed class FeatureServerExtent
{
    /// <summary>Minimum X coordinate.</summary>
    [JsonPropertyName("xmin")]
    public double Xmin { get; init; }

    /// <summary>Minimum Y coordinate.</summary>
    [JsonPropertyName("ymin")]
    public double Ymin { get; init; }

    /// <summary>Maximum X coordinate.</summary>
    [JsonPropertyName("xmax")]
    public double Xmax { get; init; }

    /// <summary>Maximum Y coordinate.</summary>
    [JsonPropertyName("ymax")]
    public double Ymax { get; init; }

    /// <summary>Spatial reference of the extent.</summary>
    [JsonPropertyName("spatialReference")]
    public FeatureServerSpatialReference? SpatialReference { get; init; }
}

/// <summary>
/// Response from the validateSQL endpoint.
/// </summary>
public sealed class FeatureServerValidateSqlResponse
{
    /// <summary>Whether the SQL expression is valid.</summary>
    [JsonPropertyName("isValidSQL")]
    public bool IsValidSql { get; init; }

    /// <summary>Validation message describing any issues.</summary>
    [JsonPropertyName("validationMessage")]
    public string? ValidationMessage { get; init; }
}

/// <summary>
/// GeoServices error payload embedded in 200 responses.
/// </summary>
internal sealed class GeoServicesError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("details")]
    public List<string>? Details { get; set; }
}

/// <summary>
/// Wrapper for GeoServices error responses.
/// </summary>
internal sealed class GeoServicesErrorResponse
{
    [JsonPropertyName("error")]
    public GeoServicesError? Error { get; set; }
}

/// <summary>
/// Spatial relationship types for spatial queries.
/// </summary>
public enum SpatialRelationship
{
    /// <summary>Intersects the geometry.</summary>
    Intersects,

    /// <summary>Contains the geometry.</summary>
    Contains,

    /// <summary>Crosses the geometry.</summary>
    Crosses,

    /// <summary>Envelope intersects the geometry.</summary>
    EnvelopeIntersects,

    /// <summary>Index intersects the geometry.</summary>
    IndexIntersects,

    /// <summary>Overlaps the geometry.</summary>
    Overlaps,

    /// <summary>Touches the geometry.</summary>
    Touches,

    /// <summary>Is within the geometry.</summary>
    Within
}

/// <summary>
/// Output format for FeatureServer queries.
/// </summary>
public enum FeatureServerFormat
{
    /// <summary>Esri JSON (default).</summary>
    Json,

    /// <summary>GeoJSON output.</summary>
    GeoJson
}

/// <summary>
/// Time relationship for temporal queries.
/// </summary>
public enum TimeRelation
{
    /// <summary>Overlaps the time extent.</summary>
    Overlaps,

    /// <summary>Is after the start of the time extent.</summary>
    AfterStartWithinEnd,

    /// <summary>Is within the time extent.</summary>
    Within
}
