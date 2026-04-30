// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk.GeoServices;

/// <summary>
/// Configuration options shared by the FeatureServer and OGC Features clients.
/// </summary>
public sealed class HonuaGeoServicesClientOptions : IHonuaAuthenticationOptions
{
    /// <summary>
    /// Base address of the Honua server.
    /// </summary>
    public Uri BaseAddress { get; set; } = new("https://localhost:5001");

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
    /// Default OAuth/OIDC scopes requested by GeoServices clients.
    /// </summary>
    public IReadOnlyList<string> AuthenticationScopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Default OAuth/OIDC audience or resource requested by GeoServices clients.
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
    /// Whether to enable automatic retry on transient HTTP failures (default: true).
    /// Retries on 429 (Too Many Requests), 502 (Bad Gateway), and 503 (Service Unavailable).
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Overall timeout for each HTTP request, including retry attempts (default: 100 seconds).
    /// Must be greater than 10 milliseconds and less than 24 hours.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// Maximum number of retry attempts (default: 3, range 2-5).
    /// Only applies when <see cref="EnableRetry"/> is true.
    /// </summary>
    public int MaxRetryAttempts
    {
        get => _maxRetryAttempts;
        set => _maxRetryAttempts = Math.Clamp(value, 2, 5);
    }

    private int _maxRetryAttempts = 3;

    /// <summary>
    /// Default GeoServices NAServer service id used by routing clients.
    /// </summary>
    public string RoutingServiceId { get; set; } = "Routing";

    /// <summary>
    /// Default NAServer route layer name used for directions and route optimization.
    /// </summary>
    public string RoutingRouteLayerName { get; set; } = "Route";

    /// <summary>
    /// Default NAServer service-area layer name used for isochrone requests.
    /// </summary>
    public string RoutingServiceAreaLayerName { get; set; } = "ServiceArea";

    /// <summary>
    /// Default NAServer closest-facility layer name used for nearest-facility requests.
    /// </summary>
    public string RoutingClosestFacilityLayerName { get; set; } = "ClosestFacility";

    internal static void ValidateBaseAddress(Uri? baseAddress)
    {
        if (baseAddress is null)
        {
            throw new InvalidOperationException("Honua GeoServices base address must be configured.");
        }

        if (!baseAddress.IsAbsoluteUri)
        {
            throw new InvalidOperationException("Honua GeoServices base address must be an absolute URI.");
        }

        if (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Honua GeoServices base address must use HTTP or HTTPS.");
        }
    }

    internal static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.FromMilliseconds(10) || timeout >= TimeSpan.FromHours(24))
        {
            throw new InvalidOperationException(
                "Honua GeoServices timeout must be greater than 10 milliseconds and less than 24 hours.");
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
