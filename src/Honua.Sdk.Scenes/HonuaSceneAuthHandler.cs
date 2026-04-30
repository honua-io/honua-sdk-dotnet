// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Scenes;

/// <summary>
/// Delegating handler that adds authentication headers to scene metadata requests.
/// </summary>
internal sealed class HonuaSceneAuthHandler : DelegatingHandler
{
    private readonly HonuaSceneClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSceneAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The scene client options containing authentication credentials.</param>
    public HonuaSceneAuthHandler(IOptions<HonuaSceneClientOptions> options)
    {
        _options = options.Value;
        HonuaSceneClientOptions.ValidateBaseAddress(_options.BaseAddress);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (HasCredentialSource() && HonuaSceneClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            throw new InvalidOperationException(
                "Refusing to send credentials over an insecure connection. Use HTTPS, " +
                "or use loopback HTTP only for local development.");
        }

        var apiKey = await ResolveApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
        }

        var bearerToken = await ResolveBearerTokenAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private bool HasCredentialSource()
        => !string.IsNullOrEmpty(_options.ApiKey) ||
           !string.IsNullOrEmpty(_options.BearerToken) ||
           _options.ApiKeyProvider is not null ||
           _options.BearerTokenProvider is not null;

    private Task<string?> ResolveApiKeyAsync(CancellationToken cancellationToken)
        => _options.ApiKeyProvider is { } provider
            ? provider(cancellationToken)
            : Task.FromResult(_options.ApiKey);

    private Task<string?> ResolveBearerTokenAsync(CancellationToken cancellationToken)
        => _options.BearerTokenProvider is { } provider
            ? provider(cancellationToken)
            : Task.FromResult(_options.BearerToken);
}
