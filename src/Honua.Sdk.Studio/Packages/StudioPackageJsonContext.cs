// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Studio.Packages;

/// <summary>
/// Source-generated JSON context for the Studio package lifecycle projection.
/// Trimming/AOT-safe: every request body, response envelope, and package
/// sub-shape resolves through this context without reflection, matching the
/// server camelCase wire format.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StudioApiResponse<StudioPackageFamilyCapabilities>))]
[JsonSerializable(typeof(StudioApiResponse<StudioPackageDraft>))]
[JsonSerializable(typeof(StudioApiResponse<StudioValidationSummary>))]
[JsonSerializable(typeof(StudioApiResponse<StudioPreviewPlan>))]
[JsonSerializable(typeof(StudioApiResponse<StudioContentVersion>))]
[JsonSerializable(typeof(StudioApiResponse<StudioContentVersionList>))]
[JsonSerializable(typeof(StudioApiResponse<StudioVersionComparison>))]
[JsonSerializable(typeof(StudioApiResponse<StudioPublicationRequest>))]
[JsonSerializable(typeof(StudioApiResponse<StudioRollbackRequest>))]
[JsonSerializable(typeof(CreateStudioPackageDraftRequest))]
[JsonSerializable(typeof(UpdateStudioPackageDraftRequest))]
[JsonSerializable(typeof(SaveStudioContentVersionRequest))]
[JsonSerializable(typeof(CompareStudioContentVersionsRequest))]
[JsonSerializable(typeof(CreateStudioPublicationRequest))]
[JsonSerializable(typeof(CreateStudioRollbackRequest))]
[JsonSerializable(typeof(StudioPackageEnvelope))]
[JsonSerializable(typeof(StudioPackageFamilyCapabilities))]
[JsonSerializable(typeof(StudioPackageDraft))]
[JsonSerializable(typeof(StudioValidationSummary))]
[JsonSerializable(typeof(StudioPreviewPlan))]
[JsonSerializable(typeof(StudioContentVersion))]
[JsonSerializable(typeof(StudioContentVersionList))]
[JsonSerializable(typeof(StudioVersionComparison))]
[JsonSerializable(typeof(StudioPublicationRequest))]
[JsonSerializable(typeof(StudioRollbackRequest))]
internal sealed partial class StudioPackageJsonContext : JsonSerializerContext
{
}
