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

public sealed class HonuaConsoleShareExportClientTests
{
    [Fact]
    public async Task ListExportDefinitionsAsync_BuildsQueryAndDeserializesPage()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "items": [
                    {
                      "exportId": "exp-1",
                      "serviceName": "parcels",
                      "layerId": 3,
                      "destinationType": "S3",
                      "destinationStatus": "Supported",
                      "destinationConfig": { "bucket": "open-data", "secretRef": "kv://x" },
                      "format": "geojson",
                      "schedule": "0 2 * * *",
                      "scheduleState": "Active",
                      "createdAt": "2026-06-01T00:00:00Z",
                      "updatedAt": "2026-06-02T00:00:00Z",
                      "lastRunAt": "2026-06-10T02:00:00Z"
                    }
                  ],
                  "nextCursor": "c2"
                }
                """);
        });
        var client = new HonuaConsoleShareExportClient(http);

        var page = await client.ListExportDefinitionsAsync(new HonuaShareExportDefinitionQuery
        {
            ServiceName = "parcels",
            DestinationType = HonuaShareExportDestinationType.S3,
            ScheduleState = HonuaShareExportScheduleState.Active,
            Limit = 25
        });

        Assert.Equal(HttpMethod.Get, captured?.Method);
        var query = captured?.RequestUri?.PathAndQuery;
        Assert.StartsWith("/api/v1/admin/share/exports?", query);
        Assert.Contains("serviceName=parcels", query, StringComparison.Ordinal);
        Assert.Contains("destinationType=S3", query, StringComparison.Ordinal);
        Assert.Contains("scheduleState=Active", query, StringComparison.Ordinal);
        Assert.Contains("limit=25", query, StringComparison.Ordinal);

        var item = Assert.Single(page.Items);
        Assert.Equal("exp-1", item.ExportId);
        Assert.Equal(HonuaShareExportDestinationType.S3, item.DestinationType);
        Assert.Equal(HonuaShareExportDestinationStatus.Supported, item.DestinationStatus);
        Assert.Equal(HonuaShareExportScheduleState.Active, item.ScheduleState);
        Assert.Equal("open-data", item.DestinationConfig["bucket"]);
        Assert.Equal("c2", page.NextCursor);
    }

    [Fact]
    public async Task ListExportDefinitionsAsync_NoQuery_OmitsQueryString()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse("""{ "items": [] }""");
        });
        var client = new HonuaConsoleShareExportClient(http);

        var page = await client.ListExportDefinitionsAsync();

        Assert.Equal("/api/v1/admin/share/exports", captured?.RequestUri?.PathAndQuery);
        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task CreateExportDefinitionAsync_SendsRequestAndReturnsDefinition()
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
                  "exportId": "exp-9",
                  "serviceName": "parcels",
                  "layerId": 3,
                  "destinationType": "Webhook",
                  "destinationStatus": "NotConfigured",
                  "destinationConfig": {},
                  "format": "csv",
                  "schedule": "@daily",
                  "scheduleState": "Active",
                  "createdAt": "2026-06-12T00:00:00Z",
                  "updatedAt": "2026-06-12T00:00:00Z"
                }
                """,
                HttpStatusCode.Created);
        });
        var client = new HonuaConsoleShareExportClient(http);

        var created = await client.CreateExportDefinitionAsync(new HonuaShareExportDefinitionRequest
        {
            ServiceName = "parcels",
            LayerId = 3,
            DestinationType = HonuaShareExportDestinationType.Webhook,
            Format = "csv",
            Schedule = "@daily"
        });

        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal("/api/v1/admin/share/exports", captured?.RequestUri?.PathAndQuery);
        Assert.Contains("\"destinationType\":\"Webhook\"", body, StringComparison.Ordinal);
        Assert.Contains("\"serviceName\":\"parcels\"", body, StringComparison.Ordinal);
        Assert.Equal("exp-9", created.ExportId);
        Assert.Equal(HonuaShareExportDestinationStatus.NotConfigured, created.DestinationStatus);
    }

    [Fact]
    public async Task GetExportDefinitionAsync_RequestsByIdAndDeserializes()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "exportId": "exp 7",
                  "serviceName": "roads",
                  "layerId": 0,
                  "destinationType": "AuditSnapshot",
                  "destinationStatus": "Supported",
                  "destinationConfig": {},
                  "format": "json",
                  "schedule": "0 0 * * 0",
                  "scheduleState": "Paused",
                  "createdAt": "2026-06-01T00:00:00Z",
                  "updatedAt": "2026-06-01T00:00:00Z"
                }
                """);
        });
        var client = new HonuaConsoleShareExportClient(http);

        var definition = await client.GetExportDefinitionAsync("exp 7");

        Assert.Equal(HttpMethod.Get, captured?.Method);
        Assert.Equal("/api/v1/admin/share/exports/exp%207", captured?.RequestUri?.PathAndQuery);
        Assert.Equal(HonuaShareExportDestinationType.AuditSnapshot, definition.DestinationType);
        Assert.Equal(HonuaShareExportScheduleState.Paused, definition.ScheduleState);
    }

    [Fact]
    public async Task UpdateExportDefinitionAsync_SendsPut()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(MinimalDefinitionJson("exp-1"));
        });
        var client = new HonuaConsoleShareExportClient(http);

        await client.UpdateExportDefinitionAsync("exp-1", new HonuaShareExportDefinitionRequest
        {
            ServiceName = "parcels",
            LayerId = 3,
            DestinationType = HonuaShareExportDestinationType.S3,
            Format = "geojson",
            Schedule = "@daily"
        });

        Assert.Equal(HttpMethod.Put, captured?.Method);
        Assert.Equal("/api/v1/admin/share/exports/exp-1", captured?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task DeleteExportDefinitionAsync_SendsDelete()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var client = new HonuaConsoleShareExportClient(http);

        await client.DeleteExportDefinitionAsync("exp-1");

        Assert.Equal(HttpMethod.Delete, captured?.Method);
        Assert.Equal("/api/v1/admin/share/exports/exp-1", captured?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task TriggerExportAsync_PostsTriggerAndReturnsRun()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return CreateJsonResponseTask(
                """
                {
                  "runId": "run-1",
                  "exportId": "exp-1",
                  "triggerKind": "Manual",
                  "status": "Queued",
                  "jobRunId": "share-export-abc",
                  "triggeredAt": "2026-06-12T03:00:00Z",
                  "resultArtifacts": []
                }
                """,
                HttpStatusCode.Accepted);
        });
        var client = new HonuaConsoleShareExportClient(http);

        var run = await client.TriggerExportAsync("exp-1");

        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal("/api/v1/admin/share/exports/exp-1/trigger", captured?.RequestUri?.PathAndQuery);
        Assert.Equal("run-1", run.RunId);
        Assert.Equal(HonuaShareExportTriggerKind.Manual, run.TriggerKind);
        Assert.Equal(HonuaShareExportRunStatus.Queued, run.Status);
        Assert.Equal("share-export-abc", run.JobRunId);
        Assert.Empty(run.ResultArtifacts);
    }

    [Fact]
    public async Task TriggerExportAsync_DestinationNotConfigured_ThrowsApiException()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """
            {
              "type": "https://honua.io/problems/unprocessable",
              "title": "share-export-destination-not-configured",
              "status": 422,
              "detail": "The Share export destination is known but is not configured for this environment."
            }
            """,
            HttpStatusCode.UnprocessableEntity));
        var client = new HonuaConsoleShareExportClient(http);

        var ex = await Assert.ThrowsAsync<HonuaConsoleShareApiException>(() => client.TriggerExportAsync("exp-1"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ex.StatusCode);
        Assert.Equal("share-export-destination-not-configured", ex.ProblemTitle);
    }

    [Theory]
    [InlineData("pause")]
    [InlineData("resume")]
    public async Task PauseResumeExportAsync_PostsActionPath(string action)
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(MinimalDefinitionJson("exp-1"));
        });
        var client = new HonuaConsoleShareExportClient(http);

        _ = action == "pause"
            ? await client.PauseExportAsync("exp-1")
            : await client.ResumeExportAsync("exp-1");

        Assert.Equal(HttpMethod.Post, captured?.Method);
        Assert.Equal($"/api/v1/admin/share/exports/exp-1/{action}", captured?.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task ListExportRunsAsync_BuildsPagingQuery()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse("""{ "items": [], "nextCursor": null }""");
        });
        var client = new HonuaConsoleShareExportClient(http);

        var page = await client.ListExportRunsAsync("exp-1", cursor: "abc", limit: 10);

        Assert.Equal("/api/v1/admin/share/exports/exp-1/runs?cursor=abc&limit=10", captured?.RequestUri?.PathAndQuery);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task GetExportRunAsync_RequestsRunPath()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "runId": "run-5",
                  "exportId": "exp-1",
                  "triggerKind": "Scheduled",
                  "status": "Failed",
                  "triggeredAt": "2026-06-12T03:00:00Z",
                  "completedAt": "2026-06-12T03:01:00Z",
                  "resultArtifacts": ["s3://open-data/parcels.geojson"],
                  "lastError": "share-export-dispatch-failed"
                }
                """);
        });
        var client = new HonuaConsoleShareExportClient(http);

        var run = await client.GetExportRunAsync("exp-1", "run-5");

        Assert.Equal("/api/v1/admin/share/exports/exp-1/runs/run-5", captured?.RequestUri?.PathAndQuery);
        Assert.Equal(HonuaShareExportTriggerKind.Scheduled, run.TriggerKind);
        Assert.Equal(HonuaShareExportRunStatus.Failed, run.Status);
        Assert.Equal("share-export-dispatch-failed", run.LastError);
        Assert.Equal("s3://open-data/parcels.geojson", Assert.Single(run.ResultArtifacts));
    }

    [Fact]
    public async Task GetTrafficSummaryAsync_BuildsPeriodQueryAndDeserializesCounts()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "itemRef": null,
                  "periodStart": "2026-06-01T00:00:00Z",
                  "periodEnd": "2026-06-02T00:00:00Z",
                  "byInteractionType": {
                    "public": 5,
                    "publicLink": 3,
                    "embed": 2,
                    "openData": 1,
                    "dcat": 4,
                    "stac": 6,
                    "export": 7
                  },
                  "totalRequests": 28
                }
                """);
        });
        var client = new HonuaConsoleShareExportClient(http);

        var summary = await client.GetTrafficSummaryAsync(new HonuaShareTrafficQuery
        {
            PeriodStart = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            PeriodEnd = new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero)
        });

        var query = captured?.RequestUri?.PathAndQuery;
        Assert.StartsWith("/api/v1/admin/share/traffic?", query);
        Assert.Contains("periodStart=", query, StringComparison.Ordinal);
        Assert.Contains("periodEnd=", query, StringComparison.Ordinal);
        Assert.Null(summary.ItemRef);
        Assert.Equal(28, summary.TotalRequests);
        Assert.Equal(7, summary.ByInteractionType.Export);
        Assert.Equal(5, summary.ByInteractionType.Public);
    }

    [Fact]
    public async Task GetTrafficSeriesAsync_BuildsBucketQueryAndDeserializesBuckets()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "itemRef": null,
                  "periodStart": "2026-06-01T00:00:00Z",
                  "periodEnd": "2026-06-01T02:00:00Z",
                  "bucketDuration": "01:00:00",
                  "buckets": [
                    {
                      "bucketStart": "2026-06-01T00:00:00Z",
                      "byInteractionType": { "public": 1, "publicLink": 0, "embed": 0, "openData": 0, "dcat": 0, "stac": 0, "export": 0 },
                      "total": 1
                    }
                  ]
                }
                """);
        });
        var client = new HonuaConsoleShareExportClient(http);

        var series = await client.GetTrafficSeriesAsync(new HonuaShareTrafficQuery { BucketMinutes = 60 });

        Assert.Equal("/api/v1/admin/share/traffic/series?bucketMinutes=60", captured?.RequestUri?.PathAndQuery);
        Assert.Equal(TimeSpan.FromHours(1), series.BucketDuration);
        var bucket = Assert.Single(series.Buckets);
        Assert.Equal(1, bucket.Total);
        Assert.Equal(1, bucket.ByInteractionType.Public);
    }

    [Fact]
    public async Task GetItemTrafficSummaryAsync_BuildsServiceLayerPath()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "itemRef": { "resourceId": "res-1", "serviceName": "parcels", "layerId": 3 },
                  "periodStart": "2026-06-01T00:00:00Z",
                  "periodEnd": "2026-06-02T00:00:00Z",
                  "byInteractionType": { "public": 0, "publicLink": 0, "embed": 0, "openData": 0, "dcat": 0, "stac": 0, "export": 0 },
                  "totalRequests": 0
                }
                """);
        });
        var client = new HonuaConsoleShareExportClient(http);

        var summary = await client.GetItemTrafficSummaryAsync("parcels", 3, resourceId: "res-1");

        var query = captured?.RequestUri?.PathAndQuery;
        Assert.StartsWith("/api/v1/admin/services/parcels/layers/3/share/traffic?", query);
        Assert.Contains("resourceId=res-1", query, StringComparison.Ordinal);
        Assert.NotNull(summary.ItemRef);
        Assert.Equal("parcels", summary.ItemRef!.ServiceName);
        Assert.Equal(3, summary.ItemRef.LayerId);
    }

    [Fact]
    public async Task GetItemTrafficSeriesAsync_BuildsServiceLayerSeriesPath()
    {
        HttpRequestMessage? captured = null;
        using var http = CreateHttpClient(request =>
        {
            captured = request;
            return JsonResponse(
                """
                {
                  "itemRef": { "serviceName": "parcels", "layerId": 3 },
                  "periodStart": "2026-06-01T00:00:00Z",
                  "periodEnd": "2026-06-01T01:00:00Z",
                  "bucketDuration": "00:30:00",
                  "buckets": []
                }
                """);
        });
        var client = new HonuaConsoleShareExportClient(http);

        var series = await client.GetItemTrafficSeriesAsync("parcels", 3, query: new HonuaShareTrafficQuery { BucketMinutes = 30 });

        var query = captured?.RequestUri?.PathAndQuery;
        Assert.StartsWith("/api/v1/admin/services/parcels/layers/3/share/traffic/series?", query);
        Assert.Contains("bucketMinutes=30", query, StringComparison.Ordinal);
        Assert.Equal(TimeSpan.FromMinutes(30), series.BucketDuration);
        Assert.Empty(series.Buckets);
    }

    [Fact]
    public async Task GetExportDefinitionAsync_NotFoundProblem_ThrowsApiException()
    {
        using var http = CreateHttpClient(_ => JsonResponse(
            """
            {
              "type": "https://honua.io/problems/not-found",
              "title": "Not Found",
              "status": 404,
              "detail": "Share export definition was not found."
            }
            """,
            HttpStatusCode.NotFound));
        var client = new HonuaConsoleShareExportClient(http);

        var ex = await Assert.ThrowsAsync<HonuaConsoleShareApiException>(() => client.GetExportDefinitionAsync("missing"));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal("Not Found", ex.ProblemTitle);
        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTrafficSummaryAsync_MalformedSuccessBody_ThrowsContractException()
    {
        using var http = CreateHttpClient(_ => JsonResponse("not-json", HttpStatusCode.OK));
        var client = new HonuaConsoleShareExportClient(http);

        var ex = await Assert.ThrowsAsync<HonuaConsoleShareContractException>(() => client.GetTrafficSummaryAsync());

        Assert.Equal("GetTrafficSummary", ex.Operation);
    }

    [Fact]
    public async Task GetExportDefinitionAsync_BlankId_Throws()
    {
        using var http = CreateHttpClient(_ => JsonResponse("{}"));
        var client = new HonuaConsoleShareExportClient(http);

        await Assert.ThrowsAsync<ArgumentException>(() => client.GetExportDefinitionAsync("   "));
    }

    [Fact]
    public void AddHonuaConsoleShareExport_ResolvesClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaConsoleShareExport(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<HonuaConsoleShareExportClient>(provider.GetRequiredService<IHonuaConsoleShareExportClient>());
    }

    private static string MinimalDefinitionJson(string exportId)
        => $$"""
            {
              "exportId": "{{exportId}}",
              "serviceName": "parcels",
              "layerId": 3,
              "destinationType": "S3",
              "destinationStatus": "Supported",
              "destinationConfig": {},
              "format": "geojson",
              "schedule": "@daily",
              "scheduleState": "Active",
              "createdAt": "2026-06-01T00:00:00Z",
              "updatedAt": "2026-06-01T00:00:00Z"
            }
            """;

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        => new(new MockHttpHandler(handler))
        {
            BaseAddress = new Uri("https://server.example")
        };

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => CreateHttpClient(request => Task.FromResult(handler(request)));

    private static Task<HttpResponseMessage> JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
        => Task.FromResult(CreateJsonResponse(json, statusCode));

    private static Task<HttpResponseMessage> CreateJsonResponseTask(string json, HttpStatusCode statusCode)
        => Task.FromResult(CreateJsonResponse(json, statusCode));

    private static HttpResponseMessage CreateJsonResponse(string json, HttpStatusCode statusCode)
        => new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
