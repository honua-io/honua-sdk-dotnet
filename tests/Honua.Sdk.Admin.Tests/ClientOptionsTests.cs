// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Reflection;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.Admin.Geocoding;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.Admin.Tests;

public sealed class ClientOptionsTests
{
    [Fact]
    public void DefaultTimeout_IsOneHundredSeconds()
    {
        var options = new HonuaAdminClientOptions();

        Assert.Equal(TimeSpan.FromSeconds(100), options.Timeout);
    }

    [Fact]
    public void AddHonuaAdmin_ConfiguresHttpClientTimeout()
    {
        var timeout = TimeSpan.FromSeconds(42);
        var services = new ServiceCollection();
        services.AddHonuaAdmin(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
            options.Timeout = timeout;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHonuaAdminClient>();

        Assert.Equal(timeout, GetHttpClient(client, "_http").Timeout);
    }

    [Fact]
    public void AddHonuaGeocoding_ConfiguresHttpClientTimeout()
    {
        var timeout = TimeSpan.FromSeconds(37);
        var services = new ServiceCollection();
        services.AddHonuaGeocoding(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
            options.Timeout = timeout;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHonuaGeocodingClient>();

        Assert.Equal(timeout, GetHttpClient(client, "_http").Timeout);
    }

    [Fact]
    public void AddHonuaAdmin_InvalidTimeout_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddHonuaAdmin(options => options.Timeout = TimeSpan.FromMilliseconds(10)));

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient GetHttpClient(object client, string fieldName)
    {
        var field = client.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(client);
        return Assert.IsType<HttpClient>(value);
    }
}
