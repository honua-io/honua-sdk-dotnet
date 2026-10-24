// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Processes.Exceptions;
using Honua.Sdk.Processes.Extensions;
using Honua.Sdk.Processes.Models;
using Microsoft.Extensions.DependencyInjection;
using Polly.Timeout;

namespace Honua.Sdk.Processes.Tests;

public sealed class HonuaProcessesExecutionTests
{
    [Fact]
    public async Task ExistingInterfaceImplementer_CanOptOutOfNewHelpers()
    {
        IHonuaProcessesClient legacy = new LifecycleOnlyClient();
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.ExecuteSyncAsync("test", Request(), TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.ExecuteSyncAsync("test", new Dictionary<string, JsonElement>(), TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.WaitForTerminalAsync("job-1", TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"jobControlOptions\":[]}")]
    [InlineData("{\"jobControlOptions\":null}")]
    [InlineData("{\"jobControlOptions\":[\"unavailable\"]}")]
    [InlineData("{\"jobControlOptions\":[null]}")]
    [InlineData("{\"jobControlOptions\":{\"sync-execute\":true}}")]
    public async Task ExecuteSync_UnsupportedOrMalformedModes_FailsBeforePosting(string description)
    {
        var requests = 0;
        using var http = CreateHttp((request, _) =>
        {
            requests++;
            Assert.Equal(HttpMethod.Get, request.Method);
            return Reply(description);
        });
        await Assert.ThrowsAsync<HonuaProcessesException>(() => new HonuaProcessesClient(http)
            .ExecuteSyncAsync("test", Request(), TimeSpan.FromSeconds(5)));
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task ExecuteSync_SyncOnlyDescription_DoesNotRequireAsyncSupport()
    {
        using var http = CreateHttp((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Reply("{\"jobControlOptions\":[\"sync-execute\"]}");
            }

            Assert.False(request.Headers.Contains("Prefer"));
            return Reply("{\"answer\":42}");
        });
        var result = await new HonuaProcessesClient(http).ExecuteSyncAsync("test",
            new Dictionary<string, JsonElement>(), TimeSpan.FromSeconds(5));
        Assert.Equal(42, result.Document!.Outputs["answer"].GetInt32());
    }

    [Theory]
    [InlineData("document")]
    [InlineData("raw")]
    public async Task ExecuteSync_AdvertisedSync_OmitsPreferAndPreservesResults(string mode)
    {
        var posts = 0;
        using var http = CreateHttp((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Reply(Description(true));
            }

            posts++;
            Assert.False(request.Headers.Contains("Prefer"));
            return Reply(mode == "raw" ? "{\"type\":\"Feature\"}" : "{\"output\":{\"value\":42,\"mediaType\":\"application/json\"}}");
        });
        var result = await new HonuaProcessesClient(http).ExecuteSyncAsync("geometry.buffer", Request(mode), TimeSpan.FromSeconds(5));

        Assert.Equal(1, posts);
        Assert.Null(result.Job);
        Assert.Equal("application/json; charset=utf-8", result.MediaType);
        if (mode == "raw")
        {
            Assert.Null(result.Document);
            Assert.Equal("{\"type\":\"Feature\"}", Encoding.UTF8.GetString(result.RawContent.Span));
        }
        else
        {
            Assert.True(result.RawContent.IsEmpty);
            Assert.Equal(42, result.Document!.Outputs["output"].GetProperty("value").GetInt32());
        }
    }

    [Theory]
    [InlineData(false, 201, "document")]
    [InlineData(false, 202, "raw")]
    [InlineData(true, 201, "document")]
    [InlineData(true, 202, "raw")]
    public async Task ExecuteSync_JobResponse_WaitsAndFetchesResultsWithoutResubmitting(bool sync, int status, string mode)
    {
        var requests = new List<string>();
        using var http = CreateHttp(async (request, cancellationToken) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            requests.Add($"{request.Method} {path}");
            if (request.Method == HttpMethod.Post)
            {
                Assert.Equal(!sync, request.Headers.Contains("Prefer"));
                if (!sync)
                {
                    Assert.Equal("respond-async", request.Headers.GetValues("Prefer").Single());
                }

                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                Assert.Equal(mode, body.RootElement.GetProperty("response").GetString());
                return await Reply(Job("accepted"), (HttpStatusCode)status);
            }

            return await Reply(path.EndsWith("/results", StringComparison.Ordinal) ? "{\"output\":42}"
                : path.EndsWith("/jobs/job-1", StringComparison.Ordinal) ? Job("successful") : Description(sync));
        });
        var result = await new HonuaProcessesClient(http).ExecuteSyncAsync("geometry.buffer", Request(mode),
            TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(1));

        Assert.Equal("successful", result.Job!.Status);
        Assert.Equal("job-1", result.Job.JobId);
        Assert.Single(requests, value => value.StartsWith("POST", StringComparison.Ordinal));
        Assert.Equal("GET /ogc/processes/jobs/job-1/results", requests[^1]);
        Assert.Equal(mode == "document", result.Document is not null);
    }

    [Fact]
    public async Task ExecuteSync_RawBinary_PreservesBytesAndFullContentType()
    {
        byte[] bytes = [0, 255, 23, 0, 128];
        using var http = CreateHttp(async (request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return await Reply(Description(true));
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
            response.Content.Headers.TryAddWithoutValidation("Content-Type", "multipart/related; boundary=gp-output");
            return response;
        });
        var result = await new HonuaProcessesClient(http).ExecuteSyncAsync("test", Request("raw"), TimeSpan.FromSeconds(5));
        Assert.Equal(bytes, result.RawContent.ToArray());
        Assert.Equal("multipart/related; boundary=gp-output", result.MediaType);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("dismissed")]
    [InlineData("surprise")]
    public async Task ExecuteSync_NonSuccessfulTerminal_DoesNotFetchResults(string status)
    {
        var requests = 0;
        using var http = CreateHttp((request, _) =>
        {
            requests++;
            Assert.False(request.RequestUri!.AbsolutePath.EndsWith("/results", StringComparison.Ordinal));
            return Reply(request.Method == HttpMethod.Post ? Job(status) : Description(false),
                request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK);
        });
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => new HonuaProcessesClient(http)
            .ExecuteSyncAsync("test", Request(), TimeSpan.FromSeconds(5)));
        Assert.Equal("job-1", exception.JobId);
        Assert.Contains(status, exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, requests);
    }

    [Theory]
    [InlineData("describe", null)]
    [InlineData("post", null)]
    [InlineData("poll", "job-1")]
    [InlineData("results", "job-1")]
    public async Task ExecuteSync_DeadlineCancelsEveryRequestStage(string stage, string? expectedJobId)
    {
        var reached = false;
        using var http = CreateHttp(async (request, cancellationToken) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var current = request.Method == HttpMethod.Post ? "post"
                : path.EndsWith("/results", StringComparison.Ordinal) ? "results"
                : path.Contains("/jobs/", StringComparison.Ordinal) ? "poll" : "describe";
            if (current == stage)
            {
                reached = true;
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return await Reply(current == "describe" ? Description(false) : Job(stage == "results" ? "successful" : "accepted"),
                current == "post" ? HttpStatusCode.Created : HttpStatusCode.OK);
        });
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => new HonuaProcessesClient(http)
            .ExecuteSyncAsync("test", Request(), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(1)));
        Assert.True(reached);
        Assert.Equal(HttpStatusCode.RequestTimeout, exception.StatusCode);
        Assert.Equal(expectedJobId, exception.JobId);
    }

    [Fact]
    public async Task ExecuteSync_DeadlineCancelsStalledResponseBodyAfterHeadersArrive()
    {
        var bodyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var headersReturned = false;
        using var http = CreateHttp((request, _) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return Reply(Description(true));
            }

            headersReturned = true;
            return Task.FromResult(StallingResponse(bodyStarted));
        });
        var execution = new HonuaProcessesClient(http).ExecuteSyncAsync("test", Request(), TimeSpan.FromMilliseconds(500));
        await bodyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => execution);
        Assert.True(headersReturned);
        Assert.Equal(HttpStatusCode.RequestTimeout, exception.StatusCode);
    }

    [Theory]
    [InlineData("document")]
    [InlineData("raw")]
    public async Task ExecuteSync_DeadlineCancelsStalledJobResultsBodyAndRetainsJob(string mode)
    {
        var bodyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = CreateHttp((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/results", StringComparison.Ordinal))
            {
                return Task.FromResult(StallingResponse(bodyStarted));
            }

            return Reply(request.Method == HttpMethod.Post ? Job("successful") : Description(false),
                request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK);
        });
        var execution = new HonuaProcessesClient(http).ExecuteSyncAsync("test", Request(mode), TimeSpan.FromMilliseconds(500));
        await bodyStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => execution);
        Assert.Equal("job-1", exception.JobId);
        Assert.Equal(HttpStatusCode.RequestTimeout, exception.StatusCode);
    }

    [Fact]
    public async Task ExecuteSync_DeadlineIsNotResetAfterDiscovery()
    {
        var posts = 0;
        using var http = CreateHttp(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                posts++;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(350), cancellationToken);
            return await Reply(request.Method == HttpMethod.Post ? Job("successful") : Description(false),
                request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK);
        });
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => new HonuaProcessesClient(http)
            .ExecuteSyncAsync("test", Request(), TimeSpan.FromMilliseconds(550)));
        Assert.Equal(1, posts);
        Assert.Null(exception.JobId);
        Assert.Equal(HttpStatusCode.RequestTimeout, exception.StatusCode);
    }

    [Fact]
    public async Task ExecuteSync_OneDeadlineIncludesDiscoveryAndPollingDelay()
    {
        var postStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var polls = 0;
        using var http = CreateHttp(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                postStarted.SetResult();
                return await Reply(Job("accepted"), HttpStatusCode.Created);
            }

            polls++;
            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
            return await Reply(Description(false));
        });
        var execution = new HonuaProcessesClient(http).ExecuteSyncAsync("test", Request(),
            TimeSpan.FromMilliseconds(350), TimeSpan.FromMinutes(1));
        await postStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => execution);
        Assert.Equal("job-1", exception.JobId);
        Assert.Equal(1, polls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cancellation_PreservesCallerCancellationAndDoesNotDismiss(bool execute)
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var http = CreateHttp(async (request, token) =>
        {
            Assert.NotEqual(HttpMethod.Delete, request.Method);
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return await Reply(Job("accepted"));
        });
        var client = new HonuaProcessesClient(http);
        var operation = execute
            ? (Task)client.ExecuteSyncAsync("test", Request(), TimeSpan.FromSeconds(5), cancellationToken: cancellation.Token)
            : client.WaitForTerminalAsync("job-1", TimeSpan.FromSeconds(5), cancellationToken: cancellation.Token);
        await started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
    }

    [Fact]
    public async Task ExecuteSync_ShorterHttpClientTimeout_IsNotCallerCancellation()
    {
        using var http = CreateHttp(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return await Reply(Description(true));
        });
        http.Timeout = TimeSpan.FromMilliseconds(100);
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => new HonuaProcessesClient(http)
            .ExecuteSyncAsync("test", Request(), TimeSpan.FromSeconds(5)));
        Assert.Equal(HttpStatusCode.RequestTimeout, exception.StatusCode);
        Assert.Contains("HTTP request", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WaitForTerminal_PollsUntilSuccessful()
    {
        var statuses = new Queue<string>(["accepted", "running", "successful"]);
        using var http = CreateHttp((_, _) => Reply(Job(statuses.Dequeue())));
        var terminal = await new HonuaProcessesClient(http).WaitForTerminalAsync("job-1", TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(1));
        Assert.Equal("successful", terminal.Status);
        Assert.Empty(statuses);
    }

    [Fact]
    public async Task WaitForTerminal_TimeoutRetainsJobIdentity()
    {
        using var http = CreateHttp((_, _) => Reply(Job("running")));
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => new HonuaProcessesClient(http)
            .WaitForTerminalAsync("job-1", TimeSpan.FromMilliseconds(100), TimeSpan.FromMinutes(1)));
        Assert.Equal("job-1", exception.JobId);
        Assert.Equal(HttpStatusCode.RequestTimeout, exception.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"jobID\":\"other-job\",\"status\":\"successful\"}")]
    [InlineData("{\"jobID\":\"job-1\"}")]
    [InlineData("{\"jobID\":\"job-1\",\"status\":null}")]
    [InlineData("{\"jobID\":\"job-1\",\"status\":42}")]
    public async Task WaitForTerminal_RejectsMalformedJobStatus(string body)
    {
        using var http = CreateHttp((_, _) => Reply(body));
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => new HonuaProcessesClient(http)
            .WaitForTerminalAsync("job-1", TimeSpan.FromSeconds(5)));
        Assert.Equal("job-1", exception.JobId);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(10, 0)]
    [InlineData(10, -1)]
    public async Task InvalidWaitDurations_FailBeforeHttp(int timeout, int interval)
    {
        using var http = CreateHttp((_, _) => throw new InvalidOperationException("HTTP must not run"));
        var client = new HonuaProcessesClient(http);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.ExecuteSyncAsync("test", Request(),
            TimeSpan.FromMilliseconds(timeout), TimeSpan.FromMilliseconds(interval)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.WaitForTerminalAsync("job-1",
            TimeSpan.FromMilliseconds(timeout), TimeSpan.FromMilliseconds(interval)));
    }

    [Fact]
    public async Task ExecuteSync_ResultsFailurePreservesProblemDetailsAndJobIdentity()
    {
        using var http = CreateHttp((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/results", StringComparison.Ordinal))
            {
                return Reply("{\"type\":\"urn:test:unavailable\",\"title\":\"Missing artifact\",\"detail\":\"Output expired\"}", HttpStatusCode.Gone);
            }

            return Reply(request.Method == HttpMethod.Post ? Job("successful") : Description(false),
                request.Method == HttpMethod.Post ? HttpStatusCode.Created : HttpStatusCode.OK);
        });
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => new HonuaProcessesClient(http)
            .ExecuteSyncAsync("test", Request(), TimeSpan.FromSeconds(5)));
        Assert.Equal("job-1", exception.JobId);
        Assert.Equal(HttpStatusCode.Gone, exception.StatusCode);
        Assert.Equal("Missing artifact", exception.ProblemTitle);
        Assert.Equal("Output expired", exception.ProblemDetail);
        Assert.Equal("urn:test:unavailable", exception.ProblemType);
    }

    [Fact]
    public async Task RegisteredClient_UnrelatedTransportFailureRemainsUnchanged()
    {
        await using var provider = RegisteredProvider((request, _) => request.Method == HttpMethod.Post
            ? throw new HttpRequestException("Connection refused") : Reply(Description(true)));
        var client = provider.GetRequiredService<IHonuaProcessesClient>();
        var exception = await Assert.ThrowsAsync<HonuaTransportException>(() => client.ExecuteSyncAsync("test", Request(), TimeSpan.FromSeconds(10)));
        Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.Contains("Connection refused", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("post", null)]
    [InlineData("execute-poll", "job-1")]
    [InlineData("wait", "job-1")]
    [InlineData("post-body", null)]
    [InlineData("results-body", "job-1")]
    public async Task RegisteredClient_NormalizesRequestAndBodyTimeouts(string stage, string? expectedJobId)
    {
        var posts = 0;
        var bodyStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var provider = RegisteredProvider(async (request, token) =>
        {
            var isPost = request.Method == HttpMethod.Post;
            if (isPost) posts++;
            var path = request.RequestUri!.AbsolutePath;
            if ((stage == "post" && isPost) || ((stage is "execute-poll" or "wait") && path.Contains("/jobs/", StringComparison.Ordinal)))
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }

            if ((stage == "post-body" && isPost) || (stage == "results-body" && path.EndsWith("/results", StringComparison.Ordinal)))
            {
                return StallingResponse(bodyStarted);
            }

            return await Reply(isPost ? Job(stage == "results-body" ? "successful" : "accepted") : Description(stage.StartsWith("post", StringComparison.Ordinal)),
                isPost ? HttpStatusCode.Created : HttpStatusCode.OK);
        });
        var client = provider.GetRequiredService<IHonuaProcessesClient>();
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => stage == "wait"
            ? (Task)client.WaitForTerminalAsync("job-1", TimeSpan.FromSeconds(10))
            : client.ExecuteSyncAsync("test", Request(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1)));

        Assert.Equal(HttpStatusCode.RequestTimeout, exception.StatusCode);
        Assert.Equal(expectedJobId, exception.JobId);
        Assert.Equal(stage == "wait" ? 0 : 1, posts);
        if (stage.EndsWith("body", StringComparison.Ordinal))
        {
            Assert.True(bodyStarted.Task.IsCompletedSuccessfully);
            Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        }
        else
        {
            var transport = Assert.IsType<HonuaTransportException>(exception.InnerException);
            Assert.IsType<TimeoutRejectedException>(transport.InnerException);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegisteredClient_CallerCancellationRemainsCancellation(bool execute)
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var posts = 0;
        await using var provider = RegisteredProvider(async (request, token) =>
        {
            Assert.NotEqual(HttpMethod.Delete, request.Method);
            if (request.Method == HttpMethod.Post)
            {
                posts++;
                return await Reply(Job("accepted"), HttpStatusCode.Created);
            }

            if (!request.RequestUri!.AbsolutePath.Contains("/jobs/", StringComparison.Ordinal))
            {
                return await Reply(Description(false));
            }

            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return await Reply(Job("running"));
        });
        var client = provider.GetRequiredService<IHonuaProcessesClient>();
        var operation = execute
            ? (Task)client.ExecuteSyncAsync("test", Request(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1), cancellation.Token)
            : client.WaitForTerminalAsync("job-1", TimeSpan.FromSeconds(10), cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(execute ? 1 : 0, posts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WrappedCancellation_StillReportsElapsedDeadline(bool execute)
    {
        using var http = CreateHttp(async (request, token) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return await Reply(Job("accepted"), HttpStatusCode.Created);
            }

            if (!request.RequestUri!.AbsolutePath.Contains("/jobs/", StringComparison.Ordinal))
            {
                return await Reply(Description(false));
            }

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException ex)
            {
                throw new HonuaTransportException("Transport wrapped the linked cancellation.", ex);
            }

            return await Reply(Job("running"));
        });
        var client = new HonuaProcessesClient(http);
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => execute
            ? (Task)client.ExecuteSyncAsync("test", Request(), TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(1))
            : client.WaitForTerminalAsync("job-1", TimeSpan.FromMilliseconds(250)));
        Assert.Equal("job-1", exception.JobId);
        Assert.Contains("elapsed timeout", exception.Message, StringComparison.Ordinal);
        Assert.IsType<HonuaTransportException>(exception.InnerException);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegisteredClient_ElapsedDeadlineStillBoundsRequests(bool execute)
    {
        await using var provider = RegisteredProvider(async (request, token) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                return await Reply(Job("accepted"), HttpStatusCode.Created);
            }

            if (!request.RequestUri!.AbsolutePath.Contains("/jobs/", StringComparison.Ordinal))
            {
                return await Reply(Description(false));
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return await Reply(Job("running"));
        });
        var client = provider.GetRequiredService<IHonuaProcessesClient>();
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => execute
            ? (Task)client.ExecuteSyncAsync("test", Request(), TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(1))
            : client.WaitForTerminalAsync("job-1", TimeSpan.FromMilliseconds(500)));
        Assert.Equal(HttpStatusCode.RequestTimeout, exception.StatusCode);
        Assert.Equal("job-1", exception.JobId);
        Assert.Contains("elapsed timeout", exception.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
    }

    [Fact]
    public async Task RegisteredClient_DoesNotRetryTransientExecutionPost()
    {
        var posts = 0;
        var services = new ServiceCollection();
        services.AddHonuaProcesses(options => options.BaseAddress = new Uri("https://server.example"));
        services.AddHttpClient<IHonuaProcessesClient, HonuaProcessesClient>().ConfigurePrimaryHttpMessageHandler(() =>
            new Handler((request, _) =>
            {
                if (request.Method == HttpMethod.Get)
                {
                    return Reply(Description(true));
                }

                posts++;
                return Reply("{\"title\":\"At capacity\",\"detail\":\"Try later\"}", HttpStatusCode.ServiceUnavailable);
            }));
        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHonuaProcessesClient>();
        var exception = await Assert.ThrowsAsync<HonuaProcessesException>(() => client.ExecuteSyncAsync("test", Request(), TimeSpan.FromSeconds(5)));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal("At capacity", exception.ProblemTitle);
        Assert.Equal(1, posts);
    }

    private static HttpClient CreateHttp(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        => new(new Handler(handler)) { BaseAddress = new Uri("https://server.example") };

    private static ServiceProvider RegisteredProvider(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var services = new ServiceCollection();
        services.AddHonuaProcesses(options =>
        {
            options.BaseAddress = new Uri("https://server.example");
            options.Timeout = TimeSpan.FromSeconds(3);
            options.PrimaryHttpMessageHandlerFactory = () => new Handler(handler);
        });
        return services.BuildServiceProvider();
    }

    private static Task<HttpResponseMessage> Reply(string body, HttpStatusCode status = HttpStatusCode.OK)
        => Task.FromResult(JsonResponse(body, status));

    // Ownership transfers to HttpClient; return the resource directly from its factory.
    private static HttpResponseMessage JsonResponse(string body, HttpStatusCode status)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage StallingResponse(TaskCompletionSource started)
        => new(HttpStatusCode.OK) { Content = new StallingContent(started) };

    private static string Description(bool sync) => sync ? "{\"jobControlOptions\":[\"sync-execute\",\"async-execute\"]}"
        : "{\"jobControlOptions\":[\"async-execute\"]}";

    private static string Job(string status) => $$"""{"jobID":"job-1","status":"{{status}}","message":"server detail"}""";

    private static HonuaProcessExecuteRequest Request(string mode = "document")
        => new() { Inputs = new HonuaProcessExecuteInputs(), Response = mode };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }

    private sealed class StallingContent(TaskCompletionSource started) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            await stream.WriteAsync("{"u8.ToArray(), cancellationToken);
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    // Intentionally implements only the interface that shipped before the optional helpers.
    private sealed class LifecycleOnlyClient : IHonuaProcessesClient
    {
        public Task<HonuaProcessesLandingPage> GetLandingPageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HonuaProcessesConformance> GetConformanceAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HonuaProcessList> ListProcessesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HonuaProcessDescription> GetProcessAsync(string processId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HonuaProcessJobStatus> SubmitJobAsync(string processId, HonuaProcessExecuteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HonuaProcessJobStatus> SubmitJobAsync(string processId, IReadOnlyDictionary<string, JsonElement> inputs, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HonuaProcessJobList> ListJobsAsync(int? limit = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HonuaProcessJobStatus> GetJobAsync(string jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HonuaProcessJobStatus> DismissJobAsync(string jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<HonuaProcessResults> GetJobResultsAsync(string jobId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
