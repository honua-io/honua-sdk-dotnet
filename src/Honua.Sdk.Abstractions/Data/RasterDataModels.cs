// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Data;

/// <summary>
/// Portable raster pixel storage type.
/// </summary>
public enum RasterPixelType
{
    /// <summary>Pixel type is unknown or provider-defined.</summary>
    Unknown = 0,

    /// <summary>Unsigned 8-bit integer.</summary>
    UnsignedByte = 1,

    /// <summary>Signed 8-bit integer.</summary>
    SignedByte = 2,

    /// <summary>Unsigned 16-bit integer.</summary>
    UnsignedShort = 3,

    /// <summary>Signed 16-bit integer.</summary>
    SignedShort = 4,

    /// <summary>Unsigned 32-bit integer.</summary>
    UnsignedInteger = 5,

    /// <summary>Signed 32-bit integer.</summary>
    SignedInteger = 6,

    /// <summary>32-bit floating-point value.</summary>
    SinglePrecision = 7,

    /// <summary>64-bit floating-point value.</summary>
    DoublePrecision = 8,

    /// <summary>64-bit complex value.</summary>
    ComplexSinglePrecision = 9,

    /// <summary>128-bit complex value.</summary>
    ComplexDoublePrecision = 10
}

/// <summary>
/// Raster sampling or aggregation resampling method.
/// </summary>
public enum RasterResamplingMethod
{
    /// <summary>Use the provider default.</summary>
    ProviderDefault = 0,

    /// <summary>Nearest-neighbor sampling.</summary>
    Nearest = 1,

    /// <summary>Bilinear interpolation.</summary>
    Bilinear = 2,

    /// <summary>Cubic interpolation.</summary>
    Cubic = 3,

    /// <summary>Majority value for categorical rasters.</summary>
    Majority = 4,

    /// <summary>Average of contributing cells.</summary>
    Average = 5
}

/// <summary>
/// Capabilities advertised by a raster data provider.
/// </summary>
public sealed record RasterDataCapabilities
{
    /// <summary>Whether raster dataset metadata can be discovered.</summary>
    public bool SupportsMetadata { get; init; }

    /// <summary>Whether band-level metadata can be returned.</summary>
    public bool SupportsBandMetadata { get; init; }

    /// <summary>Whether raster coverage statistics can be computed.</summary>
    public bool SupportsCoverageStatistics { get; init; }

    /// <summary>Whether histogram metadata can be returned.</summary>
    public bool SupportsHistograms { get; init; }

    /// <summary>Whether provider mosaic rules or raster item filters can be supplied.</summary>
    public bool SupportsMosaicRules { get; init; }

    /// <summary>Whether temporal slices can be requested.</summary>
    public bool SupportsTimeSlices { get; init; }

    /// <summary>Whether no-data masks or no-data counts can be returned.</summary>
    public bool SupportsNoDataMasks { get; init; }

    /// <summary>Native provider surface backing the implementation.</summary>
    public string? NativeSurface { get; init; }

    /// <summary>Reason the capability set is unavailable, when applicable.</summary>
    public string? UnsupportedReason { get; init; }
}

/// <summary>
/// Request for raster dataset metadata.
/// </summary>
public sealed record RasterMetadataRequest
{
    /// <summary>Provider-specific raster source identifiers.</summary>
    public SpatialDataSource Source { get; init; } = new();

    /// <summary>Whether band metadata should be included when the provider supports it.</summary>
    public bool IncludeBands { get; init; } = true;

    /// <summary>Whether statistics should be included when the provider supports them.</summary>
    public bool IncludeStatistics { get; init; }

    /// <summary>Whether histograms should be included when the provider supports them.</summary>
    public bool IncludeHistograms { get; init; }

    /// <summary>Optional provider mosaic rule, raster item filter, or version selector.</summary>
    public JsonElement? Selector { get; init; }

    /// <summary>Additional provider parameters that do not affect SDK display behavior.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Metadata for a raster band.
/// </summary>
public sealed record RasterBandMetadata
{
    /// <summary>Zero- or one-based band index as advertised by the provider.</summary>
    public required int BandIndex { get; init; }

    /// <summary>Band display or lookup name.</summary>
    public string? Name { get; init; }

    /// <summary>Band description.</summary>
    public string? Description { get; init; }

    /// <summary>Portable pixel type, when known.</summary>
    public RasterPixelType PixelType { get; init; } = RasterPixelType.Unknown;

    /// <summary>Provider-native data type string.</summary>
    public string? DataType { get; init; }

    /// <summary>Measurement unit for band values.</summary>
    public string? Unit { get; init; }

    /// <summary>No-data value for this band, when a scalar no-data value is available.</summary>
    public double? NoDataValue { get; init; }

    /// <summary>Minimum advertised value.</summary>
    public double? Minimum { get; init; }

    /// <summary>Maximum advertised value.</summary>
    public double? Maximum { get; init; }

    /// <summary>Mean advertised value.</summary>
    public double? Mean { get; init; }

    /// <summary>Standard deviation advertised by the provider.</summary>
    public double? StandardDeviation { get; init; }

    /// <summary>Additional band attributes.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Attributes { get; init; }
}

/// <summary>
/// Raster dataset metadata returned by a provider.
/// </summary>
public sealed record RasterDatasetMetadata
{
    /// <summary>Source that produced the metadata.</summary>
    public SpatialDataSource Source { get; init; } = new();

    /// <summary>Stable dataset identifier.</summary>
    public required string DatasetId { get; init; }

    /// <summary>Dataset display or lookup name.</summary>
    public string? Name { get; init; }

    /// <summary>Dataset description.</summary>
    public string? Description { get; init; }

    /// <summary>Dataset coordinate reference system identifier.</summary>
    public string? SpatialReference { get; init; }

    /// <summary>Dataset extent, when advertised.</summary>
    public FeatureBoundingBox? Extent { get; init; }

    /// <summary>Cell width in dataset units.</summary>
    public double? CellSizeX { get; init; }

    /// <summary>Cell height in dataset units.</summary>
    public double? CellSizeY { get; init; }

    /// <summary>Raster width in cells.</summary>
    public int? Width { get; init; }

    /// <summary>Raster height in cells.</summary>
    public int? Height { get; init; }

    /// <summary>Dataset pixel type, when a single type applies.</summary>
    public RasterPixelType PixelType { get; init; } = RasterPixelType.Unknown;

    /// <summary>Advertised band metadata.</summary>
    public IReadOnlyList<RasterBandMetadata> Bands { get; init; } = [];

    /// <summary>Provider capabilities or named operations available for this dataset.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Raw provider metadata payload.</summary>
    public JsonElement? RawMetadata { get; init; }
}

/// <summary>
/// Request for raster coverage statistics.
/// </summary>
public sealed record RasterCoverageStatisticsRequest
{
    /// <summary>Provider-specific raster source identifiers.</summary>
    public SpatialDataSource Source { get; init; } = new();

    /// <summary>Band indexes to include. Empty or null means provider default.</summary>
    public IReadOnlyList<int>? BandIndexes { get; init; }

    /// <summary>Bounding extent for statistics.</summary>
    public FeatureBoundingBox? Extent { get; init; }

    /// <summary>Optional area-of-interest geometry filter.</summary>
    public FeatureSpatialFilter? AreaOfInterest { get; init; }

    /// <summary>Provider or CQL filter applied before statistics are computed.</summary>
    public string? Filter { get; init; }

    /// <summary>Filter language for <see cref="Filter"/>.</summary>
    public FeatureFilterLanguage FilterLanguage { get; init; } = FeatureFilterLanguage.ProviderDefault;

    /// <summary>Requested statistic functions. Empty or null means provider default.</summary>
    public IReadOnlyList<SpatialDataStatisticType>? StatisticTypes { get; init; }

    /// <summary>Percentile values to compute, expressed in the range 0-100.</summary>
    public IReadOnlyList<double>? Percentiles { get; init; }

    /// <summary>Requested output cell size or aggregation resolution.</summary>
    public double? CellSize { get; init; }

    /// <summary>Resampling method for providers that aggregate or sample cells.</summary>
    public RasterResamplingMethod ResamplingMethod { get; init; } = RasterResamplingMethod.ProviderDefault;

    /// <summary>Optional provider mosaic rule, raster item filter, or version selector.</summary>
    public JsonElement? Selector { get; init; }

    /// <summary>Additional provider parameters that do not affect SDK display behavior.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Statistics for one raster band over a requested coverage area.
/// </summary>
public sealed record RasterBandStatistics
{
    /// <summary>Band index associated with these statistics.</summary>
    public required int BandIndex { get; init; }

    /// <summary>Count of included cells.</summary>
    public long? Count { get; init; }

    /// <summary>Count of no-data cells.</summary>
    public long? NoDataCount { get; init; }

    /// <summary>Minimum value.</summary>
    public double? Minimum { get; init; }

    /// <summary>Maximum value.</summary>
    public double? Maximum { get; init; }

    /// <summary>Mean value.</summary>
    public double? Mean { get; init; }

    /// <summary>Median value.</summary>
    public double? Median { get; init; }

    /// <summary>Standard deviation.</summary>
    public double? StandardDeviation { get; init; }

    /// <summary>Variance.</summary>
    public double? Variance { get; init; }

    /// <summary>Sum of values.</summary>
    public double? Sum { get; init; }

    /// <summary>Percentile values keyed by requested percentile label.</summary>
    public IReadOnlyDictionary<string, double>? Percentiles { get; init; }

    /// <summary>Additional provider statistics.</summary>
    public IReadOnlyDictionary<string, JsonElement>? AdditionalStatistics { get; init; }
}

/// <summary>
/// Raster coverage statistics response.
/// </summary>
public sealed record RasterCoverageStatisticsResponse
{
    /// <summary>Source that produced the response.</summary>
    public SpatialDataSource Source { get; init; } = new();

    /// <summary>Statistics grouped by band.</summary>
    public IReadOnlyList<RasterBandStatistics> Bands { get; init; } = [];

    /// <summary>Extent used by the provider for the statistics.</summary>
    public FeatureBoundingBox? Extent { get; init; }

    /// <summary>Cell size or aggregation resolution used by the provider.</summary>
    public double? CellSize { get; init; }

    /// <summary>Measurement unit for returned values when one unit applies.</summary>
    public string? Unit { get; init; }

    /// <summary>Provider messages.</summary>
    public IReadOnlyList<SpatialDataMessage> Messages { get; init; } = [];

    /// <summary>Raw provider response payload.</summary>
    public JsonElement? RawResponse { get; init; }

    /// <summary>Whether the response does not contain provider errors.</summary>
    public bool Succeeded => !Messages.Any(static message => message.Severity == SpatialDataMessageSeverity.Error);
}
