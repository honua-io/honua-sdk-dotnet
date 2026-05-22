// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk.Scenes;

/// <summary>
/// Configuration options for the Honua scene metadata client.
/// </summary>
public sealed class HonuaSceneClientOptions : IHonuaAuthenticationOptions, Honua.Sdk.Abstractions.IHonuaClientOptions
{
    /// <summary>
    /// Base address of the Honua server. Required; the SDK no longer assumes a
    /// localhost default so that missing configuration surfaces loudly at
    /// registration time via the static ValidateBaseAddress check.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Server-relative scene metadata API path.
    /// </summary>
    public string SceneApiPath { get; set; } = "/api/scenes";

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
    /// Optional request-aware access token provider. When configured, this value
    /// takes precedence over <see cref="BearerTokenProvider"/> and <see cref="BearerToken"/>.
    /// </summary>
    public IHonuaAccessTokenProvider? AccessTokenProvider { get; set; }

    /// <summary>
    /// Default OAuth/OIDC scopes requested by the scenes client.
    /// </summary>
    public IReadOnlyList<string> AuthenticationScopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Default OAuth/OIDC audience or resource requested by the scenes client.
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
    /// Whether to enable automatic retry on transient HTTP failures.
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Overall timeout for each HTTP request, including retry attempts.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Maximum number of retry attempts. Only applies when <see cref="EnableRetry"/> is true.
    /// </summary>
    public int MaxRetryAttempts
    {
        get => _maxRetryAttempts;
        set
        {
            if (value is < 2 or > 5)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value,
                    "MaxRetryAttempts must be in the inclusive range [2, 5].");
            }
            _maxRetryAttempts = value;
        }
    }

    private int _maxRetryAttempts = 3;

    internal static void ValidateBaseAddress(Uri? baseAddress)
    {
        if (baseAddress is null)
        {
            throw new Honua.Sdk.Abstractions.HonuaConfigurationException("Honua scene base address must be configured.");
        }

        if (!baseAddress.IsAbsoluteUri)
        {
            throw new Honua.Sdk.Abstractions.HonuaConfigurationException("Honua scene base address must be an absolute URI.");
        }

        if (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new Honua.Sdk.Abstractions.HonuaConfigurationException("Honua scene base address must use HTTP or HTTPS.");
        }
    }

    internal static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.FromMilliseconds(10) || timeout >= TimeSpan.FromHours(24))
        {
            throw new Honua.Sdk.Abstractions.HonuaConfigurationException("Honua scene timeout must be greater than 10 milliseconds and less than 24 hours.");
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
