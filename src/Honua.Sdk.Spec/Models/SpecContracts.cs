// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Spec.Models;

/// <summary>
/// Canonical spec document sent to plan and apply endpoints.
/// </summary>
public sealed record SpecDocumentRequest
{
    /// <summary>Declared grammar version.</summary>
    public required string GrammarVersion { get; init; }

    /// <summary>Declared process-family version.</summary>
    public required string ProcessFamilyVersion { get; init; }

    /// <summary>Optional stable document identifier.</summary>
    public string? SpecId { get; init; }

    /// <summary>Declared spec nodes.</summary>
    public IReadOnlyList<SpecNodeRequest> Nodes { get; init; } = [];

    /// <summary>Apply-only cache policy. Ignored by plan.</summary>
    public SpecCacheMode? CacheMode { get; init; }

    /// <summary>Apply-only maximum node concurrency. Ignored by plan.</summary>
    public int? MaxConcurrency { get; init; }
}

/// <summary>
/// Single canonical spec node sent to the server.
/// </summary>
public sealed record SpecNodeRequest
{
    /// <summary>Stable node identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Canonical resource kind.</summary>
    public required SpecResourceKind Kind { get; init; }

    /// <summary>Operator identifier. Null for pure source nodes.</summary>
    public string? Op { get; init; }

    /// <summary>Input bindings: scalar literals or node references.</summary>
    public IReadOnlyDictionary<string, string>? Inputs { get; init; }

    /// <summary>Flat parameter bag.</summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }

    /// <summary>Canonicalized fragment used for content hashing.</summary>
    public string? CanonicalFragment { get; init; }

    /// <summary>Optional source pins.</summary>
    public IReadOnlyDictionary<string, string>? SourcePins { get; init; }

    /// <summary>Whether the node operation is declared non-deterministic.</summary>
    public bool Nondeterministic { get; init; }
}

/// <summary>
/// Resource kinds recognized by the public spec contract.
/// </summary>
public enum SpecResourceKind
{
    /// <summary>Ephemeral compute node.</summary>
    Compute,

    /// <summary>Report output.</summary>
    Report,

    /// <summary>Dataset resource.</summary>
    Dataset,

    /// <summary>Service resource.</summary>
    Service,

    /// <summary>Application resource.</summary>
    App
}

/// <summary>
/// Apply cache policy.
/// </summary>
public enum SpecCacheMode
{
    /// <summary>Read from cache and write successful outputs.</summary>
    ReadWrite,

    /// <summary>Read from cache without writing new entries.</summary>
    ReadOnly,

    /// <summary>Ignore cache and recompute every node.</summary>
    Bypass
}

/// <summary>
/// Validation request for DSL text or canonical JSON.
/// </summary>
public sealed record SpecValidateRequest
{
    /// <summary>Spec source text in the public DSL.</summary>
    public string? Text { get; init; }

    /// <summary>Canonical JSON spec document.</summary>
    public JsonElement? Spec { get; init; }

    /// <summary>Whether valid responses should include canonical JSON.</summary>
    public bool IncludeCanonicalJson { get; init; }
}

/// <summary>
/// Validation response.
/// </summary>
public sealed record SpecValidateResponse
{
    /// <summary>Whether the document is valid.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Structured diagnostics.</summary>
    public required IReadOnlyList<SpecValidateDiagnostic> Diagnostics { get; init; }

    /// <summary>Canonical JSON returned when requested and valid.</summary>
    public string? CanonicalJson { get; init; }

    /// <summary>Grammar identifier when available.</summary>
    public string? Grammar { get; init; }

    /// <summary>Operator capability version used for validation.</summary>
    public required string OperatorCapabilityVersion { get; init; }
}

/// <summary>
/// Structured validation diagnostic.
/// </summary>
public sealed record SpecValidateDiagnostic
{
    /// <summary>Stable diagnostic code.</summary>
    public required string Code { get; init; }

    /// <summary>Diagnostic severity.</summary>
    public required string Severity { get; init; }

    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }

    /// <summary>JSON-pointer path into the canonical document.</summary>
    public string? Path { get; init; }

    /// <summary>Optional source span.</summary>
    public SpecSourceSpan? Span { get; init; }
}

/// <summary>
/// Source location for a diagnostic.
/// </summary>
public readonly record struct SpecSourceSpan(int Line, int Column, int Offset, int Length);

/// <summary>
/// Plan response returned by the server.
/// </summary>
public sealed record SpecPlanResponse
{
    /// <summary>Stable plan identifier.</summary>
    public required string PlanId { get; init; }

    /// <summary>Grammar version.</summary>
    public required string GrammarVersion { get; init; }

    /// <summary>Process-family version.</summary>
    public required string ProcessFamilyVersion { get; init; }

    /// <summary>DAG nodes in topological order.</summary>
    public required IReadOnlyList<SpecPlanNode> Nodes { get; init; }

    /// <summary>Document-level warnings.</summary>
    public IReadOnlyList<SpecWarning> Warnings { get; init; } = [];
}

/// <summary>
/// Single node in a plan response.
/// </summary>
public sealed record SpecPlanNode
{
    /// <summary>Node identifier.</summary>
    public required string NodeId { get; init; }

    /// <summary>Resource kind.</summary>
    public required SpecResourceKind Kind { get; init; }

    /// <summary>Operator identifier.</summary>
    public string? Op { get; init; }

    /// <summary>Direct dependency node identifiers.</summary>
    public IReadOnlyList<string> DependsOn { get; init; } = [];

    /// <summary>Content hash for cached output.</summary>
    public required string ContentHash { get; init; }

    /// <summary>Cost estimate.</summary>
    public required SpecCostEstimate Cost { get; init; }

    /// <summary>Per-node warnings.</summary>
    public IReadOnlyList<SpecWarning> Warnings { get; init; } = [];
}

/// <summary>
/// Estimated node cost.
/// </summary>
public sealed record SpecCostEstimate
{
    /// <summary>Estimated row count, or null when unknown.</summary>
    public long? EstimatedRows { get; init; }

    /// <summary>Estimated output bytes, or null when unknown.</summary>
    public long? EstimatedBytes { get; init; }

    /// <summary>Estimated duration in milliseconds, or null when unknown.</summary>
    public double? EstimatedDurationMs { get; init; }
}

/// <summary>
/// Diagnostic severity.
/// </summary>
public enum SpecDiagnosticSeverity
{
    /// <summary>Informational diagnostic.</summary>
    Info,

    /// <summary>Warning diagnostic.</summary>
    Warning,

    /// <summary>Error diagnostic.</summary>
    Error
}

/// <summary>
/// Structured plan or apply warning.
/// </summary>
public sealed record SpecWarning
{
    /// <summary>Stable diagnostic code.</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable message.</summary>
    public required string Message { get; init; }

    /// <summary>Diagnostic severity.</summary>
    public SpecDiagnosticSeverity Severity { get; init; } = SpecDiagnosticSeverity.Warning;

    /// <summary>Optional node identifier.</summary>
    public string? NodeId { get; init; }

    /// <summary>Optional remediation hint.</summary>
    public string? Remedy { get; init; }
}

/// <summary>
/// Per-node or apply-level event kind.
/// </summary>
public enum SpecApplyEventKind
{
    /// <summary>Node has been queued.</summary>
    Queued,

    /// <summary>Node is running.</summary>
    Running,

    /// <summary>Node output was satisfied from cache.</summary>
    Cached,

    /// <summary>Node completed successfully.</summary>
    Succeeded,

    /// <summary>Node failed.</summary>
    Failed,

    /// <summary>Node was skipped.</summary>
    Skipped,

    /// <summary>Warning event.</summary>
    Warning,

    /// <summary>Apply started.</summary>
    ApplyStarted,

    /// <summary>Apply completed.</summary>
    ApplyCompleted,

    /// <summary>Apply was cancelled.</summary>
    ApplyCancelled
}

/// <summary>
/// Single apply stream event.
/// </summary>
public sealed record SpecApplyEvent
{
    /// <summary>Monotonic event sequence number.</summary>
    public required long Sequence { get; init; }

    /// <summary>Event kind.</summary>
    public required SpecApplyEventKind Kind { get; init; }

    /// <summary>Apply token.</summary>
    public required string ApplyToken { get; init; }

    /// <summary>Optional node identifier.</summary>
    public string? NodeId { get; init; }

    /// <summary>Optional node output content hash.</summary>
    public string? ContentHash { get; init; }

    /// <summary>Event timestamp.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Structured diagnostic for warnings or failures.</summary>
    public SpecWarning? Diagnostic { get; init; }

    /// <summary>Actual measured node cost.</summary>
    public SpecCostActual? ActualCost { get; init; }

    /// <summary>Terminal apply summary.</summary>
    public SpecApplySummary? Summary { get; init; }
}

/// <summary>
/// Actual measured node cost.
/// </summary>
public sealed record SpecCostActual
{
    /// <summary>Actual output row count, if reported.</summary>
    public long? Rows { get; init; }

    /// <summary>Actual output bytes, if reported.</summary>
    public long? Bytes { get; init; }

    /// <summary>Measured duration in milliseconds.</summary>
    public required double DurationMs { get; init; }
}

/// <summary>
/// Aggregate apply run summary.
/// </summary>
public sealed record SpecApplySummary
{
    /// <summary>Total DAG nodes.</summary>
    public required int TotalNodes { get; init; }

    /// <summary>Nodes satisfied from cache.</summary>
    public required int CachedNodes { get; init; }

    /// <summary>Nodes that ran and succeeded.</summary>
    public required int RanNodes { get; init; }

    /// <summary>Nodes that failed.</summary>
    public required int FailedNodes { get; init; }

    /// <summary>Nodes skipped due to failure or cancellation.</summary>
    public required int SkippedNodes { get; init; }

    /// <summary>Total wall-clock duration in milliseconds.</summary>
    public required double TotalDurationMs { get; init; }

    /// <summary>Whether the apply run was cancelled.</summary>
    public required bool Cancelled { get; init; }
}

/// <summary>
/// Response for an apply stream handshake.
/// </summary>
public sealed class SpecApplyStream : IDisposable, IAsyncDisposable
{
    private readonly HttpResponseMessage? _response;

    internal SpecApplyStream(
        string? applyToken,
        IAsyncEnumerable<SpecApplyEvent> events,
        HttpResponseMessage? response = null)
    {
        ApplyToken = applyToken;
        Events = events;
        _response = response;
    }

    /// <summary>Apply token returned by the server.</summary>
    public string? ApplyToken { get; }

    /// <summary>Apply event stream.</summary>
    public IAsyncEnumerable<SpecApplyEvent> Events { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        _response?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Cancel request payload.
/// </summary>
public sealed record SpecCancelRequest
{
    /// <summary>Apply token to cancel.</summary>
    public required string ApplyToken { get; init; }
}

/// <summary>
/// Cancel response payload.
/// </summary>
public sealed record SpecCancelResponse
{
    /// <summary>Apply token.</summary>
    public required string ApplyToken { get; init; }

    /// <summary>Whether the token was resolved and cancellation was requested.</summary>
    public required bool Cancelled { get; init; }
}

/// <summary>
/// Problem-details payload returned by spec endpoints.
/// </summary>
public sealed record SpecProblem
{
    /// <summary>Problem type URI.</summary>
    public required string Type { get; init; }

    /// <summary>Problem title.</summary>
    public required string Title { get; init; }

    /// <summary>HTTP status code.</summary>
    public required int Status { get; init; }

    /// <summary>Problem detail.</summary>
    public required string Detail { get; init; }

    /// <summary>Stable diagnostic code.</summary>
    public required string Code { get; init; }

    /// <summary>Optional node identifier.</summary>
    public string? NodeId { get; init; }

    /// <summary>Optional remediation hint.</summary>
    public string? Remedy { get; init; }
}

/// <summary>
/// A cached spec artifact retrieved by content hash from
/// <c>GET /v1/spec/artifact/{hash}</c>. This is the closest analog the server
/// exposes today to generated-artifact retrieval. The body is read fully into
/// memory; spec artifacts are content-hash-addressed cache entries (node
/// outputs), so their size is bounded by the producing node.
/// </summary>
public sealed record HonuaSpecArtifact
{
    /// <summary>
    /// Content hash echoed by the server in the <c>X-Spec-Content-Hash</c>
    /// header (the cache key). Falls back to the requested hash when the header
    /// is absent.
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// Response content type, e.g. <c>application/x-arrow</c>. Defaults to
    /// <c>application/octet-stream</c> when the server does not specify one.
    /// </summary>
    public required string ContentType { get; init; }

    /// <summary>Raw artifact bytes.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }
}
