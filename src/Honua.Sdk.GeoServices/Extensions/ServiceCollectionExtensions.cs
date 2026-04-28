// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.FeatureServer;
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

        services.Configure(configure);
        services.AddTransient<HonuaGeoServicesAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaFeatureServerClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaGeoServicesClientOptions>>().Value;
            HonuaGeoServicesClientOptions.ValidateBaseAddress(options.BaseAddress);
            HonuaGeoServicesClientOptions.ValidateTimeout(options.Timeout);
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaGeoServicesAuthHandler>();
        services.AddTransient<IHonuaFeatureServerClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureServerEditClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureQueryClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        services.AddTransient<IHonuaFeatureEditClient>(sp => sp.GetRequiredService<HonuaFeatureServerClient>());
        ConfigureResilience(httpBuilder, configure);
        return services;
    }

    private static void ConfigureResilience(
        IHttpClientBuilder httpBuilder,
        Action<HonuaGeoServicesClientOptions> configure)
    {
        var opts = new HonuaGeoServicesClientOptions();
        configure(opts);
        HonuaGeoServicesClientOptions.ValidateTimeout(opts.Timeout);

        if (!opts.EnableRetry)
        {
            return;
        }

        httpBuilder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = opts.Timeout;
            options.AttemptTimeout.Timeout = opts.Timeout;
            options.Retry.MaxRetryAttempts = opts.MaxRetryAttempts;
            options.Retry.ShouldHandle = args => ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
            options.Retry.DisableForUnsafeHttpMethods();
            options.Retry.UseJitter = true;
        });
    }
}
