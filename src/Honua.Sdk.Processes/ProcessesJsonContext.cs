// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Processes.Models;

namespace Honua.Sdk.Processes;

/// <summary>
/// Source-generated JSON context for OGC API Processes payloads.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HonuaProcessesLandingPage))]
[JsonSerializable(typeof(HonuaProcessesConformance))]
[JsonSerializable(typeof(HonuaProcessLink))]
[JsonSerializable(typeof(HonuaProcessLink[]))]
[JsonSerializable(typeof(HonuaProcessList))]
[JsonSerializable(typeof(HonuaProcessSummary))]
[JsonSerializable(typeof(HonuaProcessSummary[]))]
[JsonSerializable(typeof(HonuaProcessDescription))]
[JsonSerializable(typeof(HonuaProcessInputDescription))]
[JsonSerializable(typeof(HonuaProcessOutputDescription))]
[JsonSerializable(typeof(HonuaProcessIoSchema))]
[JsonSerializable(typeof(HonuaProcessExecuteRequest))]
[JsonSerializable(typeof(HonuaProcessExecuteInputs))]
[JsonSerializable(typeof(HonuaAnalysisPlan))]
[JsonSerializable(typeof(HonuaProcessExecutionContext))]
[JsonSerializable(typeof(HonuaPlanStep))]
[JsonSerializable(typeof(HonuaPlanStep[]))]
[JsonSerializable(typeof(HonuaProcessJobStatus))]
[JsonSerializable(typeof(HonuaProcessJobStatus[]))]
[JsonSerializable(typeof(HonuaProcessJobList))]
[JsonSerializable(typeof(HonuaProcessResults))]
[JsonSerializable(typeof(HonuaProcessProblem))]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
internal sealed partial class ProcessesJsonContext : JsonSerializerContext
{
}
