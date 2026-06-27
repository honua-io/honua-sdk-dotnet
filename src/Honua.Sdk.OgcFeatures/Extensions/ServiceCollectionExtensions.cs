// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.OgcFeatures.Styles;
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
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to capture
    /// options; primary HTTP handler and resilience pipeline configuration are derived from
    /// that snapshot, so side-effecting <paramref name="configure"/> delegates are safe.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaOgcFeatures(
        this IServiceCollection services,
        Action<HonuaOgcFeaturesClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new HonuaOgcFeaturesClientOptions();
        configure(snapshot);
        HonuaOgcFeaturesClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaOgcFeaturesClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaOgcFeaturesClientOptions>>(Options.Create(snapshot));
        services.AddTransient<HonuaOgcFeaturesAuthHandler>();
        var httpBuilder = services.AddHttpClient<HonuaOgcFeaturesClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaOgcFeaturesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaOgcFeaturesAuthHandler>();
        services.AddTransient<IHonuaOgcFeaturesClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaOgcFeaturesEditClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaOgcFeaturesPatchClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaFeatureQueryClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaFeatureEditClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());
        services.AddTransient<IHonuaFeatureAttachmentClient>(sp => sp.GetRequiredService<HonuaOgcFeaturesClient>());

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

        var stylesBuilder = services.AddHttpClient<HonuaOgcStylesClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaOgcFeaturesClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaOgcFeaturesAuthHandler>();
        services.AddTransient<IHonuaOgcStylesClient>(sp => sp.GetRequiredService<HonuaOgcStylesClient>());

        if (snapshot.PrimaryHttpMessageHandlerFactory is { } stylesPrimaryHandlerFactory)
        {
            stylesBuilder.ConfigurePrimaryHttpMessageHandler(stylesPrimaryHandlerFactory);
        }
        else
        {
            // Disable auto-redirect by default so the custom X-API-Key header is
            // never forwarded to an attacker-controlled 30x redirect target.
            stylesBuilder.ConfigurePrimaryHttpMessageHandler(
                Honua.Sdk.Abstractions.HonuaHttpHandlerDefaults.CreateNoRedirectPrimaryHandler);
        }

        if (snapshot.EnableRetry)
        {
            stylesBuilder.AddStandardResilienceHandler(options =>
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
