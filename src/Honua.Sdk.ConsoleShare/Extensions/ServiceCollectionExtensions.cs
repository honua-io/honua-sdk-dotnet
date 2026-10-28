// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Internal.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.ConsoleShare.Extensions;

/// <summary>
/// Extension methods for registering Console Share clients.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua Console Share access, public-link, and embed-token client.
    /// </summary>
    public static IServiceCollection AddHonuaConsoleShare(
        this IServiceCollection services,
        Action<HonuaConsoleShareClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AddConsoleShareClient<IHonuaConsoleShareClient, HonuaConsoleShareClient>(services, configure);
        return services;
    }

    /// <summary>
    /// Registers the Honua Console Share export-definition, export-run, and Share-traffic
    /// admin client (<see cref="IHonuaConsoleShareExportClient"/>) over the same options,
    /// authentication, and resilience pipeline as <see cref="AddHonuaConsoleShare"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Callback that configures the client options.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddHonuaConsoleShareExport(
        this IServiceCollection services,
        Action<HonuaConsoleShareClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AddConsoleShareClient<IHonuaConsoleShareExportClient, HonuaConsoleShareExportClient>(services, configure);
        return services;
    }

    /// <summary>
    /// Registers the Honua Console Share open-data / DCAT / STAC publication client
    /// (<see cref="IHonuaConsoleShareOpenDataClient"/>) over the same options,
    /// authentication, and resilience pipeline as <see cref="AddHonuaConsoleShare"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Callback that configures the client options.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddHonuaConsoleShareOpenData(
        this IServiceCollection services,
        Action<HonuaConsoleShareClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AddConsoleShareClient<IHonuaConsoleShareOpenDataClient, HonuaConsoleShareOpenDataClient>(services, configure);
        return services;
    }

    private static void AddConsoleShareClient<TClient, TImplementation>(
        IServiceCollection services,
        Action<HonuaConsoleShareClientOptions> configure)
        where TClient : class
        where TImplementation : class, TClient
    {
        var snapshot = new HonuaConsoleShareClientOptions();
        configure(snapshot);
        HonuaConsoleShareClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaConsoleShareClientOptions.ValidateTimeout(snapshot.Timeout);

        // Each registration closes over its own snapshot. A shared IOptions singleton
        // would let a later AddHonuaConsoleShare* call replace the base address and
        // credentials of clients registered earlier.
        var httpBuilder = services.AddHttpClient<TClient, TImplementation>((_, client) =>
        {
            client.BaseAddress = snapshot.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(
                snapshot.Timeout,
                snapshot.EnableRetry);
        })
        .AddHttpMessageHandler(_ => new HonuaConsoleShareAuthHandler(snapshot));

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot);
    }
}
