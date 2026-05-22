// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.UtilityNetworks;

/// <summary>
/// Provider-neutral utility-network trace client contract.
/// </summary>
public interface IHonuaUtilityNetworkTraceClient
{
    /// <summary>Stable provider name for diagnostics and adapter selection.</summary>
    string ProviderName { get; }

    /// <summary>Provider capabilities for utility-network trace operations.</summary>
    UtilityNetworkTraceCapabilities TraceCapabilities { get; }

    /// <summary>
    /// Lists named trace configurations advertised by the provider.
    /// </summary>
    /// <param name="request">Trace configuration query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Named trace configurations that can be referenced by trace requests.</returns>
    Task<IReadOnlyList<UtilityNetworkNamedTraceConfiguration>> GetTraceConfigurationsAsync(
        UtilityNetworkTraceConfigurationQuery request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Traces all elements connected to the request's starting points.
    /// </summary>
    /// <param name="request">Trace request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Trace result data for downstream workflow or display adapters.</returns>
    Task<UtilityNetworkTraceResult> TraceConnectedAsync(
        UtilityNetworkTraceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Traces upstream from the request's starting points.
    /// </summary>
    /// <param name="request">Trace request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Trace result data for downstream workflow or display adapters.</returns>
    Task<UtilityNetworkTraceResult> TraceUpstreamAsync(
        UtilityNetworkTraceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Traces downstream from the request's starting points.
    /// </summary>
    /// <param name="request">Trace request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Trace result data for downstream workflow or display adapters.</returns>
    Task<UtilityNetworkTraceResult> TraceDownstreamAsync(
        UtilityNetworkTraceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Traces the subnetwork reachable from the request's starting points.
    /// </summary>
    /// <param name="request">Trace request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Trace result data for downstream workflow or display adapters.</returns>
    Task<UtilityNetworkTraceResult> TraceSubnetworkAsync(
        UtilityNetworkTraceRequest request,
        CancellationToken cancellationToken = default);
}
