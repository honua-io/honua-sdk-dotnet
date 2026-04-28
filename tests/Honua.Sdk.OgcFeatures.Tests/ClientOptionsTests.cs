// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Reflection;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.OgcFeatures.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.OgcFeatures.Tests;

public sealed class ClientOptionsTests
{
    [Fact]
    public void DefaultTimeout_IsOneHundredSeconds()
    {
        var options = new HonuaOgcFeaturesClientOptions();

        Assert.Equal(TimeSpan.FromSeconds(100), options.Timeout);
    }

    [Fact]
    public void AddHonuaOgcFeatures_ConfiguresHttpClientTimeout()
    {
        var timeout = TimeSpan.FromSeconds(45);
        var services = new ServiceCollection();
        services.AddHonuaOgcFeatures(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
            options.Timeout = timeout;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<HonuaOgcFeaturesClient>();

        Assert.Equal(timeout, GetHttpClient(client).Timeout);
    }

    [Fact]
    public void AddHonuaOgcFeatures_RegistersEditClients()
    {
        var services = new ServiceCollection();
        services.AddHonuaOgcFeatures(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var editClient = Assert.Single(provider.GetServices<IHonuaFeatureEditClient>());
        var nativeEditClient = Assert.Single(provider.GetServices<IHonuaOgcFeaturesEditClient>());

        Assert.Equal("ogc-features", editClient.ProviderName);
        Assert.True(editClient.EditCapabilities.SupportsAdds);
        Assert.True(editClient.EditCapabilities.SupportsUpdates);
        Assert.True(editClient.EditCapabilities.SupportsDeletes);
        Assert.False(editClient.EditCapabilities.SupportsRollbackOnFailure);
        Assert.IsType<HonuaOgcFeaturesClient>(nativeEditClient);
    }

    [Fact]
    public void AddHonuaOgcFeatures_InvalidTimeout_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddHonuaOgcFeatures(options => options.Timeout = TimeSpan.FromMilliseconds(10)));

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient GetHttpClient(HonuaOgcFeaturesClient client)
    {
        var field = typeof(HonuaOgcFeaturesClient).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(client);
        return Assert.IsType<HttpClient>(value);
    }
}
