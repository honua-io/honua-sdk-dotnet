// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Spec;

/// <summary>
/// Delegating handler that adds authentication headers to spec workspace API requests.
/// </summary>
internal sealed class HonuaSpecAuthHandler : DelegatingHandler
{
    private readonly HonuaSpecClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaSpecAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The spec client options containing authentication credentials.</param>
    public HonuaSpecAuthHandler(IOptions<HonuaSpecClientOptions> options)
    {
        _options = options.Value;
        HonuaSpecClientOptions.ValidateBaseAddress(_options.BaseAddress);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (HonuaAuthenticationSupport.HasCredentialSource(_options) &&
            HonuaSpecClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, "spec");
            await HonuaAuthenticationSupport.EmitInsecureTransportRejectedDiagnosticAsync(
                _options,
                context,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                "Refusing to send spec credentials over an insecure connection. Use HTTPS, " +
                "or use loopback HTTP only for local development.");
        }

        await HonuaAuthenticationSupport.ApplyHttpCredentialsAsync(
            request,
            _options,
            "spec",
            cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
