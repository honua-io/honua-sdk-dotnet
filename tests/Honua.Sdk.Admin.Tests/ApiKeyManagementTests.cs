// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text.Json;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.Admin.Tests;

public sealed class ApiKeyManagementTests
{
    private static readonly Guid KeyId = Guid.Parse("7efad410-a542-4915-900c-2f93c702e6a1");

    [Fact]
    public async Task ListApiKeysAsync_ReadsMetadataAndRestrictions()
    {
        var client = TestHelpers.CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/v1/admin/api-keys/", request.RequestUri!.AbsolutePath);
            return Task.FromResult(TestHelpers.CreateJsonResponse(new[] { Metadata() }));
        });

        var key = Assert.Single(await client.ListApiKeysAsync());
        Assert.Equal(KeyId, key.Id);
        Assert.Equal("operator", key.Name);
        Assert.Equal("active", key.Status);
        Assert.Equal("https://maps.example/*", Assert.Single(key.AllowedReferrers!));
        Assert.Equal("192.0.2.0/24", Assert.Single(key.AllowedIpRanges!));
        Assert.Equal("admin", Assert.Single(key.Permissions));
        Assert.Null(key.RevokedAt);
    }

    [Fact]
    public async Task CreateApiKeyAsync_SendsWireContractAndReturnsSecret()
    {
        var expiresAt = DateTimeOffset.Parse("2027-01-01T00:00:00Z");
        var client = TestHelpers.CreateClient(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v1/admin/api-keys/", request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("operator", body.RootElement.GetProperty("name").GetString());
            Assert.Equal("admin", body.RootElement.GetProperty("permissions")[0].GetString());
            Assert.Equal(expiresAt, body.RootElement.GetProperty("expiresAt").GetDateTimeOffset());
            Assert.Equal("https://maps.example/*", body.RootElement.GetProperty("allowedReferrers")[0].GetString());
            Assert.Equal("192.0.2.0/24", body.RootElement.GetProperty("allowedIpRanges")[0].GetString());
            return TestHelpers.CreateJsonResponse(new { apiKey = Metadata(), key = "test-only-secret" }, HttpStatusCode.Created);
        });

        var result = await client.CreateApiKeyAsync(new CreateAdminApiKeyRequest
        {
            Name = "operator", Permissions = ["admin"], ExpiresAt = expiresAt,
            AllowedReferrers = ["https://maps.example/*"], AllowedIpRanges = ["192.0.2.0/24"]
        });

        Assert.Equal(KeyId, result.ApiKey.Id);
        Assert.Equal("test-only-secret", result.Key);
        Assert.DoesNotContain(result.Key, result.ToString()!);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_PostsIdentifierAndReturnsMetadata()
    {
        var client = TestHelpers.CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"/api/v1/admin/api-keys/{KeyId:D}/revoke", request.RequestUri!.AbsolutePath);
            return Task.FromResult(TestHelpers.CreateJsonResponse(Metadata("revoked")));
        });
        Assert.Equal("revoked", (await client.RevokeApiKeyAsync(KeyId)).Status);
    }

    [Fact]
    public async Task CreateApiKeyAsync_RejectsNullRequestBeforeSending()
    {
        var client = TestHelpers.CreateClient(_ => throw new InvalidOperationException("Unexpected request"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.CreateApiKeyAsync(null!));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RevokeApiKeyAsync_PreservesHttpFailure(HttpStatusCode status)
    {
        var client = TestHelpers.CreateClient(_ => Task.FromResult(TestHelpers.CreateErrorResponse(status, "Request rejected")));
        var error = await Assert.ThrowsAsync<HonuaAdminApiException>(() => client.RevokeApiKeyAsync(KeyId));
        Assert.Equal(status, error.StatusCode);
    }

    [Fact]
    public async Task ListApiKeysAsync_RejectsFailureEnvelope()
    {
        var client = TestHelpers.CreateClient(_ => Task.FromResult(TestHelpers.CreateJsonEnvelopeResponse(
            Array.Empty<object>(), false, "Request rejected")));
        await Assert.ThrowsAsync<HonuaAdminApiException>(() => client.ListApiKeysAsync());
    }

    [Fact]
    public async Task ListApiKeysAsync_RejectsMissingData()
    {
        var client = TestHelpers.CreateClient(_ => Task.FromResult(TestHelpers.CreateJsonResponse<object?>(null)));
        await Assert.ThrowsAsync<HonuaAdminOperationException>(() => client.ListApiKeysAsync());
    }

    [Fact]
    public async Task ListApiKeysAsync_HonorsCancellation()
    {
        using var httpClient = new HttpClient(new CancellationHandler())
        {
            BaseAddress = new Uri("https://honua.example")
        };
        var client = new HonuaAdminClient(httpClient);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.ListApiKeysAsync(cancellation.Token));
    }

    [Fact]
    public void AddHonuaAdmin_RegistersApiKeyFacet()
    {
        var services = new ServiceCollection();
        services.AddHonuaAdmin(options => options.BaseAddress = new Uri("https://honua.example"));
        using var provider = services.BuildServiceProvider();
        Assert.IsType<HonuaAdminClient>(provider.GetRequiredService<IHonuaAdminApiKeysClient>());
    }

    private static object Metadata(string status = "active") => new
    {
        id = KeyId, name = "operator", keyPrefix = "test-prefix", permissions = new[] { "admin" }, status,
        createdAt = "2026-11-01T00:00:00Z", updatedAt = "2026-11-02T00:00:00Z",
        allowedReferrers = new[] { "https://maps.example/*" }, allowedIpRanges = new[] { "192.0.2.0/24" }
    };

    private sealed class CancellationHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Expected a canceled token");
        }
    }
}
