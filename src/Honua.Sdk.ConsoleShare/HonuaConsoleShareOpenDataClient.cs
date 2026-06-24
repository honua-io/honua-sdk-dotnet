// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Honua.Sdk.Abstractions.Console.Share;
using Honua.Sdk.ConsoleShare.Exceptions;
using Honua.Sdk.ConsoleShare.Models;

namespace Honua.Sdk.ConsoleShare;

/// <summary>
/// HTTP client implementation for the Console Share open-data / DCAT / STAC
/// publication surface. Covers the authenticated admin endpoints under
/// <c>/api/v1/console/content/{id}/open-data</c> and the anonymous public reads
/// under <c>/api/v1/open-data</c>.
/// </summary>
public sealed class HonuaConsoleShareOpenDataClient : IHonuaConsoleShareOpenDataClient
{
    private const string AdminBasePath = "/api/v1/console/content";
    private const string PublicBasePath = "/api/v1/open-data";

    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaConsoleShareOpenDataClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and auth handlers.</param>
    public HonuaConsoleShareOpenDataClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<HonuaOpenDataPageResponse> GetPageAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureId(itemId, nameof(itemId));

        using var message = new HttpRequestMessage(HttpMethod.Get, OpenDataPath(itemId));
        return await SendForEnvelopeAsync(
            message,
            ConsoleShareJsonContext.Default.ApiResponseEnvelopeHonuaOpenDataPageResponse,
            "GetOpenDataPage",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaOpenDataPageResponse> UpdatePageAsync(string itemId, HonuaUpdateOpenDataPageRequest request, CancellationToken cancellationToken = default)
    {
        EnsureId(itemId, nameof(itemId));
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(HttpMethod.Put, OpenDataPath(itemId))
        {
            Content = JsonContent(request, ConsoleShareJsonContext.Default.HonuaUpdateOpenDataPageRequest)
        };
        return await SendForEnvelopeAsync(
            message,
            ConsoleShareJsonContext.Default.ApiResponseEnvelopeHonuaOpenDataPageResponse,
            "UpdateOpenDataPage",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaOpenDataEligibility> GetEligibilityAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureId(itemId, nameof(itemId));

        using var message = new HttpRequestMessage(HttpMethod.Get, OpenDataPath(itemId, "eligibility"));
        return await SendForEnvelopeAsync(
            message,
            ConsoleShareJsonContext.Default.ApiResponseEnvelopeHonuaOpenDataEligibility,
            "GetOpenDataEligibility",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaDcatExportResponse> PreviewDcatAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureId(itemId, nameof(itemId));

        using var message = new HttpRequestMessage(HttpMethod.Get, OpenDataPath(itemId, "dcat"));
        return await SendForEnvelopeAsync(
            message,
            ConsoleShareJsonContext.Default.ApiResponseEnvelopeHonuaDcatExportResponse,
            "PreviewDcat",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaConsoleStacPublicationState> GetStacPublicationAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureId(itemId, nameof(itemId));

        using var message = new HttpRequestMessage(HttpMethod.Get, OpenDataPath(itemId, "stac"));
        return await SendForEnvelopeAsync(
            message,
            ConsoleShareJsonContext.Default.ApiResponseEnvelopeHonuaConsoleStacPublicationState,
            "GetStacPublication",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaConsoleStacPublicationState> PublishStacAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureId(itemId, nameof(itemId));

        using var message = new HttpRequestMessage(HttpMethod.Post, OpenDataPath(itemId, "stac/publish"));
        return await SendForEnvelopeAsync(
            message,
            ConsoleShareJsonContext.Default.ApiResponseEnvelopeHonuaConsoleStacPublicationState,
            "PublishStac",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaConsoleStacPublicationState> UnpublishStacAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureId(itemId, nameof(itemId));

        using var message = new HttpRequestMessage(HttpMethod.Delete, OpenDataPath(itemId, "stac"));
        return await SendForEnvelopeAsync(
            message,
            ConsoleShareJsonContext.Default.ApiResponseEnvelopeHonuaConsoleStacPublicationState,
            "UnpublishStac",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaOpenDataPage> GetPublicDatasetAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureId(itemId, nameof(itemId));

        using var message = new HttpRequestMessage(HttpMethod.Get, PublicPath($"datasets/{Uri.EscapeDataString(itemId)}"));
        return await SendForEnvelopeAsync(
            message,
            ConsoleShareJsonContext.Default.ApiResponseEnvelopeHonuaOpenDataPage,
            "GetPublicDataset",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaDcatCatalog> GetPublicDataJsonAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureId(itemId, nameof(itemId));

        using var message = new HttpRequestMessage(HttpMethod.Get, PublicPath($"datasets/{Uri.EscapeDataString(itemId)}/data.json"));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaDcatCatalog,
            "GetPublicDataJson",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaSchemaOrgDataset> GetPublicSchemaOrgAsync(string itemId, CancellationToken cancellationToken = default)
    {
        EnsureId(itemId, nameof(itemId));

        using var message = new HttpRequestMessage(HttpMethod.Get, PublicPath($"datasets/{Uri.EscapeDataString(itemId)}/schema.org"));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaSchemaOrgDataset,
            "GetPublicSchemaOrg",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaStacCatalog> GetPublicStacCatalogAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, PublicPath("stac"));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaStacCatalog,
            "GetPublicStacCatalog",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaStacCollection> GetPublicStacCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        EnsureId(collectionId, nameof(collectionId));

        using var message = new HttpRequestMessage(HttpMethod.Get, PublicPath($"stac/collections/{Uri.EscapeDataString(collectionId)}"));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaStacCollection,
            "GetPublicStacCollection",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaStacItem> GetPublicStacItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default)
    {
        EnsureId(collectionId, nameof(collectionId));
        EnsureId(itemId, nameof(itemId));

        var path = $"stac/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(itemId)}";
        using var message = new HttpRequestMessage(HttpMethod.Get, PublicPath(path));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaStacItem,
            "GetPublicStacItem",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> SendForEnvelopeAsync<T>(
        HttpRequestMessage message,
        JsonTypeInfo<ApiResponseEnvelope<T>> typeInfo,
        string operation,
        CancellationToken cancellationToken)
        where T : class
    {
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, body);
        }

        ApiResponseEnvelope<T>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize(body, typeInfo);
        }
        catch (JsonException ex)
        {
            throw new HonuaConsoleShareContractException(
                $"Failed to deserialize the {operation} response envelope.",
                operation,
                body,
                ex);
        }

        return envelope?.Data
            ?? throw new HonuaConsoleShareContractException(
                $"Server returned an empty {operation} response envelope.",
                operation,
                body);
    }

    private async Task<T> SendForJsonAsync<T>(
        HttpRequestMessage message,
        JsonTypeInfo<T> typeInfo,
        string operation,
        CancellationToken cancellationToken)
        where T : class
    {
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, body);
        }

        T? value;
        try
        {
            value = JsonSerializer.Deserialize(body, typeInfo);
        }
        catch (JsonException ex)
        {
            throw new HonuaConsoleShareContractException(
                $"Failed to deserialize the {operation} response document.",
                operation,
                body,
                ex);
        }

        return value
            ?? throw new HonuaConsoleShareContractException(
                $"Server returned an empty {operation} response document.",
                operation,
                body);
    }

    private static StringContent JsonContent<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static HonuaConsoleShareApiException CreateApiException(HttpStatusCode statusCode, string body)
    {
        if (TryParseProblem(body, out var problem) && problem is not null)
        {
            var message = problem.Detail ?? problem.Title ?? "Console Share open-data API request failed.";
            return new HonuaConsoleShareApiException(statusCode, message, body, problem.Title, problem.Detail);
        }

        return new HonuaConsoleShareApiException(statusCode, "Console Share open-data API request failed.", body);
    }

    private static bool TryParseProblem(string body, out ConsoleShareProblem? problem)
    {
        try
        {
            problem = JsonSerializer.Deserialize(body, ConsoleShareJsonContext.Default.ConsoleShareProblem);
            return problem is not null;
        }
        catch (JsonException)
        {
            // Body is not a parseable problem-details document; the caller falls back to a generic message.
            problem = null;
            return false;
        }
    }

    private static Uri OpenDataPath(string itemId, string? suffix = null)
    {
        var path = $"{AdminBasePath}/{Uri.EscapeDataString(itemId)}/open-data";
        if (suffix is not null)
        {
            path = $"{path}/{suffix}";
        }

        return new Uri(path, UriKind.RelativeOrAbsolute);
    }

    private static Uri PublicPath(string relative)
        => new($"{PublicBasePath}/{relative}", UriKind.RelativeOrAbsolute);

    private static void EnsureId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier must be supplied.", parameterName);
        }
    }
}
