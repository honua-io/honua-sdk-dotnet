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
