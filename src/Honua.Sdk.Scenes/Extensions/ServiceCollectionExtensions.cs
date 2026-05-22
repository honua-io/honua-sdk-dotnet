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
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to capture
    /// options; primary HTTP handler and resilience pipeline configuration are derived from
    /// that snapshot.
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

        var snapshot = new HonuaSceneClientOptions();
        configure(snapshot);
        HonuaSceneClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaSceneClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaSceneClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaSceneAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaSceneClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaSceneClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaSceneAuthHandler>();
        services.AddTransient<IHonuaSceneClient>(sp => sp.GetRequiredService<HonuaSceneClient>());

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

        return services;
    }
}
