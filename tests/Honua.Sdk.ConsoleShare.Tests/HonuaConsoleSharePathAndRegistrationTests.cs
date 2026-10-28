// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using Honua.Sdk.ConsoleShare.Extensions;
using Honua.Sdk.ConsoleShare.Tests.Fixtures;
using Honua.Sdk.ConsoleShare;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.ConsoleShare.Tests;

public sealed class HonuaConsoleSharePathAndRegistrationTests
{
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a\nb")]
    public async Task GetShareAsync_UnsafeShareId_ThrowsBeforeSending(string shareId)
    {
        var sent = 0;
        using var http = CreateHttpClient(_ =>
        {
            sent++;
            return Json("{}");
        });
        var client = new HonuaConsoleShareClient(http);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.GetShareAsync(shareId));

        Assert.Equal(nameof(shareId), ex.ParamName);
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task GetShareAsync_SlashStaysInsideOneSegment()
    {
        string? path = null;
        using var http = CreateHttpClient(request =>
        {
            path = request.RequestUri!.AbsolutePath;
            return Json("{}");
        });
        var client = new HonuaConsoleShareClient(http);

        try
        {
            await client.GetShareAsync("layer/1");
        }
        catch (Exception)
        {
            // The body is only there so the request is observable. Parsing may still fail.
        }

        Assert.Equal("/api/v1/console/shares/layer%2F1", path);
    }

    [Fact]
    public async Task GetExportDefinitionAsync_DotSegment_ThrowsBeforeSending()
    {
        var sent = 0;
        using var http = CreateHttpClient(_ =>
        {
            sent++;
            return Json("{}");
        });
        var client = new HonuaConsoleShareExportClient(http);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.GetExportDefinitionAsync(".."));

        Assert.Equal("exportId", ex.ParamName);
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task GetPublicDatasetAsync_DotSegment_ThrowsBeforeSending()
    {
        var sent = 0;
        using var http = CreateHttpClient(_ =>
        {
            sent++;
            return Json("{}");
        });
        var client = new HonuaConsoleShareOpenDataClient(http);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => client.GetPublicDatasetAsync(".."));

        Assert.Equal("itemId", ex.ParamName);
        Assert.Equal(0, sent);
    }

    [Fact]
    public async Task EachRegistrationKeepsItsOwnBaseAddressAndApiKey()
    {
        var share = new CaptureHandler();
        var export = new CaptureHandler();
        var openData = new CaptureHandler();
        var services = new ServiceCollection();
        services.AddHonuaConsoleShare(options => Configure(options, "https://share.example/", "share-key", share));
        services.AddHonuaConsoleShareExport(options => Configure(options, "https://export.example/", "export-key", export));
        services.AddHonuaConsoleShareOpenData(options => Configure(options, "https://open-data.example/", "open-data-key", openData));

        using var provider = services.BuildServiceProvider();
        var shareClient = provider.GetRequiredService<IHonuaConsoleShareClient>();
        var exportClient = provider.GetRequiredService<IHonuaConsoleShareExportClient>();
        var openDataClient = provider.GetRequiredService<IHonuaConsoleShareOpenDataClient>();

        await Assert.ThrowsAnyAsync<Exception>(() => shareClient.GetShareAsync("share-1"));
        await Assert.ThrowsAnyAsync<Exception>(() => exportClient.GetExportDefinitionAsync("export-1"));
        await Assert.ThrowsAnyAsync<Exception>(() => openDataClient.GetPublicDatasetAsync("item-1"));

        Assert.Equal("share.example", share.Uri!.Host);
        Assert.Equal("share-key", share.ApiKey);
        Assert.Equal("/api/v1/console/shares/share-1", share.Uri.AbsolutePath);
        Assert.Equal("export.example", export.Uri!.Host);
        Assert.Equal("export-key", export.ApiKey);
        Assert.Equal("open-data.example", openData.Uri!.Host);
        Assert.Equal("open-data-key", openData.ApiKey);
    }

    private static void Configure(
        HonuaConsoleShareClientOptions options,
        string baseAddress,
        string apiKey,
        CaptureHandler handler)
    {
        options.BaseAddress = new Uri(baseAddress);
        options.ApiKey = apiKey;
        options.EnableRetry = false;
        options.Timeout = TimeSpan.FromSeconds(30);
        options.PrimaryHttpMessageHandlerFactory = () => handler;
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => new(new MockHttpHandler(handler))
        {
            BaseAddress = new Uri("https://server.example")
        };

    private static Task<HttpResponseMessage> Json(string json)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }

        public string? ApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues("X-API-Key", out var values) ? values.Single() : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}
