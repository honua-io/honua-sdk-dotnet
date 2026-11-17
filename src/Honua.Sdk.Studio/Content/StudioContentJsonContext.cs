// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Abstractions.Operations;

namespace Honua.Sdk.Studio.Content;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StudioApiResponse<StudioContentKindCapabilities>))]
[JsonSerializable(typeof(StudioApiResponse<StudioContentDraft>))]
[JsonSerializable(typeof(StudioApiResponse<StudioPublishedArtifact>))]
[JsonSerializable(typeof(StudioApiResponse<StudioValidationSummary>))]
[JsonSerializable(typeof(StudioApiResponse<StudioPreviewPlan>))]
[JsonSerializable(typeof(StudioApiResponse<StudioContentVersion>))]
[JsonSerializable(typeof(StudioApiResponse<StudioContentVersionList>))]
[JsonSerializable(typeof(StudioApiResponse<StudioVersionComparison>))]
[JsonSerializable(typeof(StudioApiResponse<StudioPublicationRequest>))]
[JsonSerializable(typeof(StudioPublicationSubmissionEnvelope))]
[JsonSerializable(typeof(StudioContentPointerEnvelope))]
[JsonSerializable(typeof(HonuaOperationHandle))]
[JsonSerializable(typeof(StudioApiResponse<StudioRollbackRequest>))]
[JsonSerializable(typeof(CreateStudioContentDraftRequest))]
[JsonSerializable(typeof(UpdateStudioContentDraftRequest))]
[JsonSerializable(typeof(SaveStudioContentVersionRequest))]
[JsonSerializable(typeof(CompareStudioContentVersionsRequest))]
[JsonSerializable(typeof(CreateStudioPublicationRequest))]
[JsonSerializable(typeof(CreateStudioRollbackRequest))]
[JsonSerializable(typeof(StudioContentEnvelope))]
[JsonSerializable(typeof(StudioContentKindCapabilities))]
[JsonSerializable(typeof(StudioContentDraft))]
[JsonSerializable(typeof(StudioValidationSummary))]
[JsonSerializable(typeof(StudioPreviewPlan))]
[JsonSerializable(typeof(StudioContentVersion))]
[JsonSerializable(typeof(StudioContentVersionList))]
[JsonSerializable(typeof(StudioVersionComparison))]
[JsonSerializable(typeof(StudioPublicationRequest))]
[JsonSerializable(typeof(StudioRollbackRequest))]
internal sealed partial class StudioContentJsonContext : JsonSerializerContext
{
}

internal sealed class StudioPublicationSubmissionEnvelope
{
    public bool Success { get; init; }
    public JsonElement Data { get; init; }
}

internal sealed class StudioContentPointerEnvelope
{
    public bool Success { get; init; }
    public StudioContentPointerPage? Data { get; init; }
}

internal sealed class StudioContentPointerPage
{
    public IReadOnlyList<StudioContentPointers>? Items { get; init; }
    public long? Total { get; init; }
    public string? NextCursor { get; init; }
}
