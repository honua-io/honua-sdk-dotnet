// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Geocoding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Admin.Extensions;

/// <summary>
/// Extension methods for registering the Honua Admin client with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua Admin client and related services with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaAdmin(
        this IServiceCollection services,
        Action<HonuaAdminClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddTransient<HonuaAdminAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaAdminClient, HonuaAdminClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaAdminClientOptions>>().Value;
            HonuaAdminClientOptions.ValidateBaseAddress(options.BaseAddress);
            HonuaAdminClientOptions.ValidateTimeout(options.Timeout);
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaAdminAuthHandler>();
        ConfigurePrimaryHandler(httpBuilder, configure);
        ConfigureResilience(httpBuilder, configure);
        return services;
    }

    /// <summary>
    /// Registers the Honua Geocoding client and related services with the DI container.
    /// Uses the same <see cref="HonuaAdminClientOptions"/> and <see cref="HonuaAdminAuthHandler"/>
    /// as the Admin client for authentication and base address configuration.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options. If <see cref="AddHonuaAdmin"/>
    /// has already been called, options and the auth handler are shared.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaGeocoding(
        this IServiceCollection services,
        Action<HonuaAdminClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddTransient<HonuaAdminAuthHandler>();
        var httpBuilder = services.AddHttpClient(nameof(HonuaGeocodingClient), (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaAdminClientOptions>>().Value;
            HonuaAdminClientOptions.ValidateBaseAddress(options.BaseAddress);
            HonuaAdminClientOptions.ValidateTimeout(options.Timeout);
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaAdminAuthHandler>();
        httpBuilder.AddTypedClient<IHonuaGeocodingClient>(httpClient => new HonuaGeocodingClient(httpClient));
        httpBuilder.AddTypedClient<IHonuaBatchGeocodingClient>(httpClient => new HonuaGeocodingClient(httpClient));
        ConfigurePrimaryHandler(httpBuilder, configure);
        ConfigureResilience(httpBuilder, configure);
        return services;
    }

    private static void ConfigurePrimaryHandler(
        IHttpClientBuilder httpBuilder,
        Action<HonuaAdminClientOptions> configure)
    {
        var opts = new HonuaAdminClientOptions();
        configure(opts);
        if (opts.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            httpBuilder.ConfigurePrimaryHttpMessageHandler(primaryHandlerFactory);
        }
    }

    private static void ConfigureResilience(
        IHttpClientBuilder httpBuilder,
        Action<HonuaAdminClientOptions> configure)
    {
        var opts = new HonuaAdminClientOptions();
        configure(opts);
        HonuaAdminClientOptions.ValidateTimeout(opts.Timeout);

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
