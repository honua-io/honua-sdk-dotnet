// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

public sealed class IdentityAdminTests
{
    private static readonly Guid ProviderId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task ListOidcProvidersAsync_ReturnsProviders()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith("/api/v1/admin/oidc/providers", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateJsonResponse(new[]
            {
                CreateProvider()
            }));
        });

        var providers = await client.ListOidcProvidersAsync();

        var provider = Assert.Single(providers);
        Assert.Equal(ProviderId, provider.ProviderId);
        Assert.Equal("Generic OIDC", provider.Name);
    }

    [Fact]
    public async Task GetOidcProviderAsync_ReturnsNullForNotFound()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith($"/api/v1/admin/oidc/providers/{ProviderId:D}", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateErrorResponse(HttpStatusCode.NotFound, "not found"));
        });

        var provider = await client.GetOidcProviderAsync(ProviderId);

        Assert.Null(provider);
    }

    [Fact]
    public async Task CreateOidcProviderAsync_SendsRequestAndReturnsProvider()
    {
        var client = TestHelpers.CreateClient(async req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.EndsWith("/api/v1/admin/oidc/providers", req.RequestUri!.PathAndQuery);

            var body = await req.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("secret", doc.RootElement.GetProperty("clientSecret").GetString());

            return TestHelpers.CreateJsonResponse(CreateProvider(), HttpStatusCode.Created);
        });

        var provider = await client.CreateOidcProviderAsync(new CreateOidcProviderRequest
        {
            Name = "Generic OIDC",
            ProviderType = "generic",
            Authority = "https://idp.example",
            ClientId = "honua",
            ClientSecret = "secret"
        });

        Assert.Equal("generic", provider.ProviderType);
    }

    [Fact]
    public async Task UpdateOidcProviderAsync_SendsPatchShapeAndReturnsProvider()
    {
        var client = TestHelpers.CreateClient(async req =>
        {
            Assert.Equal(HttpMethod.Put, req.Method);
            Assert.EndsWith($"/api/v1/admin/oidc/providers/{ProviderId:D}", req.RequestUri!.PathAndQuery);

            var body = await req.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            Assert.False(doc.RootElement.GetProperty("enabled").GetBoolean());

            return TestHelpers.CreateJsonResponse(CreateProvider(enabled: false));
        });

        var provider = await client.UpdateOidcProviderAsync(ProviderId, new UpdateOidcProviderRequest
        {
            Enabled = false
        });

        Assert.False(provider.Enabled);
    }

    [Fact]
    public async Task DeleteOidcProviderAsync_SendsDelete()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Delete, req.Method);
            Assert.EndsWith($"/api/v1/admin/oidc/providers/{ProviderId:D}", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateJsonResponse(new { }));
        });

        await client.DeleteOidcProviderAsync(ProviderId);
    }

    [Fact]
    public async Task TestOidcProviderAsync_SendsTestRequest()
    {
        var testedAt = DateTimeOffset.UtcNow;
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.EndsWith($"/api/v1/admin/oidc/providers/{ProviderId:D}/test", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                providerId = ProviderId,
                isReachable = true,
                message = "ok",
                testedAt
            }));
        });

        var result = await client.TestOidcProviderAsync(ProviderId);

        Assert.True(result.IsReachable);
        Assert.Equal(testedAt, result.TestedAt);
    }

    [Fact]
    public async Task GetIdentityProvidersAsync_ReturnsCatalog()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith("/api/v1/admin/identity/providers", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                enabled = true,
                providers = new[]
                {
                    new
                    {
                        type = "generic",
                        enabled = true,
                        displayName = "Generic OIDC",
                        authority = "https://idp.example",
                        callbackPath = "/signin-oidc",
                        scopes = new[] { "openid", "profile" },
                        isConfigurationValid = true
                    }
                }
            }));
        });

        var catalog = await client.GetIdentityProvidersAsync();

        Assert.True(catalog.Enabled);
        Assert.Equal("generic", Assert.Single(catalog.Providers).Type);
    }

    [Fact]
    public async Task TestIdentityProviderAsync_EscapesProviderType()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.EndsWith("/api/v1/admin/identity/providers/azure%20ad/test", req.RequestUri!.PathAndQuery);
            return Task.FromResult(TestHelpers.CreateJsonResponse(new
            {
                providerType = "azure ad",
                isReachable = true,
                responseTimeMs = 42.5,
                discoveryUrl = "https://login.example/.well-known/openid-configuration",
                issuer = "https://login.example"
            }));
        });

        var result = await client.TestIdentityProviderAsync("azure ad");

        Assert.True(result.IsReachable);
        Assert.Equal(42.5, result.ResponseTimeMs);
    }

    private static object CreateProvider(bool enabled = true) => new
    {
        providerId = ProviderId,
        name = "Generic OIDC",
        providerType = "generic",
        authority = "https://idp.example",
        clientId = "honua",
        enabled,
        isHealthy = true,
        createdAt = DateTimeOffset.UtcNow.AddDays(-2),
        updatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        lastHealthCheck = DateTimeOffset.UtcNow
    };
}
