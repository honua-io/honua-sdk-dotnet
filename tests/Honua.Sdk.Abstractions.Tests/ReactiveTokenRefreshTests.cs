// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class ReactiveTokenRefreshTests
{
    [Theory]
    [InlineData(401, false)]
    [InlineData(498, true)]
    [InlineData(499, true)]
    public async Task AuthHandler_RefreshesTokenOnceOn401AndGeoServices498Or499(int code, bool geoServicesEnvelope)
    {
        var forceRefreshFlags = new List<bool>();
        var tokenCount = 0;
        var provider = new RecordingTokenProvider(req =>
        {
            forceRefreshFlags.Add(req.ForceRefresh);
            return new HonuaAccessToken { Token = $"token-{++tokenCount}" };
        });

        var seenAuth = new List<string?>();
        var seenBodies = new List<string>();
        var inner = new StubHandler(async (req, ct) =>
        {
            seenAuth.Add(req.Headers.Authorization?.ToString());
            seenBodies.Add(req.Content is not null ? await req.Content.ReadAsStringAsync(ct).ConfigureAwait(false) : string.Empty);
            if (seenAuth.Count == 1)
            {
                return geoServicesEnvelope
                    ? JsonResponse(HttpStatusCode.OK, $$$"""{"error":{"code":{{{code}}},"message":"Token expired"}}""")
                    : JsonResponse(HttpStatusCode.Unauthorized, """{"title":"Unauthorized"}""");
            }

            return JsonResponse(HttpStatusCode.OK, """{"features":[]}""");
        });

        using var handler = new TestAuthHandler(new TestOptions { AccessTokenProvider = provider }) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        using var content = new StringContent("where=1%3D1&f=json", Encoding.UTF8, "application/x-www-form-urlencoded");

        using var response = await client.PostAsync("/rest/services/Parcels/FeatureServer/0/query", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"features":[]}""", await response.Content.ReadAsStringAsync());
        Assert.Equal([false, true], forceRefreshFlags);
        Assert.Equal(["Bearer token-1", "Bearer token-2"], seenAuth);
        Assert.Equal(["where=1%3D1&f=json", "where=1%3D1&f=json"], seenBodies);
    }

    [Fact]
    public async Task AuthHandler_StopsAfterSingleRefreshWhenChallengePersists()
    {
        var tokenCount = 0;
        var provider = new RecordingTokenProvider(_ => new HonuaAccessToken { Token = $"token-{++tokenCount}" });
        var attempts = 0;
        var inner = new StubHandler((_, _) =>
        {
            attempts++;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, """{"error":{"code":498,"message":"Invalid Token"}}"""));
        });

        using var handler = new TestAuthHandler(new TestOptions { AccessTokenProvider = provider }) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };

        using var response = await client.GetAsync("/rest/services/Parcels/FeatureServer/0/query");

        Assert.Equal(2, attempts);
        Assert.Equal(2, tokenCount);
        Assert.Contains("\"code\":498", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthHandler_DoesNotRetryWhenOnlyStaticBearerTokenConfigured()
    {
        var attempts = 0;
        var inner = new StubHandler((_, _) =>
        {
            attempts++;
            return Task.FromResult(JsonResponse(HttpStatusCode.Unauthorized, """{"title":"Unauthorized"}"""));
        });

        using var handler = new TestAuthHandler(new TestOptions { BearerToken = "static-token" }) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };

        using var response = await client.GetAsync("/rest/services/Parcels/FeatureServer/0/query");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task AuthHandler_DisposesChallengedResponseWhenRefreshThrows()
    {
        var disposed = false;
        var provider = new RecordingTokenProvider(req =>
        {
            if (req.ForceRefresh)
            {
                throw new InvalidOperationException("refresh failed");
            }

            return new HonuaAccessToken { Token = "stale-token" };
        });

        var inner = new StubHandler((_, _) => Task.FromResult<HttpResponseMessage>(
            new TrackingDisposableResponse(HttpStatusCode.Unauthorized, () => disposed = true)));

        using var handler = new TestAuthHandler(new TestOptions { AccessTokenProvider = provider }) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetAsync("/rest/services/Parcels/FeatureServer/0/query"));

        Assert.True(disposed);
    }

    [Fact]
    public async Task OAuthClientCredentialsProvider_ForceRefreshEvictsCachedToken()
    {
        var calls = 0;
        using var tokenHttp = new HttpClient(new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                $$"""{"access_token":"oauth-{{calls}}","token_type":"Bearer","expires_in":3600}"""));
        }));
        var provider = new HonuaOAuthClientCredentialsTokenProvider(
            tokenHttp,
            new HonuaOAuthClientCredentialsTokenProviderOptions
            {
                TokenEndpoint = new Uri("https://auth.example.test/oauth/token"),
                ClientId = "client-1",
                ClientSecret = "secret-1",
                RefreshSkew = TimeSpan.Zero
            });

        var request = new HonuaAuthenticationRequest
        {
            Transport = HonuaAuthenticationTransport.Http,
            ServiceName = "geoservices"
        };

        var first = await provider.GetAccessTokenAsync(request);
        var cached = await provider.GetAccessTokenAsync(request);
        var refreshed = await provider.GetAccessTokenAsync(request with { ForceRefresh = true });

        Assert.Equal("oauth-1", first!.Token);
        Assert.Equal("oauth-1", cached!.Token);
        Assert.Equal("oauth-2", refreshed!.Token);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task OAuthAuthorizationCodeProvider_ForceRefreshEvictsCachedToken()
    {
        var calls = 0;
        using var tokenHttp = new HttpClient(new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(JsonResponse(
                HttpStatusCode.OK,
                $$"""{"access_token":"authcode-{{calls}}","refresh_token":"rt-{{calls}}","token_type":"Bearer","expires_in":3600}"""));
        }));
        var provider = new HonuaOAuthAuthorizationCodeTokenProvider(
            tokenHttp,
            new HonuaOAuthAuthorizationCodeTokenProviderOptions
            {
                TokenEndpoint = new Uri("https://auth.example.test/oauth/token"),
                ClientId = "client-1",
                RefreshToken = "rt-initial",
                RefreshSkew = TimeSpan.Zero
            });

        var request = new HonuaAuthenticationRequest
        {
            Transport = HonuaAuthenticationTransport.Http,
            ServiceName = "geoservices"
        };

        var first = await provider.GetAccessTokenAsync(request);
        var refreshed = await provider.GetAccessTokenAsync(request with { ForceRefresh = true });

        Assert.Equal("authcode-1", first!.Token);
        Assert.Equal("authcode-2", refreshed!.Token);
        Assert.Equal(2, calls);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class TestAuthHandler : HonuaAuthHandler<TestOptions>
    {
        public TestAuthHandler(TestOptions options)
            : base(options, "geoservices", _ => { })
        {
        }
    }

    private sealed class TestOptions : IHonuaClientOptions, IHonuaAuthenticationOptions
    {
        public Uri? BaseAddress { get; set; } = new Uri("https://example.test");
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        public bool EnableRetry { get; set; }
        public int MaxRetryAttempts { get; set; } = 1;
        public Func<HttpMessageHandler>? PrimaryHttpMessageHandlerFactory { get; set; }
        public string? ApiKey { get; init; }
        public Func<CancellationToken, Task<string?>>? ApiKeyProvider { get; init; }
        public string? BearerToken { get; init; }
        public Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; init; }
        public IHonuaAccessTokenProvider? AccessTokenProvider { get; init; }
        public IReadOnlyList<string> AuthenticationScopes { get; init; } = [];
        public string? AuthenticationAudience { get; init; }
        public HonuaAuthenticationDiagnosticHandler? AuthenticationDiagnostics { get; init; }
    }

    private sealed class RecordingTokenProvider : IHonuaAccessTokenProvider
    {
        private readonly Func<HonuaAuthenticationRequest, HonuaAccessToken?> _factory;

        public RecordingTokenProvider(Func<HonuaAuthenticationRequest, HonuaAccessToken?> factory)
            => _factory = factory;

        public ValueTask<HonuaAccessToken?> GetAccessTokenAsync(
            HonuaAuthenticationRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_factory(request));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }

    private sealed class TrackingDisposableResponse(HttpStatusCode status, Action onDispose) : HttpResponseMessage(status)
    {
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                onDispose();
            }

            base.Dispose(disposing);
        }
    }
}
