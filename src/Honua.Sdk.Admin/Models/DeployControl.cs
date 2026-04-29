// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Instance-scoped deploy preflight response.
/// </summary>
public sealed class DeployPreflightResult
{
    /// <summary>
    /// Current deploy preflight status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Whether the instance is ready for coordinated deployment.
    /// </summary>
    [JsonPropertyName("readyForCoordinatedDeploy")]
    public bool ReadyForCoordinatedDeploy { get; init; }

    /// <summary>
    /// Operator-facing summary for the preflight result.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Server version reported when diagnostics are included.
    /// </summary>
    [JsonPropertyName("serverVersion")]
    public string? ServerVersion { get; init; }

    /// <summary>
    /// Host environment reported when diagnostics are included.
    /// </summary>
    [JsonPropertyName("environment")]
    public string? Environment { get; init; }

    /// <summary>
    /// Deployment mode reported when diagnostics are included.
    /// </summary>
    [JsonPropertyName("deploymentMode")]
    public string? DeploymentMode { get; init; }

    /// <summary>
    /// Instance name reported when diagnostics are included.
    /// </summary>
    [JsonPropertyName("instanceName")]
    public string? InstanceName { get; init; }

    /// <summary>
    /// Timestamp when the payload was generated.
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Readiness detail, when diagnostics are included.
    /// </summary>
    [JsonPropertyName("readiness")]
    public DeployPreflightReadiness? Readiness { get; init; }

    /// <summary>
    /// Migration detail, when diagnostics are included.
    /// </summary>
    [JsonPropertyName("migration")]
    public DeployPreflightMigration? Migration { get; init; }

    /// <summary>
    /// Database compatibility detail, when diagnostics are included.
    /// </summary>
    [JsonPropertyName("databaseCompatibility")]
    public DeployPreflightDatabaseCompatibility? DatabaseCompatibility { get; init; }
}

/// <summary>
/// Readiness summary embedded in deploy preflight responses.
/// </summary>
public sealed class DeployPreflightReadiness
{
    /// <summary>
    /// Whether the instance is ready.
    /// </summary>
    [JsonPropertyName("isReady")]
    public bool IsReady { get; init; }

    /// <summary>
    /// HTTP status code returned by the readiness endpoint.
    /// </summary>
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; }

    /// <summary>
    /// Human-readable readiness message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Migration summary embedded in deploy preflight responses.
/// </summary>
public sealed class DeployPreflightMigration
{
    /// <summary>
    /// Migration lifecycle status.
    /// </summary>
    [JsonPropertyName("lifecycleStatus")]
    public string LifecycleStatus { get; init; } = string.Empty;

    /// <summary>
    /// Optional lifecycle detail.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Whether a migration plan could be generated.
    /// </summary>
    [JsonPropertyName("planAvailable")]
    public bool PlanAvailable { get; init; }

    /// <summary>
    /// Whether pending migrations were detected.
    /// </summary>
    [JsonPropertyName("upgradeRequired")]
    public bool UpgradeRequired { get; init; }

    /// <summary>
    /// Pending migration scripts.
    /// </summary>
    [JsonPropertyName("pendingScripts")]
    public IReadOnlyList<string> PendingScripts { get; init; } = [];

    /// <summary>
    /// Executed scripts no longer discovered by the current binary.
    /// </summary>
    [JsonPropertyName("executedButNotDiscoveredScripts")]
    public IReadOnlyList<string> ExecutedButNotDiscoveredScripts { get; init; } = [];

    /// <summary>
    /// Error detail when migration planning could not complete.
    /// </summary>
    [JsonPropertyName("planError")]
    public string? PlanError { get; init; }
}

/// <summary>
/// Database compatibility summary embedded in deploy preflight responses.
/// </summary>
public sealed class DeployPreflightDatabaseCompatibility
{
    /// <summary>
    /// Whether the database meets compatibility requirements.
    /// </summary>
    [JsonPropertyName("isCompatible")]
    public bool IsCompatible { get; init; }

    /// <summary>
    /// Database engine version.
    /// </summary>
    [JsonPropertyName("engineVersion")]
    public string EngineVersion { get; init; } = string.Empty;

    /// <summary>
    /// PostGIS extension version, if installed.
    /// </summary>
    [JsonPropertyName("postGisVersion")]
    public string? PostGisVersion { get; init; }

    /// <summary>
    /// PostGIS raster extension version, if installed.
    /// </summary>
    [JsonPropertyName("postGisRasterVersion")]
    public string? PostGisRasterVersion { get; init; }

    /// <summary>
    /// Installed database extensions.
    /// </summary>
    [JsonPropertyName("installedExtensions")]
    public IReadOnlyList<string> InstalledExtensions { get; init; } = [];

    /// <summary>
    /// Non-fatal compatibility warnings.
    /// </summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Error message when compatibility failed.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Request model for creating a deploy plan.
/// </summary>
public sealed class CreateDeployPlanRequest
{
    /// <summary>
    /// Deploy target identifier.
    /// </summary>
    [JsonPropertyName("targetId")]
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Desired target revision.
    /// </summary>
    [JsonPropertyName("desiredRevision")]
    public string DesiredRevision { get; init; } = string.Empty;

    /// <summary>
    /// Optional current revision.
    /// </summary>
    [JsonPropertyName("currentRevision")]
    public string? CurrentRevision { get; init; }

    /// <summary>
    /// Optional deploy parameters.
    /// </summary>
    [JsonPropertyName("parameters")]
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}

/// <summary>
/// A planned deployment response.
/// </summary>
public sealed class DeployPlan
{
    /// <summary>
    /// Deploy target metadata.
    /// </summary>
    [JsonPropertyName("target")]
    public DeployPlanTarget? Target { get; init; }

    /// <summary>
    /// Whether the plan can be submitted.
    /// </summary>
    [JsonPropertyName("readyToSubmit")]
    public bool ReadyToSubmit { get; init; }

    /// <summary>
    /// Whether the operation requires approval.
    /// </summary>
    [JsonPropertyName("requiresApproval")]
    public bool RequiresApproval { get; init; }

    /// <summary>
    /// Whether out-of-band migrations are required.
    /// </summary>
    [JsonPropertyName("requiresOutOfBandMigrations")]
    public bool RequiresOutOfBandMigrations { get; init; }

    /// <summary>
    /// Whether a backend was registered for the target.
    /// </summary>
    [JsonPropertyName("backendRegistered")]
    public bool BackendRegistered { get; init; }

    /// <summary>
    /// Backend capabilities used during planning.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public DeployBackendCapabilities? Capabilities { get; init; }

    /// <summary>
    /// Non-fatal planning warnings.
    /// </summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Reasons blocking submission.
    /// </summary>
    [JsonPropertyName("blockingReasons")]
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];

    /// <summary>
    /// Timestamp when the plan was generated.
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; init; }
}

/// <summary>
/// Deploy target metadata resolved during planning.
/// </summary>
public sealed class DeployPlanTarget
{
    /// <summary>
    /// Deploy target identifier.
    /// </summary>
    [JsonPropertyName("targetId")]
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Deploy target kind.
    /// </summary>
    [JsonPropertyName("targetKind")]
    public string TargetKind { get; init; } = string.Empty;

    /// <summary>
    /// Backend used for the target.
    /// </summary>
    [JsonPropertyName("backend")]
    public string Backend { get; init; } = string.Empty;

    /// <summary>
    /// Target environment.
    /// </summary>
    [JsonPropertyName("environment")]
    public string Environment { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable target name.
    /// </summary>
    [JsonPropertyName("targetName")]
    public string TargetName { get; init; } = string.Empty;

    /// <summary>
    /// Artifact reference, if any.
    /// </summary>
    [JsonPropertyName("artifactReference")]
    public string? ArtifactReference { get; init; }

    /// <summary>
    /// Runtime profile, if any.
    /// </summary>
    [JsonPropertyName("runtimeProfile")]
    public string? RuntimeProfile { get; init; }

    /// <summary>
    /// Current revision, if known.
    /// </summary>
    [JsonPropertyName("currentRevision")]
    public string? CurrentRevision { get; init; }

    /// <summary>
    /// Desired revision.
    /// </summary>
    [JsonPropertyName("desiredRevision")]
    public string DesiredRevision { get; init; } = string.Empty;

    /// <summary>
    /// Target parameters.
    /// </summary>
    [JsonPropertyName("parameters")]
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Backend capabilities used during deploy planning.
/// </summary>
public sealed class DeployBackendCapabilities
{
    /// <summary>
    /// Whether rollback is supported.
    /// </summary>
    [JsonPropertyName("supportsRollback")]
    public bool SupportsRollback { get; init; }

    /// <summary>
    /// Whether cancellation is supported.
    /// </summary>
    [JsonPropertyName("supportsCancellation")]
    public bool SupportsCancellation { get; init; }

    /// <summary>
    /// Whether traffic shifting is supported.
    /// </summary>
    [JsonPropertyName("supportsTrafficShifting")]
    public bool SupportsTrafficShifting { get; init; }

    /// <summary>
    /// Whether out-of-band migrations are required.
    /// </summary>
    [JsonPropertyName("requiresOutOfBandMigrations")]
    public bool RequiresOutOfBandMigrations { get; init; }

    /// <summary>
    /// Whether progress polling is supported.
    /// </summary>
    [JsonPropertyName("supportsProgressPolling")]
    public bool SupportsProgressPolling { get; init; }

    /// <summary>
    /// Whether revision pinning is supported.
    /// </summary>
    [JsonPropertyName("supportsRevisionPinning")]
    public bool SupportsRevisionPinning { get; init; }
}

/// <summary>
/// Request model for creating a deploy operation.
/// </summary>
public sealed class CreateDeployOperationRequest
{
    /// <summary>
    /// Deploy target identifier.
    /// </summary>
    [JsonPropertyName("targetId")]
    public string TargetId { get; init; } = string.Empty;

    /// <summary>
    /// Desired target revision.
    /// </summary>
    [JsonPropertyName("desiredRevision")]
    public string DesiredRevision { get; init; } = string.Empty;

    /// <summary>
    /// Optional current revision.
    /// </summary>
    [JsonPropertyName("currentRevision")]
    public string? CurrentRevision { get; init; }

    /// <summary>
    /// Optional reason for the operation.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Optional idempotency key.
    /// </summary>
    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; init; }

    /// <summary>
    /// Optional correlation ID.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Optional priority string.
    /// </summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; init; }

    /// <summary>
    /// Whether to submit immediately after creation.
    /// </summary>
    [JsonPropertyName("submitImmediately")]
    public bool? SubmitImmediately { get; init; }

    /// <summary>
    /// Optional deploy parameters.
    /// </summary>
    [JsonPropertyName("parameters")]
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}

/// <summary>
/// Request model for submitting or approving a deploy operation.
/// </summary>
public sealed class SubmitDeployOperationRequest
{
    /// <summary>
    /// Optional operator reason for submitting the operation.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// Request model for rolling back a deploy operation.
/// </summary>
public sealed class RollbackDeployOperationRequest
{
    /// <summary>
    /// Optional operator reason for requesting rollback.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// A deploy operation response.
/// </summary>
public sealed class DeployOperation
{
    /// <summary>
    /// Unique operation identifier.
    /// </summary>
    [JsonPropertyName("operationId")]
    public string OperationId { get; init; } = string.Empty;

    /// <summary>
    /// Operation kind.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Current operation status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Operation priority.
    /// </summary>
    [JsonPropertyName("priority")]
    public string Priority { get; init; } = string.Empty;

    /// <summary>
    /// Deploy target metadata.
    /// </summary>
    [JsonPropertyName("target")]
    public DeployPlanTarget? Target { get; init; }

    /// <summary>
    /// Provider operation identifier, if any.
    /// </summary>
    [JsonPropertyName("providerOperationId")]
    public string? ProviderOperationId { get; init; }

    /// <summary>
    /// Current provider phase, if any.
    /// </summary>
    [JsonPropertyName("currentPhase")]
    public string? CurrentPhase { get; init; }

    /// <summary>
    /// Observed provider state, if any.
    /// </summary>
    [JsonPropertyName("observedState")]
    public string? ObservedState { get; init; }

    /// <summary>
    /// Error message when the operation failed.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Non-fatal operation warnings.
    /// </summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Reasons blocking progress.
    /// </summary>
    [JsonPropertyName("blockingReasons")]
    public IReadOnlyList<string> BlockingReasons { get; init; } = [];

    /// <summary>
    /// User that requested the operation.
    /// </summary>
    [JsonPropertyName("requestedBy")]
    public string? RequestedBy { get; init; }

    /// <summary>
    /// Reason supplied for the operation.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Correlation ID supplied for the operation.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Timestamp when the operation was created.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the operation was last updated.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Timestamp when the operation completed.
    /// </summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; init; }
}
