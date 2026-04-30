// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Data;

/// <summary>
/// Severity for provider messages returned by spatial data services.
/// </summary>
public enum SpatialDataMessageSeverity
{
    /// <summary>Informational message.</summary>
    Information = 0,

    /// <summary>Warning that does not necessarily block the result.</summary>
    Warning = 1,

    /// <summary>Error reported by the provider.</summary>
    Error = 2
}

/// <summary>
/// Portable scalar value kind used by raster and enrichment metadata.
/// </summary>
public enum SpatialDataValueType
{
    /// <summary>Value type is unknown or provider-defined.</summary>
    Unknown = 0,

    /// <summary>Integral numeric value.</summary>
    IntegralNumber = 1,

    /// <summary>Floating-point numeric value.</summary>
    FloatingNumber = 2,

    /// <summary>Fixed-precision decimal value.</summary>
    FixedPrecisionNumber = 3,

    /// <summary>Boolean value.</summary>
    BooleanValue = 4,

    /// <summary>String value.</summary>
    Text = 5,

    /// <summary>Date or timestamp value.</summary>
    Timestamp = 6,

    /// <summary>Geometry value.</summary>
    Geometry = 7,

    /// <summary>Provider JSON object or array.</summary>
    Json = 8
}

/// <summary>
/// Statistic function requested from raster coverage or enrichment services.
/// </summary>
public enum SpatialDataStatisticType
{
    /// <summary>Statistic type is not specified.</summary>
    Unspecified = 0,

    /// <summary>Count of included cells or records.</summary>
    Count = 1,

    /// <summary>Count of excluded no-data cells.</summary>
    NoDataCount = 2,

    /// <summary>Sum of values.</summary>
    Sum = 3,

    /// <summary>Minimum value.</summary>
    Min = 4,

    /// <summary>Maximum value.</summary>
    Max = 5,

    /// <summary>Mean or average value.</summary>
    Mean = 6,

    /// <summary>Median value.</summary>
    Median = 7,

    /// <summary>Standard deviation.</summary>
    StandardDeviation = 8,

    /// <summary>Variance.</summary>
    Variance = 9,

    /// <summary>Requested percentile value.</summary>
    Percentile = 10,

    /// <summary>Area represented by included cells or features.</summary>
    Area = 11
}

/// <summary>
/// Provider-neutral source identifiers for raster, elevation, and enrichment data.
/// </summary>
public sealed record SpatialDataSource
{
    /// <summary>Honua service identifier or provider service id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Provider dataset identifier.</summary>
    public string? DatasetId { get; init; }

    /// <summary>Layer identifier when the provider exposes data through a layer endpoint.</summary>
    public int? LayerId { get; init; }

    /// <summary>Raster identifier when a service contains multiple rasters or mosaic items.</summary>
    public string? RasterId { get; init; }

    /// <summary>OGC collection identifier for coverage or enrichment services.</summary>
    public string? CollectionId { get; init; }

    /// <summary>Optional provider version, branch, or workspace name.</summary>
    public string? VersionName { get; init; }

    /// <summary>Optional feature source associated with the data service.</summary>
    public FeatureSource? FeatureSource { get; init; }
}

/// <summary>
/// Point coordinate used by data sampling requests.
/// </summary>
public sealed record SpatialDataPoint
{
    /// <summary>X coordinate, normally longitude or projected easting.</summary>
    public required double X { get; init; }

    /// <summary>Y coordinate, normally latitude or projected northing.</summary>
    public required double Y { get; init; }

    /// <summary>Optional Z coordinate supplied by the caller or provider.</summary>
    public double? Z { get; init; }

    /// <summary>Optional coordinate reference system identifier.</summary>
    public string? Crs { get; init; }

    /// <summary>Optional caller-stable point id for correlating batch responses.</summary>
    public string? PointId { get; init; }

    /// <summary>Additional provider-neutral point attributes.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Attributes { get; init; }

    /// <summary>
    /// Creates a point from longitude/latitude ordered values.
    /// </summary>
    /// <param name="longitude">Longitude in decimal degrees.</param>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <param name="pointId">Optional caller-stable point id.</param>
    /// <returns>A WGS84 point.</returns>
    public static SpatialDataPoint FromLongitudeLatitude(double longitude, double latitude, string? pointId = null) => new()
    {
        X = longitude,
        Y = latitude,
        Crs = "EPSG:4326",
        PointId = pointId,
    };

    /// <summary>
    /// Creates a point from latitude/longitude ordered values, which is common for GPS APIs.
    /// </summary>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <param name="longitude">Longitude in decimal degrees.</param>
    /// <param name="pointId">Optional caller-stable point id.</param>
    /// <returns>A WGS84 point.</returns>
    public static SpatialDataPoint FromLatitudeLongitude(double latitude, double longitude, string? pointId = null)
        => FromLongitudeLatitude(longitude, latitude, pointId);
}

/// <summary>
/// Provider message returned with spatial data responses.
/// </summary>
public sealed record SpatialDataMessage
{
    /// <summary>Message severity.</summary>
    public SpatialDataMessageSeverity Severity { get; init; }

    /// <summary>Provider or SDK message code.</summary>
    public string? Code { get; init; }

    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }

    /// <summary>Field, attribute, or request member associated with the message.</summary>
    public string? FieldName { get; init; }

    /// <summary>Optional suggested remediation text.</summary>
    public string? SuggestedFix { get; init; }
}
