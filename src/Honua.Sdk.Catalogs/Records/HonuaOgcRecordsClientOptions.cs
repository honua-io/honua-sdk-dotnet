// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk.Catalogs.Records;

/// <summary>
/// Configuration options for the OGC API Records client.
/// </summary>
public sealed class HonuaOgcRecordsClientOptions : Honua.Sdk.Abstractions.HonuaClientOptionsBase, IHonuaAuthenticationOptions
{
    /// <summary>
    /// API key for authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional API key provider invoked before each request.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? ApiKeyProvider { get; set; }

    /// <summary>
    /// Bearer token for authentication.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Optional bearer token provider invoked before each request.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; set; }

    /// <summary>
    /// Optional request-aware access token provider.
    /// </summary>
    public IHonuaAccessTokenProvider? AccessTokenProvider { get; set; }

    /// <summary>
    /// Default OAuth/OIDC scopes requested by OGC API Records clients.
    /// </summary>
    public IReadOnlyList<string> AuthenticationScopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Default OAuth/OIDC audience or resource requested by OGC API Records clients.
    /// </summary>
    public string? AuthenticationAudience { get; set; }

    /// <summary>
    /// Optional sanitized authentication diagnostics callback.
    /// </summary>
    public HonuaAuthenticationDiagnosticHandler? AuthenticationDiagnostics { get; set; }

    internal static void ValidateBaseAddress(Uri? baseAddress)
        => Honua.Sdk.Abstractions.HonuaClientOptionsValidation.ValidateBaseAddress(baseAddress, "Honua OGC Records");

    internal static void ValidateTimeout(TimeSpan timeout)
        => Honua.Sdk.Abstractions.HonuaClientOptionsValidation.ValidateTimeout(timeout, "Honua OGC Records");

    internal static bool RequiresHttpsForAuthentication(Uri? uri)
        => Honua.Sdk.Abstractions.Authentication.HonuaAuthenticationSupport.RequiresHttpsForAuthentication(uri);

    internal static bool IsLocalDevelopmentHttp(Uri uri)
        => Honua.Sdk.Abstractions.Authentication.HonuaAuthenticationSupport.IsLocalDevelopmentHttp(uri);
}
