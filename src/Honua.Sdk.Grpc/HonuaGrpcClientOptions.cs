// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk.Grpc;

/// <summary>
/// Configuration options for the Honua gRPC client.
/// </summary>
public sealed class HonuaGrpcClientOptions : IHonuaAuthenticationOptions
{
    /// <summary>
    /// Address of the Honua gRPC server.
    /// </summary>
    public string Address { get; set; } = "https://localhost:5001";

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
    /// Optional primary HTTP message handler factory for certificate, mTLS, or
    /// enterprise transport configuration.
    /// </summary>
    public Func<HttpMessageHandler>? PrimaryHttpMessageHandlerFactory { get; set; }

    /// <summary>
    /// Enables gRPC compression negotiation for responses.
    /// </summary>
    public bool EnableCompressionNegotiation { get; set; } = true;

    /// <summary>
    /// Accepted gRPC compression algorithms advertised to the server.
    /// </summary>
    public string AcceptedCompressionEncodings { get; set; } = "gzip,identity";

    /// <summary>
    /// Enables the default retry policy for transient gRPC failures
    /// (Unavailable, Internal). Defaults to <c>true</c>.
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Overall deadline applied to each gRPC call, including retry attempts (default: 100 seconds).
    /// Must be greater than 10 milliseconds and less than 24 hours.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Maximum number of retry attempts (including the original call).
    /// Only used when <see cref="EnableRetry"/> is <c>true</c>. Defaults to 3.
    /// Must be between 2 and 5 inclusive.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    internal static Uri ParseAndValidateAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new InvalidOperationException("Honua gRPC address must be configured.");
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("Honua gRPC address must be an absolute URI.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Honua gRPC address must use HTTP or HTTPS.");
        }

        return uri;
    }

    internal static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.FromMilliseconds(10) || timeout >= TimeSpan.FromHours(24))
        {
            throw new InvalidOperationException(
                "Honua gRPC timeout must be greater than 10 milliseconds and less than 24 hours.");
        }
    }

    internal static bool RequiresHttpsForAuthentication(Uri? uri)
    {
        if (uri is null)
        {
            return true;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsLocalDevelopmentHttp(uri);
    }

    internal static bool IsLocalDevelopmentHttp(Uri uri)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.IsLoopback ||
               string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }
}
