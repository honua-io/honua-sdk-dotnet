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
/// HTTP client implementation for the Console Share access, public-link, and
/// embed-token lifecycle surface.
/// </summary>
public sealed class HonuaConsoleShareClient : IHonuaConsoleShareClient
{
    private const string BasePath = "/api/v1/console/shares";
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaConsoleShareClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and auth handlers.</param>
    public HonuaConsoleShareClient(HttpClient httpClient)
    {
        _http = httpClient;
    }

    /// <inheritdoc />
    public async Task<HonuaShareItemDetail> GetShareAsync(string shareId, CancellationToken cancellationToken = default)
    {
        EnsureId(shareId, nameof(shareId));

        using var message = new HttpRequestMessage(HttpMethod.Get, SharePath(shareId));
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareItemDetail,
            "GetShare",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareItemDetail> UpdateAccessAsync(string shareId, HonuaShareAccessUpdate update, CancellationToken cancellationToken = default)
    {
        EnsureId(shareId, nameof(shareId));
        ArgumentNullException.ThrowIfNull(update);

        using var message = new HttpRequestMessage(HttpMethod.Put, SharePath(shareId, "access"))
        {
            Content = JsonContent(update, ConsoleShareJsonContext.Default.HonuaShareAccessUpdate)
        };
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareItemDetail,
            "UpdateAccess",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaShareDependencyClosure> ValidateDependencyClosureAsync(string shareId, HonuaShareAccessUpdate update, CancellationToken cancellationToken = default)
    {
        EnsureId(shareId, nameof(shareId));
        ArgumentNullException.ThrowIfNull(update);

        using var message = new HttpRequestMessage(HttpMethod.Post, SharePath(shareId, "access/validate"))
        {
            Content = JsonContent(update, ConsoleShareJsonContext.Default.HonuaShareAccessUpdate)
        };
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaShareDependencyClosure,
            "ValidateDependencyClosure",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaPublicLink> CreatePublicLinkAsync(string shareId, HonuaPublicLinkRequest request, CancellationToken cancellationToken = default)
    {
        EnsureId(shareId, nameof(shareId));
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(HttpMethod.Put, SharePath(shareId, "public-link"))
        {
            Content = JsonContent(request, ConsoleShareJsonContext.Default.HonuaPublicLinkRequest)
        };
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaPublicLink,
            "CreatePublicLink",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RevokePublicLinkAsync(string shareId, CancellationToken cancellationToken = default)
    {
        EnsureId(shareId, nameof(shareId));

        using var message = new HttpRequestMessage(HttpMethod.Delete, SharePath(shareId, "public-link"));
        await SendForNoContentAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<HonuaEmbedToken> CreateEmbedTokenAsync(string shareId, HonuaEmbedTokenRequest request, CancellationToken cancellationToken = default)
    {
        EnsureId(shareId, nameof(shareId));
        ArgumentNullException.ThrowIfNull(request);

        using var message = new HttpRequestMessage(HttpMethod.Put, SharePath(shareId, "embed-token"))
        {
            Content = JsonContent(request, ConsoleShareJsonContext.Default.HonuaEmbedTokenRequest)
        };
        return await SendForJsonAsync(
            message,
            ConsoleShareJsonContext.Default.HonuaEmbedToken,
            "CreateEmbedToken",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RevokeEmbedTokenAsync(string shareId, CancellationToken cancellationToken = default)
    {
        EnsureId(shareId, nameof(shareId));

        using var message = new HttpRequestMessage(HttpMethod.Delete, SharePath(shareId, "embed-token"));
        await SendForNoContentAsync(message, cancellationToken).ConfigureAwait(false);
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
                $"Failed to deserialize the {operation} response envelope.",
                operation,
                body,
                ex);
        }

        return value
            ?? throw new HonuaConsoleShareContractException(
                $"Server returned an empty {operation} response envelope.",
                operation,
                body);
    }

    private async Task SendForNoContentAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        using var response = await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw CreateApiException(response.StatusCode, body);
        }
    }

    private static HttpContent JsonContent<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static HonuaConsoleShareApiException CreateApiException(HttpStatusCode statusCode, string body)
    {
        if (TryParseProblem(body, out var problem) && problem is not null)
        {
            var message = problem.Detail ?? problem.Title ?? "Console Share API request failed.";
            return new HonuaConsoleShareApiException(statusCode, message, body, problem.Title, problem.Detail);
        }

        return new HonuaConsoleShareApiException(statusCode, "Console Share API request failed.", body);
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

    private static Uri SharePath(string shareId, string? suffix = null)
    {
        var path = $"{BasePath}/{Uri.EscapeDataString(shareId)}";
        if (suffix is not null)
        {
            path = $"{path}/{suffix}";
        }

        return new Uri(path, UriKind.RelativeOrAbsolute);
    }

    private static void EnsureId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier must be supplied.", parameterName);
        }
    }
}
