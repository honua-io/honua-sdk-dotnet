// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Data;

/// <summary>
/// Provider-neutral raster data client contract.
/// </summary>
public interface IHonuaRasterDataClient
{
    /// <summary>Stable provider name for diagnostics and adapter selection.</summary>
    string ProviderName { get; }

    /// <summary>Provider capabilities for raster data operations.</summary>
    RasterDataCapabilities RasterCapabilities { get; }

    /// <summary>
    /// Gets raster dataset metadata.
    /// </summary>
    /// <param name="request">Metadata discovery request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raster dataset metadata.</returns>
    Task<RasterDatasetMetadata> GetRasterMetadataAsync(RasterMetadataRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes raster coverage statistics for an extent or area of interest.
    /// </summary>
    /// <param name="request">Coverage statistics request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Raster coverage statistics response.</returns>
    Task<RasterCoverageStatisticsResponse> GetCoverageStatisticsAsync(
        RasterCoverageStatisticsRequest request,
        CancellationToken cancellationToken = default);
}
