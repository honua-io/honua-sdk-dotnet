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
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaConsoleShareAuthHandler>();

        if (snapshot.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            httpBuilder.ConfigurePrimaryHttpMessageHandler(primaryHandlerFactory);
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
