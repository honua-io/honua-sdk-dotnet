// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Published-layer administration plus PostGIS table discovery for a
/// secure connection.
/// </summary>
public interface IHonuaAdminLayersClient
{
    /// <summary>
    /// Lists published layers for a connection.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="serviceName">Optional service name filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of published layer summaries.</returns>
    Task<IReadOnlyList<PublishedLayerSummary>> ListLayersAsync(string connectionId, string? serviceName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a PostGIS table as a layer.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="request">The publish layer request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The published layer summary.</returns>
    Task<PublishedLayerSummary> PublishLayerAsync(string connectionId, PublishLayerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables a specific layer.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="enabled">Whether the layer should be enabled.</param>
    /// <param name="serviceName">Optional service name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated layer summary.</returns>
    Task<PublishedLayerSummary> SetLayerEnabledAsync(string connectionId, int layerId, bool enabled, string? serviceName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables all layers for a service.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="enabled">Whether the layers should be enabled.</param>
    /// <param name="serviceName">Optional service name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of updated layer summaries.</returns>
    Task<IReadOnlyList<PublishedLayerSummary>> SetServiceLayersEnabledAsync(string connectionId, bool enabled, string? serviceName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discovers PostGIS tables available on a connection.
    /// </summary>
    /// <param name="connectionId">The connection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The table discovery response.</returns>
    Task<TableDiscoveryResponse> DiscoverTablesAsync(string connectionId, CancellationToken cancellationToken = default);
}
