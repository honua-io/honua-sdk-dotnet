// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
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
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (HonuaAuthenticationSupport.HasCredentialSource(_options) &&
            HonuaWfsClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, "wfs");
            await HonuaAuthenticationSupport.EmitInsecureTransportRejectedDiagnosticAsync(
                _options,
                context,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                "Refusing to send WFS credentials over an insecure connection. Use HTTPS, " +
                "or use loopback HTTP only for local development.");
        }

        await HonuaAuthenticationSupport.ApplyHttpCredentialsAsync(
            request,
            _options,
            "wfs",
            cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
