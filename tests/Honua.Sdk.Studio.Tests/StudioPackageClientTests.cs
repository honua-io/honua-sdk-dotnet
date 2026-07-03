// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using Honua.Sdk.Studio.Exceptions;
using Honua.Sdk.Studio.Extensions;
using Honua.Sdk.Studio.Packages;
using Honua.Sdk.Studio.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.Studio.Tests;

public sealed class StudioPackageClientTests
{
    private static readonly Guid DraftId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ItemId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid VersionId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task GetPackageFamiliesAsync_UnwrapsEnvelopeData()
    {
        const string json = """
            {
              "success": true,
              "data": {
                "persistenceMode": "durable",
                "durable": true,
                "families": [
                  {
                    "family": "map",
                    "currentSchemaVersion": "honua_map_package.v1",
                    "format": "honua_map_package.v1",
                    "supportLevel": "supported",
                    "supportedOperations": ["draft.create", "validate", "preview-plan", "publish-request.create"],
                    "validationDepth": "family",
                    "maxPackageBytes": 1048576,
                    "previewSupported": true,
                    "publishSupported": true
                  }
                ]
              },
              "timestamp": "2026-07-01T12:00:00Z"
            }
            """;
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(json);
        });
        var client = new HonuaStudioPackageClient(http);

        var capabilities = await client.GetPackageFamiliesAsync();

        Assert.Equal("/api/v1/studio/package-families", captured?.RequestUri?.PathAndQuery);
        Assert.Equal(StudioPackagePersistenceMode.Durable, capabilities.PersistenceMode);
        var family = Assert.Single(capabilities.Families);
        Assert.Equal(StudioPackageFamily.Map, family.Family);
        Assert.Equal(StudioPackageSupportLevel.Supported, family.SupportLevel);
        Assert.Contains(StudioPackageOperation.PreviewPlan, family.SupportedOperations);
    }

    [Fact]
    public async Task CreateDraftAsync_SerializesEnvelopeCollectionsAsArrays()
    {
        string? sentBody = null;
        using var http = CreateHttpClient(async request =>
        {
            sentBody = await request.Content!.ReadAsStringAsync();
            return await JsonResponse(DraftEnvelope());
        });
        var client = new HonuaStudioPackageClient(http);

        var request = new CreateStudioPackageDraftRequest
        {
            PackageKey = "my-map",
            Envelope = new StudioPackageEnvelope
            {
                Family = StudioPackageFamily.Map,
                SchemaVersion = "honua_map_package.v1",
            },
        };

        var draft = await client.CreateDraftAsync(request);

        Assert.NotNull(sentBody);
        // The server rejects null bindings/dependencies/provenance; they must
        // serialize as arrays even when empty.
        Assert.Contains("\"bindings\":[]", sentBody, StringComparison.Ordinal);
        Assert.Contains("\"dependencies\":[]", sentBody, StringComparison.Ordinal);
        Assert.Contains("\"provenance\":[]", sentBody, StringComparison.Ordinal);
        Assert.Contains("\"family\":\"map\"", sentBody, StringComparison.Ordinal);
        Assert.Equal(DraftId, draft.DraftId);
        Assert.Equal(StudioPackageFamily.Map, draft.Family);
    }

    [Fact]
    public async Task ValidateDraftAsync_PostsAndReturnsSummary()
    {
        const string json = """
            {
              "success": true,
              "data": {
                "status": "warning",
                "diagnostics": [ { "code": "map.layer.unbound", "severity": "warning", "message": "Layer is unbound." } ],
                "unsupportedCapabilities": [],
                "generatedAt": "2026-07-01T12:00:00Z"
              },
              "timestamp": "2026-07-01T12:00:00Z"
            }
            """;
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(json);
        });
        var client = new HonuaStudioPackageClient(http);

        var summary = await client.ValidateDraftAsync(DraftId);

        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal($"/api/v1/studio/package-drafts/{DraftId}/validate", captured?.RequestUri?.PathAndQuery);
        Assert.Equal(StudioPackageValidationStatus.Warning, summary.Status);
        Assert.Equal(StudioPackageDiagnosticSeverity.Warning, summary.Diagnostics.Single().Severity);
    }

    [Fact]
    public async Task PreviewPlanAsync_ReturnsPlan()
    {
        const string json = """
            {
              "success": true,
              "data": {
                "draftId": "11111111-1111-1111-1111-111111111111",
                "family": "map",
                "synchronous": true,
                "requiresJob": false,
                "steps": ["compose", "render"],
                "validation": { "status": "valid" }
              },
              "timestamp": "2026-07-01T12:00:00Z"
            }
            """;
        using var http = CreateHttpClient(_ => JsonResponse(json));
        var client = new HonuaStudioPackageClient(http);

        var plan = await client.PreviewPlanAsync(DraftId);

        Assert.True(plan.Synchronous);
        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal(StudioPackageValidationStatus.Valid, plan.Validation.Status);
    }

    [Fact]
    public async Task CreatePublishRequestAsync_PostsToVersionScopedRoute()
    {
        const string json = """
            {
              "success": true,
              "data": {
                "requestId": "44444444-4444-4444-4444-444444444444",
                "itemId": "22222222-2222-2222-2222-222222222222",
                "versionId": "33333333-3333-3333-3333-333333333333",
                "status": "accepted",
                "validation": { "status": "valid" },
                "createdAt": "2026-07-01T12:00:00Z"
              },
              "timestamp": "2026-07-01T12:00:00Z"
            }
            """;
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(json, HttpStatusCode.Created);
        });
        var client = new HonuaStudioPackageClient(http);

        var publication = await client.CreatePublishRequestAsync(
            ItemId,
            VersionId,
            new CreateStudioPublicationRequest { WarningAcknowledgement = "reviewed" });

        Assert.Equal(
            $"/api/v1/studio/content-items/{ItemId}/versions/{VersionId}/publish-requests",
            captured?.RequestUri?.PathAndQuery);
        Assert.Equal(StudioPublicationRequestStatus.Accepted, publication.Status);
    }

    [Fact]
    public async Task RollbackAsync_SerializesPointerField()
    {
        string? sentBody = null;
        using var http = CreateHttpClient(async request =>
        {
            sentBody = await request.Content!.ReadAsStringAsync();
            return await JsonResponse("""
                {
                  "success": true,
                  "data": {
                    "requestId": "44444444-4444-4444-4444-444444444444",
                    "itemId": "22222222-2222-2222-2222-222222222222",
                    "targetVersionId": "33333333-3333-3333-3333-333333333333",
                    "pointer": "both",
                    "pointers": { "itemId": "22222222-2222-2222-2222-222222222222" },
                    "createdAt": "2026-07-01T12:00:00Z"
                  },
                  "timestamp": "2026-07-01T12:00:00Z"
                }
                """, HttpStatusCode.Created);
        });
        var client = new HonuaStudioPackageClient(http);

        var rollback = await client.RollbackAsync(
            ItemId,
            new CreateStudioRollbackRequest { TargetVersionId = VersionId, Target = StudioRollbackPointer.Both });

        Assert.NotNull(sentBody);
        Assert.Contains("\"pointer\":\"both\"", sentBody, StringComparison.Ordinal);
        Assert.Equal(StudioRollbackPointer.Both, rollback.Target);
    }

    [Fact]
    public async Task DeleteDraftAsync_ThrowsApiExceptionOnConflict()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """{ "title": "Conflict", "status": 409, "detail": "Draft is locked." }""",
            HttpStatusCode.Conflict));
        var client = new HonuaStudioPackageClient(http);

        var ex = await Assert.ThrowsAsync<HonuaStudioApiException>(() => client.DeleteDraftAsync(DraftId));

        Assert.Equal(HttpStatusCode.Conflict, ex.StatusCode);
        Assert.Equal("Conflict", ex.ProblemTitle);
    }

    [Fact]
    public async Task ListVersionsAsync_MissingDataPayload_ThrowsContractException()
    {
        using var http = CreateHttpClient(_ => JsonResponse("""{ "success": true, "timestamp": "2026-07-01T12:00:00Z" }"""));
        var client = new HonuaStudioPackageClient(http);

        var ex = await Assert.ThrowsAsync<HonuaStudioContractException>(() => client.ListVersionsAsync(ItemId));

        Assert.Equal("ListVersions", ex.Operation);
    }

    [Fact]
    public void AddHonuaStudio_ResolvesPackageClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaStudio(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<HonuaStudioPackageClient>(provider.GetRequiredService<IHonuaStudioPackageClient>());
    }

    private static string DraftEnvelope() => """
        {
          "success": true,
          "data": {
            "draftId": "11111111-1111-1111-1111-111111111111",
            "itemId": "22222222-2222-2222-2222-222222222222",
            "packageKey": "my-map",
            "family": "map",
            "envelope": { "family": "map", "schemaVersion": "honua_map_package.v1", "bindings": [], "dependencies": [], "provenance": [], "validation": { "status": "not-validated" } },
            "generation": 1,
            "createdAt": "2026-07-01T12:00:00Z",
            "updatedAt": "2026-07-01T12:00:00Z"
          },
          "timestamp": "2026-07-01T12:00:00Z"
        }
        """;

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => new(new MockHttpHandler(handler)) { BaseAddress = new Uri("https://server.example") };

    private static Task<HttpResponseMessage> JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => Task.FromResult(CreateJsonResponse(json, statusCode));

    private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        return response;
    }
}
