// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Studio.Extensions;

/// <summary>
/// Extension methods for registering Console Studio clients.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua Console Studio analysis-report client.
    /// </summary>
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
        var httpBuilder = services.AddHttpClient<IHonuaStudioReportsClient, HonuaStudioReportsClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaStudioClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaStudioAuthHandler>();

        if (snapshot.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            httpBuilder.ConfigurePrimaryHttpMessageHandler(primaryHandlerFactory);
        }
        else
        {
            // Disable auto-redirect by default so the custom X-API-Key header is
            // never forwarded to an attacker-controlled 30x redirect target.
            httpBuilder.ConfigurePrimaryHttpMessageHandler(
                Honua.Sdk.Abstractions.HonuaHttpHandlerDefaults.CreateNoRedirectPrimaryHandler);
        }

        if (snapshot.EnableRetry)
        {
            httpBuilder.AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.TotalRequestTimeout(snapshot.Timeout);
                options.AttemptTimeout.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.AttemptTimeout(snapshot.Timeout);
                options.CircuitBreaker.SamplingDuration = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.SamplingDuration(snapshot.Timeout);
                options.Retry.MaxRetryAttempts = snapshot.MaxRetryAttempts;
                options.Retry.ShouldHandle = args => ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
                options.Retry.DisableForUnsafeHttpMethods();
                options.Retry.UseJitter = true;
            });
        }

        return services;
    }
}
