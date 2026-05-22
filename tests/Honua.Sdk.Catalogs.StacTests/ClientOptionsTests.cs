// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Reflection;
using Honua.Sdk.Catalogs.Stac;
using Honua.Sdk.Catalogs.Stac.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.Catalogs.StacTests;

public sealed class ClientOptionsTests
{
    [Fact]
    public void DefaultTimeout_IsOneHundredSeconds()
    {
        var options = new HonuaStacClientOptions();

        Assert.Equal(TimeSpan.FromSeconds(100), options.Timeout);
    }

    [Fact]
    public void AddHonuaStac_ConfiguresHttpClientTimeout()
    {
        var timeout = TimeSpan.FromSeconds(45);
        var services = new ServiceCollection();
        services.AddHonuaStac(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
            options.Timeout = timeout;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<HonuaStacClient>();

        Assert.Equal(timeout, GetHttpClient(client).Timeout);
    }

    [Fact]
    public void AddHonuaStac_RegistersClientInterface()
    {
        var services = new ServiceCollection();
        services.AddHonuaStac(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var client = Assert.Single(provider.GetServices<IHonuaStacClient>());

        Assert.IsType<HonuaStacClient>(client);
    }

    [Fact]
    public void AddHonuaStac_InvalidTimeout_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<Honua.Sdk.Abstractions.HonuaConfigurationException>(() =>
            services.AddHonuaStac(options => { options.BaseAddress = new Uri("https://localhost:5001"); options.Timeout = TimeSpan.FromMilliseconds(10); }));

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient GetHttpClient(HonuaStacClient client)
    {
        var field = typeof(HonuaStacClient).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(client);
        return Assert.IsType<HttpClient>(value);
    }
}
