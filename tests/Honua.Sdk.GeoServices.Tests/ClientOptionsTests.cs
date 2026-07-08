// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Reflection;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.GeoServices.Tests;

public sealed class ClientOptionsTests
{
    [Fact]
    public void DefaultTimeout_IsOneHundredSeconds()
    {
        var options = new HonuaGeoServicesClientOptions();

        Assert.Equal(TimeSpan.FromSeconds(100), options.Timeout);
    }

    [Fact]
    public void AddHonuaFeatureServer_ConfiguresHttpClientTimeout()
    {
        var timeout = TimeSpan.FromSeconds(44);
        var services = new ServiceCollection();
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
            options.Timeout = timeout;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<HonuaFeatureServerClient>();

        Assert.Equal(timeout, GetHttpClient(client).Timeout);
    }

    [Fact]
    public void GeoServicesClientRegistrations_DoNotOverwriteSharedOptions()
    {
        var firstBaseAddress = new Uri("https://first.example");
        var firstTimeout = TimeSpan.FromSeconds(33);
        var services = new ServiceCollection();
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = firstBaseAddress;
            options.EnableRetry = false;
            options.Timeout = firstTimeout;
        });
        services.AddHonuaRouting(options =>
        {
            options.BaseAddress = new Uri("https://routing.example");
            options.EnableRetry = false;
            options.Timeout = TimeSpan.FromSeconds(44);
        });
        services.AddHonuaImageServer(options =>
        {
            options.BaseAddress = new Uri("https://image.example");
            options.EnableRetry = false;
            options.Timeout = TimeSpan.FromSeconds(55);
        });
        services.AddHonuaGeometryServer(options =>
        {
            options.BaseAddress = new Uri("https://geometry.example");
            options.EnableRetry = false;
            options.Timeout = TimeSpan.FromSeconds(66);
        });

        Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(IOptions<HonuaGeoServicesClientOptions>));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HonuaGeoServicesClientOptions>>().Value;

        Assert.Equal(firstBaseAddress, options.BaseAddress);
        Assert.Equal(firstTimeout, options.Timeout);
    }

    [Fact]
    public void GeoServicesClientRegistrations_PreservePerClientSnapshots()
    {
        var featureBaseAddress = new Uri("https://feature.example");
        var routingBaseAddress = new Uri("https://routing.example");
        var routingTimeout = TimeSpan.FromSeconds(44);
        var services = new ServiceCollection();
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = featureBaseAddress;
            options.EnableRetry = false;
            options.RoutingServiceId = "FeatureRouting";
        });
        services.AddHonuaRouting(options =>
        {
            options.BaseAddress = routingBaseAddress;
            options.EnableRetry = false;
            options.Timeout = routingTimeout;
            options.RoutingServiceId = "Network";
        });

        using var provider = services.BuildServiceProvider();
        var routingClient = provider.GetRequiredService<HonuaRoutingClient>();

        Assert.Equal(routingBaseAddress, GetHttpClient(routingClient).BaseAddress);
        Assert.Equal(routingTimeout, GetHttpClient(routingClient).Timeout);
        Assert.Equal("Network", GetRoutingOptions(routingClient).RoutingServiceId);
    }

    [Fact]
    public void AddHonuaFeatureServer_InvalidTimeout_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<Honua.Sdk.Abstractions.HonuaConfigurationException>(() =>
            services.AddHonuaFeatureServer(options => { options.BaseAddress = new Uri("https://localhost:5001"); options.Timeout = TimeSpan.FromMilliseconds(10); }));

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient GetHttpClient(object client)
    {
        var field = client.GetType().GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(client);
        return Assert.IsType<HttpClient>(value);
    }

    private static HonuaGeoServicesClientOptions GetRoutingOptions(HonuaRoutingClient client)
    {
        var field = typeof(HonuaRoutingClient).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(client);
        return Assert.IsType<HonuaGeoServicesClientOptions>(value);
    }
}
