// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Spec.Models;
using Honua.Sdk.Spec.Tests.Fixtures;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.Spec.Tests;

public sealed class HonuaSpecClientTests
{
    [Fact]
    public async Task AuthHandler_UsesCredentialProvidersPerRequest()
    {
        var apiKeyCalls = 0;
        var bearerTokenCalls = 0;
        var capturedCredentials = new List<(string? ApiKey, string? Authorization)>();

        var options = Options.Create(new HonuaSpecClientOptions
        {
            BaseAddress = new Uri("https://localhost:5001"),
            ApiKeyProvider = _ => Task.FromResult<string?>($"spec-key-{++apiKeyCalls}"),
            BearerTokenProvider = _ => Task.FromResult<string?>($"spec-token-{++bearerTokenCalls}")
        });

        var innerHandler = new MockHttpHandler(req =>
        {
            req.Headers.TryGetValues("X-API-Key", out var apiValues);
            capturedCredentials.Add((apiValues?.SingleOrDefault(), req.Headers.Authorization?.ToString()));
            return JsonResponse(PlanJson);
        });

        var authHandler = new HonuaSpecAuthHandler(options)
        {
            InnerHandler = innerHandler
        };

        using var http = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
        var client = new HonuaSpecClient(http);

        await client.PlanAsync(CreateDocument());
        await client.PlanAsync(CreateDocument());

        Assert.Collection(
            capturedCredentials,
            first =>
            {
                Assert.Equal("spec-key-1", first.ApiKey);
                Assert.Equal("Bearer spec-token-1", first.Authorization);
            },
            second =>
            {
                Assert.Equal("spec-key-2", second.ApiKey);
                Assert.Equal("Bearer spec-token-2", second.Authorization);
            });
    }

    [Fact]
    public async Task AuthHandler_RejectsCredentialProvidersOverRemoteHttp()
    {
        var providerCalled = false;
        var options = Options.Create(new HonuaSpecClientOptions
        {
            BaseAddress = new Uri("http://example.com"),
            ApiKeyProvider = _ =>
            {
                providerCalled = true;
                return Task.FromResult<string?>("spec-key");
            }
        });

        var authHandler = new HonuaSpecAuthHandler(options)
        {
            InnerHandler = new MockHttpHandler(_ => JsonResponse(PlanJson))
        };

        using var http = new HttpClient(authHandler)
        {
            BaseAddress = new Uri("http://example.com")
        };
        var client = new HonuaSpecClient(http);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PlanAsync(CreateDocument()));
        Assert.False(providerCalled);
    }

    [Fact]
    public async Task PlanAsync_PostsCanonicalDocumentAndReadsPlan()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(SpecFixtureReader.ReadJson("spec-plan-response.json"));
        });
        var client = new HonuaSpecClient(http);

        var response = await client.PlanAsync(CreateDocument());

        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal("/v1/spec/plan", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("plan-1", response.PlanId);
        Assert.Equal(2, response.Nodes.Count);
        Assert.Equal(SpecResourceKind.Compute, response.Nodes[1].Kind);
        Assert.Equal(12.5, response.Nodes[1].Cost.EstimatedDurationMs);
    }

    [Fact]
    public async Task ApplyAsync_SendsSseAcceptHeaderAndParsesEvents()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            var body = """
                id: 1
                event: ApplyStarted
                data: {"sequence":1,"kind":"ApplyStarted","applyToken":"apply-1","timestamp":"2026-04-29T07:00:00Z"}

                id: 2
                event: ApplyCompleted
                data: {"sequence":2,"kind":"ApplyCompleted","applyToken":"apply-1","timestamp":"2026-04-29T07:00:01Z","summary":{"totalNodes":1,"cachedNodes":0,"ranNodes":1,"failedNodes":0,"skippedNodes":0,"totalDurationMs":1000,"cancelled":false}}

                """;
            return SseResponse(body, ("X-Spec-Apply-Token", "apply-1"));
        });
        var client = new HonuaSpecClient(http);

        await using var stream = await client.ApplyAsync(CreateDocument());
        var events = new List<SpecApplyEvent>();
        await foreach (var evt in stream.Events)
        {
            events.Add(evt);
        }

        Assert.Equal("apply-1", stream.ApplyToken);
        Assert.Equal("text/event-stream", captured?.Headers.Accept.Single().MediaType);
        Assert.Collection(events,
            first => Assert.Equal(SpecApplyEventKind.ApplyStarted, first.Kind),
            second =>
            {
                Assert.Equal(SpecApplyEventKind.ApplyCompleted, second.Kind);
                Assert.False(second.Summary?.Cancelled);
            });
    }

    [Fact]
    public async Task CancelAsync_PostsApplyToken()
    {
        string? capturedBody = null;
        using var http = CreateHttpClient(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return await JsonResponse("""{"applyToken":"apply-1","cancelled":true}""").ConfigureAwait(false);
        });
        var client = new HonuaSpecClient(http);

        var response = await client.CancelAsync("apply-1");

        Assert.True(response.Cancelled);
        Assert.Contains("\"applyToken\":\"apply-1\"", capturedBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_ThrowsStructuredProblemOnFailure()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            SpecFixtureReader.ReadJson("spec-problem.json"),
            HttpStatusCode.BadRequest));
        var client = new HonuaSpecClient(http);

        var ex = await Assert.ThrowsAsync<HonuaSpecException>(() =>
            client.ValidateAsync(new SpecValidateRequest { IncludeCanonicalJson = true }));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("invalid-request-body", ex.Problem?.Code);
        Assert.Contains("exactly one", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetArtifactAsync_ReturnsBytesContentTypeAndHash()
    {
        HttpRequestMessage? captured = null;
        var payload = new byte[] { 1, 2, 3, 4 };
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return BinaryResponse(payload, "application/x-arrow", "sha256-abc");
        });
        var client = new HonuaSpecClient(http);

        var artifact = await client.GetArtifactAsync("sha256-abc");

        Assert.Equal(HttpMethod.Get, captured?.Method);
        Assert.Equal("/v1/spec/artifact/sha256-abc", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("application/x-arrow", artifact.ContentType);
        Assert.Equal("sha256-abc", artifact.ContentHash);
        Assert.Equal(payload, artifact.Content.ToArray());
    }

    [Fact]
    public async Task GetArtifactAsync_FallsBackToRequestedHash_WhenHeaderAbsent()
    {
        using var http = CreateHttpClient(_ => BinaryResponse([9, 8, 7], "application/octet-stream", contentHash: null));
        var client = new HonuaSpecClient(http);

        var artifact = await client.GetArtifactAsync("requested-hash");

        Assert.Equal("requested-hash", artifact.ContentHash);
        Assert.Equal("application/octet-stream", artifact.ContentType);
    }

    [Fact]
    public async Task GetArtifactAsync_NotFound_ThrowsSpecException()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """
            {
              "type": "urn:honua:spec:artifact-not-found",
              "title": "Artifact not found",
              "status": 404,
              "detail": "Artifact 'x' is unknown or has been evicted.",
              "code": "artifact-not-found"
            }
            """,
            HttpStatusCode.NotFound));
        var client = new HonuaSpecClient(http);

        var ex = await Assert.ThrowsAsync<HonuaSpecException>(() => client.GetArtifactAsync("x"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("artifact-not-found", ex.Problem?.Code);
    }

    [Fact]
    public async Task GetArtifactAsync_BlankHash_Throws()
    {
        using var http = CreateHttpClient(_ => JsonResponse("{}"));
        var client = new HonuaSpecClient(http);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetArtifactAsync("  "));
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        return new HttpClient(new MockHttpHandler(handler))
        {
            BaseAddress = new Uri("https://server.example")
        };
    }

    private static Task<HttpResponseMessage> BinaryResponse(byte[] bytes, string contentType, string? contentHash)
        => Task.FromResult(CreateBinaryResponse(bytes, contentType, contentHash));

    private static HttpResponseMessage CreateBinaryResponse(byte[] bytes, string contentType, string? contentHash)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        if (contentHash is not null)
        {
            response.Headers.Add("X-Spec-Content-Hash", contentHash);
        }

        return response;
    }

    private const string PlanJson = """
        {"planId":"plan-1","grammarVersion":"spec/v1","processFamilyVersion":"process/v1","nodes":[],"warnings":[]}
        """;

    private static SpecDocumentRequest CreateDocument() => new()
    {
        GrammarVersion = "spec/v1",
        ProcessFamilyVersion = "process/v1",
        Nodes =
        [
            new SpecNodeRequest
            {
                Id = "buffer",
                Kind = SpecResourceKind.Compute,
                Op = "geometry.buffer"
            }
        ]
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

    private static Task<HttpResponseMessage> SseResponse(
        string text,
        params (string Name, string Value)[] headers)
        => Task.FromResult(CreateSseResponse(text, headers));

    private static HttpResponseMessage CreateSseResponse(
        string text,
        params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(text, Encoding.UTF8, "text/event-stream")
        };

        foreach (var (name, value) in headers)
        {
            response.Headers.Add(name, value);
        }

        return response;
    }
}
