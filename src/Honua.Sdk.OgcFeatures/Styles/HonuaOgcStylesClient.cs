// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Honua.Sdk.OgcFeatures.Exceptions;
using Honua.Sdk.OgcFeatures.Styles.Models;

namespace Honua.Sdk.OgcFeatures.Styles;

/// <summary>
/// HTTP client implementation for the Honua OGC API - Styles surface keyed by
/// <c>styleId</c> (ADR-0048).
/// </summary>
public sealed class HonuaOgcStylesClient : IHonuaOgcStylesClient
{
    private const string BasePath = "/ogc/styles";
    private const string MapboxStyleMediaType = "application/vnd.mapbox.style+json";
    private const string SldMediaType = "application/vnd.ogc.sld+xml";

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcStylesClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with base address and auth handlers.</param>
    public HonuaOgcStylesClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<OgcStylesList> ListStylesAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BasePath}?f=json");
        request.Headers.Accept.ParseAdd("application/json");

        var (body, _) = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcStylesJsonContext.Default.OgcStylesList)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize styles list.", body);
    }

    /// <inheritdoc />
    public async Task<OgcStylesheet> GetStylesheetAsync(
        string styleId,
        OgcStyleEncoding encoding = OgcStyleEncoding.MapboxStyle,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(styleId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{BasePath}/{Uri.EscapeDataString(styleId)}");
        request.Headers.Accept.ParseAdd(ToAcceptHeader(encoding));

        var (body, mediaType) = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return new OgcStylesheet
        {
            StyleId = styleId,
            Encoding = encoding,
            MediaType = mediaType,
            Content = body
        };
    }

    /// <inheritdoc />
    public async Task<OgcStyleMetadata> GetStyleMetadataAsync(
        string styleId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(styleId);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{BasePath}/{Uri.EscapeDataString(styleId)}/metadata?f=json");
        request.Headers.Accept.ParseAdd("application/json");

        var (body, _) = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize(body, OgcStylesJsonContext.Default.OgcStyleMetadata)
            ?? throw new HonuaOgcFeaturesException(HttpStatusCode.OK, "Failed to deserialize style metadata.", body);
    }

    /// <inheritdoc />
    public async Task UpdateStyleAsync(
        string styleId,
        string mapLibreStyleJson,
        bool strict = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(styleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapLibreStyleJson);

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{BasePath}/{Uri.EscapeDataString(styleId)}")
        {
            Content = new StringContent(mapLibreStyleJson, Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(MapboxStyleMediaType);

        if (strict)
        {
            request.Headers.TryAddWithoutValidation("Prefer", "handling=strict");
        }

        await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string ToAcceptHeader(OgcStyleEncoding encoding) => encoding switch
    {
        OgcStyleEncoding.MapboxStyle => MapboxStyleMediaType,
        OgcStyleEncoding.Sld10 => $"{SldMediaType};version=1.0",
        OgcStyleEncoding.Sld11 => $"{SldMediaType};version=1.1",
        _ => MapboxStyleMediaType,
    };

    private async Task<(string Body, string? MediaType)> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return (body, response.Content.Headers.ContentType?.MediaType);
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? problemType = null;
        string? problemTitle = null;
        string? problemDetail = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                var problem = JsonSerializer.Deserialize(body, OgcFeaturesJsonContext.Default.OgcProblemDetails);
                if (problem is not null)
                {
                    problemType = problem.Type;
                    problemTitle = problem.Title;
                    problemDetail = problem.Detail;
                }
            }
            catch (JsonException)
            {
                // Not valid Problem Details JSON.
            }
        }

        var message = problemDetail ?? problemTitle ?? response.ReasonPhrase ?? "OGC API Styles request failed";
        throw new HonuaOgcFeaturesException(
            response.StatusCode, message, body, problemType, problemTitle, problemDetail);
    }
}
