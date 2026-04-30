// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.GeoServices;

/// <summary>
/// Delegating handler that adds authentication headers to GeoServices API requests.
/// </summary>
internal sealed class HonuaGeoServicesAuthHandler : DelegatingHandler
{
    private readonly HonuaGeoServicesClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaGeoServicesAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The GeoServices client options containing authentication credentials.</param>
    public HonuaGeoServicesAuthHandler(IOptions<HonuaGeoServicesClientOptions> options)
    {
        _options = options.Value;
        HonuaGeoServicesClientOptions.ValidateBaseAddress(_options.BaseAddress);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (HonuaAuthenticationSupport.HasCredentialSource(_options) &&
            HonuaGeoServicesClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, "geoservices");
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
            "geoservices",
            cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
