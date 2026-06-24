// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Abstractions.Console.Share;
using Honua.Sdk.ConsoleShare.Models;

namespace Honua.Sdk.ConsoleShare;

/// <summary>
/// Source-generated JSON context for Console Share payloads. Trimming/AOT-safe:
/// every share, access, public-link, and embed-token contract resolves through
/// this context without reflection.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HonuaShareItem))]
[JsonSerializable(typeof(HonuaShareItemDetail))]
[JsonSerializable(typeof(HonuaShareGrant))]
[JsonSerializable(typeof(HonuaShareAccessUpdate))]
[JsonSerializable(typeof(HonuaShareDependencyClosure))]
[JsonSerializable(typeof(HonuaShareDependency))]
[JsonSerializable(typeof(HonuaPublicLink))]
[JsonSerializable(typeof(HonuaPublicLinkRequest))]
[JsonSerializable(typeof(HonuaEmbedToken))]
[JsonSerializable(typeof(HonuaEmbedTokenRequest))]
[JsonSerializable(typeof(HonuaShareExportDefinitionRequest))]
[JsonSerializable(typeof(HonuaShareExportDefinition))]
[JsonSerializable(typeof(HonuaShareExportDefinitionPage))]
[JsonSerializable(typeof(HonuaShareExportRun))]
[JsonSerializable(typeof(HonuaShareExportRunPage))]
[JsonSerializable(typeof(HonuaShareTrafficSummary))]
[JsonSerializable(typeof(HonuaShareTrafficSeries))]
[JsonSerializable(typeof(HonuaShareTrafficBucket))]
[JsonSerializable(typeof(HonuaShareTrafficCounts))]
[JsonSerializable(typeof(HonuaShareItemRef))]
[JsonSerializable(typeof(HonuaUpdateOpenDataPageRequest))]
[JsonSerializable(typeof(HonuaOpenDataPage))]
[JsonSerializable(typeof(HonuaOpenDataEligibility))]
[JsonSerializable(typeof(HonuaOpenDataValidationResult))]
[JsonSerializable(typeof(HonuaConsoleStacPublicationState))]
[JsonSerializable(typeof(HonuaOpenDataPageResponse))]
[JsonSerializable(typeof(HonuaDcatExportResponse))]
[JsonSerializable(typeof(HonuaDcatCatalog))]
[JsonSerializable(typeof(HonuaSchemaOrgDataset))]
[JsonSerializable(typeof(HonuaStacCatalog))]
[JsonSerializable(typeof(HonuaStacCollection))]
[JsonSerializable(typeof(HonuaStacItem))]
[JsonSerializable(typeof(ApiResponseEnvelope<HonuaOpenDataPageResponse>), TypeInfoPropertyName = "ApiResponseEnvelopeHonuaOpenDataPageResponse")]
[JsonSerializable(typeof(ApiResponseEnvelope<HonuaOpenDataEligibility>), TypeInfoPropertyName = "ApiResponseEnvelopeHonuaOpenDataEligibility")]
[JsonSerializable(typeof(ApiResponseEnvelope<HonuaDcatExportResponse>), TypeInfoPropertyName = "ApiResponseEnvelopeHonuaDcatExportResponse")]
[JsonSerializable(typeof(ApiResponseEnvelope<HonuaConsoleStacPublicationState>), TypeInfoPropertyName = "ApiResponseEnvelopeHonuaConsoleStacPublicationState")]
[JsonSerializable(typeof(ApiResponseEnvelope<HonuaOpenDataPage>), TypeInfoPropertyName = "ApiResponseEnvelopeHonuaOpenDataPage")]
[JsonSerializable(typeof(ConsoleShareProblem))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal sealed partial class ConsoleShareJsonContext : JsonSerializerContext
{
}
