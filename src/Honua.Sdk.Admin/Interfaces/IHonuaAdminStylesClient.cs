// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Per-layer style read and update.
/// </summary>
public interface IHonuaAdminStylesClient
{
    /// <summary>
    /// Gets the style for a layer.
    /// </summary>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The layer style response.</returns>
    Task<LayerStyleResponse> GetLayerStyleAsync(int layerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the style for a layer.
    /// </summary>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="request">The style update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated layer style response.</returns>
    Task<LayerStyleResponse> UpdateLayerStyleAsync(int layerId, LayerStyleUpdateRequest request, CancellationToken cancellationToken = default);
}
