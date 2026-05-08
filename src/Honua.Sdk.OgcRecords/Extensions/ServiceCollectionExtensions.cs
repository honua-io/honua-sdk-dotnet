// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.OgcRecords.Extensions;

/// <summary>
/// Extension methods for registering Honua OGC API Records clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua OGC API Records client with the DI container.
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

        services.Configure(configure);
        services.AddTransient<HonuaOgcRecordsAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaOgcRecordsClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaOgcRecordsClientOptions>>().Value;
            HonuaOgcRecordsClientOptions.ValidateBaseAddress(options.BaseAddress);
            HonuaOgcRecordsClientOptions.ValidateTimeout(options.Timeout);
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaOgcRecordsAuthHandler>();
        services.AddTransient<IHonuaOgcRecordsClient>(sp => sp.GetRequiredService<HonuaOgcRecordsClient>());
        ConfigurePrimaryHandler(httpBuilder, configure);
        ConfigureResilience(httpBuilder, configure);
        return services;
    }

    private static void ConfigurePrimaryHandler(
        IHttpClientBuilder httpBuilder,
        Action<HonuaOgcRecordsClientOptions> configure)
    {
        var opts = new HonuaOgcRecordsClientOptions();
        configure(opts);
        if (opts.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            httpBuilder.ConfigurePrimaryHttpMessageHandler(primaryHandlerFactory);
        }
    }

    private static void ConfigureResilience(
        IHttpClientBuilder httpBuilder,
        Action<HonuaOgcRecordsClientOptions> configure)
    {
        var opts = new HonuaOgcRecordsClientOptions();
        configure(opts);
        HonuaOgcRecordsClientOptions.ValidateTimeout(opts.Timeout);

        if (!opts.EnableRetry)
        {
            return;
        }

        httpBuilder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = opts.Timeout;
            options.AttemptTimeout.Timeout = opts.Timeout;
            options.Retry.MaxRetryAttempts = opts.MaxRetryAttempts;
            options.Retry.ShouldHandle = args => ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
            options.Retry.DisableForUnsafeHttpMethods();
            options.Retry.UseJitter = true;
        });
    }
}
