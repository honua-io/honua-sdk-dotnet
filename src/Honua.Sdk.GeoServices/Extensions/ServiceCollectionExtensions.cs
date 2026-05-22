// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

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

        var snapshot = new HonuaGeoServicesClientOptions();
        configure(snapshot);
        HonuaGeoServicesClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaGeoServicesClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaGeoServicesClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaGeoServicesAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaFeatureServerClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaGeoServicesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaGeoServicesAuthHandler>();
        services.AddTransient<IHonuaFeatureServerClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureServerEditClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureQueryClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureEditClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureAttachmentClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());

        ApplyHandlerAndResilience(httpBuilder, snapshot);
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

        var snapshot = new HonuaGeoServicesClientOptions();
        configure(snapshot);
        HonuaGeoServicesClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaGeoServicesClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaGeoServicesClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaGeoServicesAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaRoutingClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaGeoServicesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaGeoServicesAuthHandler>();
        services.AddTransient<IHonuaRoutingClient>(sp => sp.GetRequiredService<HonuaRoutingClient>());

        ApplyHandlerAndResilience(httpBuilder, snapshot);
        return services;
    }

    private static void ApplyHandlerAndResilience(
        IHttpClientBuilder httpBuilder,
        HonuaGeoServicesClientOptions snapshot)
    {
        if (snapshot.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            httpBuilder.ConfigurePrimaryHttpMessageHandler(primaryHandlerFactory);
        }

        if (snapshot.EnableRetry)
        {
            httpBuilder.AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = snapshot.Timeout;
                options.AttemptTimeout.Timeout = snapshot.Timeout;
                options.Retry.MaxRetryAttempts = snapshot.MaxRetryAttempts;
                options.Retry.ShouldHandle = args => ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
                options.Retry.DisableForUnsafeHttpMethods();
                options.Retry.UseJitter = true;
            });
        }
    }
}
