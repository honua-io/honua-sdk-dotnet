// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using Honua.Sdk.Studio.Capabilities;
using Honua.Sdk.Studio.Exceptions;
using Honua.Sdk.Studio.Extensions;
using Honua.Sdk.Studio.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.Studio.Tests;

public sealed class CapabilityManifestClientTests
{
    private const string ManifestJson = """
        {
          "schemaVersion": "honua.capability_manifest.v1",
          "issuedAt": "2026-07-01T12:00:00Z",
          "scope": {
            "tenantId": "acme",
            "tenantSource": "header",
            "environment": "prod",
            "workspaceId": "ws-1",
            "workspaceAvailable": true,
            "authenticated": true
          },
          "server": {
            "serverVersion": "1.9.0",
            "apiVersion": "1",
            "metadataApiVersion": "2",
            "metadataSchemaVersion": "honua.metadata.v2",
            "deploymentEnvironment": "production"
          },
          "environment": { "environmentId": "prod", "requested": true, "available": true, "revision": 42 },
          "packages": {
            "schemaVersions": ["honua_map_package.v1"],
            "families": [
              { "id": "map", "kind": "storage", "schemaVersion": "honua_map_package.v1", "supported": true },
              { "id": "etl", "kind": "storage", "schemaVersion": "honua_etl_package.v1", "supported": false }
            ],
            "storageFamilies": ["map"],
            "publicationFamilies": ["map"]
          },
          "capabilities": [
            { "id": "studio.map", "category": "studio", "supported": true, "available": true },
            {
              "id": "studio.ai.generate",
              "category": "studio",
              "supported": true,
              "available": false,
              "reasonCode": "entitlement-inactive",
              "entitlementKey": "ai.generation",
              "entitlementKeys": ["ai.generation"],
              "minimumEdition": "enterprise",
              "messageKey": "capability.ai.disabled"
            }
          ],
          "transports": {
            "items": [ { "id": "grpc", "supported": true, "available": true } ],
            "mtlsMode": "disabled",
            "forwardedClientCertificateEnabled": false
          },
          "limits": {
            "preview": { "maxPreviewSizeBytes": 1048576, "maxPreviewFeatures": 500, "maxPreviewCountScan": 10000 },
            "query": {
              "defaultRecordCount": 100, "maxRecordCount": 2000, "maxFeatures": 5000, "maxPageSize": 1000,
              "queryTimeoutSeconds": 30, "maxBboxAreaSqKm": 100000.0, "maxFilterDepth": 8, "maxSpatialOperations": 4
            },
            "analysis": {
              "maxInputFeatures": 100000, "maxClusters": 50, "maxDbscanEpsMeters": 5000.0, "maxKMeansK": 25,
              "maxBufferDistanceMeters": 100000.0, "minDensityCellSizeMeters": 10.0, "maxDensityCellSizeMeters": 100000.0,
              "maxDensityCells": 100000, "maxDWithinDistanceMeters": 50000.0, "maxH3CellsPerQuery": 10000,
              "maxSpatialOperations": 8, "maxJoins": 4
            },
            "publication": { "configuredDeployTargetCount": 2, "gitOpsManifestExportSupported": true },
            "job": { "configuredWorkloadCount": 3, "availableBackendCount": 1, "supportsCancellation": true, "supportsProgressPolling": true },
            "upload": {
              "maxUploadSizeBytes": 104857600, "maxFileSizeBytes": 52428800, "maxConcurrentUploads": 4,
              "maxQueuedUploads": 16, "maxSecurityScanSizeBytes": 10485760
            },
            "streaming": {
              "maxConcurrentSessions": 100, "maxBufferPerConnection": 1000, "maxSubscriptionsPerSession": 20,
              "maxSubscriptionIdLength": 128, "maxControlFrameBytes": 65536, "cursorRetentionLimit": 1000,
              "heartbeatIntervalSeconds": 15.0, "grpcStreamBatchSize": 100
            },
            "edit": { "maxFeaturesPerEdit": 1000, "maxEditsPerTransaction": 100, "maxPayloadSizeBytes": 10485760 },
            "geometry": { "maxVerticesPerGeometry": 100000, "maxGeometrySizeBytes": 1048576, "maxCoordinatePrecision": 15 },
            "attachment": { "maxAttachmentsPerFeature": 25, "maxAttachmentSizeBytes": 10485760 }
          },
          "policies": {
            "currentEdition": "enterprise",
            "licenseValidationState": "valid",
            "licenseValid": true,
            "callerCapabilities": ["studio.map"],
            "entitlements": [
              { "key": "ai.generation", "active": false, "minimumEdition": "enterprise", "reasonCode": "entitlement-inactive" }
            ],
            "authorizationNotice": "Authorized for tenant acme."
          },
          "links": [ { "rel": "self", "href": "/api/v1/capabilities/manifest", "type": "application/json" } ]
        }
        """;

    [Fact]
    public async Task GetManifestAsync_DeserializesFrozenWireAndSurfacesReasonCodes()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(ManifestJson);
        });
        var client = new HonuaCapabilityManifestClient(http);

        var manifest = await client.GetManifestAsync();

        Assert.Equal(HttpMethod.Get, captured?.Method);
        Assert.Equal("/api/v1/capabilities/manifest", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("honua.capability_manifest.v1", manifest.SchemaVersion);
        Assert.Equal("acme", manifest.Scope?.TenantId);
        Assert.Equal("1.9.0", manifest.Server?.ServerVersion);
        Assert.Equal(42, manifest.Environment?.Revision);
        Assert.Equal(2, manifest.Capabilities.Count);
        Assert.Equal("application/json", manifest.Links.Single().MediaType);

        // Query helpers (parity with the JS getCapability / hasCapability helpers).
        Assert.True(manifest.IsAvailable("studio.map"));
        Assert.True(manifest.IsSupported("studio.ai.generate"));
        Assert.False(manifest.IsAvailable("studio.ai.generate"));
        Assert.Equal("entitlement-inactive", manifest.GetReasonCode("studio.ai.generate"));
        Assert.Equal("ai.generation", manifest.GetCapability("studio.ai.generate")?.EntitlementKey);
        Assert.Null(manifest.GetCapability("nope"));
        Assert.False(manifest.IsAvailable("nope"));

        // Package-family gating.
        Assert.True(manifest.HasPackageFamily("map"));
        Assert.False(manifest.HasPackageFamily("etl"));

        // A representative limit round-trips.
        Assert.Equal(2000, manifest.Limits?.Query?.MaxRecordCount);
        Assert.False(manifest.Policies?.Entitlements.Single().Active);
    }

    [Fact]
    public async Task GetManifestAsync_AppendsScopeQueryParameters()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(ManifestJson);
        });
        var client = new HonuaCapabilityManifestClient(http);

        await client.GetManifestAsync(environment: "prod env", workspaceId: "ws-1");

        Assert.Equal(
            "/api/v1/capabilities/manifest?environment=prod%20env&workspaceId=ws-1",
            captured?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task GetManifestAsync_ToleratesUnknownAdditiveFields()
    {
        const string json = """
            {
              "schemaVersion": "honua.capability_manifest.v1",
              "issuedAt": "2026-07-01T12:00:00Z",
              "capabilities": [ { "id": "studio.map", "supported": true, "available": true, "futureField": 7 } ],
              "somethingNew": { "nested": true }
            }
            """;
        using var http = CreateHttpClient(_ => JsonResponse(json));
        var client = new HonuaCapabilityManifestClient(http);

        var manifest = await client.GetManifestAsync();

        Assert.True(manifest.IsAvailable("studio.map"));
    }

    [Fact]
    public async Task GetManifestAsync_ProblemResponse_ThrowsApiException()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """{ "title": "Bad Request", "status": 400, "detail": "environment contains unsupported characters." }""",
            HttpStatusCode.BadRequest));
        var client = new HonuaCapabilityManifestClient(http);

        var ex = await Assert.ThrowsAsync<HonuaStudioApiException>(() => client.GetManifestAsync());

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal("Bad Request", ex.ProblemTitle);
    }

    [Fact]
    public void AddHonuaStudio_ResolvesCapabilityManifestClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaStudio(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<HonuaCapabilityManifestClient>(provider.GetRequiredService<IHonuaCapabilityManifestClient>());
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => new(new MockHttpHandler(handler)) { BaseAddress = new Uri("https://server.example") };

    private static Task<HttpResponseMessage> JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
}
