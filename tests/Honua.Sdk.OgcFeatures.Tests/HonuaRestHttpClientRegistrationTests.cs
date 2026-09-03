// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Internal.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Honua.Sdk.OgcFeatures.Tests;

/// <summary>
/// Unit coverage for the shared <see cref="HonuaRestHttpClientRegistration"/> helper
/// (AUD-082). The helper centralizes the primary-handler + resilience block that every
/// REST <c>ServiceCollectionExtensions</c> previously copy-pasted, so these tests pin
/// its behavior directly rather than through any single client registration.
/// </summary>
public sealed class HonuaRestHttpClientRegistrationTests
{
    [Fact]
    public async Task ConfigureHonuaRestHttpClient_WithDefaultRetry_RetriesTransientSafeMethods()
    {
        var handler = new CountingHandler(failuresBeforeSuccess: 2);
        var options = new TestClientOptions
        {
            // Default 100 s budget + retry enabled is the configuration that must
            // build a valid resilience pipeline (no OptionsValidationException).
            PrimaryHttpMessageHandlerFactory = () => handler,
        };

        using var provider = BuildProvider(options, configureRetry: null);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");

        using var response = await client.GetAsync(new Uri("https://example.test/collections"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.Attempts);
    }

    [Fact]
    public async Task ConfigureHonuaRestHttpClient_RetriesTransportFailuresBeforeNormalization()
    {
        var handler = new TransportFailureHandler(failuresBeforeSuccess: 2);
        var options = new TestClientOptions
        {
            PrimaryHttpMessageHandlerFactory = () => handler,
        };

        using var provider = BuildProvider(options, configureRetry: null);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");

        using var response = await client.GetAsync(
            new Uri("https://example.test/collections"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.Attempts);
    }

    [Fact]
    public async Task ConfigureHonuaRestHttpClient_WhenTransportRetriesAreExhausted_NormalizesFailure()
    {
        var handler = new TransportFailureHandler(failuresBeforeSuccess: int.MaxValue);
        var options = new TestClientOptions
        {
            MaxRetryAttempts = 3,
            PrimaryHttpMessageHandlerFactory = () => handler,
        };

        using var provider = BuildProvider(options, configureRetry: null);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");

        var exception = await Assert.ThrowsAsync<HonuaTransportException>(() =>
            client.GetAsync(new Uri("https://example.test/collections"), CancellationToken.None));

        Assert.Equal(options.MaxRetryAttempts, handler.Attempts);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    [Fact]
    public async Task ConfigureHonuaRestHttpClient_WhenRetriesAreExhausted_UsesConfiguredTotalAttempts()
    {
        var handler = new CountingHandler(failuresBeforeSuccess: int.MaxValue);
        var options = new TestClientOptions
        {
            MaxRetryAttempts = 3,
            PrimaryHttpMessageHandlerFactory = () => handler,
        };

        using var provider = BuildProvider(
            options,
            configureRetry: retry =>
            {
                retry.Delay = TimeSpan.Zero;
                retry.UseJitter = false;
                retry.ShouldHandle = args =>
                    ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
            });
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");

        using var response = await client.GetAsync(
            new Uri("https://example.test/collections"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(options.MaxRetryAttempts, handler.Attempts);
    }

    [Fact]
    public async Task ConfigureHonuaRestHttpClient_WithDefaultRetry_DoesNotRetryUnsafePost()
    {
        var handler = new CountingHandler(failuresBeforeSuccess: int.MaxValue);
        var options = new TestClientOptions
        {
            PrimaryHttpMessageHandlerFactory = () => handler,
        };

        using var provider = BuildProvider(options, configureRetry: null);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");

        using var response = await client.PostAsync(
            new Uri("https://example.test/query"), content: null, CancellationToken.None);

        // The default policy (DisableForUnsafeHttpMethods) must not retry POST.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.Attempts);
    }

    [Fact]
    public async Task ConfigureHonuaRestHttpClient_WithCustomRetry_RetriesPost()
    {
        var handler = new CountingHandler(failuresBeforeSuccess: 2);
        var options = new TestClientOptions
        {
            PrimaryHttpMessageHandlerFactory = () => handler,
        };

        // A caller-supplied retry hook (as GeoServices uses for its idempotent
        // /query POST fallback) overrides the default safe-method-only policy.
        using var provider = BuildProvider(
            options,
            configureRetry: retry =>
                retry.ShouldHandle = args => ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome)));
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");

        using var response = await client.PostAsync(
            new Uri("https://example.test/query"), content: null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.Attempts);
    }

    [Fact]
    public async Task ConfigureHonuaRestHttpClient_WithRetryDisabled_DoesNotRetry()
    {
        var handler = new CountingHandler(failuresBeforeSuccess: int.MaxValue);
        var options = new TestClientOptions
        {
            EnableRetry = false,
            PrimaryHttpMessageHandlerFactory = () => handler,
        };

        using var provider = BuildProvider(options, configureRetry: null);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("test");

        using var response = await client.GetAsync(new Uri("https://example.test/collections"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, handler.Attempts);
    }

    private static ServiceProvider BuildProvider(
        IHonuaClientOptions options,
        Action<HttpRetryStrategyOptions>? configureRetry)
    {
        var services = new ServiceCollection();
        services
            .AddHttpClient("test", client => client.BaseAddress = options.BaseAddress)
            .ConfigureHonuaRestHttpClient(options, configureRetry);

        return services.BuildServiceProvider();
    }

    private sealed class TestClientOptions : IHonuaClientOptions
    {
        public Uri? BaseAddress { get; set; } = new("https://example.test");

        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(100);

        public bool EnableRetry { get; set; } = true;

        public int MaxRetryAttempts { get; set; } = 3;

        public Func<HttpMessageHandler>? PrimaryHttpMessageHandlerFactory { get; set; }
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
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class TransportFailureHandler : HttpMessageHandler
    {
        private readonly int _failuresBeforeSuccess;
        private int _attempts;

        public TransportFailureHandler(int failuresBeforeSuccess) => _failuresBeforeSuccess = failuresBeforeSuccess;

        public int Attempts => _attempts;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _attempts);
            if (attempt <= _failuresBeforeSuccess)
            {
                throw new HttpRequestException("simulated transport failure");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
