// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using Honua.Sdk.Abstractions.Console.Share;
using Honua.Sdk.ConsoleShare.Exceptions;
using Honua.Sdk.ConsoleShare.Extensions;
using Honua.Sdk.ConsoleShare.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.ConsoleShare.Tests;

public sealed class HonuaConsoleShareClientTests
{
    [Fact]
    public async Task GetShareAsync_DeserializesDetailWithGrantsLinkAndEmbed()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(ReadFixture("share-detail.v1.json"));
        });
        var client = new HonuaConsoleShareClient(http);

        var detail = await client.GetShareAsync("share-7f3c");

        Assert.Equal(HttpMethod.Get, captured?.Method);
        Assert.Equal("/api/v1/console/shares/share-7f3c", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("share-7f3c", detail.Item.ShareId);
        Assert.Equal(HonuaShareVisibility.Organization, detail.Item.Visibility);
        Assert.Equal("map", detail.Item.ResourceKind);

        Assert.Equal(2, detail.Grants.Count);
        Assert.Contains(detail.Grants, g => g.PrincipalId == "group-gis" && g.Role == "editor");

        Assert.NotNull(detail.PublicLink);
        Assert.Equal(new Uri("https://share.example/p/9a21"), detail.PublicLink!.Url);
        Assert.True(detail.PublicLink.Enabled);

        Assert.NotNull(detail.EmbedToken);
        Assert.Equal("embed-3b55", detail.EmbedToken!.TokenId);
        Assert.Equal(["https://partner.example", "https://atlas.example"], detail.EmbedToken.AllowedOrigins);
    }

    [Fact]
    public async Task UpdateAccessAsync_SendsVisibilityAndGrantsAndReturnsDetail()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        using var http = CreateHttpClient(async request =>
        {
            captured = request;
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return CreateJsonResponse(ReadFixture("share-detail.v1.json"), HttpStatusCode.OK);
        });
        var client = new HonuaConsoleShareClient(http);

        var update = new HonuaShareAccessUpdate
        {
            Visibility = HonuaShareVisibility.Public,
            Grants =
            [
                new HonuaShareGrant { PrincipalId = "user-carol", Role = "viewer" }
            ]
        };

        var detail = await client.UpdateAccessAsync("share-7f3c", update);

        Assert.Equal(HttpMethod.Put, captured?.Method);
        Assert.Equal("/api/v1/console/shares/share-7f3c/access", captured?.RequestUri?.PathAndQuery);
        Assert.Contains("\"visibility\":\"public\"", body, StringComparison.Ordinal);
        Assert.Contains("user-carol", body, StringComparison.Ordinal);
        Assert.Equal("share-7f3c", detail.Item.ShareId);
    }

    [Fact]
    public async Task ValidateDependencyClosureAsync_ReturnsBlockingDependencies()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "valid": false,
                  "blockingDependencies": [
                    {
                      "resourceId": "layer-private",
                      "resourceKind": "layer",
                      "visibility": "private",
                      "reason": "Dependency is private and must be shared first."
                    }
                  ]
                }
                """);
        });
        var client = new HonuaConsoleShareClient(http);

        var closure = await client.ValidateDependencyClosureAsync(
            "share-7f3c",
            new HonuaShareAccessUpdate { Visibility = HonuaShareVisibility.Public });

        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal("/api/v1/console/shares/share-7f3c/access/validate", captured?.RequestUri?.PathAndQuery);
        Assert.False(closure.Valid);
        var blocker = Assert.Single(closure.BlockingDependencies);
        Assert.Equal("layer-private", blocker.ResourceId);
        Assert.Equal(HonuaShareVisibility.Private, blocker.Visibility);
    }

    [Fact]
    public async Task CreatePublicLinkAsync_SendsRequestAndReturnsLink()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        using var http = CreateHttpClient(async request =>
        {
            captured = request;
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return CreateJsonResponse(
                """
                {
                  "linkId": "link-9a21",
                  "url": "https://share.example/p/9a21",
                  "enabled": true,
                  "createdAt": "2026-05-24T18:00:00Z"
                }
                """,
                HttpStatusCode.OK);
        });
        var client = new HonuaConsoleShareClient(http);

        var link = await client.CreatePublicLinkAsync(
            "share-7f3c",
            new HonuaPublicLinkRequest { Enabled = true, ExpiresAt = new DateTimeOffset(2026, 6, 24, 18, 0, 0, TimeSpan.Zero) });

        Assert.Equal(HttpMethod.Put, captured?.Method);
        Assert.Equal("/api/v1/console/shares/share-7f3c/public-link", captured?.RequestUri?.PathAndQuery);
        Assert.Contains("\"enabled\":true", body, StringComparison.Ordinal);
        Assert.Contains("expiresAt", body, StringComparison.Ordinal);
        Assert.Equal("link-9a21", link.LinkId);
        Assert.True(link.Enabled);
    }

    [Fact]
    public async Task RevokePublicLinkAsync_SendsDelete()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var client = new HonuaConsoleShareClient(http);

        await client.RevokePublicLinkAsync("share-7f3c");

        Assert.Equal(HttpMethod.Delete, captured?.Method);
        Assert.Equal("/api/v1/console/shares/share-7f3c/public-link", captured?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task CreateEmbedTokenAsync_SendsAllowedOriginsAndReturnsToken()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        using var http = CreateHttpClient(async request =>
        {
            captured = request;
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
            return CreateJsonResponse(
                """
                {
                  "tokenId": "embed-3b55",
                  "token": "et_live_3b55abcd",
                  "allowedOrigins": ["https://partner.example"],
                  "createdAt": "2026-05-24T18:00:00Z"
                }
                """,
                HttpStatusCode.OK);
        });
        var client = new HonuaConsoleShareClient(http);

        var token = await client.CreateEmbedTokenAsync(
            "share-7f3c",
            new HonuaEmbedTokenRequest { AllowedOrigins = ["https://partner.example"] });

        Assert.Equal(HttpMethod.Put, captured?.Method);
        Assert.Equal("/api/v1/console/shares/share-7f3c/embed-token", captured?.RequestUri?.PathAndQuery);
        Assert.Contains("https://partner.example", body, StringComparison.Ordinal);
        Assert.Equal("et_live_3b55abcd", token.Token);
        Assert.Equal(["https://partner.example"], token.AllowedOrigins);
    }

    [Fact]
    public async Task RevokeEmbedTokenAsync_SendsDelete()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var client = new HonuaConsoleShareClient(http);

        await client.RevokeEmbedTokenAsync("share-7f3c");

        Assert.Equal(HttpMethod.Delete, captured?.Method);
        Assert.Equal("/api/v1/console/shares/share-7f3c/embed-token", captured?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetShareAsync_ForbiddenProblem_ThrowsApiException()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """
            {
              "type": "https://honua.io/problems/forbidden",
              "title": "Forbidden",
              "status": 403,
              "detail": "You do not have access to share 'share-7f3c'."
            }
            """,
            HttpStatusCode.Forbidden));
        var client = new HonuaConsoleShareClient(http);

        var ex = await Assert.ThrowsAsync<HonuaConsoleShareApiException>(() => client.GetShareAsync("share-7f3c"));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Equal("Forbidden", ex.ProblemTitle);
        Assert.Contains("share-7f3c", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokePublicLinkAsync_NotFoundProblem_ThrowsApiException()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """
            {
              "type": "https://honua.io/problems/not-found",
              "title": "Not Found",
              "status": 404,
              "detail": "No public link exists for share 'share-missing'."
            }
            """,
            HttpStatusCode.NotFound));
        var client = new HonuaConsoleShareClient(http);

        var ex = await Assert.ThrowsAsync<HonuaConsoleShareApiException>(() => client.RevokePublicLinkAsync("share-missing"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Not Found", ex.ProblemTitle);
    }

    [Fact]
    public async Task GetShareAsync_MalformedSuccessBody_ThrowsContractException()
    {
        using var http = CreateHttpClient(_ => JsonResponse("not-json", HttpStatusCode.OK));
        var client = new HonuaConsoleShareClient(http);

        var ex = await Assert.ThrowsAsync<HonuaConsoleShareContractException>(() => client.GetShareAsync("share-7f3c"));

        Assert.Equal("GetShare", ex.Operation);
    }

    [Fact]
    public async Task GetShareAsync_BlankShareId_Throws()
    {
        using var http = CreateHttpClient(_ => JsonResponse("{}"));
        var client = new HonuaConsoleShareClient(http);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetShareAsync("   "));
    }

    [Fact]
    public void AddHonuaConsoleShare_ConfiguresHttpClientTimeoutAndResolvesClient()
    {
        var timeout = TimeSpan.FromSeconds(42);
        var services = new ServiceCollection();
        services.AddHonuaConsoleShare(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
            options.Timeout = timeout;
        });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<HonuaConsoleShareClient>(provider.GetRequiredService<IHonuaConsoleShareClient>());
    }

    [Fact]
    public void AddHonuaConsoleShare_InvalidTimeout_Throws()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<Honua.Sdk.Abstractions.HonuaConfigurationException>(() =>
            services.AddHonuaConsoleShare(options =>
            {
                options.BaseAddress = new Uri("https://localhost:5001");
                options.Timeout = TimeSpan.FromMilliseconds(10);
            }));

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => new(new MockHttpHandler(handler))
        {
            BaseAddress = new Uri("https://server.example")
        };

    private static Task<HttpResponseMessage> JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => Task.FromResult(CreateJsonResponse(json, statusCode));

    private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        return response;
    }

    private static string ReadFixture(string name)
        => File.ReadAllText(Path.Join(FindRepoRoot(), "contracts", "fixtures", "console", name));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "Honua.Sdk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the honua-sdk-dotnet repository root.");
    }
}
