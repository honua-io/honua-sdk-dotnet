// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Processes.Authoring;

/// <summary>
/// Fluent builder for authoring a <see cref="HonuaAnalysisPlan"/> — the multi-step DAG the
/// canonical <c>honua-geoprocessing</c> process executes. Create one with
/// <see cref="HonuaProcessAuthoring.DefinePlan(string)"/>, add steps, then call
/// <see cref="Build"/> or <see cref="BuildExecuteRequest"/>.
/// </summary>
public sealed class HonuaAnalysisPlanBuilder
{
    private readonly string _planId;
    private readonly List<HonuaPlanStep> _steps = [];
    private readonly HashSet<string> _stepIds = new(StringComparer.Ordinal);
    private string? _specVersion;
    private string? _workflowFamily;
    private List<string>? _outputs;

    internal HonuaAnalysisPlanBuilder(string planId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(planId);
        _planId = planId;
    }

    /// <summary>Sets the spec/process version recorded on the plan.</summary>
    /// <param name="specVersion">Spec version.</param>
    /// <returns>The same builder.</returns>
    public HonuaAnalysisPlanBuilder WithSpecVersion(string specVersion)
    {
        _specVersion = specVersion;
        return this;
    }

    /// <summary>Sets the workflow family, for example <c>analyze</c>.</summary>
    /// <param name="workflowFamily">Workflow family.</param>
    /// <returns>The same builder.</returns>
    public HonuaAnalysisPlanBuilder WithWorkflowFamily(string workflowFamily)
    {
        _workflowFamily = workflowFamily;
        return this;
    }

    /// <summary>Declares the desired output artifact kinds for the plan.</summary>
    /// <param name="outputs">Output artifact kind identifiers.</param>
    /// <returns>The same builder.</returns>
    public HonuaAnalysisPlanBuilder WithOutputs(params string[] outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);
        _outputs = [.. outputs];
        return this;
    }

    /// <summary>
    /// Adds a geoprocessing step invoking a concrete process.
    /// </summary>
    /// <param name="stepId">Unique step identifier.</param>
    /// <param name="processId">Concrete process id, for example <c>geometry.buffer</c>.</param>
    /// <param name="configure">Optional step configuration.</param>
    /// <returns>The same builder.</returns>
    public HonuaAnalysisPlanBuilder AddGeoprocessStep(
        string stepId,
        string processId,
        Action<HonuaPlanStepBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processId);
        return AddStep(stepId, "geoprocess", step =>
        {
            step.WithProcessId(processId);
            configure?.Invoke(step);
        });
    }

    /// <summary>
    /// Adds a step of an arbitrary kind, for example <c>queryFeatures</c>, <c>aggregate</c>,
    /// <c>renderMap</c>, or <c>export</c>.
    /// </summary>
    /// <param name="stepId">Unique step identifier.</param>
    /// <param name="kind">Step kind.</param>
    /// <param name="configure">Optional step configuration.</param>
    /// <returns>The same builder.</returns>
    public HonuaAnalysisPlanBuilder AddStep(
        string stepId,
        string kind,
        Action<HonuaPlanStepBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        if (!_stepIds.Add(stepId))
        {
            throw new InvalidOperationException($"Step '{stepId}' is already declared.");
        }

        var stepBuilder = new HonuaPlanStepBuilder(stepId, kind);
        configure?.Invoke(stepBuilder);
        _steps.Add(stepBuilder.Build());
        return this;
    }

    /// <summary>
    /// Builds the immutable analysis plan after validating step references.
    /// </summary>
    /// <returns>The authored analysis plan.</returns>
    public HonuaAnalysisPlan Build()
    {
        ValidateDependencies();
        return new HonuaAnalysisPlan
        {
            PlanId = _planId,
            SpecVersion = _specVersion,
            WorkflowFamily = _workflowFamily,
            Steps = [.. _steps],
            Outputs = _outputs is null ? [] : [.. _outputs]
        };
    }

    /// <summary>
    /// Builds an OGC execute request wrapping the authored plan, ready to submit through
    /// <see cref="IHonuaProcessesClient.SubmitJobAsync(string, HonuaProcessExecuteRequest, System.Threading.CancellationToken)"/>
    /// against the canonical <c>honua-geoprocessing</c> process.
    /// </summary>
    /// <returns>The execute request.</returns>
    public HonuaProcessExecuteRequest BuildExecuteRequest() => new()
    {
        Inputs = HonuaProcessExecuteInputs.FromPlan(Build())
    };

    private void ValidateDependencies()
    {
        foreach (var step in _steps)
        {
            foreach (var dependency in step.DependsOn)
            {
                if (!_stepIds.Contains(dependency))
                {
                    throw new InvalidOperationException(
                        $"Step '{step.StepId}' depends on unknown step '{dependency}'.");
                }

                if (string.Equals(dependency, step.StepId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Step '{step.StepId}' cannot depend on itself.");
                }
            }
        }
    }
}

/// <summary>
/// Fluent builder for a single <see cref="HonuaPlanStep"/>.
/// </summary>
public sealed class HonuaPlanStepBuilder
{
    private readonly string _stepId;
    private readonly string _kind;
    private readonly Dictionary<string, string> _inputs = new(StringComparer.Ordinal);
    private readonly List<string> _dependsOn = [];
    private string? _processId;

    internal HonuaPlanStepBuilder(string stepId, string kind)
    {
        _stepId = stepId;
        _kind = kind;
    }

    /// <summary>Sets the concrete process id this step invokes.</summary>
    /// <param name="processId">Process id.</param>
    /// <returns>The same builder.</returns>
    public HonuaPlanStepBuilder WithProcessId(string processId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processId);
        _processId = processId;
        return this;
    }

    /// <summary>Sets a single step input.</summary>
    /// <param name="name">Input name.</param>
    /// <param name="value">Input value.</param>
    /// <returns>The same builder.</returns>
    public HonuaPlanStepBuilder WithInput(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _inputs[name] = value ?? string.Empty;
        return this;
    }

    /// <summary>Sets a single step input from any formattable value using invariant culture.</summary>
    /// <param name="name">Input name.</param>
    /// <param name="value">Input value.</param>
    /// <returns>The same builder.</returns>
    public HonuaPlanStepBuilder WithInput(string name, IFormattable value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _inputs[name] = value.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
        return this;
    }

    /// <summary>Declares a dependency on another step.</summary>
    /// <param name="stepIds">Step ids this step depends on.</param>
    /// <returns>The same builder.</returns>
    public HonuaPlanStepBuilder DependsOn(params string[] stepIds)
    {
        ArgumentNullException.ThrowIfNull(stepIds);
        _dependsOn.AddRange(stepIds);
        return this;
    }

    internal HonuaPlanStep Build() => new()
    {
        StepId = _stepId,
        Kind = _kind,
        ProcessId = _processId,
        Inputs = new Dictionary<string, string>(_inputs, StringComparer.Ordinal),
        DependsOn = [.. _dependsOn]
    };
}
