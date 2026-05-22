// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.Abstractions.Scenes;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Catalog;
using Honua.Sdk.Admin.Geocoding;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.Grpc;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.Catalogs.Records;
using Honua.Sdk.Spec;
using Honua.Sdk.Catalogs.Stac;
using Honua.Sdk.OgcFeatures.Wfs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Honua.Sdk.MetaSmoke.Tests;

public sealed class AddHonuaTests
{
    private static readonly Uri TestBaseAddress = new("https://localhost:5001");

    [Fact]
    public void AddHonua_WithDefaults_RegistersCoreClients()
    {
        var services = new ServiceCollection();

        services.AddHonua(o =>
        {
            o.BaseAddress = TestBaseAddress;
            // Disable retry so the resilience pipeline (whose default 30 s
            // circuit-breaker sampling duration must be at least 2× the
            // 100 s attempt timeout) doesn't need an alternate timeout for
            // a pure DI-wiring smoke test. Matches the pattern in every
            // per-package ClientOptionsTests.
            o.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();

        // Defaults register the core query/edit/admin trio (Grpc, Admin,
        // Geocoding, OgcFeatures, Wfs). Resolve each canonical interface to
        // confirm DI wiring works end-to-end without any outbound calls.
        Assert.NotNull(provider.GetRequiredService<IHonuaGrpcClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaAdminClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaCatalogClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaGeocodingClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaOgcFeaturesClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaWfsClient>());
    }

    [Fact]
    public void AddHonua_WithAllModulesEnabled_RegistersEverything()
    {
        var services = new ServiceCollection();

        services.AddHonua(o =>
        {
            o.BaseAddress = TestBaseAddress;
            o.EnableRetry = false;
            o.UseGrpc = true;
            o.UseAdmin = true;
            o.UseGeocoding = true;
            o.UseOgcFeatures = true;
            o.UseWfs = true;
            o.UseGeoServices = true;
            o.UseRouting = true;
            o.UseScenes = true;
            o.UseSpec = true;
            o.UseStac = true;
            o.UseOgcRecords = true;
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHonuaGrpcClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaAdminClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaCatalogClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaGeocodingClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaOgcFeaturesClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaWfsClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaFeatureServerClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaRoutingClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaSceneClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaSpecClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaStacClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaOgcRecordsClient>());
    }

    [Fact]
    public void AddHonua_WithNoModulesEnabled_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<HonuaConfigurationException>(() =>
            services.AddHonua(o =>
            {
                o.BaseAddress = TestBaseAddress;
                o.UseGrpc = false;
                o.UseAdmin = false;
                o.UseGeocoding = false;
                o.UseOgcFeatures = false;
                o.UseWfs = false;
                o.UseGeoServices = false;
                o.UseRouting = false;
                o.UseScenes = false;
                o.UseSpec = false;
                o.UseStac = false;
                o.UseOgcRecords = false;
            }));

        Assert.Contains("at least one Honua module", ex.Message, StringComparison.Ordinal);
    }
}
