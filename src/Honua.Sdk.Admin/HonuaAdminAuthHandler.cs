// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Admin;

/// <summary>
/// Delegating handler that adds authentication headers to admin API requests.
/// </summary>
internal sealed class HonuaAdminAuthHandler : DelegatingHandler
{
    private readonly HonuaAdminClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaAdminAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The admin client options containing authentication credentials.</param>
    public HonuaAdminAuthHandler(IOptions<HonuaAdminClientOptions> options)
    {
        _options = options.Value;
        HonuaAdminClientOptions.ValidateBaseAddress(_options.BaseAddress);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (HonuaAuthenticationSupport.HasCredentialSource(_options) &&
            HonuaAdminClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, "admin");
            await HonuaAuthenticationSupport.EmitInsecureTransportRejectedDiagnosticAsync(
                _options,
                context,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                "Refusing to send admin credentials over an insecure connection. Use HTTPS, " +
                "or use loopback HTTP only for local development.");
        }

        await HonuaAuthenticationSupport.ApplyHttpCredentialsAsync(
            request,
            _options,
            "admin",
            cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
