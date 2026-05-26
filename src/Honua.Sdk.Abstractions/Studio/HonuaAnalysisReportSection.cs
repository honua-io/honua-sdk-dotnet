// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Studio;

/// <summary>
/// Wire-level discriminator values for <see cref="HonuaAnalysisReportSection"/>
/// subtypes. Mirrors the server <c>AnalysisReportSectionKinds</c> constants so
/// the polymorphic <c>kind</c> discriminator round-trips 1:1 with the server
/// and JS SDK projections.
/// </summary>
public static class HonuaAnalysisReportSectionKinds
{
    /// <summary>Heading section.</summary>
    public const string Heading = "heading";

    /// <summary>Paragraph of plain text.</summary>
    public const string Paragraph = "paragraph";

    /// <summary>Single key/value metric section.</summary>
    public const string KeyMetric = "key-metric";

    /// <summary>Tabular data with column headers.</summary>
    public const string Table = "table";

    /// <summary>Chart data rendered server-side as inline SVG.</summary>
    public const string Chart = "chart";

    /// <summary>Pointer to a related <c>honua://map-packages/{id}</c> resource.</summary>
    public const string MapEmbed = "map-embed";

    /// <summary>Narrative block authored by the narrative provider.</summary>
    public const string Narrative = "narrative";

    /// <summary>Footer that summarizes provenance.</summary>
    public const string ProvenanceFooter = "provenance-footer";
}

/// <summary>
/// Base type for every section in a <see cref="HonuaAnalysisReport"/>. The
/// <c>kind</c> discriminator selects the concrete subtype, resolved through a
/// source-generated JSON context (trimming/AOT-safe).
/// </summary>
/// <remarks>
/// The eight modeled kinds are exhaustive within report contract version
/// <see cref="HonuaReportingContract.ContractVersionV1"/>; consumers gate on
/// <see cref="HonuaAnalysisReport.ReportContractVersion"/> for version-level
/// compatibility. New <em>fields</em> on a known kind are tolerated (ignored)
/// on read; an entirely unmodeled <c>kind</c> within a supported contract
/// version is genuine drift and surfaces as a deserialization error so it is
/// caught loudly rather than silently dropped.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(HonuaHeadingSection), HonuaAnalysisReportSectionKinds.Heading)]
[JsonDerivedType(typeof(HonuaParagraphSection), HonuaAnalysisReportSectionKinds.Paragraph)]
[JsonDerivedType(typeof(HonuaKeyMetricSection), HonuaAnalysisReportSectionKinds.KeyMetric)]
[JsonDerivedType(typeof(HonuaTableSection), HonuaAnalysisReportSectionKinds.Table)]
[JsonDerivedType(typeof(HonuaChartSection), HonuaAnalysisReportSectionKinds.Chart)]
[JsonDerivedType(typeof(HonuaMapEmbedSection), HonuaAnalysisReportSectionKinds.MapEmbed)]
[JsonDerivedType(typeof(HonuaNarrativeSection), HonuaAnalysisReportSectionKinds.Narrative)]
[JsonDerivedType(typeof(HonuaProvenanceFooterSection), HonuaAnalysisReportSectionKinds.ProvenanceFooter)]
public abstract record HonuaAnalysisReportSection
{
    /// <summary>
    /// Stable discriminator identifying the section subtype. Each concrete
    /// subtype returns its <see cref="HonuaAnalysisReportSectionKinds"/> constant.
    /// </summary>
    [JsonIgnore]
    public abstract string Kind { get; }
}

/// <summary>
/// Section heading. Renderers map <see cref="Level"/> to <c>#</c>/<c>&lt;h&gt;</c>
/// nesting; the allowed range is 1-6.
/// </summary>
public sealed record HonuaHeadingSection : HonuaAnalysisReportSection
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => HonuaAnalysisReportSectionKinds.Heading;

    /// <summary>Visible heading text.</summary>
    public required string Text { get; init; }

    /// <summary>Heading level (1-6).</summary>
    public required int Level { get; init; }
}

/// <summary>
/// A plain-text paragraph rendered factually (no narrative provenance).
/// </summary>
public sealed record HonuaParagraphSection : HonuaAnalysisReportSection
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => HonuaAnalysisReportSectionKinds.Paragraph;

    /// <summary>Paragraph body text.</summary>
    public required string Text { get; init; }
}

/// <summary>
/// A single key/value metric (e.g. "Total area: 12.4 km²"). Distinct from a
/// row in <see cref="HonuaTableSection"/> because metrics carry units explicitly.
/// </summary>
public sealed record HonuaKeyMetricSection : HonuaAnalysisReportSection
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => HonuaAnalysisReportSectionKinds.KeyMetric;

    /// <summary>Display label for the metric.</summary>
    public required string Label { get; init; }

    /// <summary>Already-formatted, locale-invariant value text.</summary>
    public required string Value { get; init; }

    /// <summary>Optional unit annotation (e.g. <c>m²</c>, <c>km</c>).</summary>
    public string? Unit { get; init; }

    /// <summary>Optional spatial reference annotation (e.g. <c>EPSG:4326</c>).</summary>
    public string? SpatialReference { get; init; }
}

/// <summary>
/// Tabular section. Excess rows beyond the server row cap are summarized via
/// <see cref="TruncatedRowCount"/>.
/// </summary>
public sealed record HonuaTableSection : HonuaAnalysisReportSection
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => HonuaAnalysisReportSectionKinds.Table;

    /// <summary>Optional table caption rendered above the table.</summary>
    public string? Caption { get; init; }

    /// <summary>Ordered list of column headers.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>Ordered list of rows; each row's length matches <see cref="Columns"/>.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>
    /// Number of rows omitted because the source data exceeded the configured
    /// row cap. Zero when no truncation occurred.
    /// </summary>
    public int TruncatedRowCount { get; init; }
}

/// <summary>
/// Chart data; renderers convert this into inline SVG. The section
/// discriminator stays <c>chart</c>; the chart variant is on
/// <see cref="ChartKind"/>.
/// </summary>
public sealed record HonuaChartSection : HonuaAnalysisReportSection
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => HonuaAnalysisReportSectionKinds.Chart;

    /// <summary>Optional caption rendered above the chart.</summary>
    public string? Caption { get; init; }

    /// <summary>Bar or line chart variant.</summary>
    public required HonuaReportChartKind ChartKind { get; init; }

    /// <summary>Category labels for the X axis.</summary>
    public required IReadOnlyList<string> Categories { get; init; }

    /// <summary>One or more named series of numeric values.</summary>
    public required IReadOnlyList<HonuaChartSeries> Series { get; init; }

    /// <summary>Optional X axis label.</summary>
    public string? XAxisLabel { get; init; }

    /// <summary>Optional Y axis label.</summary>
    public string? YAxisLabel { get; init; }
}

/// <summary>
/// Single named numeric series for a <see cref="HonuaChartSection"/>.
/// </summary>
public sealed record HonuaChartSeries
{
    /// <summary>Series display label.</summary>
    public required string Name { get; init; }

    /// <summary>Numeric values aligned to <see cref="HonuaChartSection.Categories"/>.</summary>
    public required IReadOnlyList<double> Values { get; init; }
}

/// <summary>
/// Chart variant emitted by reporting renderers. Wire values are the
/// lower-case tags so JSON envelopes carry stable string identifiers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaReportChartKind>))]
public enum HonuaReportChartKind
{
    /// <summary>Vertical bar chart.</summary>
    [JsonStringEnumMemberName("bar")]
    Bar = 0,

    /// <summary>Polyline chart.</summary>
    [JsonStringEnumMemberName("line")]
    Line = 1
}

/// <summary>
/// Pointer to a related map package on the MCP surface
/// (<c>honua://map-packages/{id}</c>). The full map-package body is not exposed
/// over HTTP today; only this reference appears in reports.
/// </summary>
public sealed record HonuaMapEmbedSection : HonuaAnalysisReportSection
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => HonuaAnalysisReportSectionKinds.MapEmbed;

    /// <summary>Display caption for the embedded map.</summary>
    public required string Caption { get; init; }

    /// <summary>Stable MCP resource URI of the underlying map package.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1056:URI-like properties should not be strings",
        Justification = "Mirrors the server string contract; carries an opaque MCP resource pointer (custom honua:// scheme) that must round-trip verbatim without Uri normalization on this read path.")]
    public required string MapPackageUri { get; init; }
}

/// <summary>
/// Narrative paragraph produced by the narrative provider. A
/// <see cref="DeterministicText"/> is always present; <see cref="LlmText"/> is
/// set when the LLM provider successfully filled the slot.
/// </summary>
public sealed record HonuaNarrativeSection : HonuaAnalysisReportSection
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => HonuaAnalysisReportSectionKinds.Narrative;

    /// <summary>Stable identifier for the narrative slot the template defined.</summary>
    public required string SlotId { get; init; }

    /// <summary>Always-available deterministic factual paragraph.</summary>
    public required string DeterministicText { get; init; }

    /// <summary>Optional LLM-authored paragraph that supersedes the deterministic text.</summary>
    public string? LlmText { get; init; }

    /// <summary>Source of the rendered text for this slot.</summary>
    public required HonuaNarrativeMode Mode { get; init; }
}

/// <summary>
/// Footer summarizing the provenance record from the originating result
/// package.
/// </summary>
public sealed record HonuaProvenanceFooterSection : HonuaAnalysisReportSection
{
    /// <inheritdoc />
    [JsonIgnore]
    public override string Kind => HonuaAnalysisReportSectionKinds.ProvenanceFooter;

    /// <summary>Identifier of the originating result package.</summary>
    public required string ResultPackageId { get; init; }

    /// <summary>Identifier of the originating geoprocessing job.</summary>
    public required string JobId { get; init; }

    /// <summary>Identifiers of the geoprocessing operations applied.</summary>
    public required IReadOnlyList<string> ProcessDefinitions { get; init; }

    /// <summary>Source dataset identifiers used to produce the result.</summary>
    public required IReadOnlyList<string> Sources { get; init; }

    /// <summary>When the originating workflow execution completed.</summary>
    public DateTimeOffset? ExecutedAt { get; init; }

    /// <summary>When the report itself was generated.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }
}
