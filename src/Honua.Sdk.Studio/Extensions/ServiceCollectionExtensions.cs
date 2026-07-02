// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Internal.Http;
using Honua.Sdk.Studio.Capabilities;
using Honua.Sdk.Studio.Packages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Studio.Extensions;

/// <summary>
/// Extension methods for registering Console Studio clients.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua Console Studio clients: the analysis-report read
    /// client, the capability-manifest client, and the Studio package-family
    /// lifecycle client.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaStudio(
        this IServiceCollection services,
        Action<HonuaStudioClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new HonuaStudioClientOptions();
        configure(snapshot);
        HonuaStudioClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaStudioClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaStudioClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaStudioAuthHandler>();

        RegisterClient<IHonuaStudioReportsClient, HonuaStudioReportsClient>(services, snapshot);
        RegisterClient<IHonuaCapabilityManifestClient, HonuaCapabilityManifestClient>(services, snapshot);
        RegisterClient<IHonuaStudioPackageClient, HonuaStudioPackageClient>(services, snapshot);

        return services;
    }

    private static void RegisterClient<TClient, TImplementation>(
        IServiceCollection services,
        HonuaStudioClientOptions snapshot)
        where TClient : class
        where TImplementation : class, TClient
    {
        services.AddHttpClient<TClient, TImplementation>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<HonuaStudioClientOptions>>().Value;
                client.BaseAddress = options.BaseAddress;
                client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
            })
            .AddHttpMessageHandler<HonuaStudioAuthHandler>()
            .ConfigureHonuaRestHttpClient(snapshot);
    }
}
