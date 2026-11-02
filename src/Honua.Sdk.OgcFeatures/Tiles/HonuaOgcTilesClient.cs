// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.OgcFeatures.Exceptions;
using Honua.Sdk.OgcFeatures.Tiles.Models;

namespace Honua.Sdk.OgcFeatures.Tiles;

/// <summary>
/// HTTP client for the Honua OGC API Tiles surface (<c>/ogc/tiles</c>).
/// </summary>
public sealed class HonuaOgcTilesClient : IHonuaOgcTilesClient
{
    private const string BasePath = "/ogc/tiles";
    private const string VectorMediaType = "application/vnd.mapbox-vector-tile";
    private const string PngMediaType = "image/png";

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcTilesClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth handlers.</param>
    public HonuaOgcTilesClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<OgcTileSetsList> ListCollectionTilesetsAsync(
        string collectionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/tiles");
        request.Headers.Accept.ParseAdd("application/json");

        var (body, _) = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcTilesJsonContext.Default.OgcTileSetsList)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize tileset list.", body);
    }

    /// <inheritdoc />
    public async Task<OgcTile> GetTileAsync(
        string collectionId,
        string tileMatrixSetId,
        int tileMatrix,
        int tileRow,
        int tileCol,
        OgcTileFormat format = OgcTileFormat.Vector,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tileMatrixSetId);

        var path = $"{BasePath}/collections/{Uri.EscapeDataString(collectionId)}/tiles/" +
                   $"{Uri.EscapeDataString(tileMatrixSetId)}/{tileMatrix.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{tileRow}/{tileCol}";
        if (format == OgcTileFormat.Png)
        {
            path += "?f=png";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd(format == OgcTileFormat.Png ? PngMediaType : VectorMediaType);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return new OgcTile
            {
                IsEmpty = true,
                MediaType = response.Content.Headers.ContentType?.MediaType,
                Content = ReadOnlyMemory<byte>.Empty,
            };
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = Encoding.UTF8.GetString(bytes);
            var message = Honua.Sdk.Abstractions.HonuaProblemDetailsParser.ResolveMessage(
                body, response.ReasonPhrase ?? "OGC API Tiles request failed", out var problem);
            throw new HonuaOgcFeaturesException(
                response.StatusCode, message, body, problem?.Type, problem?.Title, problem?.Detail);
        }

        return new OgcTile
        {
            IsEmpty = false,
            MediaType = response.Content.Headers.ContentType?.MediaType,
            Content = bytes,
        };
    }

    private async Task<(string Body, string? MediaType)> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var message = Honua.Sdk.Abstractions.HonuaProblemDetailsParser.ResolveMessage(
                body, response.ReasonPhrase ?? "OGC API Tiles request failed", out var problem);
            throw new HonuaOgcFeaturesException(
                response.StatusCode, message, body, problem?.Type, problem?.Title, problem?.Detail);
        }

        return (body, response.Content.Headers.ContentType?.MediaType);
    }
}
