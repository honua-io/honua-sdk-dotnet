// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Processes.Authoring;

/// <summary>
/// Fluent builder for authoring a Honua geoprocessing <see cref="HonuaProcessDefinition"/>
/// in C#. Create one with <see cref="HonuaProcessAuthoring.DefineProcess(string)"/>, declare
/// inputs and outputs, then call <see cref="Build"/>.
/// </summary>
public sealed class HonuaProcessBuilder
{
    private readonly string _processId;
    private readonly List<HonuaProcessParameter> _parameters = [];
    private readonly List<HonuaProcessOutput> _outputs = [];
    private string _title;
    private string _description = string.Empty;
    private string _version = "1.0.0";
    private string? _category;
    private string _runtimeProfile = "managed";
    private List<string>? _jobControlOptions;
    private List<string>? _outputTransmission;

    internal HonuaProcessBuilder(string processId)
    {
        if (string.IsNullOrWhiteSpace(processId))
        {
            throw new ArgumentException("Process id must be supplied.", nameof(processId));
        }

        _processId = processId;
        _title = processId;
    }

    /// <summary>
    /// Sets the operator-facing title.
    /// </summary>
    /// <param name="title">Process title.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessBuilder WithTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _title = title;
        return this;
    }

    /// <summary>
    /// Sets the process description.
    /// </summary>
    /// <param name="description">Process description.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessBuilder WithDescription(string description)
    {
        _description = description ?? string.Empty;
        return this;
    }

    /// <summary>
    /// Sets the process version.
    /// </summary>
    /// <param name="version">Semantic version string.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessBuilder WithVersion(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        _version = version;
        return this;
    }

    /// <summary>
    /// Sets the process category.
    /// </summary>
    /// <param name="category">Category, for example <c>geometry</c>.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessBuilder WithCategory(string category)
    {
        _category = category;
        return this;
    }

    /// <summary>
    /// Sets the runtime profile the process executes under.
    /// </summary>
    /// <param name="runtimeProfile">Runtime profile, for example <c>managed</c> or <c>native</c>.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessBuilder WithRuntimeProfile(string runtimeProfile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeProfile);
        _runtimeProfile = runtimeProfile;
        return this;
    }

    /// <summary>
    /// Overrides the advertised job control options.
    /// </summary>
    /// <param name="jobControlOptions">Job control option identifiers.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessBuilder WithJobControlOptions(params string[] jobControlOptions)
    {
        ArgumentNullException.ThrowIfNull(jobControlOptions);
        _jobControlOptions = [.. jobControlOptions];
        return this;
    }

    /// <summary>
    /// Overrides the advertised output transmission options.
    /// </summary>
    /// <param name="outputTransmission">Output transmission identifiers.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessBuilder WithOutputTransmission(params string[] outputTransmission)
    {
        ArgumentNullException.ThrowIfNull(outputTransmission);
        _outputTransmission = [.. outputTransmission];
        return this;
    }

    /// <summary>
    /// Declares an input parameter.
    /// </summary>
    /// <param name="name">Parameter name (the key used in step inputs).</param>
    /// <param name="valueType">Parameter value type.</param>
    /// <param name="configure">Optional configuration for the parameter.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessBuilder AddInput(
        string name,
        HonuaProcessParameterValueType valueType,
        Action<HonuaProcessParameterBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_parameters.Exists(p => string.Equals(p.Name, name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Input '{name}' is already declared.");
        }

        var parameterBuilder = new HonuaProcessParameterBuilder(name, valueType);
        configure?.Invoke(parameterBuilder);
        _parameters.Add(parameterBuilder.Build());
        return this;
    }

    /// <summary>
    /// Declares an output artifact.
    /// </summary>
    /// <param name="name">Output name (the result document key).</param>
    /// <param name="artifactKind">Artifact kind the output produces.</param>
    /// <param name="configure">Optional configuration for the output.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessBuilder AddOutput(
        string name,
        HonuaProcessArtifactKind artifactKind,
        Action<HonuaProcessOutputBuilder>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_outputs.Exists(o => string.Equals(o.Name, name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Output '{name}' is already declared.");
        }

        var outputBuilder = new HonuaProcessOutputBuilder(name, artifactKind);
        configure?.Invoke(outputBuilder);
        _outputs.Add(outputBuilder.Build());
        return this;
    }

    /// <summary>
    /// Builds the immutable process definition.
    /// </summary>
    /// <returns>The authored process definition.</returns>
    public HonuaProcessDefinition Build() => new()
    {
        ProcessId = _processId,
        Title = _title,
        Description = _description,
        Version = _version,
        Category = _category,
        RuntimeProfile = _runtimeProfile,
        JobControlOptions = _jobControlOptions ?? ["async-execute"],
        OutputTransmission = _outputTransmission ?? ["value"],
        Parameters = [.. _parameters],
        Outputs = [.. _outputs]
    };
}

/// <summary>
/// Fluent builder for a single process input parameter.
/// </summary>
public sealed class HonuaProcessParameterBuilder
{
    private readonly string _name;
    private readonly HonuaProcessParameterValueType _valueType;
    private string? _displayName;
    private string _description = string.Empty;
    private bool _required;
    private string? _defaultValue;
    private List<string>? _allowedValues;

    internal HonuaProcessParameterBuilder(string name, HonuaProcessParameterValueType valueType)
    {
        _name = name;
        _valueType = valueType;
    }

    /// <summary>Sets the operator-facing display name.</summary>
    /// <param name="displayName">Display name.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessParameterBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    /// <summary>Sets the parameter description.</summary>
    /// <param name="description">Description.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessParameterBuilder WithDescription(string description)
    {
        _description = description ?? string.Empty;
        return this;
    }

    /// <summary>Marks the parameter as required.</summary>
    /// <param name="required">Whether the parameter is required.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessParameterBuilder Required(bool required = true)
    {
        _required = required;
        return this;
    }

    /// <summary>Sets a default value.</summary>
    /// <param name="defaultValue">Default value rendered as a string.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessParameterBuilder WithDefault(string defaultValue)
    {
        _defaultValue = defaultValue;
        return this;
    }

    /// <summary>Restricts the parameter to an enumeration of allowed values.</summary>
    /// <param name="allowedValues">Allowed values.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessParameterBuilder WithAllowedValues(params string[] allowedValues)
    {
        ArgumentNullException.ThrowIfNull(allowedValues);
        _allowedValues = [.. allowedValues];
        return this;
    }

    internal HonuaProcessParameter Build() => new()
    {
        Name = _name,
        ValueType = _valueType,
        DisplayName = _displayName,
        Description = _description,
        Required = _required,
        DefaultValue = _defaultValue,
        AllowedValues = _allowedValues is null ? null : [.. _allowedValues]
    };
}

/// <summary>
/// Fluent builder for a single process output.
/// </summary>
public sealed class HonuaProcessOutputBuilder
{
    private readonly string _name;
    private readonly HonuaProcessArtifactKind _artifactKind;
    private string? _displayName;
    private string _description = string.Empty;

    internal HonuaProcessOutputBuilder(string name, HonuaProcessArtifactKind artifactKind)
    {
        _name = name;
        _artifactKind = artifactKind;
    }

    /// <summary>Sets the operator-facing display name.</summary>
    /// <param name="displayName">Display name.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessOutputBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    /// <summary>Sets the output description.</summary>
    /// <param name="description">Description.</param>
    /// <returns>The same builder.</returns>
    public HonuaProcessOutputBuilder WithDescription(string description)
    {
        _description = description ?? string.Empty;
        return this;
    }

    internal HonuaProcessOutput Build() => new()
    {
        Name = _name,
        ArtifactKind = _artifactKind,
        DisplayName = _displayName,
        Description = _description
    };
}
