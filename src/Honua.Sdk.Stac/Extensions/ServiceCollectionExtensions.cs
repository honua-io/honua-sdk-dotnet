// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Stac.Extensions;

/// <summary>
/// Extension methods for registering Honua STAC clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua STAC client with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaStac(
        this IServiceCollection services,
        Action<HonuaStacClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.AddTransient<HonuaStacAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaStacClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaStacClientOptions>>().Value;
            HonuaStacClientOptions.ValidateBaseAddress(options.BaseAddress);
            HonuaStacClientOptions.ValidateTimeout(options.Timeout);
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaStacAuthHandler>();
        services.AddTransient<IHonuaStacClient>(sp => sp.GetRequiredService<HonuaStacClient>());
        ConfigurePrimaryHandler(httpBuilder, configure);
        ConfigureResilience(httpBuilder, configure);
        return services;
    }

    private static void ConfigurePrimaryHandler(
        IHttpClientBuilder httpBuilder,
        Action<HonuaStacClientOptions> configure)
    {
        var opts = new HonuaStacClientOptions();
        configure(opts);
        if (opts.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            httpBuilder.ConfigurePrimaryHttpMessageHandler(primaryHandlerFactory);
        }
    }

    private static void ConfigureResilience(
        IHttpClientBuilder httpBuilder,
        Action<HonuaStacClientOptions> configure)
    {
        var opts = new HonuaStacClientOptions();
        configure(opts);
        HonuaStacClientOptions.ValidateTimeout(opts.Timeout);

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
