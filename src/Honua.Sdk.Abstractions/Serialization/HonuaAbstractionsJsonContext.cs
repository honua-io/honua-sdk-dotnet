// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Abstractions.Console;
using Honua.Sdk.Abstractions.Environments;
using Honua.Sdk.Abstractions.Studio;

namespace Honua.Sdk.Abstractions.Serialization;

/// <summary>
/// Source-generated JSON context for shared SDK contracts.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HonuaEnvironmentProfileSet))]
[JsonSerializable(typeof(HonuaEnvironmentProfile))]
[JsonSerializable(typeof(HonuaEnvironmentProfile[]))]
[JsonSerializable(typeof(HonuaTenantScope))]
[JsonSerializable(typeof(HonuaTransportCapabilities))]
[JsonSerializable(typeof(HonuaTrustProfile))]
[JsonSerializable(typeof(HonuaClientCertificateReference))]
[JsonSerializable(typeof(HonuaEnvironmentTrustState))]
[JsonSerializable(typeof(HonuaConsoleShellDescriptor))]
[JsonSerializable(typeof(HonuaConsolePrincipal))]
[JsonSerializable(typeof(HonuaConsoleNavigationItem))]
[JsonSerializable(typeof(HonuaConsoleNavigationItem[]))]
[JsonSerializable(typeof(HonuaConsoleRouteGuard))]
[JsonSerializable(typeof(HonuaConsoleRouteGuard[]))]
[JsonSerializable(typeof(HonuaConsoleRouteGuardDecision))]
[JsonSerializable(typeof(HonuaConsolePermissionGrant))]
[JsonSerializable(typeof(HonuaConsolePermissionGrant[]))]
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
[JsonSerializable(typeof(HonuaRenderedReport))]
[JsonSerializable(typeof(HonuaAnalysisResultPackage))]
[JsonSerializable(typeof(HonuaArtifactRef))]
[JsonSerializable(typeof(HonuaWorkspaceRef))]
[JsonSerializable(typeof(HonuaGeoprocessingError))]
[JsonSerializable(typeof(HonuaGeoprocessingValidationFailure))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
public sealed partial class HonuaAbstractionsJsonContext : JsonSerializerContext
{
}
