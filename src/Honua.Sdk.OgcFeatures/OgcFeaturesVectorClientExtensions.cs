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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed typed vector payload.</returns>
    public static async Task<VectorPayloadFeatureSet> GetItemsVectorAsync(
        this IHonuaOgcFeaturesClient client,
        string collectionId,
        OgcItemsParams? query = null,
        VectorPayloadFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (client is HonuaOgcFeaturesClient honuaClient)
        {
            return await honuaClient.GetItemsVectorAsync(collectionId, query, format, cancellationToken).ConfigureAwait(false);
        }

        var vectorFormat = format ?? OgcFeaturesVectorFormats.FromOgcFeaturesFormat(query?.Format);
        var protocolFormat = OgcFeaturesVectorFormats.ToOgcFeaturesFormat(vectorFormat);
        using var response = await client.GetItemsRawAsync(
            collectionId,
            (query ?? new OgcItemsParams()) with { Format = protocolFormat },
            cancellationToken).ConfigureAwait(false);

        // Route failures through the package error mapper so a non-Honua client surfaces the same
        // HonuaOgcFeaturesException (with RFC 7807 Problem Details) as the concrete client, instead
        // of a generic HttpRequestException that would escape a catch(HonuaException) handler.
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            HonuaOgcFeaturesClient.EnsureSuccess(response, errorBody);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await VectorPayloadReaders.ReadAsync(stream, vectorFormat, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
