// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Processes.Authoring;

/// <summary>
/// In-process test harness for authoring and unit-testing an <see cref="IHonuaProcessExecutor"/>
/// without a running Honua Server. The harness validates supplied inputs against the process
/// definition (when one is provided), drives the executor with a capturing context, and returns
/// the result alongside captured progress, logs, and artifacts.
/// </summary>
public sealed class HonuaProcessTestHarness
{
    private readonly IHonuaProcessExecutor _executor;
    private readonly HonuaProcessDefinition? _definition;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaProcessTestHarness"/> class.
    /// </summary>
    /// <param name="executor">The executor under test.</param>
    /// <param name="definition">
    /// Optional process definition. When supplied, required-input validation runs before execution.
    /// </param>
    public HonuaProcessTestHarness(IHonuaProcessExecutor executor, HonuaProcessDefinition? definition = null)
    {
        ArgumentNullException.ThrowIfNull(executor);
        if (definition is not null
            && !string.Equals(definition.ProcessId, executor.ProcessId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Definition process id '{definition.ProcessId}' does not match executor process id '{executor.ProcessId}'.",
                nameof(definition));
        }

        _executor = executor;
        _definition = definition;
    }

    /// <summary>
    /// Runs the executor against the supplied parameters.
    /// </summary>
    /// <param name="parameters">Flattened string parameters.</param>
    /// <param name="jobId">Optional job id; a deterministic test id is used when omitted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The captured run, including the terminal result.</returns>
    public async Task<HonuaProcessHarnessRun> RunAsync(
        IReadOnlyDictionary<string, string> parameters,
        string? jobId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var effectiveJobId = string.IsNullOrWhiteSpace(jobId) ? "harness-job" : jobId;
        var missing = FindMissingRequiredInputs(parameters);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Missing required inputs for process '{_executor.ProcessId}': {string.Join(", ", missing)}.");
        }

        var input = new HonuaProcessJobInput
        {
            JobId = effectiveJobId,
            ProcessId = _executor.ProcessId,
            Parameters = new Dictionary<string, string>(parameters, StringComparer.Ordinal)
        };

        var context = new CapturingExecutionContext(effectiveJobId);
        var result = await _executor.ExecuteAsync(input, context, cancellationToken).ConfigureAwait(false);

        return new HonuaProcessHarnessRun
        {
            Result = result,
            Logs = context.Logs,
            Artifacts = context.Artifacts,
            Progress = context.Progress
        };
    }

    /// <summary>
    /// Runs the executor against inputs taken from an authored analysis-plan step.
    /// </summary>
    /// <param name="step">The plan step supplying the inputs.</param>
    /// <param name="jobId">Optional job id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The captured run.</returns>
    public Task<HonuaProcessHarnessRun> RunAsync(
        HonuaPlanStep step,
        string? jobId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(step);
        return RunAsync(step.Inputs, jobId ?? step.StepId, cancellationToken);
    }

    private List<string> FindMissingRequiredInputs(IReadOnlyDictionary<string, string> parameters)
    {
        var missing = new List<string>();
        if (_definition is null)
        {
            return missing;
        }

        foreach (var parameter in _definition.Parameters)
        {
            if (parameter.Required && !parameters.ContainsKey(parameter.Name))
            {
                missing.Add(parameter.Name);
            }
        }

        return missing;
    }

    private sealed class CapturingExecutionContext : IHonuaProcessExecutionContext
    {
        private readonly List<string> _logs = [];
        private readonly List<string> _artifacts = [];
        private readonly List<HonuaProcessHarnessProgress> _progress = [];

        public CapturingExecutionContext(string jobId) => JobId = jobId;

        public string JobId { get; }

        public IReadOnlyList<string> Logs => _logs;

        public IReadOnlyList<string> Artifacts => _artifacts;

        public IReadOnlyList<HonuaProcessHarnessProgress> Progress => _progress;

        public Task ReportProgressAsync(double? percentComplete, string? phase, CancellationToken cancellationToken = default)
        {
            _progress.Add(new HonuaProcessHarnessProgress
            {
                PercentComplete = percentComplete,
                Phase = phase
            });
            return Task.CompletedTask;
        }

        public Task AppendLogAsync(string message, CancellationToken cancellationToken = default)
        {
            _logs.Add(message ?? string.Empty);
            return Task.CompletedTask;
        }

        public Task PublishArtifactAsync(string artifactReference, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(artifactReference);
            _artifacts.Add(artifactReference);
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// The captured outcome of a <see cref="HonuaProcessTestHarness"/> run.
/// </summary>
public sealed record HonuaProcessHarnessRun
{
    /// <summary>
    /// The terminal execution result.
    /// </summary>
    public required HonuaProcessJobResult Result { get; init; }

    /// <summary>
    /// Log messages captured during execution.
    /// </summary>
    public IReadOnlyList<string> Logs { get; init; } = [];

    /// <summary>
    /// Artifact references published during execution.
    /// </summary>
    public IReadOnlyList<string> Artifacts { get; init; } = [];

    /// <summary>
    /// Progress reports captured during execution.
    /// </summary>
    public IReadOnlyList<HonuaProcessHarnessProgress> Progress { get; init; } = [];

    /// <summary>
    /// Whether the run succeeded.
    /// </summary>
    public bool Succeeded => Result.State == HonuaProcessJobState.Succeeded;

    /// <summary>
    /// Renders a single-line summary suitable for test diagnostics.
    /// </summary>
    /// <returns>A diagnostic summary.</returns>
    public override string ToString()
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{Result.State} (logs={Logs.Count}, artifacts={Artifacts.Count}, progress={Progress.Count})");
}

/// <summary>
/// A single captured progress report.
/// </summary>
public sealed record HonuaProcessHarnessProgress
{
    /// <summary>
    /// Reported completion percentage, when supplied.
    /// </summary>
    public double? PercentComplete { get; init; }

    /// <summary>
    /// Reported phase description, when supplied.
    /// </summary>
    public string? Phase { get; init; }
}
