// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Processes.Authoring;

/// <summary>
/// A process definition authored in C#. Produced by <see cref="HonuaProcessBuilder"/> and
/// projected to the server's OGC API Processes description format via
/// <see cref="ToOgcDescription"/>.
/// </summary>
public sealed record HonuaProcessDefinition
{
    /// <summary>
    /// Stable process identifier, for example <c>geometry.buffer</c>.
    /// </summary>
    public required string ProcessId { get; init; }

    /// <summary>
    /// Operator-facing title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Process description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Process version. Defaults to <c>1.0.0</c>.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Process category, for example <c>geometry</c>.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Runtime profile the process executes under, for example <c>managed</c> or <c>native</c>.
    /// </summary>
    public string RuntimeProfile { get; init; } = "managed";

    /// <summary>
    /// Supported job control options, defaulting to <c>async-execute</c>.
    /// </summary>
    public IReadOnlyList<string> JobControlOptions { get; init; } = ["async-execute"];

    /// <summary>
    /// Supported output transmission options, defaulting to <c>value</c>.
    /// </summary>
    public IReadOnlyList<string> OutputTransmission { get; init; } = ["value"];

    /// <summary>
    /// Declared input parameters in declaration order.
    /// </summary>
    public IReadOnlyList<HonuaProcessParameter> Parameters { get; init; } = [];

    /// <summary>
    /// Declared output artifacts in declaration order.
    /// </summary>
    public IReadOnlyList<HonuaProcessOutput> Outputs { get; init; } = [];

    /// <summary>
    /// Projects this authored definition to the server's OGC API Processes description
    /// shape (the body served at <c>GET /ogc/processes/processes/{id}</c>).
    /// </summary>
    /// <returns>An OGC-shaped process description.</returns>
    public HonuaProcessDescription ToOgcDescription()
    {
        var inputs = new Dictionary<string, HonuaProcessInputDescription>(StringComparer.Ordinal);
        foreach (var parameter in Parameters)
        {
            inputs[parameter.Name] = new HonuaProcessInputDescription
            {
                Title = parameter.DisplayName ?? parameter.Name,
                Description = string.IsNullOrEmpty(parameter.Description) ? null : parameter.Description,
                Schema = parameter.ToSchema()
            };
        }

        var outputs = new Dictionary<string, HonuaProcessOutputDescription>(StringComparer.Ordinal);
        foreach (var output in Outputs)
        {
            outputs[output.Name] = new HonuaProcessOutputDescription
            {
                Title = output.DisplayName ?? output.Name,
                Description = string.IsNullOrEmpty(output.Description) ? null : output.Description,
                Schema = output.ToSchema()
            };
        }

        return new HonuaProcessDescription
        {
            Id = ProcessId,
            Title = Title,
            Description = string.IsNullOrEmpty(Description) ? null : Description,
            Version = Version,
            JobControlOptions = [.. JobControlOptions],
            OutputTransmission = [.. OutputTransmission],
            Inputs = inputs,
            Outputs = outputs
        };
    }
}

/// <summary>
/// A single declared process input parameter.
/// </summary>
public sealed record HonuaProcessParameter
{
    /// <summary>
    /// Parameter name. This is the key callers supply in step inputs.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Parameter value type, which drives the generated JSON Schema.
    /// </summary>
    public required HonuaProcessParameterValueType ValueType { get; init; }

    /// <summary>
    /// Operator-facing display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Parameter description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Whether the parameter is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Optional default value rendered as a string.
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Optional enumeration of allowed string values.
    /// </summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }

    /// <summary>
    /// Projects this parameter to an OGC/JSON Schema fragment.
    /// </summary>
    /// <returns>The schema fragment for this parameter.</returns>
    public HonuaProcessIoSchema ToSchema() => HonuaProcessSchemaFactory.ForValueType(ValueType);
}

/// <summary>
/// A single declared process output.
/// </summary>
public sealed record HonuaProcessOutput
{
    /// <summary>
    /// Output name, used as the output identifier in result documents.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Artifact kind this output produces, which drives the generated JSON Schema.
    /// </summary>
    public required HonuaProcessArtifactKind ArtifactKind { get; init; }

    /// <summary>
    /// Operator-facing display name.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Output description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Projects this output to an OGC/JSON Schema fragment.
    /// </summary>
    /// <returns>The schema fragment for this output.</returns>
    public HonuaProcessIoSchema ToSchema() => HonuaProcessSchemaFactory.ForArtifactKind(ArtifactKind);
}
