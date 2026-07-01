// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Internal.Http;
using Microsoft.Extensions.DependencyInjection;
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
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaSpecAuthHandler>();

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot);

        return services;
    }
}
