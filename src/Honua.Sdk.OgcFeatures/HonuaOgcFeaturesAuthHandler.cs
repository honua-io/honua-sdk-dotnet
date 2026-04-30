// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
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
        if (HonuaAuthenticationSupport.HasCredentialSource(_options) &&
            HonuaOgcFeaturesClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, "ogc-features");
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
            "ogc-features",
            cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
