// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Wfs.Extensions;

/// <summary>
/// Extension methods for registering the Honua WFS client with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua WFS client and related services with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaWfs(
        this IServiceCollection services,
        Action<HonuaWfsClientOptions> configure)
    {
        services.Configure(configure);
        services.AddTransient<HonuaWfsAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaWfsClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaWfsClientOptions>>().Value;
            HonuaWfsClientOptions.ValidateBaseAddress(options.BaseAddress);
            client.BaseAddress = options.BaseAddress;
        })
        .AddHttpMessageHandler<HonuaWfsAuthHandler>();
        services.AddTransient<IHonuaWfsClient>(sp => sp.GetRequiredService<HonuaWfsClient>());
        services.AddTransient<IHonuaFeatureQueryClient>(sp => sp.GetRequiredService<HonuaWfsClient>());
        ConfigureResilience(services, httpBuilder, configure);
        return services;
    }

    private static void ConfigureResilience(
        IServiceCollection services,
        IHttpClientBuilder httpBuilder,
        Action<HonuaWfsClientOptions> configure)
    {
        var opts = new HonuaWfsClientOptions();
        configure(opts);

        if (!opts.EnableRetry)
        {
            return;
        }

        httpBuilder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = opts.MaxRetryAttempts;
            options.Retry.ShouldHandle = args => ValueTask.FromResult(
                args.Outcome.Result?.StatusCode is
                    HttpStatusCode.TooManyRequests or
                    HttpStatusCode.BadGateway or
                    HttpStatusCode.ServiceUnavailable);
            options.Retry.UseJitter = true;
        });
    }
}
