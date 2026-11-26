// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Abstractions.Authentication;
using Honua.Sdk.GeoServices;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.GeoServices.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace Honua.Sdk.GeoServices.Tests;

public sealed class GeoServicesRetryPolicyTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public void IsRetryableRequest_IdempotentMethods_AreRetryable(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "https://host/rest/services/x/FeatureServer/0/query");
        Assert.True(GeoServicesRetryPolicy.IsRetryableRequest(request));
    }

    [Fact]
    public void IsRetryableRequest_QueryPostFallback_IsRetryable()
    {
        // Long filter strings force GET -> POST; the /query read is still idempotent.
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://host/rest/services/x/FeatureServer/0/query");
        Assert.True(GeoServicesRetryPolicy.IsRetryableRequest(request));
    }

    [Fact]
    public void IsRetryableRequest_QueryPostFallback_WithRelativeUri_IsRetryable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("rest/services/x/FeatureServer/0/query", UriKind.Relative));
        Assert.True(GeoServicesRetryPolicy.IsRetryableRequest(request));
    }

    [Fact]
    public void IsRetryableRequest_ApplyEditsPost_IsNotRetryable()
    {
        // applyEdits is a mutation and must never be auto-retried.
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://host/rest/services/x/FeatureServer/0/applyEdits");
        Assert.False(GeoServicesRetryPolicy.IsRetryableRequest(request));
    }

    [Fact]
    public void IsRetryableRequest_PatchAddAttachment_IsNotRetryable()
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, "https://host/rest/services/x/FeatureServer/0/1/addAttachment");
        Assert.False(GeoServicesRetryPolicy.IsRetryableRequest(request));
    }

    [Fact]
    public void IsRetryableRequest_NullRequest_FallsBackToTransientPolicy()
    {
        Assert.True(GeoServicesRetryPolicy.IsRetryableRequest(null));
    }

    [Fact]
    public async Task ShouldRetryAsync_Http503WithHonuaRetryableFalse_ReturnsFalse()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://host/rest/services/x/FeatureServer/0/query");
        using var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("""{"code":"CAPABILITY_UNAVAILABLE"}""", Encoding.UTF8, "application/json")
        };
        response.Headers.Add("Honua-Retryable", "false");

        var shouldRetry = await GeoServicesRetryPolicy.ShouldRetryAsync(Outcome.FromResult(response), request);

        Assert.False(shouldRetry);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    public async Task ShouldRetryAsync_GeoServicesHttp200RetryableEnvelope_ReturnsTrue(int code)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://host/rest/services/x/FeatureServer/0/query");
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$$"""{"error":{"code":{{{code}}},"message":"Busy"}}""", Encoding.UTF8, "application/json")
        };
        response.Headers.Add("Honua-Retryable", "true");

        var shouldRetry = await GeoServicesRetryPolicy.ShouldRetryAsync(Outcome.FromResult(response), request);

        Assert.True(shouldRetry);
    }

    [Fact]
    public async Task ShouldRetryAsync_GeoServicesHttp200WithHonuaRetryableFalse_ReturnsFalse()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://host/rest/services/x/FeatureServer/0/query");
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"error":{"code":503,"message":"Disabled"}}""", Encoding.UTF8, "application/json")
        };
        response.Headers.Add("Honua-Retryable", "false");

        var shouldRetry = await GeoServicesRetryPolicy.ShouldRetryAsync(Outcome.FromResult(response), request);

        Assert.False(shouldRetry);
    }

    [Fact]
    public async Task TryGetRetryDelayAsync_HonorsRetryAfterHeaderAndEnvelopeFallback()
    {
        using var withHeader = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"error":{"code":429,"message":"Rate limited"}}""", Encoding.UTF8, "application/json")
        };
        withHeader.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(3));

        using var withBody = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"error":{"code":503,"retryAfterSeconds":2}}""", Encoding.UTF8, "application/json")
        };

        Assert.Equal(TimeSpan.FromSeconds(3), await GeoServicesRetryPolicy.TryGetRetryDelayAsync(withHeader));
        Assert.Equal(TimeSpan.FromSeconds(2), await GeoServicesRetryPolicy.TryGetRetryDelayAsync(withBody));
    }

    [Fact]
    public async Task FeatureServerPipeline_RetriesGeoServices200Envelope429AndHonorsRetryAfter()
    {
        var attempts = 0;
        var services = new ServiceCollection();
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = new Uri("https://example.test");
            options.EnableRetry = true;
            options.MaxRetryAttempts = 3;
            options.PrimaryHttpMessageHandlerFactory = () => new MockHttpHandler(_ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    return Task.FromResult(CreateResponse(
                        HttpStatusCode.OK,
                        """{"error":{"code":429,"message":"Throttled"}}""",
                        retryableHeader: "true",
                        retryAfter: TimeSpan.Zero));
                }

                return Task.FromResult(CreateResponse(HttpStatusCode.OK, """{"features":[]}"""));
            });
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHonuaFeatureServerClient>();
        var result = await client.QueryAsync("Parcels", 0, new FeatureServerQueryParams());

        Assert.Equal(2, attempts);
        Assert.NotNull(result.Features);
        Assert.Empty(result.Features);
    }

    [Fact]
    public async Task FeatureServerPipeline_DoesNotRetryHttp503WithHonuaRetryableFalse()
    {
        var attempts = 0;
        var services = new ServiceCollection();
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = new Uri("https://example.test");
            options.EnableRetry = true;
            options.MaxRetryAttempts = 3;
            options.PrimaryHttpMessageHandlerFactory = () => new MockHttpHandler(_ =>
            {
                attempts++;
                return Task.FromResult(CreateResponse(
                    HttpStatusCode.ServiceUnavailable,
                    """{"code":"CAPABILITY_UNAVAILABLE","message":"Disabled"}""",
                    retryableHeader: "false"));
            });
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHonuaFeatureServerClient>();

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(
            () => client.QueryAsync("Parcels", 0, new FeatureServerQueryParams()));

        Assert.Equal(1, attempts);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.False(ex.Retryable);
    }

    [Fact]
    public async Task FeatureServerPipeline_RefreshesTokenOnGeoServices498AndSucceeds()
    {
        var seenAuth = new List<string?>();
        var tokenNumber = 0;
        var services = new ServiceCollection();
        services.AddHonuaFeatureServer(options =>
        {
            options.BaseAddress = new Uri("https://example.test");
            options.EnableRetry = true;
            options.MaxRetryAttempts = 2;
            options.AccessTokenProvider = new SequencedTokenProvider(() => $"geo-token-{++tokenNumber}");
            options.PrimaryHttpMessageHandlerFactory = () => new MockHttpHandler(request =>
            {
                seenAuth.Add(request.Headers.Authorization?.ToString());
                if (seenAuth.Count == 1)
                {
                    return Task.FromResult(CreateResponse(
                        HttpStatusCode.OK,
                        """{"error":{"code":498,"message":"Invalid Token"}}"""));
                }

                return Task.FromResult(CreateResponse(HttpStatusCode.OK, """{"features":[]}"""));
            });
        });

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHonuaFeatureServerClient>();
        var result = await client.QueryAsync("Parcels", 0, new FeatureServerQueryParams());

        Assert.NotNull(result.Features);
        Assert.Empty(result.Features);
        Assert.Equal(["Bearer geo-token-1", "Bearer geo-token-2"], seenAuth);
    }

    private static HttpResponseMessage CreateResponse(
        HttpStatusCode status,
        string json,
        string? retryableHeader = null,
        TimeSpan? retryAfter = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (retryableHeader is not null)
        {
            response.Headers.Add("Honua-Retryable", retryableHeader);
        }

        if (retryAfter.HasValue)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter.Value);
        }

        return response;
    }

    private sealed class SequencedTokenProvider(Func<string> nextToken) : IHonuaAccessTokenProvider
    {
        public ValueTask<HonuaAccessToken?> GetAccessTokenAsync(
            HonuaAuthenticationRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<HonuaAccessToken?>(new HonuaAccessToken { Token = nextToken() });
    }
}
