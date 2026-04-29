// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Spec.Models;

namespace Honua.Sdk.Spec;

/// <summary>
/// Source-generated JSON context for spec contracts.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(SpecDocumentRequest))]
[JsonSerializable(typeof(SpecNodeRequest))]
[JsonSerializable(typeof(SpecValidateRequest))]
[JsonSerializable(typeof(SpecValidateResponse))]
[JsonSerializable(typeof(SpecValidateDiagnostic))]
[JsonSerializable(typeof(SpecSourceSpan))]
[JsonSerializable(typeof(SpecPlanResponse))]
[JsonSerializable(typeof(SpecPlanNode))]
[JsonSerializable(typeof(SpecCostEstimate))]
[JsonSerializable(typeof(SpecWarning))]
[JsonSerializable(typeof(SpecApplyEvent))]
[JsonSerializable(typeof(SpecCostActual))]
[JsonSerializable(typeof(SpecApplySummary))]
[JsonSerializable(typeof(SpecCancelRequest))]
[JsonSerializable(typeof(SpecCancelResponse))]
[JsonSerializable(typeof(SpecProblem))]
internal sealed partial class SpecJsonContext : JsonSerializerContext
{
}
