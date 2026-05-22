// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Data;

/// <summary>
/// Provider-neutral elevation data client contract.
/// </summary>
public interface IHonuaElevationDataClient
{
    /// <summary>Stable provider name for diagnostics and adapter selection.</summary>
    string ProviderName { get; }

    /// <summary>Provider capabilities for elevation operations.</summary>
    ElevationDataCapabilities ElevationCapabilities { get; }

    /// <summary>
    /// Samples elevation at points or along a provider-supported profile geometry.
    /// </summary>
    /// <param name="request">Elevation sampling request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Elevation sampling response.</returns>
    Task<ElevationSamplingResponse> SampleElevationAsync(ElevationSamplingRequest request, CancellationToken cancellationToken = default);
}
