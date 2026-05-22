// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Routing;

/// <summary>
/// Provider-neutral routing and network-analysis client contract.
/// </summary>
public interface IHonuaRoutingClient
{
    /// <summary>Stable provider name for diagnostics and adapter selection.</summary>
    string ProviderName { get; }

    /// <summary>Provider capabilities for routing operations.</summary>
    RoutingCapabilities Capabilities { get; }

    /// <summary>
    /// Gets routing service metadata such as travel modes and supported directions languages.
    /// </summary>
    /// <param name="request">Metadata discovery request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Routing service metadata.</returns>
    Task<RouteServiceMetadata> GetServiceMetadataAsync(RouteServiceMetadataRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets directions from an origin to a destination with optional waypoints.
    /// </summary>
    /// <param name="request">Route solve request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Route result including parsed summaries and directions when advertised by the provider.</returns>
    Task<RouteResult> GetDirectionsAsync(RouteDirectionsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Optimizes the order of route stops using provider best-sequence routing.
    /// </summary>
    /// <param name="request">Route optimization request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Route result including parsed summaries and directions when advertised by the provider.</returns>
    Task<RouteResult> OptimizeRouteAsync(RouteOptimizationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a service area / isochrone polygon around a center location.
    /// </summary>
    /// <param name="request">Service-area request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Service-area result with raw provider response.</returns>
    Task<ServiceAreaResult> GetServiceAreaAsync(ServiceAreaRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the closest facilities for one or more incident locations.
    /// </summary>
    /// <param name="request">Closest-facility request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Closest-facility result including parsed summaries and directions when advertised by the provider.</returns>
    Task<ClosestFacilityResult> FindClosestFacilityAsync(ClosestFacilityRequest request, CancellationToken cancellationToken = default);
}
