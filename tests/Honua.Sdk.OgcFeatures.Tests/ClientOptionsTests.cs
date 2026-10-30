// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Reflection;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.OgcFeatures;
using Honua.Sdk.OgcFeatures.Extensions;
using Honua.Sdk.OgcFeatures.Styles;
using Honua.Sdk.OgcFeatures.Tiles;
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
        var nativePatchClient = Assert.Single(provider.GetServices<IHonuaOgcFeaturesPatchClient>());
        var attachmentClient = Assert.Single(provider.GetServices<IHonuaFeatureAttachmentClient>());

        Assert.Equal("ogc-features", editClient.ProviderName);
        Assert.True(editClient.EditCapabilities.SupportsAdds);
        Assert.True(editClient.EditCapabilities.SupportsUpdates);
        Assert.True(editClient.EditCapabilities.SupportsPatches);
        Assert.True(editClient.EditCapabilities.SupportsDeletes);
        Assert.False(editClient.EditCapabilities.SupportsRollbackOnFailure);
        Assert.IsType<HonuaOgcFeaturesClient>(nativeEditClient);
        Assert.IsType<HonuaOgcFeaturesClient>(nativePatchClient);
        Assert.Equal("ogc-features", attachmentClient.ProviderName);
        Assert.False(attachmentClient.AttachmentCapabilities.SupportsList);
        Assert.Contains("attachment operations", attachmentClient.AttachmentCapabilities.UnsupportedReason);
    }

    [Fact]
    public void AddHonuaOgcFeatures_RegistersStylesClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaOgcFeatures(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var stylesClient = Assert.Single(provider.GetServices<IHonuaOgcStylesClient>());

        Assert.IsType<HonuaOgcStylesClient>(stylesClient);
    }

    [Fact]
    public void AddHonuaOgcTiles_RegistersTilesClientWithoutFeatures()
    {
        var services = new ServiceCollection();
        services.AddHonuaOgcTiles(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var tilesClient = Assert.Single(provider.GetServices<IHonuaOgcTilesClient>());

        Assert.IsType<HonuaOgcTilesClient>(tilesClient);
        Assert.Null(provider.GetService<IHonuaOgcFeaturesClient>());
    }

    [Fact]
    public async Task AddHonuaOgcTiles_UsesTheSharedFeaturesRetryPolicy()
    {
        var handler = new CountingHandler(failuresBeforeSuccess: 1);
        var services = new ServiceCollection();
        services.AddHonuaOgcFeatures(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = true;
            options.MaxRetryAttempts = 2;
            options.PrimaryHttpMessageHandlerFactory = () => handler;
        });
        services.AddHonuaOgcTiles(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var tiles = provider.GetRequiredService<IHonuaOgcTilesClient>();

        var tile = await tiles.GetTileAsync("segments", "WebMercatorQuad", 0, 0, 0);

        Assert.False(tile.IsEmpty);
        Assert.Equal(2, handler.Attempts);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly int _failuresBeforeSuccess;
        private int _attempts;

        public CountingHandler(int failuresBeforeSuccess) => _failuresBeforeSuccess = failuresBeforeSuccess;

        public int Attempts => _attempts;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _attempts);
            var status = attempt <= _failuresBeforeSuccess
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK;
            return Task.FromResult(Fixtures.TestHelpers.CreateBytesResponse(
                [0x1a], "application/vnd.mapbox-vector-tile", status));
        }
    }

    [Fact]
    public void AddHonuaOgcFeatures_InvalidTimeout_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<Honua.Sdk.Abstractions.HonuaConfigurationException>(() =>
            services.AddHonuaOgcFeatures(options => { options.BaseAddress = new Uri("https://localhost:5001"); options.Timeout = TimeSpan.FromMilliseconds(10); }));

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
