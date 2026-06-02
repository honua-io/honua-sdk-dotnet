// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.OgcFeatures.Styles.Models;

namespace Honua.Sdk.OgcFeatures.Styles;

/// <summary>
/// Client for the Honua OGC API - Styles surface (<c>/ogc/styles</c>), keyed by
/// <c>styleId</c> per ADR-0048. This is the canonical styles client; the
/// per-layer admin styles client (<c>IHonuaAdminStylesClient</c>) keyed by
/// <c>layerId</c> remains a back-compat alias for editing a layer's default style.
/// </summary>
public interface IHonuaOgcStylesClient
{
    /// <summary>
    /// Lists the styles available on the server (<c>GET /ogc/styles</c>).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The styles list, including any declared default style.</returns>
    Task<OgcStylesList> ListStylesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a stylesheet (<c>GET /ogc/styles/{styleId}</c>), content-negotiated by
    /// the requested <paramref name="encoding"/> via the <c>Accept</c> header.
    /// </summary>
    /// <param name="styleId">The stable style identifier.</param>
    /// <param name="encoding">
    /// The desired stylesheet encoding. Defaults to
    /// <see cref="OgcStyleEncoding.MapboxStyle"/> (the canonical MapLibre JSON).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stylesheet document and its negotiated media type.</returns>
    Task<OgcStylesheet> GetStylesheetAsync(
        string styleId,
        OgcStyleEncoding encoding = OgcStyleEncoding.MapboxStyle,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets descriptive metadata for a style
    /// (<c>GET /ogc/styles/{styleId}/metadata</c>).
    /// </summary>
    /// <param name="styleId">The stable style identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The style metadata.</returns>
    Task<OgcStyleMetadata> GetStyleMetadataAsync(
        string styleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a style's MapLibre stylesheet (<c>PUT /ogc/styles/{styleId}</c>),
    /// requiring the <c>manage-styles</c> capability. The body is sent as
    /// <c>application/vnd.mapbox.style+json</c>.
    /// </summary>
    /// <param name="styleId">The stable style identifier.</param>
    /// <param name="mapLibreStyleJson">The MapLibre/Mapbox style document to store.</param>
    /// <param name="strict">
    /// When <c>true</c>, requests strict validation via <c>Prefer: handling=strict</c>;
    /// invalid styles are rejected with a 400.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the style has been updated.</returns>
    Task UpdateStyleAsync(
        string styleId,
        string mapLibreStyleJson,
        bool strict = false,
        CancellationToken cancellationToken = default);
}
