// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
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
        if (HonuaAuthenticationSupport.HasCredentialSource(_options) &&
            HonuaSceneClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, "scenes");
            await HonuaAuthenticationSupport.EmitInsecureTransportRejectedDiagnosticAsync(
                _options,
                context,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                "Refusing to send credentials over an insecure connection. Use HTTPS, " +
                "or use loopback HTTP only for local development.");
        }

        await HonuaAuthenticationSupport.ApplyHttpCredentialsAsync(
            request,
            _options,
            "scenes",
            cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
