// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Abstractions.Studio;
using Honua.Sdk.Studio.Models;

namespace Honua.Sdk.Studio;

/// <summary>
/// Source-generated JSON context for Console Studio payloads. Trimming/AOT-safe:
/// every report contract, including the polymorphic
/// <see cref="HonuaAnalysisReportSection"/> hierarchy, resolves through this
/// context without reflection.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HonuaAnalysisReport))]
[JsonSerializable(typeof(HonuaAnalysisReportSection))]
[JsonSerializable(typeof(HonuaAnalysisReportSection[]))]
[JsonSerializable(typeof(HonuaHeadingSection))]
[JsonSerializable(typeof(HonuaParagraphSection))]
[JsonSerializable(typeof(HonuaKeyMetricSection))]
[JsonSerializable(typeof(HonuaTableSection))]
[JsonSerializable(typeof(HonuaChartSection))]
[JsonSerializable(typeof(HonuaChartSeries))]
[JsonSerializable(typeof(HonuaMapEmbedSection))]
[JsonSerializable(typeof(HonuaNarrativeSection))]
[JsonSerializable(typeof(HonuaProvenanceFooterSection))]
[JsonSerializable(typeof(HonuaResultSummary))]
[JsonSerializable(typeof(HonuaProvenanceRecord))]
[JsonSerializable(typeof(HonuaProvenanceSource))]
[JsonSerializable(typeof(HonuaAnalysisResultPackage))]
[JsonSerializable(typeof(HonuaArtifactRef))]
[JsonSerializable(typeof(HonuaWorkspaceRef))]
[JsonSerializable(typeof(HonuaGeoprocessingError))]
[JsonSerializable(typeof(HonuaGeoprocessingValidationFailure))]
[JsonSerializable(typeof(StudioProblem))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal sealed partial class StudioJsonContext : JsonSerializerContext
{
}
