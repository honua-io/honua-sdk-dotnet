// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Admin.Tests;

public sealed class HonuaAdminClientTests
{
    private const string ConnectionId = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public void Options_DefaultBaseAddress_UsesHttps()
    {
        var options = new HonuaAdminClientOptions();

        Assert.Equal(Uri.UriSchemeHttps, options.BaseAddress.Scheme);
    }

    [Fact]
    public async Task AuthHandler_AddsApiKeyHeader()
    {
        string? capturedApiKey = null;

        var options = Options.Create(new HonuaAdminClientOptions
        {
            ApiKey = "test-key-123"
        });

        var innerHandler = new MockHttpHandler(req =>
        {
            if (req.Headers.TryGetValues("X-API-Key", out var values))
            {
                capturedApiKey = values.First();
            }

            return Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>()));
        });

        var authHandler = new HonuaAdminAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var client = new HonuaAdminClient(httpClient);
        await client.ListServicesAsync();

        Assert.Equal("test-key-123", capturedApiKey);
    }

    [Fact]
    public async Task AuthHandler_AddsBearerToken()
    {
        string? capturedAuth = null;

        var options = Options.Create(new HonuaAdminClientOptions
        {
            BearerToken = "my-jwt-token"
        });

        var innerHandler = new MockHttpHandler(req =>
        {
            capturedAuth = req.Headers.Authorization?.ToString();
            return Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>()));
        });

        var authHandler = new HonuaAdminAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var client = new HonuaAdminClient(httpClient);
        await client.ListServicesAsync();

        Assert.Equal("Bearer my-jwt-token", capturedAuth);
    }

    [Fact]
    public async Task AuthHandler_AddsBothHeaders()
    {
        string? capturedApiKey = null;
        string? capturedAuth = null;

        var options = Options.Create(new HonuaAdminClientOptions
        {
            ApiKey = "admin-key",
            BearerToken = "jwt-token"
        });

        var innerHandler = new MockHttpHandler(req =>
        {
            if (req.Headers.TryGetValues("X-API-Key", out var apiValues))
            {
                capturedApiKey = apiValues.First();
            }

            capturedAuth = req.Headers.Authorization?.ToString();
            return Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>()));
        });

        var authHandler = new HonuaAdminAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var client = new HonuaAdminClient(httpClient);
        await client.ListServicesAsync();

        Assert.Equal("admin-key", capturedApiKey);
        Assert.Equal("Bearer jwt-token", capturedAuth);
    }

    [Fact]
    public async Task AuthHandler_UsesCredentialProvidersPerRequest()
    {
        var apiKeyCalls = 0;
        var bearerTokenCalls = 0;
        var capturedCredentials = new List<(string? ApiKey, string? Authorization)>();

        var options = Options.Create(new HonuaAdminClientOptions
        {
            ApiKeyProvider = _ => Task.FromResult<string?>($"admin-key-{++apiKeyCalls}"),
            BearerTokenProvider = _ => Task.FromResult<string?>($"admin-token-{++bearerTokenCalls}")
        });

        var innerHandler = new MockHttpHandler(req =>
        {
            req.Headers.TryGetValues("X-API-Key", out var apiValues);
            capturedCredentials.Add((apiValues?.SingleOrDefault(), req.Headers.Authorization?.ToString()));
            return Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>()));
        });

        var authHandler = new HonuaAdminAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var client = new HonuaAdminClient(httpClient);
        await client.ListServicesAsync();
        await client.ListServicesAsync();

        Assert.Collection(
            capturedCredentials,
            first =>
            {
                Assert.Equal("admin-key-1", first.ApiKey);
                Assert.Equal("Bearer admin-token-1", first.Authorization);
            },
            second =>
            {
                Assert.Equal("admin-key-2", second.ApiKey);
                Assert.Equal("Bearer admin-token-2", second.Authorization);
            });
    }

    [Fact]
    public async Task AuthHandler_ProviderReturningNullOrEmpty_OmitsCredentials()
    {
        var capturedApiKeyHeader = true;
        var capturedAuthHeader = true;

        var options = Options.Create(new HonuaAdminClientOptions
        {
            ApiKey = "fallback-key",
            BearerToken = "fallback-token",
            ApiKeyProvider = _ => Task.FromResult<string?>(null),
            BearerTokenProvider = _ => Task.FromResult<string?>(string.Empty)
        });

        var innerHandler = new MockHttpHandler(req =>
        {
            capturedApiKeyHeader = req.Headers.Contains("X-API-Key");
            capturedAuthHeader = req.Headers.Authorization is not null;
            return Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>()));
        });

        var authHandler = new HonuaAdminAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        var client = new HonuaAdminClient(httpClient);
        await client.ListServicesAsync();

        Assert.False(capturedApiKeyHeader);
        Assert.False(capturedAuthHeader);
    }

    [Fact]
    public async Task AuthHandler_RejectsCredentialsOverRemoteHttp()
    {
        var options = Options.Create(new HonuaAdminClientOptions
        {
            BaseAddress = new Uri("http://example.com"),
            ApiKey = "admin-key",
        });

        var authHandler = new HonuaAdminAuthHandler(options)
        {
            InnerHandler = new MockHttpHandler(_ => Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>())))
        };

        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://example.com")
        };

        var client = new HonuaAdminClient(httpClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListServicesAsync());
    }

    [Fact]
    public async Task AuthHandler_RejectsCredentialProvidersOverRemoteHttp()
    {
        var providerCalled = false;
        var options = Options.Create(new HonuaAdminClientOptions
        {
            BaseAddress = new Uri("http://example.com"),
            ApiKeyProvider = _ =>
            {
                providerCalled = true;
                return Task.FromResult<string?>("admin-key");
            },
        });

        var authHandler = new HonuaAdminAuthHandler(options)
        {
            InnerHandler = new MockHttpHandler(_ => Task.FromResult(TestHelpers.CreateJsonResponse(Array.Empty<object>())))
        };

        var httpClient = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://example.com")
        };

        var client = new HonuaAdminClient(httpClient);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ListServicesAsync());
        Assert.False(providerCalled);
    }

    [Fact]
    public async Task Error400_ThrowsHonuaAdminApiException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid request")));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.ListServicesAsync());

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("Invalid request", ex.Message);
        Assert.NotNull(ex.ResponseBody);
    }

    [Fact]
    public async Task Error404_ThrowsHonuaAdminApiException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(HttpStatusCode.NotFound, "Not found")));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.GetServiceSettingsAsync("nonexistent"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Not found", ex.Message);
    }

    [Fact]
    public async Task Error409_ThrowsHonuaAdminApiException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(HttpStatusCode.Conflict, "Resource conflict")));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.DeleteConnectionAsync(ConnectionId));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal("Resource conflict", ex.Message);
    }

    [Fact]
    public async Task Error412_ThrowsHonuaAdminApiException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(HttpStatusCode.PreconditionFailed, "ETag precondition failed.")));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.UpdateMetadataResourceAsync(
                "Layer", "default", "test",
                new Models.MetadataResource
                {
                    Spec = JsonDocument.Parse("{}").RootElement
                },
                ifMatch: "\"old-etag\""));

        Assert.Equal(HttpStatusCode.PreconditionFailed, ex.StatusCode);
        Assert.Equal("ETag precondition failed.", ex.Message);
    }

    [Fact]
    public async Task Error428_ThrowsHonuaAdminApiException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(
                (HttpStatusCode)428, "If-Match header is required.")));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.DeleteMetadataResourceAsync("Layer", "default", "test"));

        Assert.Equal((HttpStatusCode)428, ex.StatusCode);
        Assert.Equal("If-Match header is required.", ex.Message);
    }

    [Fact]
    public async Task ProblemDetails_ExtractsDetailMessage()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateProblemResponse(
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "An error occurred while processing the request.")));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.GetConfigAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Equal("An error occurred while processing the request.", ex.Message);
    }

    [Fact]
    public async Task GetConfigAsync_ReturnsJsonElement()
    {
        var config = new { server = new { port = 5000, env = "development" } };

        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Contains("/admin/config", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(config));
        });

        var result = await client.GetConfigAsync();

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
    }

    [Fact]
    public async Task EnvelopeSuccessFalse_On2xx_ThrowsHonuaAdminApiException()
    {
        var client = TestHelpers.CreateClient(_ => Task.FromResult(
            TestHelpers.CreateJsonEnvelopeResponse(
                new { services = Array.Empty<object>() },
                success: false,
                message: "Operation failed.")));

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(() => client.ListServicesAsync());

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
        Assert.Equal("Operation failed.", ex.Message);
    }
}
