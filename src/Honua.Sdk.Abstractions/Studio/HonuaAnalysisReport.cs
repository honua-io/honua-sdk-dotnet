// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Studio;

/// <summary>
/// Contract version constants for the analysis-reporting surface. Mirrors the
/// server <c>ReportingConstants</c> so Console can pin and compare versions.
/// </summary>
public static class HonuaReportingContract
{
    /// <summary>
    /// The report contract version this SDK models
    /// (<c>honua.report.v1</c>). Consumers compare
    /// <see cref="HonuaAnalysisReport.ReportContractVersion"/> against this
    /// before relying on the modeled section shapes.
    /// </summary>
    public const string ContractVersionV1 = "honua.report.v1";
}

/// <summary>
/// Versioned, deterministic envelope describing the rendered shape of an
/// analytical report derived from an analysis result package. Retrieved by
/// Console hosts via <c>GET /api/v1/analysis/reports/{jobId}</c>. The server
/// returns this document unwrapped (no <c>ApiResponse&lt;T&gt;</c> envelope).
/// </summary>
public sealed record HonuaAnalysisReport
{
    /// <summary>Stable identifier for this report.</summary>
    public required string ReportId { get; init; }

    /// <summary>
    /// Schema/contract version this report conforms to. Compare against
    /// <see cref="HonuaReportingContract.ContractVersionV1"/>.
    /// </summary>
    public required string ReportContractVersion { get; init; }

    /// <summary>Originating geoprocessing job identifier.</summary>
    public required string JobId { get; init; }

    /// <summary>Originating result-package identifier.</summary>
    public required string ResultPackageId { get; init; }

    /// <summary>Process identifier, e.g. <c>analytics.buffer-aggregate</c>.</summary>
    public required string ProcessId { get; init; }

    /// <summary>Process family slice prefix, e.g. <c>analytics</c>.</summary>
    public required string ProcessFamily { get; init; }

    /// <summary>Identifier of the template that produced this report.</summary>
    public required string TemplateId { get; init; }

    /// <summary>Version of the template that produced this report.</summary>
    public required string TemplateVersion { get; init; }

    /// <summary>Result summary copied verbatim from the source result package.</summary>
    public required HonuaResultSummary Summary { get; init; }

    /// <summary>Ordered list of report sections.</summary>
    public required IReadOnlyList<HonuaAnalysisReportSection> Sections { get; init; }

    /// <summary>Path the narrative provider took for this report.</summary>
    public required HonuaNarrativeMode NarrativeMode { get; init; }

    /// <summary>Provenance copied verbatim from the source result package.</summary>
    public required HonuaProvenanceRecord Provenance { get; init; }

    /// <summary>When this report envelope was generated.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Workflow assumptions copied from the source result package.</summary>
    public IReadOnlyList<string> Assumptions { get; init; } = [];
}

/// <summary>
/// Narrative path that produced the textual blocks in a report. Wire values are
/// the kebab-case tags published by the server reporting contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaNarrativeMode>))]
public enum HonuaNarrativeMode
{
    /// <summary>All narrative blocks were produced by the deterministic provider.</summary>
    [JsonStringEnumMemberName("deterministic")]
    Deterministic = 0,

    /// <summary>One or more narrative blocks were produced by the LLM provider.</summary>
    [JsonStringEnumMemberName("llm-assisted")]
    LlmAssisted = 1,

    /// <summary>The LLM provider failed or timed out; the renderer fell back to deterministic text.</summary>
    [JsonStringEnumMemberName("fallback-from-llm-error")]
    FallbackFromLlmError = 2
}

/// <summary>
/// Human-readable summary of a geoprocessing result, embedded verbatim in a
/// report and in <see cref="HonuaAnalysisResultPackage"/>.
/// </summary>
public sealed record HonuaResultSummary
{
    /// <summary>Title of the result.</summary>
    public required string Title { get; init; }

    /// <summary>Optional detailed description of the result.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Audit trail recording the lineage of a geoprocessing result.
/// </summary>
public sealed record HonuaProvenanceRecord
{
    /// <summary>Source datasets consumed by the workflow.</summary>
    public required IReadOnlyList<HonuaProvenanceSource> Sources { get; init; }

    /// <summary>Identifiers of geoprocessing operations applied during execution.</summary>
    public required IReadOnlyList<string> ProcessDefinitions { get; init; }

    /// <summary>Assumptions made during workflow compilation.</summary>
    public IReadOnlyList<string> Assumptions { get; init; } = [];

    /// <summary>Question identifiers for clarifications that were asked.</summary>
    public IReadOnlyList<string> ClarificationsAsked { get; init; } = [];

    /// <summary>Question identifiers for clarifications that were answered.</summary>
    public IReadOnlyList<string> ClarificationsAnswered { get; init; } = [];

    /// <summary>When the workflow execution completed.</summary>
    public DateTimeOffset? ExecutedAt { get; init; }

    /// <summary>Identifiers of artifacts generated by the workflow.</summary>
    public IReadOnlyList<string> GeneratedArtifactIds { get; init; } = [];
}

/// <summary>
/// A source dataset or resource consumed during geoprocessing.
/// </summary>
public sealed record HonuaProvenanceSource
{
    /// <summary>Identifier of the source dataset or resource.</summary>
    public required string SourceId { get; init; }

    /// <summary>Version of the source at the time of access.</summary>
    public string? Version { get; init; }

    /// <summary>Human-readable description of the source.</summary>
    public string? Description { get; init; }
}

/// <summary>
/// Output format requested when rendering a report to text.
/// </summary>
public enum HonuaReportRenderFormat
{
    /// <summary>CommonMark Markdown body (<c>format=md</c>).</summary>
    Markdown = 0,

    /// <summary>Self-contained HTML document (<c>format=html</c>).</summary>
    Html = 1
}

/// <summary>
/// A rendered analysis report body retrieved from
/// <c>GET /api/v1/analysis/reports/{jobId}/render</c>. Carries text (not JSON):
/// Markdown or a self-contained HTML document.
/// </summary>
public sealed record HonuaRenderedReport
{
    /// <summary>Originating geoprocessing job identifier.</summary>
    public required string JobId { get; init; }

    /// <summary>Format of <see cref="Content"/>.</summary>
    public required HonuaReportRenderFormat Format { get; init; }

    /// <summary>Response media type, e.g. <c>text/markdown</c> or <c>text/html</c>.</summary>
    public required string MediaType { get; init; }

    /// <summary>Rendered report body.</summary>
    public required string Content { get; init; }
}
