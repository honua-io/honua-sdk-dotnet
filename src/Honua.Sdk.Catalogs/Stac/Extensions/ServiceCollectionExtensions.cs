// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Catalogs.Stac.Extensions;

/// <summary>
/// Extension methods for registering Honua STAC clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua STAC client with the DI container.
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to capture
    /// options; primary HTTP handler and resilience pipeline configuration are derived from
    /// that snapshot.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaStac(
        this IServiceCollection services,
        Action<HonuaStacClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new HonuaStacClientOptions();
        configure(snapshot);
        HonuaStacClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaStacClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaStacClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaStacAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaStacClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaStacClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaStacAuthHandler>();
        services.AddTransient<IHonuaStacClient>(sp => sp.GetRequiredService<HonuaStacClient>());
        services.AddTransient<IHonuaStacRawClient>(sp => sp.GetRequiredService<HonuaStacClient>());

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
                options.TotalRequestTimeout.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.TotalRequestTimeout(snapshot.Timeout);
                options.AttemptTimeout.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.AttemptTimeout(snapshot.Timeout);
                options.Retry.MaxRetryAttempts = snapshot.MaxRetryAttempts;
                options.Retry.ShouldHandle = args => ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
                options.Retry.DisableForUnsafeHttpMethods();
                options.Retry.UseJitter = true;
            });
        }

        return services;
    }
}
