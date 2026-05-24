// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.GeoServices;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.Grpc;
using Honua.Sdk.Grpc.Extensions;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.Catalogs.Records;
using Honua.Sdk.Catalogs.Records.Extensions;
using Honua.Sdk.Scenes;
using Honua.Sdk.Scenes.Extensions;
using Honua.Sdk.Spec;
using Honua.Sdk.Spec.Extensions;
using Honua.Sdk.Catalogs.Stac;
using Honua.Sdk.Catalogs.Stac.Extensions;
using Honua.Sdk.OgcFeatures.Wfs;
using Honua.Sdk.OgcFeatures.Wfs.Extensions;
using Honua.Sdk.Processes;
using Honua.Sdk.Processes.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk;

/// <summary>
/// Umbrella extension methods that register every Honua SDK client enabled by
/// a single <see cref="HonuaSdkOptions"/> snapshot. The per-package
/// <c>AddHonua*</c> extensions remain available and unchanged for callers who
/// want explicit, narrow control.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers every Honua SDK client enabled on the supplied
    /// <see cref="HonuaSdkOptions"/> snapshot. The configuration delegate is
    /// invoked exactly once to capture the snapshot; the snapshot's shared
    /// values (base address, authentication, retry / timeout, primary HTTP
    /// handler) are forwarded into each enabled sub-package's own
    /// <c>AddHonua*</c> extension. Per-package extensions remain available
    /// and unchanged for callers who want explicit, narrow control.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for the umbrella options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/>
    /// is <c>null</c>.
    /// </exception>
    /// <exception cref="HonuaConfigurationException">
    /// Thrown when every <c>Use*</c> flag is <c>false</c>: the umbrella has no
    /// modules to register and the caller almost certainly meant to enable at
    /// least one.
    /// </exception>
    public static IServiceCollection AddHonua(
        this IServiceCollection services,
        Action<HonuaSdkOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new HonuaSdkOptions();
        configure(snapshot);

        if (!snapshot.AnyModuleEnabled)
        {
            throw new HonuaConfigurationException(
                "Configure at least one Honua module via AddHonua(...) before resolving clients. " +
                "Every Use* flag (UseGrpc, UseAdmin, UseGeocoding, UseOgcFeatures, UseWfs, UseGeoServices, " +
                "UseProcesses, UseRouting, UseScenes, UseSpec, UseStac, UseOgcRecords) is currently false.");
        }

        if (snapshot.UseGrpc)
        {
            services.AddHonuaGrpc(options => ApplyGrpc(options, snapshot));
        }

        if (snapshot.UseAdmin)
        {
            services.AddHonuaAdmin(options => ApplyAdmin(options, snapshot));
        }

        if (snapshot.UseGeocoding)
        {
            services.AddHonuaGeocoding(options => ApplyAdmin(options, snapshot));
        }

        if (snapshot.UseOgcFeatures)
        {
            services.AddHonuaOgcFeatures(options => ApplyOgcFeatures(options, snapshot));
        }

        if (snapshot.UseProcesses)
        {
            services.AddHonuaProcesses(options => ApplyProcesses(options, snapshot));
        }

        if (snapshot.UseWfs)
        {
            services.AddHonuaWfs(options => ApplyWfs(options, snapshot));
        }

        if (snapshot.UseGeoServices)
        {
            services.AddHonuaFeatureServer(options => ApplyGeoServices(options, snapshot));
        }

        if (snapshot.UseRouting)
        {
            services.AddHonuaRouting(options => ApplyGeoServices(options, snapshot));
        }

        if (snapshot.UseScenes)
        {
            services.AddHonuaScenes(options => ApplyScenes(options, snapshot));
        }

        if (snapshot.UseSpec)
        {
            services.AddHonuaSpec(options => ApplySpec(options, snapshot));
        }

        if (snapshot.UseStac)
        {
            services.AddHonuaStac(options => ApplyStac(options, snapshot));
        }

        if (snapshot.UseOgcRecords)
        {
            services.AddHonuaOgcRecords(options => ApplyOgcRecords(options, snapshot));
        }

        return services;
    }

    private static void ApplyGrpc(HonuaGrpcClientOptions target, HonuaSdkOptions source)
    {
        target.BaseAddress = source.BaseAddress;
        target.ApiKey = source.ApiKey;
        target.ApiKeyProvider = source.ApiKeyProvider;
        target.BearerToken = source.BearerToken;
        target.BearerTokenProvider = source.BearerTokenProvider;
        target.AccessTokenProvider = source.AccessTokenProvider;
        target.AuthenticationScopes = source.AuthenticationScopes;
        target.AuthenticationAudience = source.AuthenticationAudience;
        target.AuthenticationDiagnostics = source.AuthenticationDiagnostics;
        target.PrimaryHttpMessageHandlerFactory = source.PrimaryHttpMessageHandlerFactory;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Timeout = source.Timeout;
    }

    private static void ApplyAdmin(HonuaAdminClientOptions target, HonuaSdkOptions source)
    {
        target.BaseAddress = source.BaseAddress;
        target.ApiKey = source.ApiKey;
        target.ApiKeyProvider = source.ApiKeyProvider;
        target.BearerToken = source.BearerToken;
        target.BearerTokenProvider = source.BearerTokenProvider;
        target.AccessTokenProvider = source.AccessTokenProvider;
        target.AuthenticationScopes = source.AuthenticationScopes;
        target.AuthenticationAudience = source.AuthenticationAudience;
        target.AuthenticationDiagnostics = source.AuthenticationDiagnostics;
        target.PrimaryHttpMessageHandlerFactory = source.PrimaryHttpMessageHandlerFactory;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Timeout = source.Timeout;
    }

    private static void ApplyOgcFeatures(HonuaOgcFeaturesClientOptions target, HonuaSdkOptions source)
    {
        target.BaseAddress = source.BaseAddress;
        target.ApiKey = source.ApiKey;
        target.ApiKeyProvider = source.ApiKeyProvider;
        target.BearerToken = source.BearerToken;
        target.BearerTokenProvider = source.BearerTokenProvider;
        target.AccessTokenProvider = source.AccessTokenProvider;
        target.AuthenticationScopes = source.AuthenticationScopes;
        target.AuthenticationAudience = source.AuthenticationAudience;
        target.AuthenticationDiagnostics = source.AuthenticationDiagnostics;
        target.PrimaryHttpMessageHandlerFactory = source.PrimaryHttpMessageHandlerFactory;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Timeout = source.Timeout;
    }

    private static void ApplyWfs(HonuaWfsClientOptions target, HonuaSdkOptions source)
    {
        target.BaseAddress = source.BaseAddress;
        target.ApiKey = source.ApiKey;
        target.ApiKeyProvider = source.ApiKeyProvider;
        target.BearerToken = source.BearerToken;
        target.BearerTokenProvider = source.BearerTokenProvider;
        target.AccessTokenProvider = source.AccessTokenProvider;
        target.AuthenticationScopes = source.AuthenticationScopes;
        target.AuthenticationAudience = source.AuthenticationAudience;
        target.AuthenticationDiagnostics = source.AuthenticationDiagnostics;
        target.PrimaryHttpMessageHandlerFactory = source.PrimaryHttpMessageHandlerFactory;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Timeout = source.Timeout;
    }

    private static void ApplyProcesses(HonuaProcessesClientOptions target, HonuaSdkOptions source)
    {
        target.BaseAddress = source.BaseAddress;
        target.ApiKey = source.ApiKey;
        target.ApiKeyProvider = source.ApiKeyProvider;
        target.BearerToken = source.BearerToken;
        target.BearerTokenProvider = source.BearerTokenProvider;
        target.AccessTokenProvider = source.AccessTokenProvider;
        target.AuthenticationScopes = source.AuthenticationScopes;
        target.AuthenticationAudience = source.AuthenticationAudience;
        target.AuthenticationDiagnostics = source.AuthenticationDiagnostics;
        target.PrimaryHttpMessageHandlerFactory = source.PrimaryHttpMessageHandlerFactory;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Timeout = source.Timeout;
    }

    private static void ApplyGeoServices(HonuaGeoServicesClientOptions target, HonuaSdkOptions source)
    {
        target.BaseAddress = source.BaseAddress;
        target.ApiKey = source.ApiKey;
        target.ApiKeyProvider = source.ApiKeyProvider;
        target.BearerToken = source.BearerToken;
        target.BearerTokenProvider = source.BearerTokenProvider;
        target.AccessTokenProvider = source.AccessTokenProvider;
        target.AuthenticationScopes = source.AuthenticationScopes;
        target.AuthenticationAudience = source.AuthenticationAudience;
        target.AuthenticationDiagnostics = source.AuthenticationDiagnostics;
        target.PrimaryHttpMessageHandlerFactory = source.PrimaryHttpMessageHandlerFactory;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Timeout = source.Timeout;
    }

    private static void ApplyScenes(HonuaSceneClientOptions target, HonuaSdkOptions source)
    {
        target.BaseAddress = source.BaseAddress;
        target.ApiKey = source.ApiKey;
        target.ApiKeyProvider = source.ApiKeyProvider;
        target.BearerToken = source.BearerToken;
        target.BearerTokenProvider = source.BearerTokenProvider;
        target.AccessTokenProvider = source.AccessTokenProvider;
        target.AuthenticationScopes = source.AuthenticationScopes;
        target.AuthenticationAudience = source.AuthenticationAudience;
        target.AuthenticationDiagnostics = source.AuthenticationDiagnostics;
        target.PrimaryHttpMessageHandlerFactory = source.PrimaryHttpMessageHandlerFactory;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Timeout = source.Timeout;
    }

    private static void ApplySpec(HonuaSpecClientOptions target, HonuaSdkOptions source)
    {
        target.BaseAddress = source.BaseAddress;
        target.ApiKey = source.ApiKey;
        target.ApiKeyProvider = source.ApiKeyProvider;
        target.BearerToken = source.BearerToken;
        target.BearerTokenProvider = source.BearerTokenProvider;
        target.AccessTokenProvider = source.AccessTokenProvider;
        target.AuthenticationScopes = source.AuthenticationScopes;
        target.AuthenticationAudience = source.AuthenticationAudience;
        target.AuthenticationDiagnostics = source.AuthenticationDiagnostics;
        target.PrimaryHttpMessageHandlerFactory = source.PrimaryHttpMessageHandlerFactory;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Timeout = source.Timeout;
    }

    private static void ApplyStac(HonuaStacClientOptions target, HonuaSdkOptions source)
    {
        target.BaseAddress = source.BaseAddress;
        target.ApiKey = source.ApiKey;
        target.ApiKeyProvider = source.ApiKeyProvider;
        target.BearerToken = source.BearerToken;
        target.BearerTokenProvider = source.BearerTokenProvider;
        target.AccessTokenProvider = source.AccessTokenProvider;
        target.AuthenticationScopes = source.AuthenticationScopes;
        target.AuthenticationAudience = source.AuthenticationAudience;
        target.AuthenticationDiagnostics = source.AuthenticationDiagnostics;
        target.PrimaryHttpMessageHandlerFactory = source.PrimaryHttpMessageHandlerFactory;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Timeout = source.Timeout;
    }

    private static void ApplyOgcRecords(HonuaOgcRecordsClientOptions target, HonuaSdkOptions source)
    {
        target.BaseAddress = source.BaseAddress;
        target.ApiKey = source.ApiKey;
        target.ApiKeyProvider = source.ApiKeyProvider;
        target.BearerToken = source.BearerToken;
        target.BearerTokenProvider = source.BearerTokenProvider;
        target.AccessTokenProvider = source.AccessTokenProvider;
        target.AuthenticationScopes = source.AuthenticationScopes;
        target.AuthenticationAudience = source.AuthenticationAudience;
        target.AuthenticationDiagnostics = source.AuthenticationDiagnostics;
        target.PrimaryHttpMessageHandlerFactory = source.PrimaryHttpMessageHandlerFactory;
        target.EnableRetry = source.EnableRetry;
        target.MaxRetryAttempts = source.MaxRetryAttempts;
        target.Timeout = source.Timeout;
    }
}
