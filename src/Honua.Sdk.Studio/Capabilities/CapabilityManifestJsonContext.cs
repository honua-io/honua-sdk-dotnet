// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Studio.Capabilities;

/// <summary>
/// Source-generated JSON context for the capability manifest projection.
/// Trimming/AOT-safe: the entire <see cref="CapabilityManifest"/> graph resolves
/// through this context without reflection, matching the server
/// <c>honua.capability_manifest.v1</c> camelCase wire format.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CapabilityManifest))]
[JsonSerializable(typeof(CapabilityManifestScope))]
[JsonSerializable(typeof(CapabilityManifestServerInfo))]
[JsonSerializable(typeof(CapabilityManifestEnvironment))]
[JsonSerializable(typeof(CapabilityManifestPackages))]
[JsonSerializable(typeof(CapabilityPackageFamily))]
[JsonSerializable(typeof(CapabilityEntry))]
[JsonSerializable(typeof(CapabilityManifestTransports))]
[JsonSerializable(typeof(CapabilityTransportState))]
[JsonSerializable(typeof(CapabilityManifestLimits))]
[JsonSerializable(typeof(CapabilityPreviewLimits))]
[JsonSerializable(typeof(CapabilityQueryLimits))]
[JsonSerializable(typeof(CapabilityAnalysisLimits))]
[JsonSerializable(typeof(CapabilityPublicationLimits))]
[JsonSerializable(typeof(CapabilityJobLimits))]
[JsonSerializable(typeof(CapabilityUploadLimits))]
[JsonSerializable(typeof(CapabilityStreamingLimits))]
[JsonSerializable(typeof(CapabilityEditLimits))]
[JsonSerializable(typeof(CapabilityGeometryLimits))]
[JsonSerializable(typeof(CapabilityAttachmentLimits))]
[JsonSerializable(typeof(CapabilityManifestPolicies))]
[JsonSerializable(typeof(CapabilityEntitlementDecision))]
[JsonSerializable(typeof(CapabilityManifestLink))]
internal sealed partial class CapabilityManifestJsonContext : JsonSerializerContext
{
}
