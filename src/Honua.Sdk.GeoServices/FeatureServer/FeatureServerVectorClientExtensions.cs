// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text;
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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed typed vector payload.</returns>
    public static async Task<VectorPayloadFeatureSet> QueryVectorAsync(
        this IHonuaFeatureServerClient client,
        string serviceId,
        int layerId,
        FeatureServerQueryParams query,
        VectorPayloadFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(query);

        if (client is HonuaFeatureServerClient honuaClient)
        {
            return await honuaClient.QueryVectorAsync(serviceId, layerId, query, format, cancellationToken).ConfigureAwait(false);
        }

        var vectorFormat = format ?? FeatureServerVectorFormats.FromFeatureServerFormat(query.Format);
        var protocolFormat = FeatureServerVectorFormats.ToFeatureServerFormat(vectorFormat);
        using var response = await client.QueryRawAsync(
            serviceId,
            layerId,
            query with { Format = protocolFormat },
            cancellationToken).ConfigureAwait(false);

        // GeoServices reports failures in-band as HTTP 200 with a JSON `{"error":{...}}` envelope
        // even when a binary (PBF) format was requested. A non-success transport status, or a JSON
        // content type on a binary request, signals the server fell back to an error envelope (or a
        // GeoJSON body). Route those through the shared envelope-aware error check so the vector path
        // surfaces a HonuaFeatureServerException with parity to the JSON query path, instead of
        // handing an error body to the binary reader and throwing an unhelpful decode error.
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!response.IsSuccessStatusCode || IsJsonMediaType(mediaType))
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            GeoServicesHttp.EnsureSuccess(response, body);

            // Success + JSON with no error envelope: a genuine GeoJSON payload. Parse it from the body.
            using var jsonStream = new MemoryStream(Encoding.UTF8.GetBytes(body));
            return await VectorPayloadReaders.ReadAsync(jsonStream, vectorFormat, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return await VectorPayloadReaders.ReadAsync(stream, vectorFormat, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static bool IsJsonMediaType(string? mediaType)
        => mediaType is not null && mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
}
