// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using Honua.Sdk.Field.Tasks;

namespace Honua.Sdk.Field.Tests;

/// <summary>Checks two service-scoped principals against an isolated Server with a disposable assigned task.</summary>
public sealed class WorkTaskLiveTests
{
    [LiveWorkTaskFact]
    public async Task AssignedWorkerCanStartAndReplayWhileOtherCrewIsDenied()
    {
        var baseUri = new Uri(Read("URL").TrimEnd('/') + "/");
        using var worker = CreateClient(baseUri, Read("WORKER_KEY"));
        using var other = CreateClient(baseUri, Read("OTHER_KEY"));
        await VerifyAsync(worker, other, new AssignedWorkTaskQuery
        {
            ServiceId = Read("SERVICE"),
            Assignee = Read("ASSIGNEE"),
        }, Guid.Parse(Read("TASK")));
    }

    public static async Task VerifyAsync(HttpClient worker, HttpClient other, AssignedWorkTaskQuery query, Guid taskId)
    {
        var client = new WorkTaskClient(worker);
        var listing = await client.ListAssignedAsync(query);
        var task = Assert.Single(listing.Items, task => task.TaskId == taskId);
        var command = new WorkTaskTransitionRequest
        {
            CommandId = Guid.NewGuid(),
            ExpectedVersion = task.Version,
            Action = "start",
        };

        var denied = await Assert.ThrowsAsync<HttpRequestException>(
            () => new WorkTaskClient(other).TransitionAsync(taskId, command));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var receipt = await client.TransitionAsync(taskId, command);
        Assert.Equal("accepted", receipt.Outcome);
        Assert.Equal(command.CommandId, receipt.CommandId);
        Assert.Equal(task.Version + 1, receipt.Task.Version);

        var replay = await client.TransitionAsync(taskId, command);
        Assert.Equal("duplicate", replay.Outcome);
        Assert.Equal(receipt.CommandId, replay.CommandId);
        Assert.Equal(receipt.Task.Version, replay.Task.Version);
    }

    private static HttpClient CreateClient(Uri baseUri, string key)
    {
        var client = new HttpClient { BaseAddress = baseUri };
        client.DefaultRequestHeaders.Add("X-API-Key", key);
        return client;
    }

    private static string Read(string suffix)
        => Environment.GetEnvironmentVariable($"HONUA_LIVE_WORK_TASK_{suffix}")
            ?? throw new InvalidOperationException($"Set HONUA_LIVE_WORK_TASK_{suffix} for the isolated Server fixture.");
}

// Enable with HONUA_LIVE_WORK_TASK=1. Seed one assigned task for exact ASSIGNEE and set SERVICE to the canonical ID.
// Supply its TASK UUID plus two keys from distinct principals with write permission on SERVICE.
// The test advances the task to in_progress. Use a fresh task for each run.
internal sealed class LiveWorkTaskFactAttribute : FactAttribute
{
    public LiveWorkTaskFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HONUA_LIVE_WORK_TASK") != "1")
        {
            Skip = "Set HONUA_LIVE_WORK_TASK=1 to run against a disposable Honua Server fixture.";
        }
    }
}
