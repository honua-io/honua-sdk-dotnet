// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.ConsoleShare;

/// <summary>
/// Delegating handler that adds authentication headers to Console Share requests.
/// </summary>
internal sealed class HonuaConsoleShareAuthHandler : DelegatingHandler
{
    private readonly HonuaConsoleShareClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaConsoleShareAuthHandler"/> class.
    /// </summary>
    public HonuaConsoleShareAuthHandler(IOptions<HonuaConsoleShareClientOptions> options)
    {
        _options = options.Value;
        HonuaConsoleShareClientOptions.ValidateBaseAddress(_options.BaseAddress);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (HonuaAuthenticationSupport.HasCredentialSource(_options) &&
            HonuaConsoleShareClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, "console-share");
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
            "console-share",
            cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
