// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Abstractions.Authentication;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task ClientCredentialsProvider_RequestsMergedScopesAndCachesToken()
    {
        var calls = 0;
        var formBodies = new List<string>();
        using var http = new HttpClient(new RecordingHandler(async (request, cancellationToken) =>
        {
            calls++;
            formBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            return JsonResponse(
                $$"""
                {
                  "access_token": "access-{{calls}}",
                  "token_type": "Bearer",
                  "expires_in": 3600,
                  "scope": "sdk.read admin.write"
                }
                """);
        }));
        var provider = new HonuaOAuthClientCredentialsTokenProvider(
            http,
            new HonuaOAuthClientCredentialsTokenProviderOptions
            {
                TokenEndpoint = new Uri("http://localhost/oauth/token"),
                ClientId = "client-1",
                ClientSecret = "secret-1",
                Scopes = ["sdk.read"],
                Audience = "server-default",
                RefreshSkew = TimeSpan.Zero
            });

        var request = new HonuaAuthenticationRequest
        {
            Transport = HonuaAuthenticationTransport.Http,
            ServiceName = "admin",
            Scopes = ["admin.write"],
            Audience = "server-override"
        };

        var first = await provider.GetAccessTokenAsync(request);
        var second = await provider.GetAccessTokenAsync(request);
        var third = await provider.GetAccessTokenAsync(new HonuaAuthenticationRequest
        {
            Transport = request.Transport,
            ServiceName = request.ServiceName,
            Scopes = ["admin.write", "admin.delete"],
            Audience = request.Audience
        });

        Assert.Same(first, second);
        Assert.Equal(2, calls);
        Assert.NotNull(first);
        Assert.Equal("access-1", first!.Token);
        Assert.Equal("access-2", third!.Token);
        var form = ParseForm(formBodies[0]);
        Assert.Equal("client_credentials", form["grant_type"]);
        Assert.Equal("client-1", form["client_id"]);
        Assert.Equal("secret-1", form["client_secret"]);
        Assert.Equal("sdk.read admin.write", form["scope"]);
        Assert.Equal("server-override", form["audience"]);
        Assert.Equal("sdk.read admin.write admin.delete", ParseForm(formBodies[1])["scope"]);
    }

    [Fact]
    public async Task ClientCredentialsProvider_CoalescedRefreshIsNotFailedByOneCallerCancelling()
    {
        var calls = 0;
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = new HttpClient(new RecordingHandler(async (request, cancellationToken) =>
        {
            Interlocked.Increment(ref calls);
            requestStarted.TrySetResult();
            await release.Task.ConfigureAwait(false);
            return JsonResponse(
                """
                {
                  "access_token": "shared-token",
                  "token_type": "Bearer",
                  "expires_in": 3600
                }
                """);
        }));
        var provider = new HonuaOAuthClientCredentialsTokenProvider(
            http,
            new HonuaOAuthClientCredentialsTokenProviderOptions
            {
                TokenEndpoint = new Uri("http://localhost/oauth/token"),
                ClientId = "client-1",
                ClientSecret = "secret-1",
                RefreshSkew = TimeSpan.Zero
            });
        var request = new HonuaAuthenticationRequest
        {
            Transport = HonuaAuthenticationTransport.Http,
            ServiceName = "admin"
        };

        using var firstCallerCts = new CancellationTokenSource();
        using var secondCallerCts = new CancellationTokenSource();

        // First caller starts the shared refresh; second caller coalesces onto it.
        var firstCaller = provider.GetAccessTokenAsync(request, firstCallerCts.Token).AsTask();
        await requestStarted.Task;
        var secondCaller = provider.GetAccessTokenAsync(request, secondCallerCts.Token).AsTask();

        // The first caller abandons its request; this must not fail the second caller.
        await firstCallerCts.CancelAsync();
        release.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstCaller);
        var secondToken = await secondCaller;

        Assert.NotNull(secondToken);
        Assert.Equal("shared-token", secondToken!.Token);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task AuthorizationCodeProvider_RefreshesWithReturnedRefreshToken()
    {
        var requests = new List<IReadOnlyDictionary<string, string>>();
        using var http = new HttpClient(new RecordingHandler(async (request, cancellationToken) =>
        {
            requests.Add(ParseForm(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)));
            return JsonResponse(requests.Count == 1
                ? """
                  {
                    "access_token": "access-1",
                    "refresh_token": "refresh-1",
                    "expires_in": 0
                  }
                  """
                : """
                  {
                    "access_token": "access-2",
                    "expires_in": 3600
                  }
                  """);
        }));
        var provider = new HonuaOAuthAuthorizationCodeTokenProvider(
            http,
            new HonuaOAuthAuthorizationCodeTokenProviderOptions
            {
                TokenEndpoint = new Uri("http://localhost/oauth/token"),
                ClientId = "client-1",
                AuthorizationCode = "code-1",
                RedirectUri = new Uri("https://app.example/callback"),
                CodeVerifier = "verifier-1",
                RefreshSkew = TimeSpan.Zero
            });

        var context = new HonuaAuthenticationRequest
        {
            Transport = HonuaAuthenticationTransport.Http,
            ServiceName = "admin"
        };
        var first = await provider.GetAccessTokenAsync(context);
        var second = await provider.GetAccessTokenAsync(context);

        Assert.Equal("access-1", first!.Token);
        Assert.Equal("access-2", second!.Token);
        Assert.Collection(
            requests,
            initial =>
            {
                Assert.Equal("authorization_code", initial["grant_type"]);
                Assert.Equal("code-1", initial["code"]);
                Assert.Equal("https://app.example/callback", initial["redirect_uri"]);
                Assert.Equal("verifier-1", initial["code_verifier"]);
            },
            refresh =>
            {
                Assert.Equal("refresh_token", refresh["grant_type"]);
                Assert.Equal("refresh-1", refresh["refresh_token"]);
            });
    }

    [Fact]
    public async Task ApplyHttpCredentials_UsesAccessTokenProviderAndSanitizedDiagnostics()
    {
        HonuaAuthenticationRequest? providerRequest = null;
        HonuaAuthenticationDiagnostic? diagnostic = null;
        var options = new TestAuthenticationOptions
        {
            ApiKey = "api-secret",
            AccessTokenProvider = new DelegateAccessTokenProvider(request =>
            {
                providerRequest = request;
                return new HonuaAccessToken { Token = "bearer-secret" };
            }),
            AuthenticationScopes = ["admin.read"],
            AuthenticationAudience = "honua-admin",
            AuthenticationDiagnostics = (authDiagnostic, _) =>
            {
                diagnostic = authDiagnostic;
                return ValueTask.CompletedTask;
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://honua.example/admin/services?token=raw-secret");

        await HonuaAuthenticationSupport.ApplyHttpCredentialsAsync(
            request,
            options,
            "admin",
            CancellationToken.None);

        Assert.Equal("api-secret", request.Headers.GetValues("X-API-Key").Single());
        Assert.Equal("Bearer bearer-secret", request.Headers.Authorization?.ToString());
        Assert.NotNull(providerRequest);
        Assert.Equal("admin", providerRequest!.ServiceName);
        Assert.Equal(["admin.read"], providerRequest.Scopes);
        Assert.Equal("honua-admin", providerRequest.Audience);
        Assert.NotNull(diagnostic);
        Assert.Equal(HonuaAuthenticationSupport.CredentialAppliedEvent, diagnostic!.EventName);
        Assert.Equal("https://honua.example/admin/services", diagnostic.RequestUri?.ToString());
        Assert.Equal("present", diagnostic.Attributes["apiKey"]);
        Assert.Equal("present", diagnostic.Attributes["authorization"]);
        Assert.DoesNotContain("secret", string.Join(',', diagnostic.Attributes.Values), StringComparison.Ordinal);
        Assert.Equal("[redacted]", HonuaAuthenticationSupport.RedactSecret("raw-secret"));
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        };

    private static IReadOnlyDictionary<string, string> ParseForm(string body)
        => body
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => Decode(pair[0]),
                pair => pair.Length == 2 ? Decode(pair[1]) : string.Empty,
                StringComparer.Ordinal);

    private static string Decode(string value)
        => Uri.UnescapeDataString(value.Replace("+", "%20", StringComparison.Ordinal));

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public RecordingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
            => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => _responder(request, cancellationToken);
    }

    private sealed class DelegateAccessTokenProvider : IHonuaAccessTokenProvider
    {
        private readonly Func<HonuaAuthenticationRequest, HonuaAccessToken?> _provider;

        public DelegateAccessTokenProvider(Func<HonuaAuthenticationRequest, HonuaAccessToken?> provider)
            => _provider = provider;

        public ValueTask<HonuaAccessToken?> GetAccessTokenAsync(
            HonuaAuthenticationRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_provider(request));
    }

    private sealed class TestAuthenticationOptions : IHonuaAuthenticationOptions
    {
        public string? ApiKey { get; init; }

        public Func<CancellationToken, Task<string?>>? ApiKeyProvider { get; init; }

        public string? BearerToken { get; init; }

        public Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; init; }

        public IHonuaAccessTokenProvider? AccessTokenProvider { get; init; }

        public IReadOnlyList<string> AuthenticationScopes { get; init; } = Array.Empty<string>();

        public string? AuthenticationAudience { get; init; }

        public HonuaAuthenticationDiagnosticHandler? AuthenticationDiagnostics { get; init; }
    }
}
