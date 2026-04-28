// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Microsoft.Extensions.DependencyInjection;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Grpc.Extensions;

/// <summary>
/// Extension methods for registering the Honua gRPC client with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Honua gRPC client and related services with the DI container.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">Configuration delegate for client options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHonuaGrpc(
        this IServiceCollection services,
        Action<HonuaGrpcClientOptions> configure)
    {
        services.Configure(configure);
        services.AddSingleton<HonuaGrpcClient>();
        services.AddSingleton<IHonuaGrpcClient>(sp => sp.GetRequiredService<HonuaGrpcClient>());
        services.AddSingleton<IHonuaFeatureQueryClient>(sp => sp.GetRequiredService<HonuaGrpcClient>());
        services.AddSingleton<IHonuaFeatureEditClient>(sp => sp.GetRequiredService<HonuaGrpcClient>());
        return services;
    }
}
