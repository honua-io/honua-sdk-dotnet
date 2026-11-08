// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.Admin.Tests;

public sealed class FolderManagementTests
{
    private const string ETag = "\"catalog-17\"";

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "?namespace=")]
    [InlineData("workspace one", "?namespace=workspace%20one")]
    public async Task ListFoldersAsync_PreservesNamespaceAndReturnsSnapshot(string? namespaceName, string query)
    {
        var client = TestHelpers.CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/v1/admin/folders" + query, request.RequestUri!.PathAndQuery);
            Assert.False(request.Headers.Contains("If-Match"));
            return Task.FromResult(WithETag(new[] { FolderPayload() }));
        });
        var (folders, etag) = await client.ListFoldersAsync(namespaceName);
        Assert.Equal(ETag, etag);
        Assert.Equal("Survey", Assert.Single(folders).Metadata.Name);
    }

    [Fact]
    public async Task CreateFolderAsync_PreservesConditionalRequestAndMetadata()
    {
        var client = TestHelpers.CreateClient(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(ETag, Assert.Single(request.Headers.GetValues("If-Match")));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("Survey", body.RootElement.GetProperty("name").GetString());
            Assert.Equal("Field survey", body.RootElement.GetProperty("title").GetString());
            return WithETag(FolderPayload());
        });
        var (folder, etag) = await client.CreateFolderAsync(new() { Name = "Survey", Title = "Field survey" }, new() { IfMatch = ETag });
        Assert.Equal("folder-1", folder.Metadata.Id);
        Assert.Equal("Field survey", folder.Metadata.Title);
        Assert.Equal(ETag, etag);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdateFolderAsync_DistinguishesOmittedTitleFromClearing(bool clearTitle)
    {
        var client = TestHelpers.CreateClient(async request =>
        {
            Assert.Equal(HttpMethod.Patch, request.Method);
            Assert.Equal("/api/v1/admin/folders/f%2F1", request.RequestUri!.AbsolutePath);
            Assert.Equal(ETag, Assert.Single(request.Headers.GetValues("If-Match")));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(clearTitle, body.RootElement.TryGetProperty("title", out var title));
            if (clearTitle) Assert.Equal(JsonValueKind.Null, title.ValueKind);
            return WithETag(FolderPayload());
        });
        await client.UpdateFolderAsync("f/1", new() { Title = clearTitle ? new OptionalString(null) : default }, new() { IfMatch = ETag });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("folder-1")]
    public async Task MoveServiceAsync_UsesLogicalIdentityAndExplicitRoot(string? folderId)
    {
        var client = TestHelpers.CreateClient(async request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/api/v1/admin/services/a%2Fb%20c/folder?namespace=workspace", request.RequestUri!.PathAndQuery);
            Assert.Equal(ETag, Assert.Single(request.Headers.GetValues("If-Match")));
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(folderId, body.RootElement.GetProperty("folderId").GetString());
            return WithETag(status: HttpStatusCode.NoContent);
        });
        var etag = await client.MoveServiceAsync("a/b c", new() { FolderId = folderId }, new() { IfMatch = ETag, Namespace = "workspace" });
        Assert.Equal(ETag, etag);
    }

    [Fact]
    public async Task DeleteFolderAsync_ReturnsCatalogETag()
    {
        var client = TestHelpers.CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            Assert.Equal("/api/v1/admin/folders/folder-1", request.RequestUri!.AbsolutePath);
            Assert.Equal(ETag, Assert.Single(request.Headers.GetValues("If-Match")));
            return Task.FromResult(WithETag(status: HttpStatusCode.NoContent));
        });
        Assert.Equal(ETag, await client.DeleteFolderAsync("folder-1", new() { IfMatch = ETag }));
    }

    [Theory]
    [InlineData("POST", 403)]
    [InlineData("DELETE", 409)]
    [InlineData("PATCH", 412)]
    [InlineData("PUT", 428)]
    public async Task FolderMutation_PreservesServerRefusal(string method, int status)
    {
        var client = TestHelpers.CreateClient(request =>
        {
            Assert.Equal(method, request.Method.Method);
            return Task.FromResult(TestHelpers.CreateErrorResponse((HttpStatusCode)status, "Refresh the catalog."));
        });
        var options = new FolderMutationOptions { IfMatch = ETag };
        Func<Task> mutation = method switch
        {
            "POST" => () => client.CreateFolderAsync(new() { Name = "Survey" }, options),
            "PATCH" => () => client.UpdateFolderAsync("folder", new(), options),
            "PUT" => () => client.MoveServiceAsync("service", new(), options),
            _ => () => client.DeleteFolderAsync("folder", options)
        };
        var failure = await Assert.ThrowsAsync<HonuaAdminApiException>(mutation);
        Assert.Equal((HttpStatusCode)status, failure.StatusCode);
        Assert.Equal("Refresh the catalog.", failure.Message);
        Assert.Contains("Refresh the catalog.", failure.ResponseBody, StringComparison.Ordinal);
        Assert.NotNull(failure.FailureReceipt);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    [InlineData("move")]
    public async Task LegacyFoldersClient_RejectsUnsupportedFolderCalls(string operation)
    {
        IHonuaAdminFoldersClient client = new LegacyFoldersClient();
        var options = new FolderMutationOptions { IfMatch = ETag };
        Func<Task> call = operation switch
        {
            "list" => () => client.ListFoldersAsync(),
            "create" => () => client.CreateFolderAsync(new() { Name = "Survey" }, options),
            "update" => () => client.UpdateFolderAsync("folder", new(), options),
            "delete" => () => client.DeleteFolderAsync("folder", options),
            _ => () => client.MoveServiceAsync("service", new(), options)
        };
        var failure = await Assert.ThrowsAsync<NotSupportedException>(call);
        Assert.Contains("HonuaAdminClient", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddHonuaAdmin_ResolvesFolderClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaAdmin(options =>
        {
            options.BaseAddress = new Uri("https://test.honua.io/mounted/");
            options.EnableRetry = false;
        });
        using var provider = services.BuildServiceProvider();
        Assert.IsType<HonuaAdminClient>(provider.GetRequiredService<IHonuaAdminFoldersClient>());
    }

    [Fact]
    public async Task AdminRequests_PreserveConfiguredDeploymentPrefix()
    {
        using var handler = new MockHttpHandler(request =>
        {
            Assert.StartsWith("/mounted/api/v1/admin/", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            if (request.Method == HttpMethod.Put)
            {
                Assert.Equal("/mounted/api/v1/admin/services/a%2Fb/folder?namespace=workspace%20one", request.RequestUri.PathAndQuery);
                Assert.Equal(ETag, Assert.Single(request.Headers.GetValues("If-Match")));
                return Task.FromResult(WithETag(status: HttpStatusCode.NoContent));
            }
            return Task.FromResult(request.RequestUri.AbsolutePath.EndsWith("folders", StringComparison.Ordinal)
                ? WithETag(new[] { FolderPayload() }) : TestHelpers.CreateJsonResponse(TestHelpers.EmptyServicePage()));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://test.honua.io/mounted/") };
        var client = new HonuaAdminClient(http);
        await client.ListServicesAsync();
        await client.ListFoldersAsync();
        await client.MoveServiceAsync("a/b", new(), new() { IfMatch = ETag, Namespace = "workspace one" });
    }

    private static object FolderPayload() => new
    {
        metadata = new { id = "folder-1", name = "Survey", title = "Field survey" },
        previousNames = new[] { "SurveyOld" }, deleted = false
    };

    private static HttpResponseMessage WithETag<T>(T payload)
    {
        var response = TestHelpers.CreateJsonResponse(payload);
        response.Headers.ETag = EntityTagHeaderValue.Parse(ETag);
        return response;
    }

    private static HttpResponseMessage WithETag(HttpStatusCode status)
        => new(status) { Headers = { ETag = EntityTagHeaderValue.Parse(ETag) } };

    private sealed class LegacyFoldersClient : IHonuaAdminFoldersClient
    {
    }
}
