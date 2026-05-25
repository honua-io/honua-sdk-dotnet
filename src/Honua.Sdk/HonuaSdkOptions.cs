// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk;

/// <summary>
/// Aggregate configuration for the <c>Honua.Sdk</c> umbrella / meta package.
/// <see cref="ServiceCollectionExtensions.AddHonua"/> captures a single instance
/// of this type and fans the shared cross-cutting values (base address, auth,
/// retry / timeout, primary HTTP handler) out into every enabled sub-package's
/// own <c>AddHonua*</c> registration.
/// </summary>
/// <remarks>
/// <para>
/// This is the only options class the umbrella exposes. It does <i>not</i>
/// replace any per-package options class (<see cref="Honua.Sdk.Grpc.HonuaGrpcClientOptions"/>,
/// <see cref="Honua.Sdk.Admin.HonuaAdminClientOptions"/>, etc.) — the umbrella
/// translates this snapshot into each sub-package's <c>Action&lt;TOptions&gt;</c>
/// delegate at registration time. Callers who need protocol-specific knobs
/// (gRPC compression negotiation, OGC vector format defaults, etc.) should
/// keep using the per-package <c>AddHonua*</c> extension directly.
/// </para>
/// <para>
/// Module opt-in flags (<see cref="UseGrpc"/>, <see cref="UseAdmin"/>,
/// <see cref="UseGeocoding"/>, <see cref="UseOgcFeatures"/>,
/// <see cref="UseProcesses"/>, and <see cref="UseWfs"/>) default to
/// <c>true</c> for the common query, edit, admin, geocoding, OGC API Features,
/// OGC API Processes, and WFS client set. The more situational sub-packages
/// (<see cref="UseGeoServices"/>, <see cref="UseRouting"/>, <see cref="UseScenes"/>,
/// <see cref="UseSpec"/>, <see cref="UseStac"/>, <see cref="UseOgcRecords"/>)
/// default to <c>false</c> so callers who want the smallest registration footprint
/// don't get every client they don't use.
/// </para>
/// </remarks>
public sealed class HonuaSdkOptions
{
    /// <summary>
    /// Base address of the Honua server, applied to every enabled sub-package's
    /// client options. Required: the SDK does not bake in a localhost default,
    /// so leaving this null causes the first enabled sub-package to fail
    /// validation at registration time.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// API key forwarded to every enabled client. Sub-packages send it as an
    /// API key header (REST) or as gRPC metadata.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// API key provider invoked before each request. Takes precedence over
    /// <see cref="ApiKey"/>. Returning null or an empty string omits the
    /// API key.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? ApiKeyProvider { get; set; }

    /// <summary>
    /// Bearer token forwarded to every enabled client.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Bearer token provider invoked before each request. Takes precedence
    /// over <see cref="BearerToken"/>.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; set; }

    /// <summary>
    /// Request-aware access token provider. Takes precedence over
    /// <see cref="BearerToken"/> and <see cref="BearerTokenProvider"/>.
    /// </summary>
    public IHonuaAccessTokenProvider? AccessTokenProvider { get; set; }

    /// <summary>
    /// Default OAuth / OIDC scopes requested by every enabled client.
    /// </summary>
    public IReadOnlyList<string> AuthenticationScopes { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Default OAuth / OIDC audience or resource requested by every enabled
    /// client.
    /// </summary>
    public string? AuthenticationAudience { get; set; }

    /// <summary>
    /// Optional sanitized authentication diagnostics callback. Raw credential
    /// values are never supplied to this callback. Forwarded to every enabled
    /// client.
    /// </summary>
    public HonuaAuthenticationDiagnosticHandler? AuthenticationDiagnostics { get; set; }

    /// <summary>
    /// Optional primary HTTP message handler factory for certificate, mTLS,
    /// proxy, or enterprise transport configuration. Forwarded to every
    /// enabled client.
    /// </summary>
    public Func<HttpMessageHandler>? PrimaryHttpMessageHandlerFactory { get; set; }

    /// <summary>
    /// Whether to enable the default transient-failure retry policy on every
    /// enabled client. Defaults to <c>true</c>.
    /// </summary>
    public bool EnableRetry { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts (including the original call) applied
    /// to every enabled client. Must be in the inclusive range <c>[2, 5]</c>;
    /// the setter throws <see cref="ArgumentOutOfRangeException"/> for values
    /// outside that range. Only applied when <see cref="EnableRetry"/> is
    /// <c>true</c>. Defaults to 3.
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

    /// <summary>
    /// Overall request budget per call, including retry attempts. Defaults to
    /// 100 seconds. Sub-package options classes additionally enforce
    /// <c>10 ms &lt; Timeout &lt; 24 h</c> at registration time.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>
    /// When <c>true</c>, registers the gRPC FeatureService client
    /// (<c>IHonuaGrpcClient</c>), the native ProcessService job lifecycle
    /// client (<c>IHonuaProcessGrpcClient</c>), and the shared feature query /
    /// edit / attachment interface aliases. Defaults to <c>true</c>.
    /// </summary>
    public bool UseGrpc { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, registers the Admin REST client
    /// (<c>IHonuaAdminClient</c>) and its included <c>IHonuaCatalogClient</c>
    /// catalog-discovery view. Defaults to <c>true</c>.
    /// </summary>
    public bool UseAdmin { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, registers the Geocoding client
    /// (<c>IHonuaGeocodingClient</c> / <c>IHonuaBatchGeocodingClient</c>).
    /// Ships in the Admin package and reuses the Admin options / auth handler.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool UseGeocoding { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, registers the OGC API Features client
    /// (<c>IHonuaOgcFeaturesClient</c>). Defaults to <c>true</c>.
    /// </summary>
    public bool UseOgcFeatures { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, registers the OGC API Processes REST client
    /// (<c>IHonuaProcessesClient</c>). Defaults to <c>true</c>.
    /// </summary>
    public bool UseProcesses { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, registers the WFS 2.0 client (<c>IHonuaWfsClient</c>).
    /// The WFS surface ships inside the <c>Honua.Sdk.OgcFeatures</c> package
    /// after the recent consolidation. Defaults to <c>true</c>.
    /// </summary>
    public bool UseWfs { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, registers the GeoServices FeatureServer client
    /// (<c>IHonuaFeatureServerClient</c>). Defaults to <c>false</c>.
    /// </summary>
    public bool UseGeoServices { get; set; }

    /// <summary>
    /// When <c>true</c>, registers the GeoServices NAServer Routing client
    /// (<c>IHonuaRoutingClient</c>). Ships in the GeoServices package and
    /// reuses its options / auth handler. Defaults to <c>false</c>.
    /// </summary>
    public bool UseRouting { get; set; }

    /// <summary>
    /// When <c>true</c>, registers the scene metadata client
    /// (<c>IHonuaSceneClient</c>). Defaults to <c>false</c>.
    /// </summary>
    public bool UseScenes { get; set; }

    /// <summary>
    /// When <c>true</c>, registers the Spec workspace validate / plan / apply
    /// client (<c>IHonuaSpecClient</c>). Defaults to <c>false</c>.
    /// </summary>
    public bool UseSpec { get; set; }

    /// <summary>
    /// When <c>true</c>, registers the STAC catalog client
    /// (<c>IHonuaStacClient</c>). Defaults to <c>false</c>.
    /// </summary>
    public bool UseStac { get; set; }

    /// <summary>
    /// When <c>true</c>, registers the OGC API Records catalog client
    /// (<c>IHonuaOgcRecordsClient</c>). Defaults to <c>false</c>.
    /// </summary>
    public bool UseOgcRecords { get; set; }

    /// <summary>
    /// When <c>true</c>, registers the Console Studio analysis-report read
    /// client (<c>IHonuaStudioReportsClient</c>). Defaults to <c>false</c>.
    /// </summary>
    public bool UseStudio { get; set; }

    internal bool AnyModuleEnabled =>
        UseGrpc ||
        UseAdmin ||
        UseGeocoding ||
        UseOgcFeatures ||
        UseProcesses ||
        UseWfs ||
        UseGeoServices ||
        UseRouting ||
        UseScenes ||
        UseSpec ||
        UseStac ||
        UseOgcRecords ||
        UseStudio;
}
