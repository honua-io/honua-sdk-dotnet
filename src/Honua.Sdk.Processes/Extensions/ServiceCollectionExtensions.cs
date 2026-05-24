// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Processes.Extensions;

/// <summary>
/// Extension methods for registering OGC API Processes clients.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua OGC API Processes client.
    /// </summary>
    public static IServiceCollection AddHonuaProcesses(
        this IServiceCollection services,
        Action<HonuaProcessesClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new HonuaProcessesClientOptions();
        configure(snapshot);
        HonuaProcessesClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaProcessesClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaProcessesClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaProcessesAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaProcessesClient, HonuaProcessesClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaProcessesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaProcessesAuthHandler>();

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
