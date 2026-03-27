// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Features.FeatureServer;
using Honua.Sdk.Features.OgcFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Features.Extensions;

/// <summary>
/// Extension methods for registering Honua feature clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua FeatureServer client and related services with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaFeatureServer(
        this IServiceCollection services,
        Action<HonuaFeaturesClientOptions> configure)
    {
        services.Configure(configure);
        services.AddTransient<HonuaFeaturesAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaFeatureServerClient, HonuaFeatureServerClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaFeaturesClientOptions>>().Value;
            HonuaFeaturesClientOptions.ValidateBaseAddress(options.BaseAddress);
            client.BaseAddress = options.BaseAddress;
        })
        .AddHttpMessageHandler<HonuaFeaturesAuthHandler>();
        ConfigureResilience(httpBuilder, configure);
        return services;
    }

    /// <summary>
    /// Registers the Honua OGC Features client and related services with the DI container.
    /// Uses the same <see cref="HonuaFeaturesClientOptions"/> and <see cref="HonuaFeaturesAuthHandler"/>
    /// as the FeatureServer client for authentication and base address configuration.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaOgcFeatures(
        this IServiceCollection services,
        Action<HonuaFeaturesClientOptions> configure)
    {
        services.Configure(configure);
        services.AddTransient<HonuaFeaturesAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaOgcFeaturesClient, HonuaOgcFeaturesClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaFeaturesClientOptions>>().Value;
            HonuaFeaturesClientOptions.ValidateBaseAddress(options.BaseAddress);
            client.BaseAddress = options.BaseAddress;
        })
        .AddHttpMessageHandler<HonuaFeaturesAuthHandler>();
        ConfigureResilience(httpBuilder, configure);
        return services;
    }

    private static void ConfigureResilience(
        IHttpClientBuilder httpBuilder,
        Action<HonuaFeaturesClientOptions> configure)
    {
        var opts = new HonuaFeaturesClientOptions();
        configure(opts);

        if (!opts.EnableRetry)
        {
            return;
        }

        httpBuilder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = opts.MaxRetryAttempts;
            options.Retry.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result?.StatusCode is
                    HttpStatusCode.TooManyRequests or
                    HttpStatusCode.BadGateway or
                    HttpStatusCode.ServiceUnavailable);
            options.Retry.UseJitter = true;
        });
    }
}
