// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Studio.Capabilities;

/// <summary>
/// Client projection of the server capability manifest returned by
/// <c>GET /api/v1/capabilities/manifest</c> (schema
/// <c>honua.capability_manifest.v1</c>). Lets a Console/MCP/QGIS client gate UI
/// and tool exposure on what the connected server actually supports for the
/// current tenant, workspace, environment, and caller policy scope.
/// </summary>
/// <remarks>
/// This shape mirrors the frozen server wire contract and is the .NET analogue
/// of the honua-sdk-js <c>StudioCapabilityManifest</c> projection. The JS
/// projection is a looser subset; the query helpers (<see cref="GetCapability"/>,
/// <see cref="IsAvailable"/>, <see cref="IsSupported"/>, <see cref="GetReasonCode"/>)
/// provide the same semantics as the JS <c>getCapability</c>/<c>hasCapability</c>
/// helpers.
/// </remarks>
public sealed record CapabilityManifest
{
    /// <summary>Manifest schema version, for example <c>honua.capability_manifest.v1</c>.</summary>
    public string SchemaVersion { get; init; } = "honua.capability_manifest.v1";

    /// <summary>Timestamp when the manifest was generated.</summary>
    public DateTimeOffset IssuedAt { get; init; }

    /// <summary>Tenant, workspace, environment, and authentication scope the manifest was generated for.</summary>
    public CapabilityManifestScope? Scope { get; init; }

    /// <summary>Server and API version information.</summary>
    public CapabilityManifestServerInfo? Server { get; init; }

    /// <summary>Environment (metadata snapshot) availability state.</summary>
    public CapabilityManifestEnvironment? Environment { get; init; }

    /// <summary>Package schema versions and family support state.</summary>
    public CapabilityManifestPackages? Packages { get; init; }

    /// <summary>Advertised capability entries.</summary>
    public IReadOnlyList<CapabilityEntry> Capabilities { get; init; } = Array.Empty<CapabilityEntry>();

    /// <summary>Transport availability (HTTP, gRPC, gRPC-Web) and mTLS state.</summary>
    public CapabilityManifestTransports? Transports { get; init; }

    /// <summary>Operational limits advertised by the server.</summary>
    public CapabilityManifestLimits? Limits { get; init; }

    /// <summary>Edition, license, and entitlement policy state.</summary>
    public CapabilityManifestPolicies? Policies { get; init; }

    /// <summary>Related resource links.</summary>
    public IReadOnlyList<CapabilityManifestLink> Links { get; init; } = Array.Empty<CapabilityManifestLink>();

    /// <summary>
    /// Returns the capability entry with the given id, or <see langword="null"/>
    /// when the manifest does not advertise it (regardless of support/availability).
    /// Mirrors the JS <c>getCapability</c> helper.
    /// </summary>
    /// <param name="id">Capability identifier.</param>
    public CapabilityEntry? GetCapability(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        for (var i = 0; i < Capabilities.Count; i++)
        {
            if (string.Equals(Capabilities[i].Id, id, StringComparison.Ordinal))
            {
                return Capabilities[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Returns <see langword="true"/> when the manifest advertises the capability
    /// as available in the current scope. Mirrors the JS <c>hasCapability</c>
    /// helper (JS <c>enabled</c> maps to the server <c>available</c> flag).
    /// </summary>
    /// <param name="id">Capability identifier.</param>
    public bool IsAvailable(string id) => GetCapability(id)?.Available == true;

    /// <summary>
    /// Returns <see langword="true"/> when the server implements the capability at
    /// all, even if it is not currently available (for example gated by edition or
    /// entitlement). Inspect <see cref="GetReasonCode"/> to explain an unavailable
    /// but supported capability.
    /// </summary>
    /// <param name="id">Capability identifier.</param>
    public bool IsSupported(string id) => GetCapability(id)?.Supported == true;

    /// <summary>
    /// Returns the machine-friendly reason code explaining a capability's
    /// availability state, or <see langword="null"/> when unknown or the
    /// capability is unrecognized.
    /// </summary>
    /// <param name="id">Capability identifier.</param>
    public string? GetReasonCode(string id) => GetCapability(id)?.ReasonCode;

    /// <summary>
    /// Returns <see langword="true"/> when the server advertises a supported
    /// package family with the given identifier (for example <c>map</c>).
    /// </summary>
    /// <param name="familyId">Package family identifier.</param>
    public bool HasPackageFamily(string familyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyId);
        var families = Packages?.Families;
        if (families is null)
        {
            return false;
        }

        for (var i = 0; i < families.Count; i++)
        {
            if (families[i].Supported &&
                string.Equals(families[i].Id, familyId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Tenant, workspace, environment, and authentication scope of a capability manifest.</summary>
public sealed record CapabilityManifestScope
{
    /// <summary>Resolved tenant identifier, when known.</summary>
    public string? TenantId { get; init; }

    /// <summary>How the tenant was resolved.</summary>
    public string? TenantSource { get; init; }

    /// <summary>Requested environment identifier, when supplied.</summary>
    public string? Environment { get; init; }

    /// <summary>Resolved workspace identifier, when known.</summary>
    public string? WorkspaceId { get; init; }

    /// <summary>Whether a workspace was available for the request.</summary>
    public bool WorkspaceAvailable { get; init; }

    /// <summary>Reason code explaining an unavailable workspace, when applicable.</summary>
    public string? WorkspaceReasonCode { get; init; }

    /// <summary>Whether the caller was authenticated.</summary>
    public bool Authenticated { get; init; }
}

/// <summary>Server and API version information carried by a capability manifest.</summary>
public sealed record CapabilityManifestServerInfo
{
    /// <summary>Server build version.</summary>
    public string? ServerVersion { get; init; }

    /// <summary>Public API version.</summary>
    public string? ApiVersion { get; init; }

    /// <summary>Metadata API version.</summary>
    public string? MetadataApiVersion { get; init; }

    /// <summary>Metadata schema version.</summary>
    public string? MetadataSchemaVersion { get; init; }

    /// <summary>Deployment environment label, for example <c>production</c>.</summary>
    public string? DeploymentEnvironment { get; init; }
}

/// <summary>Environment (metadata snapshot) availability state for a capability manifest.</summary>
public sealed record CapabilityManifestEnvironment
{
    /// <summary>Resolved environment identifier, when known.</summary>
    public string? EnvironmentId { get; init; }

    /// <summary>Whether a specific environment was requested.</summary>
    public bool Requested { get; init; }

    /// <summary>Whether the environment is available.</summary>
    public bool Available { get; init; }

    /// <summary>Reason code explaining an unavailable environment, when applicable.</summary>
    public string? ReasonCode { get; init; }

    /// <summary>Environment revision, when known.</summary>
    public long? Revision { get; init; }

    /// <summary>Timestamp when the environment snapshot was loaded, when known.</summary>
    public DateTimeOffset? LoadedAt { get; init; }
}

/// <summary>Package schema versions and family support state for a capability manifest.</summary>
public sealed record CapabilityManifestPackages
{
    /// <summary>Supported package schema versions.</summary>
    public IReadOnlyList<string> SchemaVersions { get; init; } = Array.Empty<string>();

    /// <summary>Package families, including unsupported families.</summary>
    public IReadOnlyList<CapabilityPackageFamily> Families { get; init; } = Array.Empty<CapabilityPackageFamily>();

    /// <summary>Families that can be persisted through the Studio lifecycle.</summary>
    public IReadOnlyList<string> StorageFamilies { get; init; } = Array.Empty<string>();

    /// <summary>Families that can be published.</summary>
    public IReadOnlyList<string> PublicationFamilies { get; init; } = Array.Empty<string>();
}

/// <summary>One package family advertised by a capability manifest.</summary>
public sealed record CapabilityPackageFamily
{
    /// <summary>Family identifier, for example <c>map</c>.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Family kind classifier.</summary>
    public string? Kind { get; init; }

    /// <summary>Current schema version for the family.</summary>
    public string? SchemaVersion { get; init; }

    /// <summary>Whether the family is supported in this deployment.</summary>
    public bool Supported { get; init; }
}

/// <summary>
/// One advertised capability entry. Mirrors the frozen server
/// <c>honua.capability_manifest.v1</c> capability shape and the honua-sdk-js
/// <c>StudioCapabilityEntry</c> projection (JS <c>enabled</c> maps to
/// <see cref="Available"/>).
/// </summary>
public sealed record CapabilityEntry
{
    private string _lifecycle = string.Empty;

    /// <summary>Stable capability identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Capability category.</summary>
    public string? Category { get; init; }

    /// <summary>
    /// Server-owned governance lifecycle (for example <c>Implemented</c>, <c>Preview</c>,
    /// or <c>Experimental</c>). Unknown future values are preserved verbatim.
    /// </summary>
    [JsonRequired]
    public string Lifecycle
    {
        get => _lifecycle;
        init => _lifecycle = value ?? throw new JsonException("Capability lifecycle cannot be null.");
    }

    /// <summary>Whether server governance requires explicit opt-in before use.</summary>
    [JsonRequired]
    public bool OptInRequired { get; init; }

    /// <summary>Whether the server implements the capability at all.</summary>
    public bool Supported { get; init; }

    /// <summary>Whether the capability is available in the current scope.</summary>
    public bool Available { get; init; }

    /// <summary>Machine-friendly reason code explaining the availability state.</summary>
    public string? ReasonCode { get; init; }

    /// <summary>Primary entitlement key gating the capability, when applicable.</summary>
    public string? EntitlementKey { get; init; }

    /// <summary>All entitlement keys gating the capability, when applicable.</summary>
    public IReadOnlyList<string>? EntitlementKeys { get; init; }

    /// <summary>Minimum edition required for the capability, when applicable.</summary>
    public string? MinimumEdition { get; init; }

    /// <summary>Localization message key describing the capability state, when applicable.</summary>
    public string? MessageKey { get; init; }
}

/// <summary>Transport availability and mTLS state for a capability manifest.</summary>
public sealed record CapabilityManifestTransports
{
    /// <summary>Per-transport availability states.</summary>
    public IReadOnlyList<CapabilityTransportState> Items { get; init; } = Array.Empty<CapabilityTransportState>();

    /// <summary>Mutual-TLS mode, for example <c>disabled</c> or <c>required</c>.</summary>
    public string? MtlsMode { get; init; }

    /// <summary>Whether forwarded client-certificate headers are honored.</summary>
    public bool ForwardedClientCertificateEnabled { get; init; }
}

/// <summary>Availability state of one transport (for example HTTP, gRPC, gRPC-Web).</summary>
public sealed record CapabilityTransportState
{
    /// <summary>Transport identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Whether the transport is supported by the build.</summary>
    public bool Supported { get; init; }

    /// <summary>Whether the transport is available in the current deployment.</summary>
    public bool Available { get; init; }

    /// <summary>Reason code explaining an unavailable transport, when applicable.</summary>
    public string? ReasonCode { get; init; }

    /// <summary>Localization message key describing the transport state, when applicable.</summary>
    public string? MessageKey { get; init; }
}

/// <summary>Operational limits advertised by a capability manifest.</summary>
public sealed record CapabilityManifestLimits
{
    /// <summary>Preview limits.</summary>
    public CapabilityPreviewLimits? Preview { get; init; }

    /// <summary>Query limits.</summary>
    public CapabilityQueryLimits? Query { get; init; }

    /// <summary>Analysis limits.</summary>
    public CapabilityAnalysisLimits? Analysis { get; init; }

    /// <summary>Publication limits.</summary>
    public CapabilityPublicationLimits? Publication { get; init; }

    /// <summary>Job limits.</summary>
    public CapabilityJobLimits? Job { get; init; }

    /// <summary>Upload limits.</summary>
    public CapabilityUploadLimits? Upload { get; init; }

    /// <summary>Streaming limits.</summary>
    public CapabilityStreamingLimits? Streaming { get; init; }

    /// <summary>Edit limits.</summary>
    public CapabilityEditLimits? Edit { get; init; }

    /// <summary>Geometry limits.</summary>
    public CapabilityGeometryLimits? Geometry { get; init; }

    /// <summary>Attachment limits.</summary>
    public CapabilityAttachmentLimits? Attachment { get; init; }
}

/// <summary>Preview limits advertised by a capability manifest.</summary>
public sealed record CapabilityPreviewLimits
{
    /// <summary>Maximum preview payload size in bytes.</summary>
    public long MaxPreviewSizeBytes { get; init; }

    /// <summary>Maximum number of preview features.</summary>
    public int MaxPreviewFeatures { get; init; }

    /// <summary>Maximum number of features scanned for a preview count.</summary>
    public int MaxPreviewCountScan { get; init; }
}

/// <summary>Query limits advertised by a capability manifest.</summary>
public sealed record CapabilityQueryLimits
{
    /// <summary>Default record count for a query.</summary>
    public int DefaultRecordCount { get; init; }

    /// <summary>Maximum record count for a query.</summary>
    public int MaxRecordCount { get; init; }

    /// <summary>Maximum number of features returnable by a query.</summary>
    public int MaxFeatures { get; init; }

    /// <summary>Maximum page size.</summary>
    public int MaxPageSize { get; init; }

    /// <summary>Query timeout in seconds.</summary>
    public int QueryTimeoutSeconds { get; init; }

    /// <summary>Maximum bounding-box area in square kilometers.</summary>
    public double MaxBboxAreaSqKm { get; init; }

    /// <summary>Maximum filter nesting depth.</summary>
    public int MaxFilterDepth { get; init; }

    /// <summary>Maximum number of spatial operations per query.</summary>
    public int MaxSpatialOperations { get; init; }
}

/// <summary>Analysis limits advertised by a capability manifest.</summary>
public sealed record CapabilityAnalysisLimits
{
    /// <summary>Maximum number of input features.</summary>
    public int MaxInputFeatures { get; init; }

    /// <summary>Maximum number of clusters.</summary>
    public int MaxClusters { get; init; }

    /// <summary>Maximum DBSCAN epsilon in meters.</summary>
    public double MaxDbscanEpsMeters { get; init; }

    /// <summary>Maximum k for k-means.</summary>
    public int MaxKMeansK { get; init; }

    /// <summary>Maximum buffer distance in meters.</summary>
    public double MaxBufferDistanceMeters { get; init; }

    /// <summary>Minimum density cell size in meters.</summary>
    public double MinDensityCellSizeMeters { get; init; }

    /// <summary>Maximum density cell size in meters.</summary>
    public double MaxDensityCellSizeMeters { get; init; }

    /// <summary>Maximum number of density cells.</summary>
    public int MaxDensityCells { get; init; }

    /// <summary>Maximum d-within distance in meters.</summary>
    public double MaxDWithinDistanceMeters { get; init; }

    /// <summary>Maximum number of H3 cells per query.</summary>
    public int MaxH3CellsPerQuery { get; init; }

    /// <summary>Maximum number of spatial operations.</summary>
    public int MaxSpatialOperations { get; init; }

    /// <summary>Maximum number of joins.</summary>
    public int MaxJoins { get; init; }
}

/// <summary>Publication limits advertised by a capability manifest.</summary>
public sealed record CapabilityPublicationLimits
{
    /// <summary>Number of configured deploy targets.</summary>
    public int ConfiguredDeployTargetCount { get; init; }

    /// <summary>Whether GitOps manifest export is supported.</summary>
    public bool GitOpsManifestExportSupported { get; init; }
}

/// <summary>Job limits advertised by a capability manifest.</summary>
public sealed record CapabilityJobLimits
{
    /// <summary>Number of configured workloads.</summary>
    public int ConfiguredWorkloadCount { get; init; }

    /// <summary>Number of available job backends.</summary>
    public int AvailableBackendCount { get; init; }

    /// <summary>Whether job cancellation is supported.</summary>
    public bool SupportsCancellation { get; init; }

    /// <summary>Whether job progress polling is supported.</summary>
    public bool SupportsProgressPolling { get; init; }
}

/// <summary>Upload limits advertised by a capability manifest.</summary>
public sealed record CapabilityUploadLimits
{
    /// <summary>Maximum total upload size in bytes.</summary>
    public long MaxUploadSizeBytes { get; init; }

    /// <summary>Maximum single-file size in bytes.</summary>
    public long MaxFileSizeBytes { get; init; }

    /// <summary>Maximum number of concurrent uploads.</summary>
    public int MaxConcurrentUploads { get; init; }

    /// <summary>Maximum number of queued uploads.</summary>
    public int MaxQueuedUploads { get; init; }

    /// <summary>Maximum size scanned for security in bytes.</summary>
    public long MaxSecurityScanSizeBytes { get; init; }
}

/// <summary>Streaming limits advertised by a capability manifest.</summary>
public sealed record CapabilityStreamingLimits
{
    /// <summary>Maximum number of concurrent streaming sessions.</summary>
    public int MaxConcurrentSessions { get; init; }

    /// <summary>Maximum buffer per connection.</summary>
    public int MaxBufferPerConnection { get; init; }

    /// <summary>Maximum subscriptions per session.</summary>
    public int MaxSubscriptionsPerSession { get; init; }

    /// <summary>Maximum subscription identifier length.</summary>
    public int MaxSubscriptionIdLength { get; init; }

    /// <summary>Maximum control-frame size in bytes.</summary>
    public int MaxControlFrameBytes { get; init; }

    /// <summary>Cursor retention limit.</summary>
    public int CursorRetentionLimit { get; init; }

    /// <summary>Heartbeat interval in seconds.</summary>
    public double HeartbeatIntervalSeconds { get; init; }

    /// <summary>gRPC stream batch size.</summary>
    public int GrpcStreamBatchSize { get; init; }
}

/// <summary>Edit limits advertised by a capability manifest.</summary>
public sealed record CapabilityEditLimits
{
    /// <summary>Maximum number of features per edit.</summary>
    public int MaxFeaturesPerEdit { get; init; }

    /// <summary>Maximum number of edits per transaction.</summary>
    public int MaxEditsPerTransaction { get; init; }

    /// <summary>Maximum edit payload size in bytes.</summary>
    public long MaxPayloadSizeBytes { get; init; }
}

/// <summary>Geometry limits advertised by a capability manifest.</summary>
public sealed record CapabilityGeometryLimits
{
    /// <summary>Maximum number of vertices per geometry.</summary>
    public int MaxVerticesPerGeometry { get; init; }

    /// <summary>Maximum geometry size in bytes.</summary>
    public long MaxGeometrySizeBytes { get; init; }

    /// <summary>Maximum coordinate precision.</summary>
    public int MaxCoordinatePrecision { get; init; }
}

/// <summary>Attachment limits advertised by a capability manifest.</summary>
public sealed record CapabilityAttachmentLimits
{
    /// <summary>Maximum number of attachments per feature.</summary>
    public int MaxAttachmentsPerFeature { get; init; }

    /// <summary>Maximum attachment size in bytes.</summary>
    public long MaxAttachmentSizeBytes { get; init; }
}

/// <summary>Edition, license, and entitlement policy state for a capability manifest.</summary>
public sealed record CapabilityManifestPolicies
{
    /// <summary>Current server edition.</summary>
    public string? CurrentEdition { get; init; }

    /// <summary>License validation state.</summary>
    public string? LicenseValidationState { get; init; }

    /// <summary>Whether the license is valid.</summary>
    public bool LicenseValid { get; init; }

    /// <summary>Capabilities granted to the caller.</summary>
    public IReadOnlyList<string> CallerCapabilities { get; init; } = Array.Empty<string>();

    /// <summary>Per-entitlement decisions.</summary>
    public IReadOnlyList<CapabilityEntitlementDecision> Entitlements { get; init; } = Array.Empty<CapabilityEntitlementDecision>();

    /// <summary>Human-readable authorization notice.</summary>
    public string? AuthorizationNotice { get; init; }
}

/// <summary>One entitlement decision advertised by a capability manifest.</summary>
public sealed record CapabilityEntitlementDecision
{
    /// <summary>Entitlement key.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Whether the entitlement is active.</summary>
    public bool Active { get; init; }

    /// <summary>Minimum edition required for the entitlement.</summary>
    public string? MinimumEdition { get; init; }

    /// <summary>Reason code explaining an inactive entitlement, when applicable.</summary>
    public string? ReasonCode { get; init; }
}

/// <summary>A related resource link advertised by a capability manifest.</summary>
public sealed record CapabilityManifestLink
{
    /// <summary>Link relation.</summary>
    public string Rel { get; init; } = string.Empty;

    /// <summary>Link target.</summary>
    public string Href { get; init; } = string.Empty;

    /// <summary>Media type of the link target.</summary>
    [JsonPropertyName("type")]
    public string? MediaType { get; init; }
}
