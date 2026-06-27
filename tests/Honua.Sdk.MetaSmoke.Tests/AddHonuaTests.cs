// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.Abstractions.Scenes;
using Honua.Sdk.Admin;
using Honua.Sdk.Admin.Catalog;
using Honua.Sdk.Abstractions.Data;
using Honua.Sdk.Admin.Geocoding;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.ImageServer;
using Honua.Sdk.Grpc;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.Processes;
using Honua.Sdk.Catalogs.Records;
using Honua.Sdk.Spec;
using Honua.Sdk.Studio;
using Honua.Sdk.ConsoleShare;
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
        Assert.NotNull(provider.GetRequiredService<IHonuaProcessesClient>());
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
            o.UseProcesses = true;
            o.UseWfs = true;
            o.UseGeoServices = true;
            o.UseRouting = true;
            o.UseImageServer = true;
            o.UseScenes = true;
            o.UseSpec = true;
            o.UseStac = true;
            o.UseOgcRecords = true;
            o.UseStudio = true;
            o.UseConsoleShare = true;
            o.UseGeoprocessingProfile = true;
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHonuaGrpcClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaFeatureGateway>());
        Assert.NotNull(provider.GetRequiredService<IHonuaAdminClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaCatalogClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaGeocodingClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaOgcFeaturesClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaProcessesClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaWfsClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaFeatureServerClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaRoutingClient>());
        Assert.NotNull(provider.GetRequiredService<HonuaImageServerClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaRasterDataClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaSceneClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaSpecClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaStacClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaOgcRecordsClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaStudioReportsClient>());
        Assert.NotNull(provider.GetRequiredService<IHonuaConsoleShareClient>());
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
                o.UseProcesses = false;
                o.UseWfs = false;
                o.UseGeoServices = false;
                o.UseRouting = false;
                o.UseImageServer = false;
                o.UseScenes = false;
                o.UseSpec = false;
                o.UseStac = false;
                o.UseOgcRecords = false;
                o.UseStudio = false;
                o.UseConsoleShare = false;
                o.UseGeoprocessingProfile = false;
            }));

        Assert.Contains("at least one Honua module", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddHonua_WithGeoprocessingProfile_PullsInGeoServicesAndGateway()
    {
        var services = new ServiceCollection();

        // A GP consumer enables only the workhorse gRPC transport plus the GP
        // profile. The profile must transparently pull in the GeoServices
        // FeatureServer client (the attachment + time/having query backend) even
        // though UseGeoServices was never set explicitly.
        services.AddHonua(o =>
        {
            o.BaseAddress = TestBaseAddress;
            o.EnableRetry = false;
            o.UseGrpc = true;
            o.UseAdmin = false;
            o.UseGeocoding = false;
            o.UseOgcFeatures = false;
            o.UseProcesses = false;
            o.UseWfs = false;
            o.UseGeoprocessingProfile = true;
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IHonuaFeatureServerClient>());
        var gateway = provider.GetRequiredService<IHonuaFeatureGateway>();

        // The gateway aggregates capabilities across providers: attachments and
        // time/having queries are reachable even though the gRPC transport exposes
        // neither, because GeoServices backs them.
        Assert.True(gateway.AttachmentCapabilities.SupportsList);
        Assert.True(gateway.AttachmentCapabilities.SupportsAdd);
        Assert.True(gateway.QueryCapabilities.SupportsTimeFilter);
        Assert.True(gateway.QueryCapabilities.SupportsHaving);
    }

    [Fact]
    public void AddHonua_WithUseImageServer_RegistersRasterClients()
    {
        var services = new ServiceCollection();

        services.AddHonua(o =>
        {
            o.BaseAddress = TestBaseAddress;
            o.EnableRetry = false;
            o.UseImageServer = true;
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<HonuaImageServerClient>());
        var raster = provider.GetRequiredService<IHonuaRasterDataClient>();
        Assert.True(raster.RasterCapabilities.SupportsWindowReads);
    }

    [Fact]
    public void AddHonua_WithDefaults_DoesNotRegisterImageServer()
    {
        var services = new ServiceCollection();

        services.AddHonua(o =>
        {
            o.BaseAddress = TestBaseAddress;
            o.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();

        // UseImageServer defaults to false, so the raster clients are not registered.
        Assert.Null(provider.GetService<HonuaImageServerClient>());
        Assert.Null(provider.GetService<IHonuaRasterDataClient>());
    }
}
