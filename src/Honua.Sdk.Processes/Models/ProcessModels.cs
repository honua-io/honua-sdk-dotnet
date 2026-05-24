// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Processes.Models;

/// <summary>
/// Hypermedia link.
/// </summary>
public sealed class HonuaProcessLink
{
    /// <summary>
    /// Link target.
    /// </summary>
    [JsonPropertyName("href")]
    public string Href { get; init; } = string.Empty;

    /// <summary>
    /// Link relation.
    /// </summary>
    [JsonPropertyName("rel")]
    public string? Rel { get; init; }

    /// <summary>
    /// Media type.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Operator-facing link title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

/// <summary>
/// OGC API Processes landing page.
/// </summary>
public sealed class HonuaProcessesLandingPage
{
    /// <summary>
    /// Landing page title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Landing page description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Landing page links.
    /// </summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<HonuaProcessLink> Links { get; init; } = [];
}

/// <summary>
/// OGC API Processes conformance declaration.
/// </summary>
public sealed class HonuaProcessesConformance
{
    /// <summary>
    /// Conformance class URIs.
    /// </summary>
    [JsonPropertyName("conformsTo")]
    public IReadOnlyList<string> ConformsTo { get; init; } = [];
}

/// <summary>
/// Process list response.
/// </summary>
public sealed class HonuaProcessList
{
    /// <summary>
    /// Available processes.
    /// </summary>
    [JsonPropertyName("processes")]
    public IReadOnlyList<HonuaProcessSummary> Processes { get; init; } = [];

    /// <summary>
    /// Response links.
    /// </summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<HonuaProcessLink> Links { get; init; } = [];
}

/// <summary>
/// Process summary.
/// </summary>
public class HonuaProcessSummary
{
    /// <summary>
    /// Process identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Process title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Process description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Process version.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Supported job control options.
    /// </summary>
    [JsonPropertyName("jobControlOptions")]
    public IReadOnlyList<string> JobControlOptions { get; init; } = [];

    /// <summary>
    /// Supported output transmission options.
    /// </summary>
    [JsonPropertyName("outputTransmission")]
    public IReadOnlyList<string> OutputTransmission { get; init; } = [];

    /// <summary>
    /// Process links.
    /// </summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<HonuaProcessLink> Links { get; init; } = [];
}

/// <summary>
/// Process description including inputs and outputs.
/// </summary>
public sealed class HonuaProcessDescription : HonuaProcessSummary
{
    /// <summary>
    /// Process input descriptions keyed by input id.
    /// </summary>
    [JsonPropertyName("inputs")]
    public IReadOnlyDictionary<string, HonuaProcessInputDescription> Inputs { get; init; } =
        new Dictionary<string, HonuaProcessInputDescription>(StringComparer.Ordinal);

    /// <summary>
    /// Process output descriptions keyed by output id.
    /// </summary>
    [JsonPropertyName("outputs")]
    public IReadOnlyDictionary<string, HonuaProcessOutputDescription> Outputs { get; init; } =
        new Dictionary<string, HonuaProcessOutputDescription>(StringComparer.Ordinal);
}

/// <summary>
/// Process input description.
/// </summary>
public sealed class HonuaProcessInputDescription
{
    /// <summary>
    /// Input title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Input description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Input schema.
    /// </summary>
    [JsonPropertyName("schema")]
    public HonuaProcessIoSchema? Schema { get; init; }
}

/// <summary>
/// Process output description.
/// </summary>
public sealed class HonuaProcessOutputDescription
{
    /// <summary>
    /// Output title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Output description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Output schema.
    /// </summary>
    [JsonPropertyName("schema")]
    public HonuaProcessIoSchema? Schema { get; init; }
}

/// <summary>
/// Minimal JSON Schema fragment for a process input or output.
/// </summary>
public sealed class HonuaProcessIoSchema
{
    /// <summary>
    /// JSON Schema type.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Content media type hint.
    /// </summary>
    [JsonPropertyName("contentMediaType")]
    public string? ContentMediaType { get; init; }

    /// <summary>
    /// Additional schema keywords.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json extension data requires a settable dictionary property.")]
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
}

/// <summary>
/// OGC process execution request.
/// </summary>
public sealed record HonuaProcessExecuteRequest
{
    /// <summary>
    /// Process inputs.
    /// </summary>
    [JsonPropertyName("inputs")]
    public required HonuaProcessExecuteInputs Inputs { get; init; }

    /// <summary>
    /// Response mode. Honua Server currently supports document mode.
    /// </summary>
    [JsonPropertyName("response")]
    public string Response { get; init; } = "document";
}

/// <summary>
/// Process execution inputs.
/// </summary>
public sealed record HonuaProcessExecuteInputs
{
    /// <summary>
    /// Analysis plan input for the canonical Honua geoprocessing process.
    /// </summary>
    [JsonPropertyName("plan")]
    public HonuaAnalysisPlan? Plan { get; init; }

    /// <summary>
    /// Direct process inputs keyed by input identifier for concrete OGC processes.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json extension data requires a settable dictionary property.")]
    [JsonExtensionData]
    public Dictionary<string, JsonElement> DirectInputs { get; set; } = [];

    /// <summary>
    /// Creates inputs for the canonical Honua geoprocessing plan contract.
    /// </summary>
    /// <param name="plan">Analysis plan to submit.</param>
    /// <returns>Execution inputs containing the supplied plan.</returns>
    public static HonuaProcessExecuteInputs FromPlan(HonuaAnalysisPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new HonuaProcessExecuteInputs
        {
            Plan = plan
        };
    }

    /// <summary>
    /// Creates inputs for a concrete process that accepts direct OGC input values.
    /// </summary>
    /// <param name="inputs">Direct process inputs keyed by input identifier.</param>
    /// <returns>Execution inputs containing the supplied direct input values.</returns>
    public static HonuaProcessExecuteInputs FromDirectInputs(IReadOnlyDictionary<string, JsonElement> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        return new HonuaProcessExecuteInputs
        {
            DirectInputs = inputs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone(), StringComparer.Ordinal)
        };
    }
}

/// <summary>
/// Analysis plan with steps and output expectations.
/// </summary>
public sealed record HonuaAnalysisPlan
{
    /// <summary>
    /// Plan identifier.
    /// </summary>
    [JsonPropertyName("planId")]
    public required string PlanId { get; init; }

    /// <summary>
    /// Optional process/spec version.
    /// </summary>
    [JsonPropertyName("specVersion")]
    public string? SpecVersion { get; init; }

    /// <summary>
    /// Workflow family, such as analyze, publish, build, or deploy.
    /// </summary>
    [JsonPropertyName("workflowFamily")]
    public string? WorkflowFamily { get; init; }

    /// <summary>
    /// Ordered processing steps.
    /// </summary>
    [JsonPropertyName("steps")]
    public IReadOnlyList<HonuaPlanStep> Steps { get; init; } = [];

    /// <summary>
    /// Desired output artifact kinds.
    /// </summary>
    [JsonPropertyName("outputs")]
    public IReadOnlyList<string> Outputs { get; init; } = [];
}

/// <summary>
/// Native process execution context.
/// </summary>
public sealed record HonuaProcessExecutionContext
{
    /// <summary>
    /// Optional workspace identifier.
    /// </summary>
    public string? WorkspaceId { get; init; }

    /// <summary>
    /// Optional server-side timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Non-secret metadata for execution.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// Single processing step in an analysis plan.
/// </summary>
public sealed record HonuaPlanStep
{
    /// <summary>
    /// Step identifier.
    /// </summary>
    [JsonPropertyName("stepId")]
    public string? StepId { get; init; }

    /// <summary>
    /// Step kind.
    /// </summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>
    /// Optional canonical process identifier.
    /// </summary>
    [JsonPropertyName("processId")]
    public string? ProcessId { get; init; }

    /// <summary>
    /// Step inputs.
    /// </summary>
    [JsonPropertyName("inputs")]
    public IReadOnlyDictionary<string, string> Inputs { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Step dependencies.
    /// </summary>
    [JsonPropertyName("dependsOn")]
    public IReadOnlyList<string> DependsOn { get; init; } = [];
}

/// <summary>
/// Process job status.
/// </summary>
public sealed class HonuaProcessJobStatus
{
    /// <summary>
    /// Process identifier.
    /// </summary>
    [JsonPropertyName("processID")]
    public string? ProcessId { get; init; }

    /// <summary>
    /// Resource type discriminator.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Job identifier.
    /// </summary>
    [JsonPropertyName("jobID")]
    public string JobId { get; init; } = string.Empty;

    /// <summary>
    /// Job status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Optional status message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    [JsonPropertyName("created")]
    public DateTimeOffset? Created { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    [JsonPropertyName("updated")]
    public DateTimeOffset? Updated { get; init; }

    /// <summary>
    /// Progress percent.
    /// </summary>
    [JsonPropertyName("progress")]
    public int? Progress { get; init; }

    /// <summary>
    /// Job links.
    /// </summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<HonuaProcessLink> Links { get; init; } = [];
}

/// <summary>
/// Process job list.
/// </summary>
public sealed class HonuaProcessJobList
{
    /// <summary>
    /// Jobs in the page.
    /// </summary>
    [JsonPropertyName("jobs")]
    public IReadOnlyList<HonuaProcessJobStatus> Jobs { get; init; } = [];

    /// <summary>
    /// Response links.
    /// </summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<HonuaProcessLink> Links { get; init; } = [];
}

/// <summary>
/// Document-mode process results.
/// </summary>
public sealed class HonuaProcessResults
{
    /// <summary>
    /// Result values keyed by output identifier.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "System.Text.Json extension data requires a settable dictionary property.")]
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Outputs { get; set; } = [];
}

/// <summary>
/// Validation response for native process plans.
/// </summary>
public sealed record HonuaProcessPlanValidationResult
{
    /// <summary>
    /// Whether the plan is valid.
    /// </summary>
    public bool Valid { get; init; }

    /// <summary>
    /// Validation issues.
    /// </summary>
    public IReadOnlyList<HonuaProcessValidationIssue> Issues { get; init; } = [];
}

/// <summary>
/// Dry-run response for native process plans.
/// </summary>
public sealed record HonuaProcessDryRunResult
{
    /// <summary>
    /// Whether the plan is valid.
    /// </summary>
    public bool Valid { get; init; }

    /// <summary>
    /// Validation issues.
    /// </summary>
    public IReadOnlyList<HonuaProcessValidationIssue> Issues { get; init; } = [];

    /// <summary>
    /// Dry-run estimate.
    /// </summary>
    public HonuaProcessDryRunSummary? Result { get; init; }
}

/// <summary>
/// Plan validation issue.
/// </summary>
public sealed record HonuaProcessValidationIssue
{
    /// <summary>
    /// Node or step identifier.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// Field associated with the issue.
    /// </summary>
    public string? Field { get; init; }

    /// <summary>
    /// Issue message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Issue severity.
    /// </summary>
    public string? Severity { get; init; }
}

/// <summary>
/// Dry-run estimate summary.
/// </summary>
public sealed record HonuaProcessDryRunSummary
{
    /// <summary>
    /// Estimated duration.
    /// </summary>
    public TimeSpan? EstimatedDuration { get; init; }

    /// <summary>
    /// Estimated artifacts.
    /// </summary>
    public IReadOnlyList<HonuaProcessEstimatedArtifact> EstimatedArtifacts { get; init; } = [];

    /// <summary>
    /// Side effects that execution would produce.
    /// </summary>
    public IReadOnlyList<HonuaProcessSideEffect> SideEffects { get; init; } = [];

    /// <summary>
    /// Cost estimate.
    /// </summary>
    public HonuaProcessCostEstimate? CostEstimate { get; init; }
}

/// <summary>
/// Estimated execution artifact.
/// </summary>
public sealed record HonuaProcessEstimatedArtifact
{
    /// <summary>
    /// Artifact class.
    /// </summary>
    public string? ArtifactClass { get; init; }

    /// <summary>
    /// Estimated size in bytes.
    /// </summary>
    public long EstimatedSizeBytes { get; init; }

    /// <summary>
    /// Artifact description.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Estimated side effect.
/// </summary>
public sealed record HonuaProcessSideEffect
{
    /// <summary>
    /// Effect type.
    /// </summary>
    public string? EffectType { get; init; }

    /// <summary>
    /// Target affected by the effect.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Effect description.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Cost estimate.
/// </summary>
public sealed record HonuaProcessCostEstimate
{
    /// <summary>
    /// Cost units.
    /// </summary>
    public string? Units { get; init; }

    /// <summary>
    /// Cost amount.
    /// </summary>
    public double Amount { get; init; }
}

/// <summary>
/// Native process execution outcome.
/// </summary>
public sealed record HonuaProcessExecutionOutcome
{
    /// <summary>
    /// Job identifier.
    /// </summary>
    public string? JobId { get; init; }

    /// <summary>
    /// Successful execution result.
    /// </summary>
    public HonuaProcessExecutionResult? Result { get; init; }

    /// <summary>
    /// Terminal execution error.
    /// </summary>
    public HonuaProcessError? Error { get; init; }
}

/// <summary>
/// Process execution stream event.
/// </summary>
public sealed record HonuaProcessExecutionEvent
{
    /// <summary>
    /// Event type.
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// Progress event, when present.
    /// </summary>
    public HonuaProcessJobProgress? Progress { get; init; }

    /// <summary>
    /// Stage result event, when present.
    /// </summary>
    public HonuaProcessStageResult? StageResult { get; init; }

    /// <summary>
    /// Terminal result event, when present.
    /// </summary>
    public HonuaProcessExecutionResult? Result { get; init; }

    /// <summary>
    /// Terminal error event, when present.
    /// </summary>
    public HonuaProcessError? Error { get; init; }
}

/// <summary>
/// Process job progress.
/// </summary>
public sealed record HonuaProcessJobProgress
{
    /// <summary>
    /// Job identifier.
    /// </summary>
    public string? JobId { get; init; }

    /// <summary>
    /// Job state.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Progress percent.
    /// </summary>
    public int ProgressPercent { get; init; }

    /// <summary>
    /// Current node identifier.
    /// </summary>
    public string? CurrentNodeId { get; init; }

    /// <summary>
    /// Start timestamp.
    /// </summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Progress message.
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Process execution result.
/// </summary>
public sealed record HonuaProcessExecutionResult
{
    /// <summary>
    /// Result identifier.
    /// </summary>
    public string? ResultId { get; init; }

    /// <summary>
    /// Job state.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Result summary.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Assumptions recorded during execution.
    /// </summary>
    public IReadOnlyList<HonuaProcessAssumption> Assumptions { get; init; } = [];

    /// <summary>
    /// Produced artifacts.
    /// </summary>
    public IReadOnlyList<HonuaProcessArtifactRef> Artifacts { get; init; } = [];

    /// <summary>
    /// Stage results.
    /// </summary>
    public IReadOnlyList<HonuaProcessStageResult> StageResults { get; init; } = [];
}

/// <summary>
/// Process execution assumption.
/// </summary>
public sealed record HonuaProcessAssumption
{
    /// <summary>
    /// Assumption identifier.
    /// </summary>
    public string? AssumptionId { get; init; }

    /// <summary>
    /// Assumption description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Assumption rationale.
    /// </summary>
    public string? Rationale { get; init; }

    /// <summary>
    /// Whether the user confirmed the assumption.
    /// </summary>
    public bool UserConfirmed { get; init; }
}

/// <summary>
/// Produced artifact reference.
/// </summary>
public sealed record HonuaProcessArtifactRef
{
    /// <summary>
    /// Artifact identifier.
    /// </summary>
    public string? ArtifactId { get; init; }

    /// <summary>
    /// Artifact class.
    /// </summary>
    public string? ArtifactClass { get; init; }

    /// <summary>
    /// Artifact version.
    /// </summary>
    public int ArtifactVersion { get; init; }

    /// <summary>
    /// Producer reference.
    /// </summary>
    public string? ProducerRef { get; init; }
}

/// <summary>
/// Stage result.
/// </summary>
public sealed record HonuaProcessStageResult
{
    /// <summary>
    /// Node identifier.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// Stage state.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Stage error.
    /// </summary>
    public HonuaProcessError? Error { get; init; }

    /// <summary>
    /// Partial artifacts.
    /// </summary>
    public IReadOnlyList<HonuaProcessArtifactRef> PartialArtifacts { get; init; } = [];
}

/// <summary>
/// Structured process execution error.
/// </summary>
public sealed record HonuaProcessError
{
    /// <summary>
    /// Error code.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Error category.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Execution phase.
    /// </summary>
    public string? Phase { get; init; }

    /// <summary>
    /// Node identifier.
    /// </summary>
    public string? NodeId { get; init; }

    /// <summary>
    /// Retryability classification.
    /// </summary>
    public string? Retryability { get; init; }

    /// <summary>
    /// Suggested operator action.
    /// </summary>
    public string? SuggestedAction { get; init; }

    /// <summary>
    /// Additional non-secret error details.
    /// </summary>
    public IReadOnlyDictionary<string, string> Details { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// Problem details returned by a process endpoint.
/// </summary>
public sealed class HonuaProcessProblem
{
    /// <summary>
    /// Problem type.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>
    /// Problem title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// HTTP status code.
    /// </summary>
    [JsonPropertyName("status")]
    public int? Status { get; init; }

    /// <summary>
    /// Problem detail.
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}
