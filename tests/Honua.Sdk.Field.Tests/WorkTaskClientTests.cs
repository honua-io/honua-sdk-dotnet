// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Field.Tasks;

namespace Honua.Sdk.Field.Tests;

public sealed class WorkTaskClientTests
{
    private static readonly Guid TaskId = Guid.Parse("a738f361-0dcf-4e2f-8eac-b38a2d3a5d5a");
    private static readonly Guid CommandId = Guid.Parse("fa8a8af6-6a68-4469-b9e6-1fd046bd9e06");

    private static string TaskJson => $$"""
        {"taskId":"{{TaskId:D}}","serviceId":"inspection","title":"Inspect","type":"inspection",
         "status":"in_progress","version":3,"profileId":"3b5e740a-4ebc-476d-961a-c1af073301e9","assignee":"worker+1","dueAt":"2026-11-26T05:00:00-10:00",
         "form":{"formId":"inspection","version":2},"permittedActions":["complete"]}
        """;

    private static readonly Guid ProfileId = Guid.Parse("3b5e740a-4ebc-476d-961a-c1af073301e9");

    private static WorkTaskAssetRequest AssetRequest => new()
    {
        CommandId = CommandId,
        TaskId = TaskId,
        ServiceId = "inspection",
        ProfileId = ProfileId,
        AssetId = "hydrant-1",
        Title = "Check valve",
    };

    [Fact]
    public async Task CreateAssetRequestAsync_ReplaysSameBodyAndReadsDuplicateReceipt()
    {
        var bodies = new List<string>();
        using var http = CreateHttp(_ => Response(
            AssetReceipt(bodies.Count == 1 ? "accepted" : "duplicate", "hydrant-1"), HttpStatusCode.Created),
            async request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/api/v1/tasks", request.RequestUri!.AbsolutePath);
                bodies.Add(await request.Content!.ReadAsStringAsync());
            });

        var client = new WorkTaskClient(http);
        var first = await client.CreateAssetRequestAsync(AssetRequest);
        var replay = await client.CreateAssetRequestAsync(AssetRequest);

        Assert.Equal("accepted", first.Outcome);
        Assert.Equal("duplicate", replay.Outcome);
        Assert.Equal(first.Task, replay.Task);
        Assert.Equal(2, bodies.Count);
        Assert.Equal(bodies[0], bodies[1]);
        using var json = JsonDocument.Parse(bodies[0]);
        var body = json.RootElement;
        Assert.Equal(7, body.EnumerateObject().Count());
        Assert.Equal(TaskId, body.GetProperty("taskId").GetGuid());
        Assert.Equal(CommandId, body.GetProperty("commandId").GetGuid());
        Assert.Equal(ProfileId, body.GetProperty("profileId").GetGuid());
        Assert.Equal("inspection", body.GetProperty("serviceId").GetString());
        Assert.Equal("hydrant-1", body.GetProperty("assetId").GetString());
        Assert.Equal("Check valve", body.GetProperty("title").GetString());
        Assert.Equal("request", body.GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("007")]
    [InlineData("3b5e740a-4ebc-476d-961a-c1af073301e9")]
    public async Task CreateAssetRequestAsync_AcceptsExactAssetId(string assetId)
    {
        using var http = CreateHttp(_ => Response(AssetReceipt("accepted", assetId), HttpStatusCode.Created));
        var receipt = await new WorkTaskClient(http).CreateAssetRequestAsync(AssetRequest with { AssetId = assetId });
        Assert.Equal(assetId, receipt.Task.AssetId);
    }

    [Theory]
    [InlineData("hydrant-1", "another-asset")]
    [InlineData("007", "7")]
    public async Task CreateAssetRequestAsync_RejectsReceiptForAnotherAsset(string requestedId, string returnedId)
    {
        using var http = CreateHttp(_ => Response(AssetReceipt("accepted", returnedId), HttpStatusCode.Created));
        var error = await Assert.ThrowsAsync<WorkTaskApiException>(
            () => new WorkTaskClient(http).CreateAssetRequestAsync(AssetRequest with { AssetId = requestedId }));
        Assert.Equal(201, error.HttpStatus);
    }

    [Fact]
    public async Task CreateAssetRequestAsync_RequiresStableIdsBeforeSending()
    {
        using var http = CreateHttp(_ => throw new InvalidOperationException("Must validate before sending."));
        var client = new WorkTaskClient(http);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.CreateAssetRequestAsync(AssetRequest with { CommandId = Guid.Empty }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.CreateAssetRequestAsync(AssetRequest with { TaskId = Guid.Empty }));
    }

    private static string AssetReceipt(string outcome, string assetId) => $$"""
        {"commandId":"{{CommandId:D}}","outcome":"{{outcome}}","task":{
           "taskId":"{{TaskId:D}}","serviceId":"inspection","title":"Check valve","type":"request",
           "status":"unassigned","version":1,"profileId":"{{ProfileId:D}}","assetId":"{{assetId}}"}
        }
        """;

    [Fact]
    public async Task ListAssignedAsync_ReadsExactScopeAndCursor()
    {
        using var http = CreateHttp(request =>
        {
            Assert.Equal("/api/v1/tasks?serviceId=inspection&assignee=worker%2B1&cursor=next%2F%2B&limit=12", request.RequestUri!.PathAndQuery);
            return Response($$"""{"items":[{{TaskJson}}],"nextCursor":"opaque"}""");
        });

        var page = await new WorkTaskClient(http).ListAssignedAsync(
            new AssignedWorkTaskQuery { ServiceId = "inspection", Assignee = "worker+1", Cursor = "next/+", Limit = 12 });

        var task = Assert.Single(page.Items);
        Assert.Equal("opaque", page.NextCursor);
        Assert.Equal(2, task.Form!.Version);
        Assert.Equal(Guid.Parse("3b5e740a-4ebc-476d-961a-c1af073301e9"), task.ProfileId);
        Assert.Equal(TimeSpan.FromHours(-10), task.DueAt!.Value.Offset);
    }

    [Fact]
    public async Task ListAssignedAsync_RejectsUnscopedResponseAndEmptyViewer()
    {
        using var http = CreateHttp(_ => Response($$"""{"items":[{{TaskJson}}]}"""));
        var client = new WorkTaskClient(http);

        var error = await Assert.ThrowsAsync<WorkTaskApiException>(() => client.ListAssignedAsync(
            new AssignedWorkTaskQuery { ServiceId = "inspection", Assignee = "other" }));
        Assert.Equal(200, error.HttpStatus);
        Assert.IsType<JsonException>(error.InnerException);
        await Assert.ThrowsAsync<ArgumentException>(() => client.ListAssignedAsync(
            new AssignedWorkTaskQuery { ServiceId = "inspection", Assignee = " " }));
    }

    [Fact]
    public async Task CompleteAsync_ReplaysWithSameCommandAndReadsDuplicateReceipt()
    {
        using var http = CreateHttp(request => Response(
            $$"""{"commandId":"{{CommandId:D}}","outcome":"duplicate","task":{{TaskJson}}}"""),
            async request =>
            {
                Assert.Equal($"/api/v1/tasks/{TaskId:D}/completion", request.RequestUri!.AbsolutePath);
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal(CommandId, body.RootElement.GetProperty("commandId").GetGuid());
                Assert.Equal(2, body.RootElement.GetProperty("expectedVersion").GetInt64());
                Assert.Equal("good", body.RootElement.GetProperty("answers").GetProperty("condition").GetString());
            });

        var client = new WorkTaskClient(http);
        var command = new WorkTaskCompletionRequest
        {
            CommandId = CommandId,
            ExpectedVersion = 2,
            Answers = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["condition"] = "good" }),
        };

        Assert.Equal("duplicate", (await client.CompleteAsync(TaskId, command)).Outcome);
        Assert.Equal(CommandId, (await client.CompleteAsync(TaskId, command)).CommandId);
    }

    [Fact]
    public async Task TransitionAsync_ReadsVersionConflict()
    {
        using var http = CreateHttp(request =>
        {
            Assert.Equal($"/api/v1/tasks/{TaskId:D}/transitions", request.RequestUri!.AbsolutePath);
            var response = Response(
                $$"""{"commandId":"{{CommandId:D}}","outcome":"conflict","reason":"Version changed.","currentVersion":3}""",
                HttpStatusCode.Conflict);
            response.Headers.Add("Honua-Command-Outcome", "conflict");
            return response;
        });

        var ex = await Assert.ThrowsAsync<WorkTaskVersionConflictException>(() => new WorkTaskClient(http).TransitionAsync(
            TaskId, new WorkTaskTransitionRequest { CommandId = CommandId, ExpectedVersion = 2, Action = "start" }));
        Assert.Equal(CommandId, ex.CommandId);
        Assert.Equal(3, ex.CurrentVersion);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task TransitionAsync_PreservesOrdinaryHttpFailure(HttpStatusCode status)
    {
        using var http = CreateHttp(_ => Response("{}", status));
        var error = await Assert.ThrowsAsync<WorkTaskApiException>(() => new WorkTaskClient(http).TransitionAsync(
            TaskId, new WorkTaskTransitionRequest { CommandId = CommandId, ExpectedVersion = 2, Action = "start" }));

        Assert.Equal((int)status, error.HttpStatus);
        Assert.Equal(status, Assert.IsType<HttpRequestException>(error.InnerException).StatusCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "null")]
    [InlineData(HttpStatusCode.OK, "{")]
    [InlineData(HttpStatusCode.Conflict, "{}")]
    public async Task TransitionAsync_WrapsInvalidResponseWithoutReportingConflict(HttpStatusCode status, string body)
    {
        using var http = CreateHttp(_ =>
        {
            var response = Response(body, status);
            response.Headers.Add("Honua-Command-Outcome", "conflict");
            return response;
        });
        var error = await Assert.ThrowsAsync<WorkTaskApiException>(() => new WorkTaskClient(http).TransitionAsync(
            TaskId, new WorkTaskTransitionRequest { CommandId = CommandId, ExpectedVersion = 2, Action = "start" }));

        Assert.Equal((int)status, error.HttpStatus);
        Assert.IsType<JsonException>(error.InnerException);
    }

    [Fact]
    public async Task ListAssignedAsync_PreservesTransportFailureAndCancellation()
    {
        var transportError = new HttpRequestException("Request interrupted.");
        var query = new AssignedWorkTaskQuery { ServiceId = "inspection", Assignee = "worker+1" };
        using var unavailable = CreateHttp(_ => throw transportError);
        var error = await Assert.ThrowsAsync<WorkTaskApiException>(() => new WorkTaskClient(unavailable).ListAssignedAsync(query));
        Assert.Null(error.HttpStatus);
        Assert.Same(transportError, error.InnerException);

        using var cancelled = new CancellationTokenSource();
        using var http = CreateHttp(_ =>
        {
            cancelled.Cancel();
            cancelled.Token.ThrowIfCancellationRequested();
            return Response("{}");
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new WorkTaskClient(http).ListAssignedAsync(query, cancelled.Token));
    }

    private static HttpResponseMessage Response(string json, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpClient CreateHttp(Func<HttpRequestMessage, HttpResponseMessage> send, Func<HttpRequestMessage, Task>? verify = null)
        => new(new Handler(send, verify)) { BaseAddress = new Uri("https://test.invalid/") };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send, Func<HttpRequestMessage, Task>? verify) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (verify is not null)
            {
                await verify(request);
            }

            return send(request);
        }
    }
}
