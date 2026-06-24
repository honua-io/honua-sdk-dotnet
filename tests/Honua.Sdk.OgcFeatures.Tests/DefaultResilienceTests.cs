// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.OgcFeatures.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.OgcFeatures.Tests;

/// <summary>
/// Regression tests for issue #200: registering and using a client with the
/// SDK's default options (retry enabled, 100 s timeout) must not throw
/// <c>OptionsValidationException</c> when the standard resilience pipeline is
/// built on first use.
/// </summary>
public sealed class DefaultResilienceTests
{
    private static IServiceProvider BuildDefaultProvider(HttpStatusCode responseStatus)
    {
        var services = new ServiceCollection();
        services.AddHonuaOgcFeatures(options =>
        {
            // Only the required BaseAddress is configured. EnableRetry (true) and
            // Timeout (100 s) keep their defaults so the resilience pipeline is
            // built with the exact configuration that previously threw.
            options.BaseAddress = new Uri("https://example.test");
            options.PrimaryHttpMessageHandlerFactory =
                () => new StubHandler(responseStatus);
        });

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task DefaultConfiguredClient_ResolvesAndSendsWithoutValidationException()
    {
        using var provider = (ServiceProvider)BuildDefaultProvider(HttpStatusCode.OK);

        // Resolving builds the typed client; the standard resilience pipeline is
        // materialized and validated lazily on the first send. Both steps must
        // succeed with default options.
        var client = provider.GetRequiredService<HonuaOgcFeaturesClient>();
        Assert.NotNull(client);

        var http = GetHttpClient(client);
        using var response = await http.GetAsync(
            new Uri("https://example.test/collections"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DefaultConfiguredClient_RetriesTransientFailures()
    {
        var services = new ServiceCollection();
        var handler = new CountingTransientHandler(failuresBeforeSuccess: 2);
        services.AddHonuaOgcFeatures(options =>
        {
            options.BaseAddress = new Uri("https://example.test");
            options.PrimaryHttpMessageHandlerFactory = () => handler;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<HonuaOgcFeaturesClient>();
        var http = GetHttpClient(client);

        using var response = await http.GetAsync(
            new Uri("https://example.test/collections"), CancellationToken.None);

        // The two 503s should have been retried and then succeeded, proving the
        // total budget actually leaves room for retry attempts.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.Attempts);
    }

    private static HttpClient GetHttpClient(HonuaOgcFeaturesClient client)
    {
        var field = typeof(HonuaOgcFeaturesClient).GetField(
            "_http",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<HttpClient>(field!.GetValue(client));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public StubHandler(HttpStatusCode status) => _status = status;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_status));
    }

    private sealed class CountingTransientHandler : HttpMessageHandler
    {
        private readonly int _failuresBeforeSuccess;
        private int _attempts;

        public CountingTransientHandler(int failuresBeforeSuccess)
            => _failuresBeforeSuccess = failuresBeforeSuccess;

        public int Attempts => _attempts;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _attempts);
            var status = attempt <= _failuresBeforeSuccess
                ? HttpStatusCode.ServiceUnavailable
                : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
}
