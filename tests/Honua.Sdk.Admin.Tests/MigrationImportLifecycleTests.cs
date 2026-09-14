// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;

namespace Honua.Sdk.Admin.Tests;

/// <summary>
/// Tests for the typed GeoServices/migration import lifecycle client (#340). Response fixtures are
/// raw JSON strings written to match the server's actual wire contracts
/// (<c>GeoservicesImportEndpoints</c>, <c>MigrationBatchEndpoints</c>, <c>MigrationRunAdminEndpoints</c>
/// in honua-server) field-for-field, not a serialize/deserialize round trip of the SDK's own model, so
/// a field-name or shape regression in the SDK model is caught.
/// </summary>
public sealed class MigrationImportLifecycleTests
{
    [Fact]
    public async Task DiscoverGeoservicesServiceAsync_PostsRequestAndParsesLayers()
    {
        string? body = null;
        var client = TestHelpers.CreateClient(async req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/v1/admin/import/geoservices/discover", req.RequestUri!.PathAndQuery);
            body = await req.Content!.ReadAsStringAsync();
            return RawJson("""
                {
                  "serviceUrl": "https://gis.example.com/arcgis/rest/services/Parcels/FeatureServer",
                  "serviceName": "Parcels",
                  "description": "City parcels",
                  "spatialReferenceWkid": 4326,
                  "maxRecordCount": 2000,
                  "layers": [
                    {
                      "id": 0,
                      "name": "Parcels",
                      "description": "Tax parcels",
                      "geometryType": "esriGeometryPolygon",
                      "featureCount": 15234,
                      "hasAttachments": true
                    },
                    {
                      "id": 1,
                      "name": "Zoning",
                      "geometryType": "esriGeometryPolygon",
                      "featureCount": 512,
                      "hasAttachments": false
                    }
                  ]
                }
                """);
        });

        var result = await client.DiscoverGeoservicesServiceAsync(new GeoservicesDiscoverRequest
        {
            ServiceUrl = "https://gis.example.com/arcgis/rest/services/Parcels/FeatureServer",
            Credentials = new GeoservicesCredentialDescriptor
            {
                Mode = "token",
                AccessTokenSecretReference = "secret://arcgis/token"
            }
        });

        using var sent = JsonDocument.Parse(body!);
        Assert.Equal("secret://arcgis/token", sent.RootElement.GetProperty("credentials").GetProperty("accessTokenSecretReference").GetString());

        Assert.Equal("Parcels", result.ServiceName);
        Assert.Equal(4326, result.SpatialReferenceWkid);
        Assert.Equal(2000, result.MaxRecordCount);
        Assert.Equal(2, result.Layers.Count);
        Assert.Equal(15234, result.Layers[0].FeatureCount);
        Assert.True(result.Layers[0].HasAttachments);
        Assert.False(result.Layers[1].HasAttachments);
    }

    [Fact]
    public async Task StartGeoservicesImportAsync_UsesSecretReferenceNotInlineSecret()
    {
        string? body = null;
        var client = TestHelpers.CreateClient(async req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/v1/admin/import/geoservices/start", req.RequestUri!.PathAndQuery);
            body = await req.Content!.ReadAsStringAsync();
            return RawJson("""
                {
                  "jobId": "8f1c2a9b3d4e",
                  "message": "Import job queued for processing",
                  "statusUrl": "jobs/8f1c2a9b3d4e",
                  "cancelUrl": "jobs/8f1c2a9b3d4e/cancel"
                }
                """, HttpStatusCode.Accepted);
        });

        var result = await client.StartGeoservicesImportAsync(new GeoservicesStartImportRequest
        {
            ServiceUrl = "https://gis.example.com/arcgis/rest/services/Parcels/FeatureServer",
            LayerId = 0,
            TableName = "parcels",
            TargetSrid = 4326,
            AutoPublish = true,
            Credentials = new GeoservicesCredentialDescriptor
            {
                Mode = "token",
                AccessTokenSecretReference = "secret://arcgis/token"
            }
        });

        using var sent = JsonDocument.Parse(body!);
        Assert.False(sent.RootElement.GetProperty("credentials").TryGetProperty("accessToken", out _));
        Assert.Equal("secret://arcgis/token", sent.RootElement.GetProperty("credentials").GetProperty("accessTokenSecretReference").GetString());
        Assert.Equal(0, sent.RootElement.GetProperty("layerId").GetInt32());

        Assert.Equal("8f1c2a9b3d4e", result.JobId);
        Assert.Equal("jobs/8f1c2a9b3d4e", result.StatusUrl);
        Assert.Equal("jobs/8f1c2a9b3d4e/cancel", result.CancelUrl);
    }

    [Fact]
    public async Task GetGeoservicesImportJobStatusAsync_ParsesNeedsReviewWithFieldLevelFidelityFindings()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            Assert.Equal("/api/v1/admin/import/geoservices/jobs/8f1c2a9b3d4e", req.RequestUri!.PathAndQuery);
            return Task.FromResult(RawJson(NeedsReviewProgressJson));
        });

        var progress = await client.GetGeoservicesImportJobStatusAsync("8f1c2a9b3d4e");

        Assert.Equal(GeoservicesImportStatus.NeedsReview, progress.Status);
        Assert.Equal(15234, progress.FeaturesProcessed);
        Assert.Equal("parcels", progress.TableName);
        Assert.Equal(42, progress.PublishedLayerId);

        Assert.NotNull(progress.ReconciliationArtifact);
        var reconciliation = progress.ReconciliationArtifact;
        Assert.Equal("run-42", reconciliation!.RunId);
        Assert.Equal("fail", reconciliation.Classification);
        Assert.Equal(1, reconciliation.Summary.LayerCount);
        Assert.Equal(1, reconciliation.Summary.FailCount);

        var layer = Assert.Single(reconciliation.Layers);
        Assert.Equal("0", layer.SourceLayerId);
        Assert.Equal(42, layer.TargetHonuaLayerId);
        Assert.Equal("fail", layer.Classification);

        // Independently computed: |15100 - 15234| / 15234 = 0.008796... which is the deltaRatio the
        // fixture asserts the SDK deserializes verbatim (not recomputed by the SDK).
        Assert.Equal(15234, layer.Count.SourceCount);
        Assert.Equal(15100, layer.Count.TargetCount);
        Assert.Equal(-134, layer.Count.Delta);
        Assert.Equal("pass", layer.Count.Classification);

        Assert.Equal(["OBJECTID", "PARCEL_ID", "STATUS", "ZONE_CODE"], layer.Content.SourceFieldNames);
        Assert.Equal(["OBJECTID", "PARCEL_ID", "STATUS"], layer.Content.TargetFieldNames);
        Assert.Equal(["ZONE_CODE"], layer.Content.MissingOnTarget);
        Assert.Empty(layer.Content.ExtraOnTarget);
        Assert.Equal("fail", layer.Content.Classification);
        Assert.Contains("ZONE_CODE", layer.Content.Reason);

        Assert.Equal(100, layer.Geometry.Sampled);
        Assert.Equal(99, layer.Geometry.Valid);
        Assert.Equal(0.99, layer.Geometry.Ratio);

        Assert.Equal(4326, layer.Extent.Source!.Srid);
        Assert.Equal(-122.5, layer.Extent.Source.MinX);
        Assert.Equal(100, reconciliation.Options.SampleSize);
        Assert.Equal(0.05, reconciliation.Options.CountWarnRatio);

        // The catalog-parity dimension is preserved losslessly as raw JSON rather than deep-typed
        // (see the property doc comment); prove it round-trips a field the typed model does not know.
        Assert.True(progress.CatalogReconciliationReport!.Value.TryGetProperty("schemaFindingCount", out var schemaFindingCount));
        Assert.Equal(3, schemaFindingCount.GetInt32());
    }

    [Fact]
    public async Task ListGeoservicesImportJobsAsync_ParsesJobArray()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/import/geoservices/jobs", req.RequestUri!.PathAndQuery);
            return Task.FromResult(RawJson($$"""
                { "jobs": [ {{QueuedProgressJson}}, {{RunningProgressJson}} ] }
                """));
        });

        var jobs = await client.ListGeoservicesImportJobsAsync();

        Assert.Equal(2, jobs.Count);
        Assert.Equal(GeoservicesImportStatus.Queued, jobs[0].Status);
        Assert.Equal(GeoservicesImportStatus.RetrievingFeatures, jobs[1].Status);
    }

    [Fact]
    public async Task CancelGeoservicesImportJobAsync_PostsToCancelRoute()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/v1/admin/import/geoservices/jobs/8f1c2a9b3d4e/cancel", req.RequestUri!.PathAndQuery);
            return Task.FromResult(RawJson("""
                { "jobId": "8f1c2a9b3d4e", "message": "Import job cancellation requested" }
                """));
        });

        var result = await client.CancelGeoservicesImportJobAsync("8f1c2a9b3d4e");

        Assert.Equal("8f1c2a9b3d4e", result.JobId);
        Assert.Equal("Import job cancellation requested", result.Message);
    }

    [Fact]
    public async Task WaitForGeoservicesImportJobAsync_PollsUntilTerminalStatusAndStops()
    {
        var callCount = 0;
        var client = TestHelpers.CreateClient(req =>
        {
            callCount++;
            var json = callCount switch
            {
                1 => QueuedProgressJson,
                2 => RunningProgressJson,
                _ => CompletedProgressJson
            };
            return Task.FromResult(RawJson(json));
        });

        var result = await client.WaitForGeoservicesImportJobAsync(
            "8f1c2a9b3d4e",
            pollInterval: TimeSpan.FromMilliseconds(1),
            timeout: TimeSpan.FromSeconds(5));

        Assert.Equal(GeoservicesImportStatus.Completed, result.Status);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task WaitForGeoservicesImportJobAsync_NeverCallsStartAgain_OnlyPollsStatus()
    {
        var requestedPaths = new List<string>();
        var client = TestHelpers.CreateClient(req =>
        {
            requestedPaths.Add(req.RequestUri!.PathAndQuery);
            return Task.FromResult(RawJson(CompletedProgressJson));
        });

        await client.WaitForGeoservicesImportJobAsync("8f1c2a9b3d4e", pollInterval: TimeSpan.FromMilliseconds(1));

        Assert.All(requestedPaths, path => Assert.Equal("/api/v1/admin/import/geoservices/jobs/8f1c2a9b3d4e", path));
        Assert.DoesNotContain(requestedPaths, p => p.Contains("/start", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WaitForGeoservicesImportJobAsync_ThrowsTimeoutExceptionWhenBoundElapses()
    {
        var client = TestHelpers.CreateClient(req => Task.FromResult(RawJson(RunningProgressJson)));

        await Assert.ThrowsAsync<TimeoutException>(() => client.WaitForGeoservicesImportJobAsync(
            "8f1c2a9b3d4e",
            pollInterval: TimeSpan.FromMilliseconds(5),
            timeout: TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public async Task StartMigrationBatchAsync_PostsFootprintAndParsesPerChildProgress()
    {
        string? body = null;
        var client = TestHelpers.CreateClient(async req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/v1/admin/import/migrations/", req.RequestUri!.PathAndQuery);
            body = await req.Content!.ReadAsStringAsync();
            return RawJson("""
                {
                  "batchId": "batch-77",
                  "sourceKind": "arcgis-geoservices-rest",
                  "sourceUrl": "https://gis.example.com/arcgis/rest/services",
                  "status": "running",
                  "startedAt": "2026-09-10T12:00:00Z",
                  "totalChildren": 2,
                  "succeededChildren": 1,
                  "failedChildren": 0,
                  "cancelledChildren": 0,
                  "applyRelationships": true,
                  "relationshipsApplied": false,
                  "children": [
                    {
                      "ordinal": 0,
                      "sourceResourceId": "res:parcels",
                      "serviceUrl": "https://gis.example.com/arcgis/rest/services/Parcels/FeatureServer",
                      "sourceLayerId": 0,
                      "tableName": "parcels",
                      "dependsOn": [],
                      "status": "succeeded",
                      "jobId": "job-1",
                      "publishedLayerId": 42
                    },
                    {
                      "ordinal": 1,
                      "sourceResourceId": "res:zoning",
                      "serviceUrl": "https://gis.example.com/arcgis/rest/services/Zoning/FeatureServer",
                      "sourceLayerId": 0,
                      "tableName": "zoning",
                      "dependsOn": ["res:parcels"],
                      "status": "running",
                      "jobId": "job-2"
                    }
                  ]
                }
                """, HttpStatusCode.Accepted);
        });

        var result = await client.StartMigrationBatchAsync(new MigrationBatchStartRequest
        {
            SourceKind = "arcgis-geoservices-rest",
            SourceUrl = "https://gis.example.com/arcgis/rest/services",
            Layers =
            [
                new MigrationBatchLayerSpec
                {
                    SourceResourceId = "res:parcels",
                    ServiceUrl = "https://gis.example.com/arcgis/rest/services/Parcels/FeatureServer",
                    LayerId = 0,
                    TableName = "parcels"
                },
                new MigrationBatchLayerSpec
                {
                    SourceResourceId = "res:zoning",
                    ServiceUrl = "https://gis.example.com/arcgis/rest/services/Zoning/FeatureServer",
                    LayerId = 0,
                    TableName = "zoning",
                    DependsOn = ["res:parcels"]
                }
            ]
        });

        using var sent = JsonDocument.Parse(body!);
        Assert.Equal(2, sent.RootElement.GetProperty("layers").GetArrayLength());
        Assert.Equal("res:parcels", sent.RootElement.GetProperty("layers")[1].GetProperty("dependsOn")[0].GetString());

        Assert.Equal("batch-77", result.BatchId);
        Assert.Equal("running", result.Status);
        Assert.Equal(2, result.Children.Count);
        Assert.Equal("succeeded", result.Children[0].Status);
        Assert.Equal(42, result.Children[0].PublishedLayerId);
        Assert.Equal(["res:parcels"], result.Children[1].DependsOn);
        Assert.Equal("job-2", result.Children[1].JobId);
    }

    [Fact]
    public async Task GetMigrationBatchAsync_GetsBatchStatusRoute()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/import/migrations/batch-77", req.RequestUri!.PathAndQuery);
            return Task.FromResult(RawJson("""
                {
                  "batchId": "batch-77",
                  "sourceKind": "arcgis-geoservices-rest",
                  "status": "needs-review",
                  "startedAt": "2026-09-10T12:00:00Z",
                  "completedAt": "2026-09-10T12:05:00Z",
                  "totalChildren": 1,
                  "succeededChildren": 0,
                  "failedChildren": 0,
                  "cancelledChildren": 0,
                  "applyRelationships": false,
                  "relationshipsApplied": false,
                  "children": []
                }
                """));
        });

        var result = await client.GetMigrationBatchAsync("batch-77");

        Assert.Equal("needs-review", result.Status);
        Assert.Equal(DateTimeOffset.Parse("2026-09-10T12:05:00Z"), result.CompletedAt);
    }

    [Fact]
    public async Task ListMigrationRunsAsync_BuildsQueryStringFromFilters()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal(
                "/api/v1/admin/migration/runs/?limit=10&offset=5&sourceKind=arcgis-geoservices-rest&status=failed",
                req.RequestUri!.PathAndQuery);
            return Task.FromResult(RawJson("""
                { "items": [], "totalCount": 0, "limit": 10, "offset": 5 }
                """));
        });

        var result = await client.ListMigrationRunsAsync(new MigrationRunListQuery
        {
            Limit = 10,
            Offset = 5,
            SourceKind = "arcgis-geoservices-rest",
            Status = "failed"
        });

        Assert.Equal(0, result.TotalCount);
        Assert.Equal(10, result.Limit);
    }

    [Fact]
    public async Task ListMigrationRunsAsync_NoFilters_HitsBareRoute()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/migration/runs/", req.RequestUri!.PathAndQuery);
            return Task.FromResult(RawJson("""
                { "items": [], "totalCount": 0, "limit": 25, "offset": 0 }
                """));
        });

        await client.ListMigrationRunsAsync();
    }

    [Fact]
    public async Task GetMigrationRunAsync_ParsesRunDto()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/migration/runs/run-42", req.RequestUri!.PathAndQuery);
            return Task.FromResult(RawJson("""
                {
                  "runId": "run-42",
                  "sourceKind": "arcgis-geoservices-rest",
                  "sourceUrl": "https://gis.example.com/arcgis/rest/services",
                  "status": "needs-review",
                  "startedAt": "2026-09-10T12:00:00Z",
                  "hasEvidencePack": true,
                  "evidencePackFingerprint": "sha256:abc123",
                  "hasReconciliationScorecard": true,
                  "reconciliationScorecardFingerprint": "sha256:def456"
                }
                """));
        });

        var run = await client.GetMigrationRunAsync("run-42");

        Assert.Equal("needs-review", run.Status);
        Assert.True(run.HasEvidencePack);
        Assert.Equal("sha256:abc123", run.EvidencePackFingerprint);
        Assert.True(run.HasReconciliationScorecard);
    }

    [Fact]
    public async Task CancelMigrationRunAsync_PostsReasonAndParsesUpdatedRun()
    {
        string? body = null;
        var client = TestHelpers.CreateClient(async req =>
        {
            Assert.Equal(HttpMethod.Post, req.Method);
            Assert.Equal("/api/v1/admin/migration/runs/run-42/cancel", req.RequestUri!.PathAndQuery);
            body = await req.Content!.ReadAsStringAsync();
            return RawJson("""
                {
                  "runId": "run-42",
                  "sourceKind": "arcgis-geoservices-rest",
                  "status": "cancelled",
                  "startedAt": "2026-09-10T12:00:00Z",
                  "completedAt": "2026-09-10T12:01:00Z",
                  "statusNote": "operator cancel",
                  "hasEvidencePack": false,
                  "hasReconciliationScorecard": false
                }
                """);
        });

        var run = await client.CancelMigrationRunAsync("run-42", reason: "operator cancel");

        using var sent = JsonDocument.Parse(body!);
        Assert.Equal("operator cancel", sent.RootElement.GetProperty("reason").GetString());
        Assert.Equal("cancelled", run.Status);
        Assert.Equal("operator cancel", run.StatusNote);
    }

    [Fact]
    public async Task GetMigrationRunEvidencePackAsync_ReturnsRawBodyAndETag()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/migration/runs/run-42/evidence-pack", req.RequestUri!.PathAndQuery);
            var response = RawJson("""{"artifactKind":"honua.migration.evidence-pack","runId":"run-42"}""");
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"sha256:abc123\"");
#pragma warning disable CA2025 // Ownership of this HttpResponseMessage test double transfers to the HTTP pipeline, which disposes it.
            return Task.FromResult(response);
#pragma warning restore CA2025
        });

        var download = await client.GetMigrationRunEvidencePackAsync("run-42");

        using var doc = JsonDocument.Parse(download.Body);
        Assert.Equal("run-42", doc.RootElement.GetProperty("runId").GetString());
        Assert.Equal("\"sha256:abc123\"", download.ETag);
    }

    [Fact]
    public async Task GetMigrationRunReconciliationScorecardAsync_ParsesBothDimensions()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/migration/runs/run-42/scorecard", req.RequestUri!.PathAndQuery);
            return Task.FromResult(RawJson("""
                {
                  "runId": "run-42",
                  "sourceKind": "arcgis-geoservices-rest",
                  "generatedAt": "2026-09-10T12:06:00Z",
                  "verdict": "fail",
                  "dataReconciliation": {
                    "classification": "fail",
                    "layerCount": 2,
                    "passCount": 1,
                    "warnCount": 0,
                    "failCount": 1,
                    "skippedCount": 0,
                    "catalogFindingCount": 3,
                    "layers": [
                      { "sourceLayerId": "0", "targetHonuaLayerId": 42, "classification": "fail" },
                      { "sourceLayerId": "1", "targetHonuaLayerId": 43, "classification": "pass" }
                    ],
                    "reasons": ["Layer 0: missing field ZONE_CODE on target"]
                  },
                  "capabilityParity": {
                    "constructCount": 10,
                    "automatedCount": 7,
                    "assistedCount": 2,
                    "manualReviewCount": 1,
                    "unsupportedCount": 0,
                    "parityRatio": 0.9
                  },
                  "fingerprint": "sha256:deadbeef"
                }
                """));
        });

        var scorecard = await client.GetMigrationRunReconciliationScorecardAsync("run-42");

        Assert.Equal("fail", scorecard.Verdict);
        Assert.Equal(2, scorecard.DataReconciliation.LayerCount);
        Assert.Equal(3, scorecard.DataReconciliation.CatalogFindingCount);
        Assert.Equal("fail", scorecard.DataReconciliation.Layers[0].Classification);
        Assert.Equal("pass", scorecard.DataReconciliation.Layers[1].Classification);

        // Independently computed: (7 + 2) / 10 = 0.9, matches the server's own ParityRatio formula.
        Assert.Equal(0.9, scorecard.CapabilityParity.ParityRatio);
        Assert.Equal("sha256:deadbeef", scorecard.Fingerprint);
    }

    [Fact]
    public async Task ErrorResponse_CapturesRetryAfterHeader()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            var response = RawJson(
                """{"success":false,"message":"Distributed import coordination is unavailable. Retry when Redis is healthy."}""",
                HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return Task.FromResult(response);
        });

        var ex = await Assert.ThrowsAsync<HonuaAdminApiException>(() => client.GetGeoservicesImportJobStatusAsync("job-x"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(30), ex.RetryAfter);
    }

    [Fact]
    public async Task ExtensionMethods_DelegateThroughInterfaceToConcreteClient()
    {
        IHonuaAdminClient client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/import/geoservices/jobs/8f1c2a9b3d4e", req.RequestUri!.PathAndQuery);
            return Task.FromResult(RawJson(CompletedProgressJson));
        });

        var progress = await client.GetGeoservicesImportJobStatusAsync("8f1c2a9b3d4e");

        Assert.Equal(GeoservicesImportStatus.Completed, progress.Status);
    }

    private static HttpResponseMessage RawJson(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private const string QueuedProgressJson = """
        {
          "jobId": "8f1c2a9b3d4e",
          "status": "queued",
          "featuresProcessed": 0,
          "sourceServiceUrl": "https://gis.example.com/arcgis/rest/services/Parcels/FeatureServer",
          "sourceLayerId": 0,
          "tableName": "parcels",
          "startedAt": "2026-09-10T12:00:00Z",
          "currentPhase": "Queued for processing"
        }
        """;

    private const string RunningProgressJson = """
        {
          "jobId": "8f1c2a9b3d4e",
          "status": "retrievingFeatures",
          "featuresProcessed": 4200,
          "estimatedTotalFeatures": 15234,
          "sourceServiceUrl": "https://gis.example.com/arcgis/rest/services/Parcels/FeatureServer",
          "sourceLayerId": 0,
          "tableName": "parcels",
          "startedAt": "2026-09-10T12:00:00Z",
          "currentPhase": "Retrieving features"
        }
        """;

    private const string CompletedProgressJson = """
        {
          "jobId": "8f1c2a9b3d4e",
          "status": "completed",
          "featuresProcessed": 15234,
          "estimatedTotalFeatures": 15234,
          "sourceServiceUrl": "https://gis.example.com/arcgis/rest/services/Parcels/FeatureServer",
          "sourceLayerId": 0,
          "tableName": "parcels",
          "publishedLayerId": 42,
          "startedAt": "2026-09-10T12:00:00Z",
          "completedAt": "2026-09-10T12:04:30Z"
        }
        """;

    private const string NeedsReviewProgressJson = """
        {
          "jobId": "8f1c2a9b3d4e",
          "status": "needsReview",
          "featuresProcessed": 15234,
          "estimatedTotalFeatures": 15234,
          "sourceServiceUrl": "https://gis.example.com/arcgis/rest/services/Parcels/FeatureServer",
          "sourceLayerId": 0,
          "tableName": "parcels",
          "publishedLayerId": 42,
          "startedAt": "2026-09-10T12:00:00Z",
          "completedAt": "2026-09-10T12:05:00Z",
          "reconciliationArtifact": {
            "runId": "run-42",
            "sourceKind": "arcgis-geoservices-rest",
            "classification": "fail",
            "startedAt": "2026-09-10T12:04:30Z",
            "completedAt": "2026-09-10T12:05:00Z",
            "summary": { "layerCount": 1, "passCount": 0, "warnCount": 0, "failCount": 1, "skippedCount": 0 },
            "layers": [
              {
                "sourceLayerId": "0",
                "sourceLayerName": "Parcels",
                "targetHonuaLayerId": 42,
                "classification": "fail",
                "count": { "sourceCount": 15234, "targetCount": 15100, "delta": -134, "deltaRatio": 0.008796640409057358, "classification": "pass" },
                "geometry": { "sampled": 100, "valid": 99, "ratio": 0.99, "classification": "pass" },
                "content": {
                  "sourceFieldNames": ["OBJECTID", "PARCEL_ID", "STATUS", "ZONE_CODE"],
                  "targetFieldNames": ["OBJECTID", "PARCEL_ID", "STATUS"],
                  "missingOnTarget": ["ZONE_CODE"],
                  "extraOnTarget": [],
                  "classification": "fail",
                  "reason": "Source field ZONE_CODE is missing on the published target."
                },
                "extent": {
                  "source": { "minX": -122.5, "minY": 37.7, "maxX": -122.3, "maxY": 37.9, "srid": 4326 },
                  "target": { "minX": -122.5, "minY": 37.7, "maxX": -122.3, "maxY": 37.9, "srid": 4326 },
                  "maxDimensionDelta": 0.0,
                  "classification": "pass"
                }
              }
            ],
            "reasons": ["Layer 0: missing field ZONE_CODE on target"],
            "options": { "sampleSize": 100, "countWarnRatio": 0.05, "countFailRatio": 0.20, "geometryPassRatio": 0.99, "geometryWarnRatio": 0.95, "extentTolerance": 0.001 }
          },
          "catalogReconciliationReport": { "schemaFindingCount": 3, "domainFindingCount": 0 }
        }
        """;
}
