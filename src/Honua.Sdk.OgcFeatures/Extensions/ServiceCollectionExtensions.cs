// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Internal.Http;
using Honua.Sdk.OgcFeatures.Styles;
using Honua.Sdk.OgcFeatures.Tiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.OgcFeatures.Extensions;

/// <summary>
/// Extension methods for registering Honua OGC API Features clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua OGC API Features client and related services with the DI container.
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to capture
    /// options; primary HTTP handler and resilience pipeline configuration are derived from
    /// that snapshot, so side-effecting <paramref name="configure"/> delegates are safe.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaOgcFeatures(
        this IServiceCollection services,
        Action<HonuaOgcFeaturesClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new HonuaOgcFeaturesClientOptions();
        configure(snapshot);
        HonuaOgcFeaturesClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaOgcFeaturesClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaOgcFeaturesClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaOgcFeaturesAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaOgcFeaturesClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaOgcFeaturesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaOgcFeaturesAuthHandler>();
        services.AddTransient<IHonuaOgcFeaturesClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaOgcFeaturesEditClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaOgcFeaturesPatchClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaFeatureQueryClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaFeatureEditClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaFeatureAttachmentClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot);

        var stylesBuilder = services.AddHttpClient<HonuaOgcStylesClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaOgcFeaturesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaOgcFeaturesAuthHandler>();
        services.AddTransient<IHonuaOgcStylesClient>(sp => sp.GetRequiredService<HonuaOgcStylesClient>());

        stylesBuilder.ConfigureHonuaRestHttpClient(snapshot);

        return services;
    }

    /// <summary>
    /// Registers the Honua OGC API Tiles client. The client shares
    /// <see cref="HonuaOgcFeaturesClientOptions"/> and the OGC features auth handler
    /// with <see cref="AddHonuaOgcFeatures"/>, and can be registered on its own.
    /// When the features client is already registered, that options snapshot is kept.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaOgcTiles(
        this IServiceCollection services,
        Action<HonuaOgcFeaturesClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new HonuaOgcFeaturesClientOptions();
        configure(snapshot);
        HonuaOgcFeaturesClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaOgcFeaturesClientOptions.ValidateTimeout(snapshot.Timeout);

        // Features registration owns the shared options snapshot when both are on.
        // Tiles-only registration still has to publish one so the auth handler resolves.
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IOptions<HonuaOgcFeaturesClientOptions>)))
        {
            services.AddSingleton<IOptions<HonuaOgcFeaturesClientOptions>>(Options.Create(snapshot));
        }

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(HonuaOgcFeaturesAuthHandler)))
        {
            services.AddTransient<HonuaOgcFeaturesAuthHandler>();
        }

        var httpBuilder = services.AddHttpClient<HonuaOgcTilesClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaOgcFeaturesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaOgcFeaturesAuthHandler>();
        services.AddTransient<IHonuaOgcTilesClient>(sp => sp.GetRequiredService<HonuaOgcTilesClient>());

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot);

        return services;
    }
}
