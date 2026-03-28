// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Wfs;

/// <summary>
/// Delegating handler that adds authentication headers to WFS requests.
/// </summary>
internal sealed class HonuaWfsAuthHandler : DelegatingHandler
{
    private readonly HonuaWfsClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaWfsAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The WFS client options containing authentication credentials.</param>
    public HonuaWfsAuthHandler(IOptions<HonuaWfsClientOptions> options)
    {
        _options = options.Value;
        HonuaWfsClientOptions.ValidateBaseAddress(_options.BaseAddress);
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var hasCredentials = !string.IsNullOrEmpty(_options.ApiKey) || !string.IsNullOrEmpty(_options.BearerToken);
        if (hasCredentials && HonuaWfsClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            throw new InvalidOperationException(
                "Refusing to send WFS credentials over an insecure connection. Use HTTPS, " +
                "or use loopback HTTP only for local development.");
        }

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", _options.ApiKey);
        }

        if (!string.IsNullOrEmpty(_options.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
