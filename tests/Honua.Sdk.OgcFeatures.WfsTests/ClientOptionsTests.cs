// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Reflection;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.OgcFeatures.Wfs.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.OgcFeatures.WfsTests;

public sealed class ClientOptionsTests
{
    [Fact]
    public void DefaultTimeout_IsOneHundredSeconds()
    {
        var options = new HonuaWfsClientOptions();

        Assert.Equal(TimeSpan.FromSeconds(100), options.Timeout);
    }

    [Fact]
    public void AddHonuaWfs_ConfiguresHttpClientTimeout()
    {
        var timeout = TimeSpan.FromSeconds(43);
        var services = new ServiceCollection();
        services.AddHonuaWfs(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
            options.Timeout = timeout;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<HonuaWfsClient>();

        Assert.Equal(timeout, GetHttpClient(client).Timeout);
    }

    [Fact]
    public void AddHonuaWfs_RegistersUnsupportedSharedEditClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaWfs(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var editClient = Assert.Single(provider.GetServices<IHonuaFeatureEditClient>());
        var attachmentClient = Assert.Single(provider.GetServices<IHonuaFeatureAttachmentClient>());

        Assert.Equal("wfs", editClient.ProviderName);
        Assert.False(editClient.EditCapabilities.SupportsAdds);
        Assert.False(editClient.EditCapabilities.SupportsUpdates);
        Assert.False(editClient.EditCapabilities.SupportsPatches);
        Assert.False(editClient.EditCapabilities.SupportsDeletes);
        Assert.Contains("WFS-T", editClient.EditCapabilities.UnsupportedReason);
        Assert.Equal("wfs", attachmentClient.ProviderName);
        Assert.False(attachmentClient.AttachmentCapabilities.SupportsList);
        Assert.Contains("attachment operations", attachmentClient.AttachmentCapabilities.UnsupportedReason);
    }

    [Fact]
    public void AddHonuaWfs_InvalidTimeout_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<Honua.Sdk.Abstractions.HonuaConfigurationException>(() =>
            services.AddHonuaWfs(options => { options.BaseAddress = new Uri("https://localhost:5001"); options.Timeout = TimeSpan.FromMilliseconds(10); }));

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient GetHttpClient(HonuaWfsClient client)
    {
        var field = typeof(HonuaWfsClient).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var value = field!.GetValue(client);
        return Assert.IsType<HttpClient>(value);
    }
}
