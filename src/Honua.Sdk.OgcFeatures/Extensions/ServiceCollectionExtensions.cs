// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.OgcFeatures.Extensions;

/// <summary>
/// Extension methods for registering Honua OGC API Features clients with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua OGC API Features client and related services with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaOgcFeatures(
        this IServiceCollection services,
        Action<HonuaOgcFeaturesClientOptions> configure)
    {
        services.Configure(configure);
        services.AddTransient<HonuaOgcFeaturesAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaOgcFeaturesClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaOgcFeaturesClientOptions>>().Value;
            HonuaOgcFeaturesClientOptions.ValidateBaseAddress(options.BaseAddress);
            HonuaOgcFeaturesClientOptions.ValidateTimeout(options.Timeout);
            client.BaseAddress = options.BaseAddress;
            client.Timeout = options.Timeout;
        })
        .AddHttpMessageHandler<HonuaOgcFeaturesAuthHandler>();
        services.AddTransient<IHonuaOgcFeaturesClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaOgcFeaturesEditClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaFeatureQueryClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaFeatureEditClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        ConfigureResilience(httpBuilder, configure);
        return services;
    }

    private static void ConfigureResilience(
        IHttpClientBuilder httpBuilder,
        Action<HonuaOgcFeaturesClientOptions> configure)
    {
        var opts = new HonuaOgcFeaturesClientOptions();
        configure(opts);
        HonuaOgcFeaturesClientOptions.ValidateTimeout(opts.Timeout);

        if (!opts.EnableRetry)
        {
            return;
        }

        httpBuilder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = opts.Timeout;
            options.AttemptTimeout.Timeout = opts.Timeout;
            options.Retry.MaxRetryAttempts = opts.MaxRetryAttempts;
            options.Retry.ShouldHandle = args => ValueTask.FromResult(ShouldRetry(args.Outcome.Result));
            options.Retry.UseJitter = true;
        });
    }

    private static bool ShouldRetry(HttpResponseMessage? response)
    {
        return IsSafeRetryMethod(response?.RequestMessage?.Method) &&
            response?.StatusCode is
                HttpStatusCode.TooManyRequests or
                HttpStatusCode.BadGateway or
                HttpStatusCode.ServiceUnavailable;
    }

    private static bool IsSafeRetryMethod(HttpMethod? method) =>
        method == HttpMethod.Get ||
        method == HttpMethod.Head ||
        method == HttpMethod.Options ||
        method == HttpMethod.Trace;
}
