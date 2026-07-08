// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.GeometryServer;
using Honua.Sdk.GeoServices.ImageServer;
using Honua.Sdk.GeoServices.Routing;
using Honua.Sdk.Internal.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace Honua.Sdk.GeoServices.Extensions;

/// <summary>
/// Extension methods for registering Honua GeoServices clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua FeatureServer client and related services with the DI container.
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to capture
    /// options; primary HTTP handler and resilience pipeline configuration are derived from
    /// that snapshot.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaFeatureServer(
        this IServiceCollection services,
        Action<HonuaGeoServicesClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = CaptureSnapshot(configure);

        TryAddGeoServicesOptions(services, snapshot);
        var httpBuilder = services.AddHttpClient<HonuaFeatureServerClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaGeoServicesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaGeoServicesAuthHandler>();
        services.AddTransient<IHonuaFeatureServerClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureServerEditClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureQueryClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureEditClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureAttachmentClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot, ConfigureGeoServicesRetry);
        return services;
    }

    /// <summary>
    /// Registers the Honua GeoServices NAServer routing client with the DI container.
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to capture
    /// options; primary HTTP handler and resilience pipeline configuration are derived from
    /// that snapshot.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaRouting(
        this IServiceCollection services,
        Action<HonuaGeoServicesClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = CaptureSnapshot(configure);

        TryAddGeoServicesOptions(services, snapshot);
        var httpBuilder = services.AddHttpClient<HonuaRoutingClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaGeoServicesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaGeoServicesAuthHandler>();
        services.AddTransient<IHonuaRoutingClient>(sp => sp.GetRequiredService<HonuaRoutingClient>());

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot, ConfigureGeoServicesRetry);
        return services;
    }

    /// <summary>
    /// Registers the Honua GeoServices ImageServer client with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaImageServer(
        this IServiceCollection services,
        Action<HonuaGeoServicesClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = CaptureSnapshot(configure);

        TryAddGeoServicesOptions(services, snapshot);
        var httpBuilder = services.AddHttpClient<HonuaImageServerClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaGeoServicesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaGeoServicesAuthHandler>();

        // Provider-neutral raster data client (metadata, coverage statistics, windowed
        // reads) so a raster geoprocessing tool can resolve IHonuaRasterDataClient from DI.
        services.AddTransient<HonuaImageServerRasterDataClient>();
        services.AddTransient<Honua.Sdk.Abstractions.Data.IHonuaRasterDataClient>(
            sp => sp.GetRequiredService<HonuaImageServerRasterDataClient>());

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot, ConfigureGeoServicesRetry);
        return services;
    }

    /// <summary>
    /// Registers the Honua GeoServices GeometryServer client with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaGeometryServer(
        this IServiceCollection services,
        Action<HonuaGeoServicesClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = CaptureSnapshot(configure);

        TryAddGeoServicesOptions(services, snapshot);
        var httpBuilder = services.AddHttpClient<HonuaGeometryServerClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaGeoServicesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaGeoServicesAuthHandler>();

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot, ConfigureGeoServicesRetry);
        return services;
    }

    private static void ConfigureGeoServicesRetry(HttpRetryStrategyOptions retry)
    {
        retry.ShouldHandle = args =>
        {
            if (!HttpClientResiliencePredicates.IsTransient(args.Outcome))
            {
                return ValueTask.FromResult(false);
            }

            // Retry idempotent methods plus the idempotent /query POST fallback, so that a
            // long filter string (which forces GET->POST) does not silently lose retry.
            // Genuine mutations (applyEdits, attachment edits) remain excluded. Replaces
            // DisableForUnsafeHttpMethods(), which would drop the /query POST.
            return ValueTask.FromResult(GeoServicesRetryPolicy.IsRetryableRequest(args.Context.GetRequestMessage()));
        };
    }

    private static HonuaGeoServicesClientOptions CaptureSnapshot(Action<HonuaGeoServicesClientOptions> configure)
    {
        var snapshot = new HonuaGeoServicesClientOptions();
        configure(snapshot);
        HonuaGeoServicesClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaGeoServicesClientOptions.ValidateTimeout(snapshot.Timeout);
        return snapshot;
    }

    private static void TryAddGeoServicesOptions(IServiceCollection services, HonuaGeoServicesClientOptions snapshot)
    {
        // TryAdd: if a sibling GeoServices extension (FeatureServer, Routing,
        // ImageServer, or GeometryServer) already registered shared GeoServices
        // options, share that snapshot rather than silently overwriting it.
        // The first AddHonua* wins; subsequent registrations must use compatible
        // BaseAddress / auth configuration.
        services.TryAddSingleton<IOptions<HonuaGeoServicesClientOptions>>(Options.Create(snapshot));
        services.TryAddTransient<HonuaGeoServicesAuthHandler>();
    }
}
