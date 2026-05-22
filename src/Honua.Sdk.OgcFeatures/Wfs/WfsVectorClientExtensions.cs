// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry.Vector;
using Honua.Sdk.OgcFeatures.Wfs.Formats;
using Honua.Sdk.OgcFeatures.Wfs.Models;

namespace Honua.Sdk.OgcFeatures.Wfs;

/// <summary>
/// Typed vector payload helpers for WFS clients.
/// </summary>
public static class WfsVectorClientExtensions
{
    /// <summary>
    /// Retrieves features using a supported typed vector output format and parses geometries
    /// with NetTopologySuite.
    /// </summary>
    /// <param name="client">WFS client.</param>
    /// <param name="request">The feature request parameters.</param>
    /// <param name="format">Vector payload format to request. Defaults to GeoJSON.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed typed vector payload.</returns>
    public static Task<VectorPayloadFeatureSet> GetFeaturesVectorAsync(
        this IHonuaWfsClient client,
        GetFeaturesRequest request,
        VectorPayloadFormat format = VectorPayloadFormat.GeoJson,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.GetFeaturesAsync(request, new VectorPayloadFeatureSetHandler(format), cancellationToken);
    }
}
