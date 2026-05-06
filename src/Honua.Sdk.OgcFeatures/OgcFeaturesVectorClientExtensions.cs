// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry.Vector;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures;

/// <summary>
/// Typed vector payload helpers for OGC API Features clients.
/// </summary>
public static class OgcFeaturesVectorClientExtensions
{
    /// <summary>
    /// Gets items from a collection and parses supported vector payload formats into typed features
    /// with NetTopologySuite geometries.
    /// </summary>
    /// <param name="client">OGC API Features client.</param>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="query">Optional query parameters for filtering, paging, and projection.</param>
    /// <param name="format">Optional shared vector format. Defaults to the format specified by <paramref name="query"/>, or GeoJSON.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed typed vector payload.</returns>
    public static async Task<VectorPayloadFeatureSet> GetItemsVectorAsync(
        this IHonuaOgcFeaturesClient client,
        string collectionId,
        OgcItemsParams? query = null,
        VectorPayloadFormat? format = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (client is HonuaOgcFeaturesClient honuaClient)
        {
            return await honuaClient.GetItemsVectorAsync(collectionId, query, format, ct).ConfigureAwait(false);
        }

        var vectorFormat = format ?? OgcFeaturesVectorFormats.FromOgcFeaturesFormat(query?.Format);
        var protocolFormat = OgcFeaturesVectorFormats.ToOgcFeaturesFormat(vectorFormat);
        using var response = await client.GetItemsRawAsync(
            collectionId,
            (query ?? new OgcItemsParams()) with { Format = protocolFormat },
            ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await VectorPayloadReaders.ReadAsync(stream, vectorFormat, ct: ct).ConfigureAwait(false);
    }
}
