// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Service-level administration: list services, read and update service settings
/// (protocols, MapServer rendering, access policy, time info, and layer metadata).
/// </summary>
public interface IHonuaAdminServicesClient
{
    /// <summary>
    /// Lists all registered services.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of service summaries.</returns>
    Task<IReadOnlyList<ServiceSummary>> ListServicesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the settings for a specific service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The service settings.</returns>
    Task<ServiceSettingsResponse> GetServiceSettingsAsync(string serviceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the enabled protocols for a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="protocols">The protocols to enable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated service settings.</returns>
    Task<ServiceSettingsResponse> UpdateProtocolsAsync(string serviceName, IReadOnlyList<string> protocols, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the MapServer rendering settings for a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="request">The MapServer settings to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated service settings.</returns>
    Task<ServiceSettingsResponse> UpdateMapServerSettingsAsync(string serviceName, UpdateMapServerSettingsRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the access policy for a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="request">The access policy update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated service settings.</returns>
    Task<ServiceSettingsResponse> UpdateAccessPolicyAsync(string serviceName, UpdateAccessPolicyRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates temporal metadata for a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="request">The time info update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated service settings.</returns>
    Task<ServiceSettingsResponse> UpdateTimeInfoAsync(string serviceName, UpdateTimeInfoRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates metadata for a specific layer within a service.
    /// </summary>
    /// <param name="serviceName">The service name.</param>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="request">The layer metadata update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated layer metadata.</returns>
    Task<LayerMetadataResponse> UpdateLayerMetadataAsync(string serviceName, int layerId, UpdateLayerMetadataRequest request, CancellationToken cancellationToken = default);
}
