// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Catalogs.Records;

/// <summary>
/// Delegating handler that adds authentication headers to OGC API Records requests.
/// </summary>
internal sealed class HonuaOgcRecordsAuthHandler : DelegatingHandler
{
    private readonly HonuaOgcRecordsClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOgcRecordsAuthHandler"/> class.
    /// </summary>
    /// <param name="options">The OGC API Records client options containing authentication credentials.</param>
    public HonuaOgcRecordsAuthHandler(IOptions<HonuaOgcRecordsClientOptions> options)
    {
        _options = options.Value;
        HonuaOgcRecordsClientOptions.ValidateBaseAddress(_options.BaseAddress);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (HonuaAuthenticationSupport.HasCredentialSource(_options) &&
            HonuaOgcRecordsClientOptions.RequiresHttpsForAuthentication(request.RequestUri))
        {
            var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, "ogc-records");
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
            "ogc-records",
            cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
