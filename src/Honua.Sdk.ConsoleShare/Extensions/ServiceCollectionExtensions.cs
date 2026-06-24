// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

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

        var snapshot = new HonuaConsoleShareClientOptions();
        configure(snapshot);
        HonuaConsoleShareClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaConsoleShareClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaConsoleShareClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaConsoleShareAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaConsoleShareClient, HonuaConsoleShareClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaConsoleShareClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaConsoleShareAuthHandler>();

        ConfigureTransport(httpBuilder, snapshot);

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

        var snapshot = new HonuaConsoleShareClientOptions();
        configure(snapshot);
        HonuaConsoleShareClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaConsoleShareClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaConsoleShareClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaConsoleShareAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaConsoleShareExportClient, HonuaConsoleShareExportClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaConsoleShareClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaConsoleShareAuthHandler>();

        ConfigureTransport(httpBuilder, snapshot);

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

        var snapshot = new HonuaConsoleShareClientOptions();
        configure(snapshot);
        HonuaConsoleShareClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaConsoleShareClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaConsoleShareClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaConsoleShareAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaConsoleShareOpenDataClient, HonuaConsoleShareOpenDataClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaConsoleShareClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaConsoleShareAuthHandler>();

        ConfigureTransport(httpBuilder, snapshot);

        return services;
    }

    private static void ConfigureTransport(IHttpClientBuilder httpBuilder, HonuaConsoleShareClientOptions snapshot)
    {
        if (snapshot.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            httpBuilder.ConfigurePrimaryHttpMessageHandler(primaryHandlerFactory);
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
    }
}
