// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk.Grpc;

/// <summary>
/// Configuration options for the Honua gRPC client.
/// </summary>
public sealed class HonuaGrpcClientOptions : Honua.Sdk.Abstractions.HonuaClientOptionsBase, IHonuaAuthenticationOptions
{
    /// <summary>
    /// API key for authentication (sent as grpc-metadata header).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Optional API key provider invoked before each RPC. When configured,
    /// its value takes precedence over <see cref="ApiKey"/>; returning null or
    /// an empty string omits the API key metadata entry.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? ApiKeyProvider { get; set; }

    /// <summary>
    /// Bearer token for authentication.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Optional bearer token provider invoked before each RPC. When configured,
    /// its value takes precedence over <see cref="BearerToken"/>; returning null or
    /// an empty string omits the authorization metadata entry.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; set; }

    /// <summary>
    /// Optional request-aware access token provider. When configured, this value
    /// takes precedence over <see cref="BearerTokenProvider"/> and <see cref="BearerToken"/>.
    /// </summary>
    public IHonuaAccessTokenProvider? AccessTokenProvider { get; set; }

    /// <summary>
    /// Default OAuth/OIDC scopes requested by the gRPC client.
    /// </summary>
    public IReadOnlyList<string> AuthenticationScopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Default OAuth/OIDC audience or resource requested by the gRPC client.
    /// </summary>
    public string? AuthenticationAudience { get; set; }

    /// <summary>
    /// Optional sanitized authentication diagnostics callback. Raw credential
    /// values are never supplied to this callback.
    /// </summary>
    public HonuaAuthenticationDiagnosticHandler? AuthenticationDiagnostics { get; set; }

    /// <summary>
    /// Enables gRPC compression negotiation for responses.
    /// </summary>
    public bool EnableCompressionNegotiation { get; set; } = true;

    /// <summary>
    /// Accepted gRPC compression algorithms advertised to the server.
    /// </summary>
    public string AcceptedCompressionEncodings { get; set; } = "gzip,identity";

    internal static Uri ParseAndValidateAddress(HonuaGrpcClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.BaseAddress is not { } baseAddress)
        {
            throw new Honua.Sdk.Abstractions.HonuaConfigurationException(
                "Honua gRPC address must be configured. Set HonuaGrpcClientOptions.BaseAddress " +
                "to your Honua server's URL.");
        }

        return ValidateUri(baseAddress);
    }

    private static Uri ValidateUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri)
        {
            throw new Honua.Sdk.Abstractions.HonuaConfigurationException("Honua gRPC address must be an absolute URI.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new Honua.Sdk.Abstractions.HonuaConfigurationException("Honua gRPC address must use HTTP or HTTPS.");
        }

        return uri;
    }

    internal static void ValidateTimeout(TimeSpan timeout)
        => Honua.Sdk.Abstractions.HonuaClientOptionsValidation.ValidateTimeout(timeout, "Honua gRPC");

    internal static bool RequiresHttpsForAuthentication(Uri? uri)
        => Honua.Sdk.Abstractions.Authentication.HonuaAuthenticationSupport.RequiresHttpsForAuthentication(uri);

    internal static bool IsLocalDevelopmentHttp(Uri uri)
        => Honua.Sdk.Abstractions.Authentication.HonuaAuthenticationSupport.IsLocalDevelopmentHttp(uri);
}
