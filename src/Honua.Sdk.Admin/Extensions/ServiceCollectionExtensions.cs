// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Catalog;
using Honua.Sdk.Admin.Geocoding;
using Honua.Sdk.Internal.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Admin.Extensions;

/// <summary>
/// Extension methods for registering the Honua Admin client with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua Admin client and related services with the DI container.
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to capture
    /// options; primary HTTP handler and resilience pipeline configuration are derived from
    /// that snapshot.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaAdmin(
        this IServiceCollection services,
        Action<HonuaAdminClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = CaptureSnapshot(configure);

        // TryAdd: if a sibling Admin extension (AddHonuaAdmin / AddHonuaCatalog /
        // AddHonuaGeocoding) already registered IOptions<HonuaAdminClientOptions>,
        // share that snapshot rather than silently overwriting it with this call's
        // snapshot. The first AddHonua* wins; subsequent ones must use compatible
        // BaseAddress / auth configuration.
        services.TryAddSingleton<IOptions<HonuaAdminClientOptions>>(Options.Create(snapshot));
        services.TryAddTransient<HonuaAdminAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaAdminClient, HonuaAdminClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaAdminClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaAdminAuthHandler>();
        httpBuilder.AddTypedClient<IHonuaCatalogClient>(httpClient => new HonuaCatalogClient(httpClient));

        // Register each role-segregated sub-interface as a forwarding alias to
        // IHonuaAdminClient so consumers can inject the narrowest contract they
        // need (Services, Connections, Deploy, etc.) without taking a dependency
        // on the full aggregate surface.
        services.AddTransient<IHonuaAdminServicesClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminMetadataClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminCompatibilityClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminManifestClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminConnectionsClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminLayersClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminStylesClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminConfigClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminIdentityClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminRolesClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminUsersClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminLicenseClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminObservabilityClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminAlertsClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminFeatureEventsClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminStreamingOperationsClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminDeployClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());
        services.AddTransient<IHonuaAdminRasterImportClient>(sp => sp.GetRequiredService<IHonuaAdminClient>());

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot);
        return services;
    }

    /// <summary>
    /// Registers the Honua catalog discovery client with the DI container.
    /// Uses the same <see cref="HonuaAdminClientOptions"/> and <see cref="HonuaAdminAuthHandler"/>
    /// as the Admin client for authentication and base address configuration.
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaCatalog(
        this IServiceCollection services,
        Action<HonuaAdminClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = CaptureSnapshot(configure);

        // TryAdd: if a sibling Admin extension (AddHonuaAdmin / AddHonuaCatalog /
        // AddHonuaGeocoding) already registered IOptions<HonuaAdminClientOptions>,
        // share that snapshot rather than silently overwriting it with this call's
        // snapshot. The first AddHonua* wins; subsequent ones must use compatible
        // BaseAddress / auth configuration.
        services.TryAddSingleton<IOptions<HonuaAdminClientOptions>>(Options.Create(snapshot));
        services.TryAddTransient<HonuaAdminAuthHandler>();
        var httpBuilder = services.AddHttpClient<IHonuaCatalogClient, HonuaCatalogClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaAdminClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaAdminAuthHandler>();

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot);
        return services;
    }

    /// <summary>
    /// Registers the Honua Geocoding client and related services with the DI container.
    /// Uses the same <see cref="HonuaAdminClientOptions"/> and <see cref="HonuaAdminAuthHandler"/>
    /// as the Admin client for authentication and base address configuration.
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options. If <see cref="AddHonuaAdmin"/>
    /// has already been called, options and the auth handler are shared.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaGeocoding(
        this IServiceCollection services,
        Action<HonuaAdminClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = CaptureSnapshot(configure);

        // TryAdd: if a sibling Admin extension (AddHonuaAdmin / AddHonuaCatalog /
        // AddHonuaGeocoding) already registered IOptions<HonuaAdminClientOptions>,
        // share that snapshot rather than silently overwriting it with this call's
        // snapshot. The first AddHonua* wins; subsequent ones must use compatible
        // BaseAddress / auth configuration.
        services.TryAddSingleton<IOptions<HonuaAdminClientOptions>>(Options.Create(snapshot));
        services.TryAddTransient<HonuaAdminAuthHandler>();
        var httpBuilder = services.AddHttpClient(nameof(HonuaGeocodingClient), (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HonuaAdminClientOptions>>().Value;
            client.BaseAddress = options.BaseAddress;
            client.Timeout = Honua.Sdk.Abstractions.HonuaResilienceTimeouts.HttpClientTimeout(options.Timeout, options.EnableRetry);
        })
        .AddHttpMessageHandler<HonuaAdminAuthHandler>();
        httpBuilder.AddTypedClient<IHonuaGeocodingClient>(httpClient => new HonuaGeocodingClient(httpClient));
        httpBuilder.AddTypedClient<IHonuaBatchGeocodingClient>(httpClient => new HonuaGeocodingClient(httpClient));

        httpBuilder.ConfigureHonuaRestHttpClient(snapshot);
        return services;
    }

    private static HonuaAdminClientOptions CaptureSnapshot(Action<HonuaAdminClientOptions> configure)
    {
        var snapshot = new HonuaAdminClientOptions();
        configure(snapshot);
        HonuaAdminClientOptions.ValidateBaseAddress(snapshot.BaseAddress);
        HonuaAdminClientOptions.ValidateTimeout(snapshot.Timeout);
        return snapshot;
    }
}
