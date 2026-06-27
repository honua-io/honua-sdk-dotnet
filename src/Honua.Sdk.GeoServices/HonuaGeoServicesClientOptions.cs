// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk.GeoServices;

/// <summary>
/// Configuration options shared by the FeatureServer and OGC Features clients.
/// </summary>
public sealed class HonuaGeoServicesClientOptions : Honua.Sdk.Abstractions.HonuaClientOptionsBase, IHonuaAuthenticationOptions
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

    /// <summary>
    /// Default GeoServices GeometryServer service id used by the geometry client.
    /// </summary>
    public string GeometryServiceId { get; set; } = "Geometry";

    internal static void ValidateBaseAddress(Uri? baseAddress)
        => Honua.Sdk.Abstractions.HonuaClientOptionsValidation.ValidateBaseAddress(baseAddress, "Honua GeoServices");

    internal static void ValidateTimeout(TimeSpan timeout)
        => Honua.Sdk.Abstractions.HonuaClientOptionsValidation.ValidateTimeout(timeout, "Honua GeoServices");

    internal static bool RequiresHttpsForAuthentication(Uri? uri)
        => Honua.Sdk.Abstractions.Authentication.HonuaAuthenticationSupport.RequiresHttpsForAuthentication(uri);

    internal static bool IsLocalDevelopmentHttp(Uri uri)
        => Honua.Sdk.Abstractions.Authentication.HonuaAuthenticationSupport.IsLocalDevelopmentHttp(uri);
}
