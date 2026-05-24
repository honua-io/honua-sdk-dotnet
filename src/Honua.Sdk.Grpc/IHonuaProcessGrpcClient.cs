// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Grpc;

/// <summary>
/// Native gRPC ProcessService client for job lifecycle access.
/// </summary>
public interface IHonuaProcessGrpcClient
{
    /// <summary>
    /// Validates a process plan.
    /// </summary>
    Task<HonuaProcessPlanValidationResult> ValidatePlanAsync(HonuaAnalysisPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a dry run for a process plan.
    /// </summary>
    Task<HonuaProcessDryRunResult> DryRunPlanAsync(HonuaAnalysisPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a plan synchronously.
    /// </summary>
    Task<HonuaProcessExecutionOutcome> ExecutePlanAsync(HonuaAnalysisPlan plan, HonuaProcessExecutionContext? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a plan and streams progress, stage, result, or error events.
    /// </summary>
    IAsyncEnumerable<HonuaProcessExecutionEvent> ExecutePlanStreamAsync(HonuaAnalysisPlan plan, HonuaProcessExecutionContext? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a plan as an async job.
    /// </summary>
    Task<HonuaProcessJobStatus> SubmitJobAsync(HonuaAnalysisPlan plan, HonuaProcessExecutionContext? context = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current job state and progress.
    /// </summary>
    Task<HonuaProcessJobStatus> GetJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a completed job result or terminal error.
    /// </summary>
    Task<HonuaProcessExecutionOutcome> GetJobResultAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests cancellation for a pending or running job.
    /// </summary>
    Task<HonuaProcessJobStatus> CancelJobAsync(string jobId, CancellationToken cancellationToken = default);
}
