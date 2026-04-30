// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Spec.Extensions;

/// <summary>
/// Dependency injection extensions for the Honua spec client.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IHonuaSpecClient"/> and its typed HTTP client.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Options configuration delegate.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddHonuaSpec(
        this IServiceCollection services,
        Action<HonuaSpecClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddTransient<HonuaSpecAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaSpecClient, HonuaSpecClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaSpecClientOptions>>().Value;
            HonuaSpecClientOptions.ValidateBaseAddress(options.BaseAddress);
            HonuaSpecClientOptions.ValidateTimeout(options.Timeout);
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaSpecAuthHandler>();

        ConfigurePrimaryHandler(httpBuilder, configure);
        ConfigureResilience(httpBuilder, configure);
        return services;
    }

    private static void ConfigurePrimaryHandler(
        IHttpClientBuilder httpBuilder,
        Action<HonuaSpecClientOptions> configure)
    {
        var opts = new HonuaSpecClientOptions();
        configure(opts);
        if (opts.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            httpBuilder.ConfigurePrimaryHttpMessageHandler(primaryHandlerFactory);
        }
    }

    private static void ConfigureResilience(
        IHttpClientBuilder httpBuilder,
        Action<HonuaSpecClientOptions> configure)
    {
        var opts = new HonuaSpecClientOptions();
        configure(opts);
        HonuaSpecClientOptions.ValidateTimeout(opts.Timeout);

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
