// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk.OgcFeatures;

/// <summary>
/// Configuration options for the OGC API Features client.
/// </summary>
public sealed class HonuaOgcFeaturesClientOptions : Honua.Sdk.Abstractions.HonuaClientOptionsBase, IHonuaAuthenticationOptions
{
    /// <summary>
    /// API key for authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional API key provider invoked before each request. When configured,
    /// its value takes precedence over <see cref="ApiKey"/>; returning null or
    /// an empty string omits the API key header.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? ApiKeyProvider { get; set; }

    /// <summary>
    /// Bearer token for authentication.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Optional bearer token provider invoked before each request. When configured,
    /// its value takes precedence over <see cref="BearerToken"/>; returning null or
    /// an empty string omits the authorization header.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; set; }

    /// <summary>
    /// Optional request-aware access token provider. When configured, this value
    /// takes precedence over <see cref="BearerTokenProvider"/> and <see cref="BearerToken"/>.
    /// </summary>
    public IHonuaAccessTokenProvider? AccessTokenProvider { get; set; }

    /// <summary>
    /// Default OAuth/OIDC scopes requested by OGC API Features clients.
    /// </summary>
    public IReadOnlyList<string> AuthenticationScopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Default OAuth/OIDC audience or resource requested by OGC API Features clients.
    /// </summary>
    public string? AuthenticationAudience { get; set; }

    /// <summary>
    /// Optional sanitized authentication diagnostics callback. Raw credential
    /// values are never supplied to this callback.
    /// </summary>
    public HonuaAuthenticationDiagnosticHandler? AuthenticationDiagnostics { get; set; }

    internal static void ValidateBaseAddress(Uri? baseAddress)
        => Honua.Sdk.Abstractions.HonuaClientOptionsValidation.ValidateBaseAddress(baseAddress, "Honua OGC Features");

    internal static void ValidateTimeout(TimeSpan timeout)
        => Honua.Sdk.Abstractions.HonuaClientOptionsValidation.ValidateTimeout(timeout, "Honua OGC Features");

    internal static bool RequiresHttpsForAuthentication(Uri? uri)
        => Honua.Sdk.Abstractions.Authentication.HonuaAuthenticationSupport.RequiresHttpsForAuthentication(uri);

    internal static bool IsLocalDevelopmentHttp(Uri uri)
        => Honua.Sdk.Abstractions.Authentication.HonuaAuthenticationSupport.IsLocalDevelopmentHttp(uri);
}
