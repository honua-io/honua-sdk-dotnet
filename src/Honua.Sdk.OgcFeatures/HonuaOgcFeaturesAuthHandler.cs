// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.OgcFeatures;

/// <summary>
/// Delegating handler that adds authentication headers to OGC API Features requests.
/// </summary>
internal sealed class HonuaOgcFeaturesAuthHandler : DelegatingHandler
{
    private readonly HonuaOgcFeaturesClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcFeaturesAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The OGC API Features client options containing authentication credentials.</param>
    public HonuaOgcFeaturesAuthHandler(IOptions<HonuaOgcFeaturesClientOptions> options)
    {
        _options = options.Value;
        HonuaOgcFeaturesClientOptions.ValidateBaseAddress(_options.BaseAddress);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (HasCredentialSource() && HonuaOgcFeaturesClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
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
