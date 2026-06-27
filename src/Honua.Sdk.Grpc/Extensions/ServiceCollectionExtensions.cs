// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Grpc.Extensions;

/// <summary>
/// Extension methods for registering the Honua gRPC client with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua gRPC client and related services with the DI container.
    /// The supplied <paramref name="configure"/> delegate is invoked exactly once to
    /// capture options; <see cref="Honua.Sdk.Abstractions.HonuaClientOptionsBase.BaseAddress"/> and
    /// <see cref="Honua.Sdk.Abstractions.HonuaClientOptionsBase.Timeout"/> are validated eagerly so a
    /// misconfigured client fails at registration time, matching the behavior of every
    /// REST SDK package.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaGrpc(
        this IServiceCollection services,
        Action<HonuaGrpcClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var snapshot = new HonuaGrpcClientOptions();
        configure(snapshot);
        HonuaGrpcClientOptions.ParseAndValidateAddress(snapshot);
        HonuaGrpcClientOptions.ValidateTimeout(snapshot.Timeout);

        services.AddSingleton<IOptions<HonuaGrpcClientOptions>>(Options.Create(snapshot));

        // Register only via the public interfaces so consumers can't accidentally
        // resolve the concrete `HonuaGrpcClient` (which is `IDisposable` and owns
        // the GrpcChannel) and dispose a DI-owned singleton. The IHonuaGrpcClient
        // registration is the canonical singleton; the other interface views
        // share that one instance.
        services.AddSingleton<IHonuaGrpcClient>(sp =>
            new HonuaGrpcClient(sp.GetRequiredService<IOptions<HonuaGrpcClientOptions>>()));
        services.AddSingleton<IHonuaProcessGrpcClient>(sp =>
            new HonuaProcessGrpcClient(sp.GetRequiredService<IOptions<HonuaGrpcClientOptions>>()));
        services.AddSingleton<IHonuaFeatureQueryClient>(sp => (IHonuaFeatureQueryClient)sp.GetRequiredService<IHonuaGrpcClient>());
        services.AddSingleton<IHonuaFeatureEditClient>(sp => (IHonuaFeatureEditClient)sp.GetRequiredService<IHonuaGrpcClient>());
        services.AddSingleton<IHonuaFeatureAttachmentClient>(sp => (IHonuaFeatureAttachmentClient)sp.GetRequiredService<IHonuaGrpcClient>());
        return services;
    }
}
