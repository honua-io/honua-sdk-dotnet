// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Grpc.Core;
using Honua.Sdk.Processes.Models;
using Moq;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Tests;

public sealed class HonuaProcessGrpcClientTests
{
    [Fact]
    public async Task SubmitJobAsync_MapsSharedPlanAndNativeContext()
    {
        Proto.SubmitJobRequest? capturedRequest = null;
        Metadata? capturedMetadata = null;
        var mockClient = new Mock<Proto.ProcessService.ProcessServiceClient>();
        mockClient
            .Setup(c => c.SubmitJobAsync(
                It.IsAny<Proto.SubmitJobRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.SubmitJobRequest, Metadata, DateTime?, CancellationToken>((request, metadata, _, _) =>
            {
                capturedRequest = request;
                capturedMetadata = metadata;
            })
            .Returns(CreateAsyncUnaryCall(new Proto.SubmitJobResponse
            {
                JobId = "job-1",
                State = Proto.JobState.Running
            }));
        var metadataOverride = new Metadata { { "x-api-key", "test-key" } };
        var client = new HonuaProcessGrpcClient(mockClient.Object, metadataOverride);

        var status = await client.SubmitJobAsync(CreatePlan(), new HonuaProcessExecutionContext
        {
            WorkspaceId = "workspace-1",
            Timeout = TimeSpan.FromSeconds(45),
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["operator"] = "console"
            }
        });

        Assert.Equal("job-1", status.JobId);
        Assert.Equal("running", status.Status);
        Assert.Equal("plan-1", capturedRequest?.Plan.PlanId);
        Assert.Equal(Proto.WorkflowFamily.Analyze, capturedRequest?.Plan.WorkflowFamily);
        Assert.Equal("featureLayer", capturedRequest?.Plan.ExpectedOutputs.Single());
        Assert.Equal("geoprocess", capturedRequest?.Plan.Steps.Single().Kind);
        Assert.Equal("geometry.buffer", capturedRequest?.Plan.Steps.Single().Inputs["processId"].StringValue);
        Assert.Equal("AAAA", capturedRequest?.Plan.Steps.Single().Inputs["wkb"].StringValue);
        Assert.Equal("4326", capturedRequest?.Plan.Steps.Single().Inputs["srid"].StringValue);
        Assert.Equal("25", capturedRequest?.Plan.Steps.Single().Inputs["distance"].StringValue);
        Assert.Equal("workspace-1", capturedRequest?.Context.Workspace.WorkspaceId);
        Assert.Equal(45, capturedRequest?.Context.TimeoutSeconds);
        Assert.Equal("console", capturedRequest?.Context.Metadata["operator"]);
        Assert.Equal("test-key", capturedMetadata?.GetValue("x-api-key"));
    }

    [Fact]
    public async Task ExecutePlanStreamAsync_MapsProgressAndResultEvents()
    {
        var mockClient = new Mock<Proto.ProcessService.ProcessServiceClient>();
        mockClient
            .Setup(c => c.ExecutePlanStream(
                It.IsAny<Proto.ExecutePlanRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncServerStreamingCall(
            [
                new Proto.ExecutionEvent
                {
                    Progress = new Proto.JobProgress
                    {
                        JobId = "job-1",
                        State = Proto.JobState.Running,
                        ProgressPercent = 50,
                        CurrentNodeId = "buffer",
                        Message = "Buffering features"
                    }
                },
                new Proto.ExecutionEvent
                {
                    Result = new Proto.ExecutionResult
                    {
                        ResultId = "result-1",
                        Status = Proto.JobState.Completed,
                        Summary = "Analysis complete."
                    }
                }
            ]));
        var client = new HonuaProcessGrpcClient(mockClient.Object, new Metadata());

        var events = new List<HonuaProcessExecutionEvent>();
        await foreach (var evt in client.ExecutePlanStreamAsync(CreatePlan()))
        {
            events.Add(evt);
        }

        Assert.Collection(
            events,
            first =>
            {
                Assert.Equal("progress", first.EventType);
                Assert.Equal("job-1", first.Progress?.JobId);
                Assert.Equal("running", first.Progress?.State);
                Assert.Equal(50, first.Progress?.ProgressPercent);
                Assert.Equal("buffer", first.Progress?.CurrentNodeId);
            },
            second =>
            {
                Assert.Equal("result", second.EventType);
                Assert.Equal("result-1", second.Result?.ResultId);
                Assert.Equal("successful", second.Result?.Status);
                Assert.Equal("Analysis complete.", second.Result?.Summary);
            });
    }

    [Fact]
    public async Task GetJobResultAsync_MapsTerminalErrorOutcome()
    {
        var mockClient = new Mock<Proto.ProcessService.ProcessServiceClient>();
        mockClient
            .Setup(c => c.GetJobResultAsync(
                It.IsAny<Proto.GetJobResultRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(new Proto.GetJobResultResponse
            {
                JobId = "job-1",
                Error = new Proto.ErrorDetail
                {
                    Code = 403,
                    Message = "Client certificate was rejected.",
                    Phase = "admission",
                    SuggestedAction = "Select a certificate for this environment."
                }
            }));
        var client = new HonuaProcessGrpcClient(mockClient.Object, new Metadata());

        var outcome = await client.GetJobResultAsync("job-1");

        Assert.Equal("job-1", outcome.JobId);
        Assert.Null(outcome.Result);
        Assert.Equal("403", outcome.Error?.ErrorCode);
        Assert.Equal("Client certificate was rejected.", outcome.Error?.Message);
        Assert.Equal("admission", outcome.Error?.Phase);
    }

    [Fact]
    public async Task ValidatePlanAsync_MapsValidationIssues()
    {
        var mockClient = new Mock<Proto.ProcessService.ProcessServiceClient>();
        var response = new Proto.ValidateResponse { Valid = false };
        response.Issues.Add(new Proto.PlanValidationIssue
        {
            NodeId = "buffer",
            Field = "inputs.distance",
            Message = "Distance is required.",
            Severity = Proto.Severity.Error
        });
        mockClient
            .Setup(c => c.ValidatePlanAsync(
                It.IsAny<Proto.ValidatePlanRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(response));
        var client = new HonuaProcessGrpcClient(mockClient.Object, new Metadata());

        var result = await client.ValidatePlanAsync(CreatePlan());

        Assert.False(result.Valid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("buffer", issue.NodeId);
        Assert.Equal("inputs.distance", issue.Field);
        Assert.Equal("Distance is required.", issue.Message);
        Assert.Equal("Error", issue.Severity);
    }

    [Theory]
    [InlineData(Proto.JobState.Unspecified, "accepted")]
    [InlineData(Proto.JobState.Draft, "accepted")]
    [InlineData(Proto.JobState.AwaitingClarification, "accepted")]
    [InlineData(Proto.JobState.Validated, "accepted")]
    [InlineData(Proto.JobState.AwaitingApproval, "accepted")]
    [InlineData(Proto.JobState.Running, "running")]
    [InlineData(Proto.JobState.Completed, "successful")]
    [InlineData(Proto.JobState.Failed, "failed")]
    [InlineData(Proto.JobState.Cancelled, "dismissed")]
    public async Task GetJobAsync_NormalizesJobStateToOgcStatus(Proto.JobState protoState, string expectedOgcStatus)
    {
        var mockClient = new Mock<Proto.ProcessService.ProcessServiceClient>();
        mockClient
            .Setup(c => c.GetJobAsync(
                It.IsAny<Proto.GetJobRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(new Proto.GetJobResponse
            {
                JobId = "job-1",
                State = protoState,
                Progress = new Proto.JobProgress { ProgressPercent = 25 }
            }));
        var client = new HonuaProcessGrpcClient(mockClient.Object, new Metadata());

        var status = await client.GetJobAsync("job-1");

        Assert.Equal(expectedOgcStatus, status.Status);
    }

    [Fact]
    public async Task CancelJobAsync_EmitsDismissedOgcStatus()
    {
        var mockClient = new Mock<Proto.ProcessService.ProcessServiceClient>();
        mockClient
            .Setup(c => c.CancelJobAsync(
                It.IsAny<Proto.CancelJobRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(new Proto.CancelJobResponse
            {
                JobId = "job-1",
                State = Proto.JobState.Cancelled
            }));
        var client = new HonuaProcessGrpcClient(mockClient.Object, new Metadata());

        var status = await client.CancelJobAsync("job-1");

        Assert.Equal("dismissed", status.Status);
        Assert.Equal("process", status.Type);
    }

    private static HonuaAnalysisPlan CreatePlan()
        => new()
        {
            PlanId = "plan-1",
            SpecVersion = "spec/v1",
            WorkflowFamily = "analyze",
            Outputs = ["featureLayer"],
            Steps =
            [
                new HonuaPlanStep
                {
                    StepId = "buffer",
                    Kind = "geoprocess",
                    ProcessId = "geometry.buffer",
                    Inputs = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["wkb"] = "AAAA",
                        ["srid"] = "4326",
                        ["distance"] = "25"
                    }
                }
            ]
        };

    private static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response)
        => new(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    private static AsyncServerStreamingCall<T> CreateAsyncServerStreamingCall<T>(IEnumerable<T> responses)
        => new(
            new TestAsyncStreamReader<T>(responses),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });

    private sealed class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        private readonly IEnumerator<T> _enumerator;

        public TestAsyncStreamReader(IEnumerable<T> items)
        {
            _enumerator = items.GetEnumerator();
        }

        public T Current => _enumerator.Current;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
            => Task.FromResult(_enumerator.MoveNext());
    }
}
