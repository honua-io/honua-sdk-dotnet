// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Internal.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.OgcFeatures.Wfs.Extensions;

/// <summary>
/// Extension methods for registering the Honua WFS client with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua WFS client and related services with the DI container.
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to capture
    /// options; primary HTTP handler and resilience pipeline configuration are derived from
    /// that snapshot.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaWfs(
        this IServiceCollection services,
        Action<HonuaWfsClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new HonuaWfsClientOptions();
        configure(snapshot);
        HonuaWfsClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaWfsClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaWfsClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaWfsAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaWfsClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaWfsClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaWfsAuthHandler>();
        services.AddTransient<IHonuaWfsClient>(sp => sp.GetRequiredService<HonuaWfsClient>());
        services.AddTransient<IHonuaFeatureQueryClient>(sp => sp.GetRequiredService<HonuaWfsClient>());
        services.AddTransient<IHonuaFeatureEditClient>(sp => sp.GetRequiredService<HonuaWfsClient>());
        services.AddTransient<IHonuaFeatureAttachmentClient>(sp => sp.GetRequiredService<HonuaWfsClient>());

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot);

        return services;
    }
}
