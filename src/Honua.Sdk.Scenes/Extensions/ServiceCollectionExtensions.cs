// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Scenes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Scenes.Extensions;

/// <summary>
/// Extension methods for registering Honua scene clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua scene metadata client with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaScenes(
        this IServiceCollection services,
        Action<HonuaSceneClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddTransient<HonuaSceneAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaSceneClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaSceneClientOptions>>().Value;
            HonuaSceneClientOptions.ValidateBaseAddress(options.BaseAddress);
            HonuaSceneClientOptions.ValidateTimeout(options.Timeout);
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaSceneAuthHandler>();
        services.AddTransient<IHonuaSceneClient>(sp => sp.GetRequiredService<HonuaSceneClient>());
        ConfigureResilience(httpBuilder, configure);
        return services;
    }

    private static void ConfigureResilience(
        IHttpClientBuilder httpBuilder,
        Action<HonuaSceneClientOptions> configure)
    {
        var opts = new HonuaSceneClientOptions();
        configure(opts);
        HonuaSceneClientOptions.ValidateTimeout(opts.Timeout);

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
