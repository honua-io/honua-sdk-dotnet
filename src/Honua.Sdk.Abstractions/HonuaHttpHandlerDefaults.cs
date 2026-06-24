// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions;

/// <summary>
/// Shared defaults for the primary HTTP message handler used by every Honua SDK
/// REST client registration.
/// </summary>
/// <remarks>
/// <para>
/// Honua REST clients attach the API key as a custom <c>X-API-Key</c> header
/// (and a bearer token via <c>Authorization</c>). <see cref="System.Net.Http.HttpClient"/>
/// strips <c>Authorization</c> on cross-origin auto-redirects, but it forwards
/// custom headers such as <c>X-API-Key</c> to the redirect <c>Location</c> host.
/// An attacker-controlled <c>30x</c> response could therefore exfiltrate the API
/// key to an arbitrary host.
/// </para>
/// <para>
/// To close that hole, every Honua SDK client disables automatic redirect
/// following by default. A <c>30x</c> response surfaces to the caller as a
/// redirect status code (handled like any other non-success response) rather
/// than being silently followed with credentials attached. Consumers that
/// genuinely need redirect following can supply their own
/// <see cref="IHonuaClientOptions.PrimaryHttpMessageHandlerFactory"/> and accept
/// the responsibility for the credential-forwarding semantics that entails.
/// </para>
/// </remarks>
public static class HonuaHttpHandlerDefaults
{
    /// <summary>
    /// Creates the default primary HTTP message handler used when a consumer does
    /// not supply a <see cref="IHonuaClientOptions.PrimaryHttpMessageHandlerFactory"/>.
    /// Automatic redirect following is disabled so the custom <c>X-API-Key</c>
    /// header is never forwarded to a redirect target host.
    /// </summary>
    /// <returns>A fresh <see cref="System.Net.Http.HttpMessageHandler"/> with auto-redirect disabled.</returns>
    public static System.Net.Http.HttpMessageHandler CreateNoRedirectPrimaryHandler()
        => new System.Net.Http.SocketsHttpHandler
        {
            AllowAutoRedirect = false,
        };
}
