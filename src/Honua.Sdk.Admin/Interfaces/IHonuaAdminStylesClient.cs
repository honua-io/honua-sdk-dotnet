// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Per-layer style read and update over the admin metadata API
/// (<c>/api/v1/admin/metadata/layers/{layerId}/style</c>), keyed by <c>layerId</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deprecated.</b> This layer-keyed surface is retained only as a back-compat
/// alias for editing a layer's default style and may be removed in a future
/// major release. New code should use the canonical, <c>styleId</c>-keyed styles
/// surface (ADR-0048): <c>Honua.Sdk.OgcFeatures.Styles.IHonuaOgcStylesClient</c>
/// over <c>/ogc/styles</c>, which supports listing styles, content-negotiated
/// stylesheet encodings (MapLibre/SLD), and style metadata.
/// </para>
/// <para>
/// Deprecation is documented here (rather than via <c>[Obsolete]</c>) because the
/// SDK builds with <c>TreatWarningsAsErrors</c>, under which the <c>CS0618</c>
/// produced by <c>[Obsolete]</c> would break consumers that still call this
/// alias. This is consistent with how the alias was documented when the
/// <c>styleId</c> client was introduced.
/// </para>
/// </remarks>
public interface IHonuaAdminStylesClient
{
    /// <summary>
    /// Gets the style for a layer.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated</b> in favor of the <c>styleId</c>-keyed
    /// <c>Honua.Sdk.OgcFeatures.Styles.IHonuaOgcStylesClient.GetStylesheetAsync</c>
    /// for the canonical OGC API - Styles surface (ADR-0048).
    /// </remarks>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The layer style response.</returns>
    Task<LayerStyleResponse> GetLayerStyleAsync(int layerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the style for a layer.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated</b> in favor of the <c>styleId</c>-keyed
    /// <c>Honua.Sdk.OgcFeatures.Styles.IHonuaOgcStylesClient.UpdateStyleAsync</c>
    /// for the canonical OGC API - Styles surface (ADR-0048).
    /// </remarks>
    /// <param name="layerId">The layer identifier.</param>
    /// <param name="request">The style update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated layer style response.</returns>
    Task<LayerStyleResponse> UpdateLayerStyleAsync(int layerId, LayerStyleUpdateRequest request, CancellationToken cancellationToken = default);
}
