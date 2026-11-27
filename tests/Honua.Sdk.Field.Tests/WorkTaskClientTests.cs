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
         "status":"in_progress","version":3,"assignee":"worker+1","dueAt":"2026-11-26T05:00:00-10:00",
         "form":{"formId":"inspection","version":2},"permittedActions":["complete"]}
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
        Assert.Equal(TimeSpan.FromHours(-10), task.DueAt!.Value.Offset);
    }

    [Fact]
    public async Task ListAssignedAsync_RejectsUnscopedResponseAndEmptyViewer()
    {
        using var http = CreateHttp(_ => Response($$"""{"items":[{{TaskJson}}]}"""));
        var client = new WorkTaskClient(http);

        await Assert.ThrowsAsync<JsonException>(() => client.ListAssignedAsync(
            new AssignedWorkTaskQuery { ServiceId = "inspection", Assignee = "other" }));
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
