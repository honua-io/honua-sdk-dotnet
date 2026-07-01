// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Internal.Http;
using Microsoft.Extensions.DependencyInjection;
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

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot);

        return services;
    }
}
