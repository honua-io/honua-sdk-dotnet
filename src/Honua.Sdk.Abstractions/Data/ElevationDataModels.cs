// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Abstractions.Data;

/// <summary>
/// Capabilities advertised by an elevation data provider.
/// </summary>
public sealed record ElevationDataCapabilities
{
    /// <summary>Whether point elevation sampling is supported.</summary>
    public bool SupportsPointSampling { get; init; }

    /// <summary>Whether multiple points can be sampled in one request.</summary>
    public bool SupportsBatchSampling { get; init; }

    /// <summary>Whether elevation profiles can be sampled along a line geometry.</summary>
    public bool SupportsProfileSampling { get; init; }

    /// <summary>Whether output units can be requested.</summary>
    public bool SupportsOutputUnits { get; init; }

    /// <summary>Whether vertical datum selection is supported.</summary>
    public bool SupportsVerticalDatum { get; init; }

    /// <summary>Whether sample resolution metadata can be returned.</summary>
    public bool SupportsResolutionMetadata { get; init; }

    /// <summary>Native provider surface backing the implementation.</summary>
    public string? NativeSurface { get; init; }

    /// <summary>Reason the capability set is unavailable, when applicable.</summary>
    public string? UnsupportedReason { get; init; }
}

/// <summary>
/// Elevation sampling request for points or provider-supported profile geometry.
/// </summary>
public sealed record ElevationSamplingRequest
{
    /// <summary>Provider-specific elevation source identifiers.</summary>
    public SpatialDataSource Source { get; init; } = new();

    /// <summary>Point samples to request.</summary>
    public IReadOnlyList<SpatialDataPoint> Points { get; init; } = [];

    /// <summary>Optional line geometry for profile sampling using provider JSON geometry.</summary>
    public JsonElement? PathGeometry { get; init; }

    /// <summary>Coordinate reference system for <see cref="PathGeometry"/> when not embedded in the geometry.</summary>
    public string? PathCrs { get; init; }

    /// <summary>Requested sample spacing for profile sampling.</summary>
    public double? SampleDistance { get; init; }

    /// <summary>Requested output elevation unit.</summary>
    public string? Unit { get; init; }

    /// <summary>Requested output vertical datum.</summary>
    public string? VerticalDatum { get; init; }

    /// <summary>Whether no-data samples should be included instead of filtered.</summary>
    public bool IncludeNoData { get; init; }

    /// <summary>Optional provider selector, such as a raster item filter or terrain version.</summary>
    public JsonElement? Selector { get; init; }

    /// <summary>Additional provider parameters that do not affect SDK display behavior.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Elevation sample returned by a provider.
/// </summary>
public sealed record ElevationSample
{
    /// <summary>Point associated with the sample.</summary>
    public required SpatialDataPoint Location { get; init; }

    /// <summary>Elevation value in <see cref="Unit"/>, or null for no-data samples.</summary>
    public double? Elevation { get; init; }

    /// <summary>Elevation unit for this sample.</summary>
    public string? Unit { get; init; }

    /// <summary>Vertical datum used by this sample.</summary>
    public string? VerticalDatum { get; init; }

    /// <summary>Distance along a sampled profile, when applicable.</summary>
    public double? DistanceAlong { get; init; }

    /// <summary>Resolution or cell size used to produce the sample.</summary>
    public double? Resolution { get; init; }

    /// <summary>Whether this sample represents provider no-data.</summary>
    public bool IsNoData { get; init; }

    /// <summary>Additional provider sample attributes.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Attributes { get; init; }
}

/// <summary>
/// Elevation sampling response.
/// </summary>
public sealed record ElevationSamplingResponse
{
    /// <summary>Source that produced the response.</summary>
    public SpatialDataSource Source { get; init; } = new();

    /// <summary>Returned elevation samples.</summary>
    public IReadOnlyList<ElevationSample> Samples { get; init; } = [];

    /// <summary>Provider messages.</summary>
    public IReadOnlyList<SpatialDataMessage> Messages { get; init; } = [];

    /// <summary>Raw provider response payload.</summary>
    public JsonElement? RawResponse { get; init; }

    /// <summary>Whether the response does not contain provider errors.</summary>
    public bool Succeeded => !Messages.Any(static message => message.Severity == SpatialDataMessageSeverity.Error);
}
