// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Catalogs.Stac;

/// <summary>
/// Delegating handler that adds authentication headers to STAC requests.
/// </summary>
internal sealed class HonuaStacAuthHandler : DelegatingHandler
{
    private readonly HonuaStacClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaStacAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The STAC client options containing authentication credentials.</param>
    public HonuaStacAuthHandler(IOptions<HonuaStacClientOptions> options)
    {
        _options = options.Value;
        HonuaStacClientOptions.ValidateBaseAddress(_options.BaseAddress);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (HonuaAuthenticationSupport.HasCredentialSource(_options) &&
            HonuaStacClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, "stac");
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
            "stac",
            cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
