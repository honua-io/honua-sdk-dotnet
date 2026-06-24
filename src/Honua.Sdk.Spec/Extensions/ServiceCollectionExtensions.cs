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
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to capture
    /// options; primary HTTP handler and resilience pipeline configuration are derived from
    /// that snapshot.
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

        var snapshot = new HonuaSpecClientOptions();
        configure(snapshot);
        HonuaSpecClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaSpecClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaSpecClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaSpecAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaSpecClient, HonuaSpecClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaSpecClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaSpecAuthHandler>();

        if (snapshot.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            httpBuilder.ConfigurePrimaryHttpMessageHandler(primaryHandlerFactory);
        }
        else
        {
            // Disable auto-redirect by default so the custom X-API-Key header is
            // never forwarded to an attacker-controlled 30x redirect target.
            httpBuilder.ConfigurePrimaryHttpMessageHandler(
                Honua.Sdk.Abstractions.HonuaHttpHandlerDefaults.CreateNoRedirectPrimaryHandler);
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
