// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Internal.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Catalogs.Records.Extensions;

/// <summary>
/// Extension methods for registering Honua OGC API Records clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua OGC API Records client with the DI container.
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to capture
    /// options; primary HTTP handler and resilience pipeline configuration are derived from
    /// that snapshot.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaOgcRecords(
        this IServiceCollection services,
        Action<HonuaOgcRecordsClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new HonuaOgcRecordsClientOptions();
        configure(snapshot);
        HonuaOgcRecordsClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaOgcRecordsClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaOgcRecordsClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaOgcRecordsAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaOgcRecordsClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaOgcRecordsClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaOgcRecordsAuthHandler>();
        services.AddTransient<IHonuaOgcRecordsClient>(sp => sp.GetRequiredService<HonuaOgcRecordsClient>());

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot);

        return services;
    }
}
