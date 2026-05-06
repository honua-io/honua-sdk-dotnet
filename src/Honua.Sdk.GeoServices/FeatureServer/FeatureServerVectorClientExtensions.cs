// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.Geometry.Vector;

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>
/// Typed vector payload helpers for FeatureServer clients.
/// </summary>
public static class FeatureServerVectorClientExtensions
{
    /// <summary>
    /// Executes a feature query and parses supported vector payload formats into typed features
    /// with NetTopologySuite geometries.
    /// </summary>
    /// <param name="client">FeatureServer client.</param>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="query">Query parameters including filters, paging, and projection.</param>
    /// <param name="format">Optional shared vector format. Defaults to the format specified by <paramref name="query"/>, or Esri JSON.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The parsed typed vector payload.</returns>
    public static async Task<VectorPayloadFeatureSet> QueryVectorAsync(
        this IHonuaFeatureServerClient client,
        string serviceId,
        int layerId,
        FeatureServerQueryParams query,
        VectorPayloadFormat? format = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(query);

        if (client is HonuaFeatureServerClient honuaClient)
        {
            return await honuaClient.QueryVectorAsync(serviceId, layerId, query, format, ct).ConfigureAwait(false);
        }

        var vectorFormat = format ?? FeatureServerVectorFormats.FromFeatureServerFormat(query.Format);
        var protocolFormat = FeatureServerVectorFormats.ToFeatureServerFormat(vectorFormat);
        using var response = await client.QueryRawAsync(
            serviceId,
            layerId,
            query with { Format = protocolFormat },
            ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await VectorPayloadReaders.ReadAsync(stream, vectorFormat, ct: ct).ConfigureAwait(false);
    }
}
