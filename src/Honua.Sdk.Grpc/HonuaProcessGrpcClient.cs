// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using Grpc.Core;
using Grpc.Net.Client;
using Honua.Sdk.Processes.Models;
using Microsoft.Extensions.Options;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc;

/// <summary>
/// Native gRPC ProcessService client.
/// </summary>
public sealed class HonuaProcessGrpcClient : IHonuaProcessGrpcClient, IDisposable
{
    private const string ProcessServiceName = "geospatial.v1.ProcessService";

    private readonly Proto.ProcessService.ProcessServiceClient _client;
    private readonly GrpcChannel? _ownedChannel;
    private readonly HonuaGrpcClientOptions _options;
    private readonly Metadata? _metadataOverride;

    /// <summary>
    /// Creates a process gRPC client using configured options.
    /// </summary>
    public HonuaProcessGrpcClient(IOptions<HonuaGrpcClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var opts = options.Value;
        var address = HonuaGrpcClientOptions.ParseAndValidateAddress(opts);
        HonuaGrpcClientOptions.ValidateTimeout(opts.Timeout);
        HonuaGrpcClientSupport.ValidateAuthenticationTransport(opts, address);

        var channelOptions = new GrpcChannelOptions
        {
            UnsafeUseInsecureChannelCallCredentials =
                HonuaGrpcClientSupport.HasCredentials(opts) && HonuaGrpcClientOptions.IsLocalDevelopmentHttp(address),
            ServiceConfig = HonuaGrpcClientSupport.BuildServiceConfig(
                opts,
                ProcessServiceName,
                ["ValidatePlan", "DryRunPlan", "GetJob", "GetJobResult"])
        };
        if (opts.PrimaryHttpMessageHandlerFactory is { } primaryHandlerFactory)
        {
            channelOptions.HttpHandler = primaryHandlerFactory();
        }

        _ownedChannel = GrpcChannel.ForAddress(address, channelOptions);
        _client = new Proto.ProcessService.ProcessServiceClient(_ownedChannel);
        _options = opts;
    }

    /// <summary>
    /// Creates a process gRPC client using a preconfigured channel.
    /// </summary>
    public HonuaProcessGrpcClient(GrpcChannel channel, HonuaGrpcClientOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var opts = options ?? new HonuaGrpcClientOptions();
        HonuaGrpcClientOptions.ValidateTimeout(opts.Timeout);
        if (HonuaGrpcClientSupport.HasCredentials(opts))
        {
            HonuaGrpcClientSupport.ValidateAuthenticationTransport(opts, HonuaGrpcClientSupport.ResolveChannelAddress(channel));
        }

        _client = new Proto.ProcessService.ProcessServiceClient(channel);
        _options = opts;
    }

    internal HonuaProcessGrpcClient(Proto.ProcessService.ProcessServiceClient client, Metadata? metadata = null)
    {
        _client = client;
        _options = new HonuaGrpcClientOptions();
        _metadataOverride = metadata ?? new Metadata();
    }

    internal HonuaProcessGrpcClient(Proto.ProcessService.ProcessServiceClient client, HonuaGrpcClientOptions options)
    {
        _client = client;
        HonuaGrpcClientOptions.ValidateTimeout(options.Timeout);
        _options = options;
    }

    /// <inheritdoc />
    public async Task<HonuaProcessPlanValidationResult> ValidatePlanAsync(HonuaAnalysisPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        try
        {
            var metadata = await BuildMetadataAsync("ValidatePlan", cancellationToken).ConfigureAwait(false);
            var response = await _client.ValidatePlanAsync(
                new Proto.ValidatePlanRequest { Plan = ToProtoPlan(plan) },
                metadata,
                deadline: CreateDeadline(),
                cancellationToken: cancellationToken);
            return new HonuaProcessPlanValidationResult
            {
                Valid = response.Valid,
                Issues = response.Issues.Select(ToValidationIssue).ToList()
            };
        }
        catch (RpcException ex)
        {
            throw HonuaGrpcException.FromRpcException(ex);
        }
    }

    /// <inheritdoc />
    public async Task<HonuaProcessDryRunResult> DryRunPlanAsync(HonuaAnalysisPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        try
        {
            var metadata = await BuildMetadataAsync("DryRunPlan", cancellationToken).ConfigureAwait(false);
            var response = await _client.DryRunPlanAsync(
                new Proto.DryRunPlanRequest { Plan = ToProtoPlan(plan) },
                metadata,
                deadline: CreateDeadline(),
                cancellationToken: cancellationToken);
            return new HonuaProcessDryRunResult
            {
                Valid = response.Valid,
                Issues = response.Issues.Select(ToValidationIssue).ToList(),
                Result = response.Result is null ? null : ToDryRunSummary(response.Result)
            };
        }
        catch (RpcException ex)
        {
            throw HonuaGrpcException.FromRpcException(ex);
        }
    }

    /// <inheritdoc />
    public async Task<HonuaProcessExecutionOutcome> ExecutePlanAsync(
        HonuaAnalysisPlan plan,
        HonuaProcessExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        try
        {
            var metadata = await BuildMetadataAsync("ExecutePlan", cancellationToken).ConfigureAwait(false);
            var response = await _client.ExecutePlanAsync(
                new Proto.ExecutePlanRequest
                {
                    Plan = ToProtoPlan(plan),
                    Context = ToProtoContext(context)
                },
                metadata,
                deadline: CreateDeadline(),
                cancellationToken: cancellationToken);
            return ToExecutionOutcome(response.JobId, response.OutcomeCase, response.Result, response.Error);
        }
        catch (RpcException ex)
        {
            throw HonuaGrpcException.FromRpcException(ex);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<HonuaProcessExecutionEvent> ExecutePlanStreamAsync(
        HonuaAnalysisPlan plan,
        HonuaProcessExecutionContext? context = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        AsyncServerStreamingCall<Proto.ExecutionEvent> call;
        try
        {
            var metadata = await BuildMetadataAsync("ExecutePlanStream", cancellationToken).ConfigureAwait(false);
            call = _client.ExecutePlanStream(
                new Proto.ExecutePlanRequest
                {
                    Plan = ToProtoPlan(plan),
                    Context = ToProtoContext(context)
                },
                metadata,
                deadline: CreateDeadline(),
                cancellationToken: cancellationToken);
        }
        catch (RpcException ex)
        {
            throw HonuaGrpcException.FromRpcException(ex);
        }

        using (call)
        {
            while (true)
            {
                Proto.ExecutionEvent current;
                try
                {
                    if (!await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
                    {
                        yield break;
                    }

                    current = call.ResponseStream.Current;
                }
                catch (RpcException ex)
                {
                    throw HonuaGrpcException.FromRpcException(ex);
                }

                yield return ToExecutionEvent(current);
            }
        }
    }

    /// <inheritdoc />
    public async Task<HonuaProcessJobStatus> SubmitJobAsync(
        HonuaAnalysisPlan plan,
        HonuaProcessExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        try
        {
            var metadata = await BuildMetadataAsync("SubmitJob", cancellationToken).ConfigureAwait(false);
            var response = await _client.SubmitJobAsync(
                new Proto.SubmitJobRequest
                {
                    Plan = ToProtoPlan(plan),
                    Context = ToProtoContext(context)
                },
                metadata,
                deadline: CreateDeadline(),
                cancellationToken: cancellationToken);
            return new HonuaProcessJobStatus
            {
                JobId = response.JobId,
                Status = MapJobStateToOgcStatus(response.State),
                Type = "process"
            };
        }
        catch (RpcException ex)
        {
            throw HonuaGrpcException.FromRpcException(ex);
        }
    }

    /// <inheritdoc />
    public async Task<HonuaProcessJobStatus> GetJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        EnsureJobId(jobId);

        try
        {
            var metadata = await BuildMetadataAsync("GetJob", cancellationToken).ConfigureAwait(false);
            var response = await _client.GetJobAsync(
                new Proto.GetJobRequest { JobId = jobId },
                metadata,
                deadline: CreateDeadline(),
                cancellationToken: cancellationToken);
            return ToJobStatus(response.JobId, response.State, response.Progress);
        }
        catch (RpcException ex)
        {
            throw HonuaGrpcException.FromRpcException(ex);
        }
    }

    /// <inheritdoc />
    public async Task<HonuaProcessExecutionOutcome> GetJobResultAsync(string jobId, CancellationToken cancellationToken = default)
    {
        EnsureJobId(jobId);

        try
        {
            var metadata = await BuildMetadataAsync("GetJobResult", cancellationToken).ConfigureAwait(false);
            var response = await _client.GetJobResultAsync(
                new Proto.GetJobResultRequest { JobId = jobId },
                metadata,
                deadline: CreateDeadline(),
                cancellationToken: cancellationToken);
            return ToExecutionOutcome(response.JobId, response.OutcomeCase, response.Result, response.Error);
        }
        catch (RpcException ex)
        {
            throw HonuaGrpcException.FromRpcException(ex);
        }
    }

    /// <inheritdoc />
    public async Task<HonuaProcessJobStatus> CancelJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        EnsureJobId(jobId);

        try
        {
            var metadata = await BuildMetadataAsync("CancelJob", cancellationToken).ConfigureAwait(false);
            var response = await _client.CancelJobAsync(
                new Proto.CancelJobRequest { JobId = jobId },
                metadata,
                deadline: CreateDeadline(),
                cancellationToken: cancellationToken);
            return new HonuaProcessJobStatus
            {
                JobId = response.JobId,
                Status = MapJobStateToOgcStatus(response.State),
                Type = "process"
            };
        }
        catch (RpcException ex)
        {
            throw HonuaGrpcException.FromRpcException(ex);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ownedChannel?.Dispose();
    }

    private Task<Metadata> BuildMetadataAsync(string methodName, CancellationToken cancellationToken)
        => HonuaGrpcClientSupport.BuildMetadataAsync(
            _options,
            "process-grpc",
            methodName,
            _metadataOverride,
            cancellationToken);

    private DateTime CreateDeadline()
        => HonuaGrpcClientSupport.CreateDeadline(_options);

    private static Proto.ExecutionPlan ToProtoPlan(HonuaAnalysisPlan plan)
    {
        var proto = new Proto.ExecutionPlan
        {
            PlanId = plan.PlanId,
            SpecVersion = plan.SpecVersion ?? string.Empty,
            WorkflowFamily = ToWorkflowFamily(plan.WorkflowFamily)
        };
        proto.ExpectedOutputs.AddRange(plan.Outputs);
        proto.Steps.AddRange(plan.Steps.Select(ToProtoStep));
        return proto;
    }

    private static Proto.PlanStep ToProtoStep(HonuaPlanStep step)
    {
        var proto = new Proto.PlanStep
        {
            StepId = step.StepId ?? string.Empty,
            Kind = step.Kind
        };
        foreach (var (key, value) in step.Inputs)
        {
            proto.Inputs[key] = new Proto.ParameterValue { StringValue = value };
        }
        if (!string.IsNullOrWhiteSpace(step.ProcessId))
        {
            proto.Inputs["processId"] = new Proto.ParameterValue { StringValue = step.ProcessId };
        }

        proto.Dependencies.AddRange(step.DependsOn);
        return proto;
    }

    private static Proto.ExecutionContext ToProtoContext(HonuaProcessExecutionContext? context)
    {
        var proto = new Proto.ExecutionContext();
        if (context is null)
        {
            return proto;
        }

        if (!string.IsNullOrWhiteSpace(context.WorkspaceId))
        {
            proto.Workspace = new Proto.WorkspaceRef { WorkspaceId = context.WorkspaceId };
        }
        if (context.Timeout.HasValue)
        {
            proto.TimeoutSeconds = (long)Math.Ceiling(context.Timeout.Value.TotalSeconds);
        }

        foreach (var (key, value) in context.Metadata)
        {
            proto.Metadata[key] = value;
        }

        return proto;
    }

    private static Proto.WorkflowFamily ToWorkflowFamily(string? workflowFamily)
    {
        if (string.Equals(workflowFamily, "analyze", StringComparison.OrdinalIgnoreCase))
        {
            return Proto.WorkflowFamily.Analyze;
        }

        if (string.Equals(workflowFamily, "publish", StringComparison.OrdinalIgnoreCase))
        {
            return Proto.WorkflowFamily.Publish;
        }

        if (string.Equals(workflowFamily, "build", StringComparison.OrdinalIgnoreCase))
        {
            return Proto.WorkflowFamily.Build;
        }

        if (string.Equals(workflowFamily, "deploy", StringComparison.OrdinalIgnoreCase))
        {
            return Proto.WorkflowFamily.Deploy;
        }

        return Proto.WorkflowFamily.Unspecified;
    }

    private static HonuaProcessValidationIssue ToValidationIssue(Proto.PlanValidationIssue issue)
        => new()
        {
            NodeId = issue.NodeId,
            Field = issue.Field,
            Message = issue.Message,
            Severity = issue.Severity.ToString()
        };

    private static HonuaProcessDryRunSummary ToDryRunSummary(Proto.DryRunResult result)
        => new()
        {
            EstimatedDuration = result.EstimatedDurationSeconds > 0
                ? TimeSpan.FromSeconds(result.EstimatedDurationSeconds)
                : null,
            EstimatedArtifacts = result.EstimatedArtifacts.Select(artifact => new HonuaProcessEstimatedArtifact
            {
                ArtifactClass = artifact.ArtifactClass.ToString(),
                EstimatedSizeBytes = artifact.EstimatedSizeBytes,
                Description = artifact.Description
            }).ToList(),
            SideEffects = result.SideEffects.Select(effect => new HonuaProcessSideEffect
            {
                EffectType = effect.EffectType,
                Target = effect.Target,
                Description = effect.Description
            }).ToList(),
            CostEstimate = result.CostEstimate is null
                ? null
                : new HonuaProcessCostEstimate
                {
                    Units = result.CostEstimate.Units,
                    Amount = result.CostEstimate.Amount
                }
        };

    private static HonuaProcessJobStatus ToJobStatus(string jobId, Proto.JobState state, Proto.JobProgress? progress)
        => new()
        {
            JobId = jobId,
            Status = MapJobStateToOgcStatus(state),
            Type = "process",
            Progress = progress?.ProgressPercent,
            Message = progress?.Message,
            Created = progress is null ? null : FromUnixMilliseconds(progress.StartedAt),
            Updated = progress is null ? null : FromUnixMilliseconds(progress.UpdatedAt)
        };

    // Normalizes the proto JobState enum to the OGC API Processes job status values
    // ("accepted", "running", "successful", "failed", "dismissed") so the shared
    // HonuaProcessJobStatus / HonuaProcessJobProgress / HonuaProcessExecutionResult
    // surface stays consistent across the REST and gRPC adapters. Mirrors the
    // honua-server ExecutionJobStatus → OGC mapping in
    // Honua.Server.Features.Protocols.Ogc.Api.Processes.OgcProcessesConversionHelpers.
    private static string MapJobStateToOgcStatus(Proto.JobState state) => state switch
    {
        Proto.JobState.Running => "running",
        Proto.JobState.Completed => "successful",
        Proto.JobState.Failed => "failed",
        Proto.JobState.Cancelled => "dismissed",
        _ => "accepted"
    };

    private static HonuaProcessExecutionOutcome ToExecutionOutcome(
        string jobId,
        Proto.ExecutePlanResponse.OutcomeOneofCase outcomeCase,
        Proto.ExecutionResult result,
        Proto.ErrorDetail error)
        => outcomeCase switch
        {
            Proto.ExecutePlanResponse.OutcomeOneofCase.Result => new HonuaProcessExecutionOutcome
            {
                JobId = jobId,
                Result = ToExecutionResult(result)
            },
            Proto.ExecutePlanResponse.OutcomeOneofCase.Error => new HonuaProcessExecutionOutcome
            {
                JobId = jobId,
                Error = ToError(error)
            },
            _ => new HonuaProcessExecutionOutcome { JobId = jobId }
        };

    private static HonuaProcessExecutionOutcome ToExecutionOutcome(
        string jobId,
        Proto.GetJobResultResponse.OutcomeOneofCase outcomeCase,
        Proto.ExecutionResult result,
        Proto.ErrorDetail error)
        => outcomeCase switch
        {
            Proto.GetJobResultResponse.OutcomeOneofCase.Result => new HonuaProcessExecutionOutcome
            {
                JobId = jobId,
                Result = ToExecutionResult(result)
            },
            Proto.GetJobResultResponse.OutcomeOneofCase.Error => new HonuaProcessExecutionOutcome
            {
                JobId = jobId,
                Error = ToError(error)
            },
            _ => new HonuaProcessExecutionOutcome { JobId = jobId }
        };

    private static HonuaProcessExecutionEvent ToExecutionEvent(Proto.ExecutionEvent evt)
        => evt.EventCase switch
        {
            Proto.ExecutionEvent.EventOneofCase.Progress => new HonuaProcessExecutionEvent
            {
                EventType = "progress",
                Progress = ToProgress(evt.Progress)
            },
            Proto.ExecutionEvent.EventOneofCase.StageResult => new HonuaProcessExecutionEvent
            {
                EventType = "stageResult",
                StageResult = ToStageResult(evt.StageResult)
            },
            Proto.ExecutionEvent.EventOneofCase.Result => new HonuaProcessExecutionEvent
            {
                EventType = "result",
                Result = ToExecutionResult(evt.Result)
            },
            Proto.ExecutionEvent.EventOneofCase.Error => new HonuaProcessExecutionEvent
            {
                EventType = "error",
                Error = ToError(evt.Error)
            },
            _ => new HonuaProcessExecutionEvent { EventType = "unspecified" }
        };

    private static HonuaProcessJobProgress ToProgress(Proto.JobProgress progress)
        => new()
        {
            JobId = progress.JobId,
            State = MapJobStateToOgcStatus(progress.State),
            ProgressPercent = progress.ProgressPercent,
            CurrentNodeId = progress.CurrentNodeId,
            StartedAt = FromUnixMilliseconds(progress.StartedAt),
            UpdatedAt = FromUnixMilliseconds(progress.UpdatedAt),
            Message = progress.Message
        };

    private static HonuaProcessExecutionResult ToExecutionResult(Proto.ExecutionResult result)
        => new()
        {
            ResultId = result.ResultId,
            Status = MapJobStateToOgcStatus(result.Status),
            Summary = result.Summary,
            Assumptions = result.Assumptions.Select(assumption => new HonuaProcessAssumption
            {
                AssumptionId = assumption.AssumptionId,
                Description = assumption.Description,
                Rationale = assumption.Rationale,
                UserConfirmed = assumption.UserConfirmed
            }).ToList(),
            Artifacts = result.Artifacts.Select(ToArtifactRef).ToList(),
            StageResults = result.StageResults.Select(ToStageResult).ToList()
        };

    private static HonuaProcessStageResult ToStageResult(Proto.StageResult stage)
        => new()
        {
            NodeId = stage.NodeId,
            State = stage.State.ToString(),
            Error = stage.Error is null ? null : ToError(stage.Error),
            PartialArtifacts = stage.PartialArtifacts.Select(ToArtifactRef).ToList()
        };

    private static HonuaProcessArtifactRef ToArtifactRef(Proto.ArtifactRef artifact)
        => new()
        {
            ArtifactId = artifact.ArtifactId,
            ArtifactClass = artifact.ArtifactClass.ToString(),
            ArtifactVersion = artifact.ArtifactVersion,
            ProducerRef = artifact.ProducerRef
        };

    private static HonuaProcessError ToError(Proto.ErrorDetail error)
        => new()
        {
            ErrorCode = error.Code.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Category = error.Category.ToString(),
            Message = error.Message,
            Phase = error.Phase,
            NodeId = error.NodeId,
            Retryability = error.Retryability.ToString(),
            SuggestedAction = error.SuggestedAction,
            Details = error.Details.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
        };

    private static DateTimeOffset? FromUnixMilliseconds(long value)
        => value > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(value) : null;

    private static void EnsureJobId(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            throw new ArgumentException("Job ID must be supplied.", nameof(jobId));
        }
    }
}
