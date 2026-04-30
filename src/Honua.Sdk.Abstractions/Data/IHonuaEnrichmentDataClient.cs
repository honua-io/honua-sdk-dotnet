// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Data;

/// <summary>
/// Provider-neutral enrichment data client contract.
/// </summary>
public interface IHonuaEnrichmentDataClient
{
    /// <summary>Stable provider name for diagnostics and adapter selection.</summary>
    string ProviderName { get; }

    /// <summary>Provider capabilities for enrichment operations.</summary>
    EnrichmentDataCapabilities EnrichmentCapabilities { get; }

    /// <summary>
    /// Gets available enrichment attributes or variable metadata.
    /// </summary>
    /// <param name="request">Metadata discovery request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Enrichment metadata.</returns>
    Task<EnrichmentMetadata> GetEnrichmentMetadataAsync(EnrichmentMetadataRequest request, CancellationToken ct = default);

    /// <summary>
    /// Enriches feature identifiers, geometry, or an area of interest with requested attributes.
    /// </summary>
    /// <param name="request">Enrichment request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Enrichment response.</returns>
    Task<EnrichmentResponse> EnrichAsync(EnrichmentRequest request, CancellationToken ct = default);
}
