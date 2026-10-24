// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Processes;

/// <summary>
/// Browser-safe OGC API Processes REST client.
/// </summary>
public interface IHonuaProcessesClient
{
    /// <summary>
    /// Gets the processes landing page.
    /// </summary>
    Task<HonuaProcessesLandingPage> GetLandingPageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the conformance declaration.
    /// </summary>
    Task<HonuaProcessesConformance> GetConformanceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available processes.
    /// </summary>
    Task<HonuaProcessList> ListProcessesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one process description.
    /// </summary>
    Task<HonuaProcessDescription> GetProcessAsync(string processId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes once, using advertised synchronous support or waiting for an asynchronous job.
    /// The timeout spans discovery, submission, polling delays, and result retrieval.
    /// </summary>
    /// <remarks>
    /// Synchronous requests omit Prefer; asynchronous requests use respond-async.
    /// Timeout and cancellation stop waiting without dismissing the server job.
    /// HonuaProcessesClient implements this helper. The default interface implementation
    /// throws NotSupportedException to preserve compatibility with existing custom clients.
    /// </remarks>
    Task<HonuaProcessExecutionResponse> ExecuteSyncAsync(string processId, HonuaProcessExecuteRequest request,
        TimeSpan timeout, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This client does not implement bounded process execution. Use HonuaProcessesClient or the lifecycle methods.");

    /// <summary>
    /// Executes direct process inputs in document mode within one elapsed timeout.
    /// </summary>
    /// <remarks>
    /// HonuaProcessesClient implements this helper. The default interface implementation
    /// throws NotSupportedException to preserve compatibility with existing custom clients.
    /// </remarks>
    Task<HonuaProcessExecutionResponse> ExecuteSyncAsync(string processId, IReadOnlyDictionary<string, JsonElement> inputs,
        TimeSpan timeout, TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This client does not implement bounded process execution. Use HonuaProcessesClient or the lifecycle methods.");

    /// <summary>
    /// Waits for a successful job within an elapsed timeout, throwing for failed or dismissed jobs.
    /// Timeout and cancellation stop waiting without dismissing the server job.
    /// </summary>
    /// <remarks>
    /// HonuaProcessesClient implements this helper. The default interface implementation
    /// throws NotSupportedException to preserve compatibility with existing custom clients.
    /// </remarks>
    Task<HonuaProcessJobStatus> WaitForTerminalAsync(string jobId, TimeSpan timeout,
        TimeSpan? pollingInterval = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This client does not implement bounded job waiting. Use HonuaProcessesClient or the lifecycle methods.");

    /// <summary>
    /// Submits an async process execution job.
    /// </summary>
    Task<HonuaProcessJobStatus> SubmitJobAsync(string processId, HonuaProcessExecuteRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an async process execution job with direct OGC process inputs.
    /// </summary>
    Task<HonuaProcessJobStatus> SubmitJobAsync(string processId, IReadOnlyDictionary<string, JsonElement> inputs, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists process jobs.
    /// </summary>
    Task<HonuaProcessJobList> ListJobsAsync(int? limit = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets process job status.
    /// </summary>
    Task<HonuaProcessJobStatus> GetJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dismisses or cancels a process job.
    /// </summary>
    Task<HonuaProcessJobStatus> DismissJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets document-mode process job results.
    /// </summary>
    Task<HonuaProcessResults> GetJobResultsAsync(string jobId, CancellationToken cancellationToken = default);
}
