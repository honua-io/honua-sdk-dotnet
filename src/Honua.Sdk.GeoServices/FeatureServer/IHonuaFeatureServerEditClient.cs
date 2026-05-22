// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.FeatureServer.Models;

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>
/// Client interface for the Honua FeatureServer (GeoServices) feature edit API.
/// </summary>
public interface IHonuaFeatureServerEditClient
{
    /// <summary>
    /// Gets FeatureServer edit capabilities for a layer.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-neutral edit capabilities inferred from layer metadata.</returns>
    Task<FeatureEditCapabilities> GetEditCapabilitiesAsync(string serviceId, int layerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies add, update, and delete feature edits using the FeatureServer <c>applyEdits</c> endpoint.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="request">Edit request containing adds, updates, deletes, and rollback behavior.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Edit response with per-operation results.</returns>
    Task<FeatureServerEditResponse> ApplyEditsAsync(
        string serviceId,
        int layerId,
        FeatureServerEditRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds features using the FeatureServer <c>applyEdits</c> endpoint.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="features">Features to add.</param>
    /// <param name="rollbackOnFailure">Whether the server should roll back all edits if any add fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Edit response with add results.</returns>
    Task<FeatureServerEditResponse> AddFeaturesAsync(
        string serviceId,
        int layerId,
        IReadOnlyList<FeatureServerFeature> features,
        bool rollbackOnFailure = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates features using the FeatureServer <c>applyEdits</c> endpoint.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="features">Features to update. Each feature must include the layer object ID field.</param>
    /// <param name="rollbackOnFailure">Whether the server should roll back all edits if any update fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Edit response with update results.</returns>
    Task<FeatureServerEditResponse> UpdateFeaturesAsync(
        string serviceId,
        int layerId,
        IReadOnlyList<FeatureServerFeature> features,
        bool rollbackOnFailure = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes features using the FeatureServer <c>applyEdits</c> endpoint.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="objectIds">Object IDs to delete.</param>
    /// <param name="rollbackOnFailure">Whether the server should roll back all edits if any delete fails.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Edit response with delete results.</returns>
    Task<FeatureServerEditResponse> DeleteFeaturesAsync(
        string serviceId,
        int layerId,
        IReadOnlyList<long> objectIds,
        bool rollbackOnFailure = true,
        CancellationToken cancellationToken = default);
}
