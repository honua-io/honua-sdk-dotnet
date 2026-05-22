// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Shared feature read/query contract implemented by protocol-specific Honua clients.
/// </summary>
public interface IHonuaFeatureQueryClient
{
    /// <summary>
    /// Provider name for diagnostics and provider selection.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Executes a feature query and returns a single result page.
    /// </summary>
    /// <param name="request">Provider-neutral query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A provider-neutral result page.</returns>
    Task<FeatureQueryResult> QueryAsync(FeatureQueryRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and returns provider-neutral result pages.
    /// </summary>
    /// <param name="request">Provider-neutral query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-neutral result pages.</returns>
    IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(FeatureQueryRequest request, CancellationToken cancellationToken = default);
}
