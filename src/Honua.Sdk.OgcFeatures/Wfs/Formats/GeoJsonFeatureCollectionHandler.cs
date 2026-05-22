// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.OgcFeatures.Wfs.Models;

namespace Honua.Sdk.OgcFeatures.Wfs.Formats;

/// <summary>
/// Deserializes WFS GeoJSON responses into <see cref="WfsFeatureCollection"/>.
/// </summary>
public sealed class GeoJsonFeatureCollectionHandler : IWfsOutputFormatHandler<WfsFeatureCollection>
{
    /// <inheritdoc />
    public string MediaType => "application/geo+json";

    /// <inheritdoc />
    public async Task<WfsFeatureCollection> ReadAsync(Stream responseStream, CancellationToken cancellationToken = default)
    {
        var wire = await JsonSerializer.DeserializeAsync(
            responseStream,
            WfsJsonContext.Default.WfsGeoJsonFeatureCollection,
            cancellationToken).ConfigureAwait(false);

        if (wire is null)
        {
            return new WfsFeatureCollection();
        }

        return wire.ToPublicModel();
    }
}
