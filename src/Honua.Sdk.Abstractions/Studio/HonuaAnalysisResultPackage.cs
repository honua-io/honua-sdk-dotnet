// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Studio;

/// <summary>
/// Deserialization-only projection of the server <c>AnalysisResultPackage</c> —
/// the final envelope of artifacts, workspace references, provenance, and
/// errors produced by a geoprocessing workflow.
/// </summary>
/// <remarks>
/// <para>
/// There is intentionally no retrieval client for this type. The server does
/// not expose result packages over HTTP today (they are referenced by id from
/// reports and surfaced over MCP); this projection lets Console deserialize the
/// shape where it is embedded without re-declaring the DTO. A retrieval client
/// is deferred to a server-gated child ticket.
/// </para>
/// <para>
/// Enum members mirror the server's numeric wire encoding (the control-plane
/// JSON context serializes these enums as integers, not strings), including the
/// explicit <see cref="HonuaGeoprocessingWorkflowStatus"/> ordinals.
/// </para>
/// </remarks>
public sealed record HonuaAnalysisResultPackage
{
    /// <summary>Unique identifier for this result package.</summary>
    public required string ResultPackageId { get; init; }

    /// <summary>Terminal workflow status of the operation that produced this package.</summary>
    public required HonuaGeoprocessingWorkflowStatus Status { get; init; }

    /// <summary>Human-readable summary of the result.</summary>
    public required HonuaResultSummary Summary { get; init; }

    /// <summary>Assumptions made during workflow compilation and execution.</summary>
    public IReadOnlyList<string> Assumptions { get; init; } = [];

    /// <summary>Output artifacts produced by the workflow.</summary>
    public IReadOnlyList<HonuaArtifactRef> Artifacts { get; init; } = [];

    /// <summary>Workspace references used or created during the workflow.</summary>
    public IReadOnlyList<HonuaWorkspaceRef> WorkspaceRefs { get; init; } = [];

    /// <summary>Reference to a separately materialized map package resource, when applicable.</summary>
    public string? MapPackageId { get; init; }

    /// <summary>Reference to a separately materialized application package resource, when applicable.</summary>
    public string? AppPackageId { get; init; }

    /// <summary>Audit trail for this result.</summary>
    public required HonuaProvenanceRecord Provenance { get; init; }

    /// <summary>Errors encountered during the workflow.</summary>
    public IReadOnlyList<HonuaGeoprocessingError> Errors { get; init; } = [];
}

/// <summary>
/// Typed reference to an output artifact produced by a geoprocessing workflow.
/// </summary>
public sealed record HonuaArtifactRef
{
    /// <summary>Unique identifier for this artifact.</summary>
    public required string ArtifactId { get; init; }

    /// <summary>Category of the artifact.</summary>
    public required HonuaArtifactKind Kind { get; init; }

    /// <summary>Human-readable name for the artifact.</summary>
    public required string Label { get; init; }

    /// <summary>Location of the artifact when materialized.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Mirrors the server string contract for an artifact location that must round-trip verbatim without Uri normalization on this read projection.")]
    public string? Uri { get; init; }

    /// <summary>MIME type of the artifact content when applicable.</summary>
    public string? ContentType { get; init; }

    /// <summary>Opaque metadata associated with the artifact.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// Reference to a managed working-state container used during geoprocessing.
/// </summary>
public sealed record HonuaWorkspaceRef
{
    /// <summary>Unique identifier for this workspace.</summary>
    public required string WorkspaceId { get; init; }

    /// <summary>Lifetime class of the workspace.</summary>
    public required HonuaWorkspaceKind Kind { get; init; }

    /// <summary>Human-readable name for the workspace.</summary>
    public required string Label { get; init; }

    /// <summary>Location of the workspace when materialized.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Mirrors the server string contract for a workspace location that must round-trip verbatim without Uri normalization on this read projection.")]
    public string? Uri { get; init; }

    /// <summary>When the workspace expires, for temporary or scratch workspaces.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// Structured error produced during a geoprocessing workflow, optionally tied
/// to a specific plan step.
/// </summary>
public sealed record HonuaGeoprocessingError
{
    /// <summary>Category of the error.</summary>
    public required HonuaGeoprocessingErrorKind Kind { get; init; }

    /// <summary>Human-readable error message.</summary>
    public required string Message { get; init; }

    /// <summary>Identifier of the plan step that failed, when applicable.</summary>
    public string? StepId { get; init; }

    /// <summary>Validation failures contributing to this error.</summary>
    public IReadOnlyList<HonuaGeoprocessingValidationFailure>? Violations { get; init; }
}

/// <summary>
/// A single validation failure within a geoprocessing error.
/// </summary>
public sealed record HonuaGeoprocessingValidationFailure
{
    /// <summary>Machine-readable violation code.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable violation message.</summary>
    public required string Message { get; init; }

    /// <summary>Path to the offending field, when applicable.</summary>
    public string? FieldPath { get; init; }
}

/// <summary>
/// Workflow-level lifecycle status for a geoprocessing operation. Ordinals
/// mirror the server enum exactly (note the non-sequential
/// <see cref="AwaitingExecution"/> = 8).
/// </summary>
public enum HonuaGeoprocessingWorkflowStatus
{
    /// <summary>Intent captured but not yet validated.</summary>
    Draft = 0,

    /// <summary>Workflow is waiting for user clarification before proceeding.</summary>
    AwaitingClarification = 1,

    /// <summary>Intent and plan have been validated.</summary>
    Validated = 2,

    /// <summary>Plan requires explicit user approval before execution.</summary>
    AwaitingApproval = 3,

    /// <summary>Execution is in progress.</summary>
    Running = 4,

    /// <summary>Execution completed successfully.</summary>
    Completed = 5,

    /// <summary>Execution failed.</summary>
    Failed = 6,

    /// <summary>Execution was cancelled by the user or system.</summary>
    Cancelled = 7,

    /// <summary>Plan is validated and submitted but waiting for an execution slot.</summary>
    AwaitingExecution = 8
}

/// <summary>
/// Categories of output artifacts produced by geoprocessing workflows. Ordinals
/// mirror the server enum.
/// </summary>
public enum HonuaArtifactKind
{
    /// <summary>A single scalar value (count, area, distance).</summary>
    Scalar = 0,

    /// <summary>A feature layer with geometry and attributes.</summary>
    FeatureLayer = 1,

    /// <summary>A tabular dataset without geometry.</summary>
    Table = 2,

    /// <summary>A raster dataset.</summary>
    Raster = 3,

    /// <summary>A generic file output.</summary>
    File = 4,

    /// <summary>A formatted report document.</summary>
    Report = 5,

    /// <summary>A composed map.</summary>
    Map = 6,

    /// <summary>A packaged application bundle.</summary>
    AppBundle = 7
}

/// <summary>
/// Lifetime classes for managed workspaces. Ordinals mirror the server enum.
/// </summary>
public enum HonuaWorkspaceKind
{
    /// <summary>Temporary scratch workspace, automatically cleaned up.</summary>
    Scratch = 0,

    /// <summary>Persistent workspace that survives beyond the operation.</summary>
    Persistent = 1,

    /// <summary>Temporary layer workspace for intermediate results.</summary>
    TempLayer = 2,

    /// <summary>Saved layer workspace for published results.</summary>
    SavedLayer = 3,

    /// <summary>Collection of result artifacts.</summary>
    ResultCollection = 4
}

/// <summary>
/// Categories of errors that can occur during geoprocessing. Ordinals mirror
/// the server enum.
/// </summary>
public enum HonuaGeoprocessingErrorKind
{
    /// <summary>Input validation failed.</summary>
    ValidationFailed = 0,

    /// <summary>The user is not authorized to perform the requested operation.</summary>
    AuthorizationDenied = 1,

    /// <summary>A referenced dataset could not be found.</summary>
    UnknownDataset = 2,

    /// <summary>A referenced geoprocessing operation could not be found.</summary>
    UnknownProcess = 3,

    /// <summary>An error occurred during plan execution.</summary>
    ExecutionFailed = 4,

    /// <summary>The operation exceeded its time limit.</summary>
    Timeout = 5,

    /// <summary>The operation was cancelled.</summary>
    Cancelled = 6,

    /// <summary>An output could not be bound to its target destination.</summary>
    OutputBindingFailed = 7
}
