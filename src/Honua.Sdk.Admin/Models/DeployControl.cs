// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Result of a deploy preflight check.
/// </summary>
public sealed class DeployPreflightResult
{
    /// <summary>
    /// Whether the system is ready for deployment.
    /// </summary>
    [JsonPropertyName("ready")]
    public bool Ready { get; init; }

    /// <summary>
    /// Individual preflight checks that were performed.
    /// </summary>
    [JsonPropertyName("checks")]
    public IReadOnlyList<PreflightCheck> Checks { get; init; } = [];

    /// <summary>
    /// Non-blocking warnings about the deployment environment.
    /// </summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// An individual preflight check result.
/// </summary>
public sealed class PreflightCheck
{
    /// <summary>
    /// Name of the check.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether the check passed.
    /// </summary>
    [JsonPropertyName("passed")]
    public bool Passed { get; init; }

    /// <summary>
    /// Optional message describing the check result.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>
/// A planned deployment describing the operations to be performed.
/// </summary>
public sealed class DeployPlan
{
    /// <summary>
    /// Unique identifier for the plan.
    /// </summary>
    [JsonPropertyName("planId")]
    public string PlanId { get; init; } = string.Empty;

    /// <summary>
    /// When the plan was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Ordered list of operations in the plan.
    /// </summary>
    [JsonPropertyName("operations")]
    public IReadOnlyList<PlannedOperation> Operations { get; init; } = [];

    /// <summary>
    /// Estimated duration for the deployment.
    /// </summary>
    [JsonPropertyName("estimatedDuration")]
    public string? EstimatedDuration { get; init; }
}

/// <summary>
/// A single operation within a deploy plan.
/// </summary>
public sealed class PlannedOperation
{
    /// <summary>
    /// Type of the operation.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Target resource for the operation.
    /// </summary>
    [JsonPropertyName("target")]
    public string Target { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable description of the operation.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Request model for creating a deploy plan.
/// </summary>
public sealed class CreateDeployPlanRequest
{
    /// <summary>
    /// Target version to deploy to.
    /// </summary>
    [JsonPropertyName("targetVersion")]
    public string? TargetVersion { get; init; }

    /// <summary>
    /// Whether to include data migrations in the plan.
    /// </summary>
    [JsonPropertyName("includeDataMigrations")]
    public bool IncludeDataMigrations { get; init; } = true;
}

/// <summary>
/// A deploy operation representing an in-progress or completed deployment.
/// </summary>
public sealed class DeployOperation
{
    /// <summary>
    /// Unique identifier for the operation.
    /// </summary>
    [JsonPropertyName("operationId")]
    public string OperationId { get; init; } = string.Empty;

    /// <summary>
    /// Identifier of the plan this operation executes.
    /// </summary>
    [JsonPropertyName("planId")]
    public string PlanId { get; init; } = string.Empty;

    /// <summary>
    /// Current status of the operation.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// When the operation was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the operation started executing.
    /// </summary>
    [JsonPropertyName("startedAt")]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// When the operation completed.
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// Request model for creating a deploy operation from a plan.
/// </summary>
public sealed class CreateDeployOperationRequest
{
    /// <summary>
    /// Identifier of the plan to execute.
    /// </summary>
    [JsonPropertyName("planId")]
    public string PlanId { get; init; } = string.Empty;
}
