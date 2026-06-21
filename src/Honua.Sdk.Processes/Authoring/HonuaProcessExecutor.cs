// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Processes.Authoring;

/// <summary>
/// Authoring-side contract for a Honua geoprocessing process. Mirrors the server's
/// <c>IJobExecutor</c> contract so logic written and tested against this SDK ports
/// directly into a server-hosted executor. Implementations receive flattened string
/// inputs (matching <c>AnalysisPlanStep.Inputs</c>) and report progress, logs, and
/// artifacts through the supplied context.
/// </summary>
public interface IHonuaProcessExecutor
{
    /// <summary>
    /// The process id this executor handles, for example <c>geometry.buffer</c>.
    /// </summary>
    string ProcessId { get; }

    /// <summary>
    /// Executes the process against the supplied job inputs.
    /// </summary>
    /// <param name="job">The job inputs and metadata.</param>
    /// <param name="context">Execution context for progress, logging, and artifacts.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The terminal execution result.</returns>
    Task<HonuaProcessJobResult> ExecuteAsync(
        HonuaProcessJobInput job,
        IHonuaProcessExecutionContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Job inputs handed to an <see cref="IHonuaProcessExecutor"/>.
/// </summary>
public sealed record HonuaProcessJobInput
{
    /// <summary>
    /// Job identifier.
    /// </summary>
    public required string JobId { get; init; }

    /// <summary>
    /// Process id being executed.
    /// </summary>
    public required string ProcessId { get; init; }

    /// <summary>
    /// Flattened string parameters, matching the server's job parameter contract.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets a required parameter, throwing when it is missing.
    /// </summary>
    /// <param name="name">Parameter name.</param>
    /// <returns>The parameter value.</returns>
    public string GetRequired(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!Parameters.TryGetValue(name, out var value))
        {
            throw new KeyNotFoundException($"Required parameter '{name}' was not supplied.");
        }

        return value;
    }

    /// <summary>
    /// Gets an optional parameter, or <paramref name="fallback"/> when missing.
    /// </summary>
    /// <param name="name">Parameter name.</param>
    /// <param name="fallback">Fallback value.</param>
    /// <returns>The parameter value or the fallback.</returns>
    public string? GetOptional(string name, string? fallback = null)
        => Parameters.TryGetValue(name, out var value) ? value : fallback;
}

/// <summary>
/// Terminal status of a process execution.
/// </summary>
public enum HonuaProcessJobState
{
    /// <summary>Execution completed successfully.</summary>
    Succeeded,

    /// <summary>Execution failed.</summary>
    Failed,

    /// <summary>Execution was cancelled.</summary>
    Cancelled
}

/// <summary>
/// Terminal result of an <see cref="IHonuaProcessExecutor"/> execution.
/// </summary>
public sealed record HonuaProcessJobResult
{
    /// <summary>
    /// Terminal job state.
    /// </summary>
    public required HonuaProcessJobState State { get; init; }

    /// <summary>
    /// Optional error message for failed jobs.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Warnings emitted during execution.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Output values keyed by output identifier, mirroring document-mode results.
    /// </summary>
    public IReadOnlyDictionary<string, string> Outputs { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="outputs">Optional output values.</param>
    /// <returns>A successful result.</returns>
    public static HonuaProcessJobResult Success(IReadOnlyDictionary<string, string>? outputs = null) => new()
    {
        State = HonuaProcessJobState.Succeeded,
        Outputs = outputs ?? new Dictionary<string, string>(StringComparer.Ordinal)
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorMessage">Failure message.</param>
    /// <returns>A failed result.</returns>
    public static HonuaProcessJobResult Failure(string errorMessage) => new()
    {
        State = HonuaProcessJobState.Failed,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Execution context surfaced to an <see cref="IHonuaProcessExecutor"/>.
/// </summary>
public interface IHonuaProcessExecutionContext
{
    /// <summary>
    /// The job identifier currently executing.
    /// </summary>
    string JobId { get; }

    /// <summary>
    /// Reports execution progress.
    /// </summary>
    /// <param name="percentComplete">Optional completion percentage (0-100).</param>
    /// <param name="phase">Optional phase description.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the progress is recorded.</returns>
    Task ReportProgressAsync(double? percentComplete, string? phase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a log entry.
    /// </summary>
    /// <param name="message">Log message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the message is recorded.</returns>
    Task AppendLogAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a produced artifact reference.
    /// </summary>
    /// <param name="artifactReference">Artifact reference (URI, layer id, or data URI).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the artifact is recorded.</returns>
    Task PublishArtifactAsync(string artifactReference, CancellationToken cancellationToken = default);
}
