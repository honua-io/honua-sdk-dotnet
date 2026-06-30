// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Authentication;

/// <summary>
/// Shared <see cref="DelegatingHandler"/> base for Honua REST clients that applies SDK
/// authentication credentials and enforces the plain-HTTP credential guard. Every paginating
/// and non-paginating HTTP client delegates to this single implementation so that a hardening
/// change (for example to the insecure-transport policy or the rejection message) reaches all
/// clients at once instead of drifting across per-package copies.
/// </summary>
/// <typeparam name="TOptions">
/// The concrete client options type, which must expose both the transport surface
/// (<see cref="IHonuaClientOptions"/>) and the authentication surface
/// (<see cref="IHonuaAuthenticationOptions"/>).
/// </typeparam>
public abstract class HonuaAuthHandler<TOptions> : DelegatingHandler
    where TOptions : class, IHonuaClientOptions, IHonuaAuthenticationOptions
{
    private readonly TOptions _options;
    private readonly string _serviceName;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaAuthHandler{TOptions}"/> class.
    /// </summary>
    /// <param name="options">The resolved client options carrying credentials and base address.</param>
    /// <param name="serviceName">
    /// The logical Honua service name used in authentication diagnostics (for example
    /// <c>"stac"</c> or <c>"admin"</c>).
    /// </param>
    /// <param name="validateBaseAddress">
    /// The owning package's base-address validation callback, invoked once at construction so the
    /// failure carries the client-specific display name.
    /// </param>
    protected HonuaAuthHandler(TOptions options, string serviceName, Action<Uri?> validateBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(validateBaseAddress);

        _options = options;
        _serviceName = serviceName;
        validateBaseAddress(options.BaseAddress);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (HonuaAuthenticationSupport.HasCredentialSource(_options) &&
            HonuaAuthenticationSupport.RequiresHttpsForAuthentication(request.RequestUri))
        {
            var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, _serviceName);
            await HonuaAuthenticationSupport.EmitInsecureTransportRejectedDiagnosticAsync(
                _options,
                context,
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(HonuaAuthenticationSupport.InsecureTransportRejectedMessage);
        }

        await HonuaAuthenticationSupport.ApplyHttpCredentialsAsync(
            request,
            _options,
            _serviceName,
            cancellationToken).ConfigureAwait(false);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
