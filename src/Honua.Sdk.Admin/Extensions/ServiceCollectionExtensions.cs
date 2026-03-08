// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Geocoding;
using Microsoft.Extensions.DependencyInjection;
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
        services.Configure(configure);
        services.AddTransient<HonuaAdminAuthHandler>();
        services.AddHttpClient<IHonuaAdminClient, HonuaAdminClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaAdminClientOptions>>().Value;
            HonuaAdminClientOptions.ValidateBaseAddress(options.BaseAddress);
            client.BaseAddress = options.BaseAddress;
        })
        .AddHttpMessageHandler<HonuaAdminAuthHandler>();
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
        services.Configure(configure);
        services.AddTransient<HonuaAdminAuthHandler>();
        services.AddHttpClient<IHonuaGeocodingClient, HonuaGeocodingClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaAdminClientOptions>>().Value;
            HonuaAdminClientOptions.ValidateBaseAddress(options.BaseAddress);
            client.BaseAddress = options.BaseAddress;
        })
        .AddHttpMessageHandler<HonuaAdminAuthHandler>();
        return services;
    }
}
