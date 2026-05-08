// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Reflection;
using Honua.Sdk.OgcRecords.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.OgcRecords.Tests;

public sealed class ClientOptionsTests
{
    [Fact]
    public void DefaultTimeout_IsOneHundredSeconds()
    {
        var options = new HonuaOgcRecordsClientOptions();

        Assert.Equal(TimeSpan.FromSeconds(100), options.Timeout);
    }

    [Fact]
    public void AddHonuaOgcRecords_ConfiguresHttpClientTimeout()
    {
        var timeout = TimeSpan.FromSeconds(45);
        var services = new ServiceCollection();
        services.AddHonuaOgcRecords(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
            options.Timeout = timeout;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<HonuaOgcRecordsClient>();

        Assert.Equal(timeout, GetHttpClient(client).Timeout);
    }

    [Fact]
    public void AddHonuaOgcRecords_RegistersClientInterface()
    {
        var services = new ServiceCollection();
        services.AddHonuaOgcRecords(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var client = Assert.Single(provider.GetServices<IHonuaOgcRecordsClient>());

        Assert.IsType<HonuaOgcRecordsClient>(client);
    }

    [Fact]
    public void AddHonuaOgcRecords_InvalidTimeout_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddHonuaOgcRecords(options => options.Timeout = TimeSpan.FromMilliseconds(10)));

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient GetHttpClient(HonuaOgcRecordsClient client)
    {
        var field = typeof(HonuaOgcRecordsClient).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(client);
        return Assert.IsType<HttpClient>(value);
    }
}
