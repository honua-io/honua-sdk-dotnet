// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Request payload for scanning a migration source inventory.
/// </summary>
public sealed class MigrationInventoryScanRequest
{
    /// <summary>
    /// Source family or alias, such as <c>geoserver</c> or <c>geoservices</c>.
    /// </summary>
    [JsonPropertyName("sourceKind")]
    public string? SourceKind { get; init; }

    /// <summary>
    /// Canonical source URL to scan.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The migration scanner JSON contract represents source URLs as strings.")]
    [JsonPropertyName("sourceUrl")]
    public string? SourceUrl { get; init; }

    /// <summary>
    /// Optional GeoServer basic-auth user name.
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>
    /// Optional GeoServer basic-auth password.
    /// </summary>
    [JsonPropertyName("password")]
    public string? Password { get; init; }

    /// <summary>
    /// Optional scan timeout in seconds.
    /// </summary>
    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// When true, GeoServer scans fetch style content for deeper classification.
    /// </summary>
    [JsonPropertyName("includeStyleContent")]
    public bool? IncludeStyleContent { get; init; }
}

/// <summary>
/// Deterministic planning artifact that describes a scanned migration source.
/// </summary>
public sealed class MigrationSourceInventoryArtifact
{
    /// <summary>
    /// Current source inventory artifact kind.
    /// </summary>
    public const string CurrentArtifactKind = "honua.migration.source-inventory";

    /// <summary>
    /// Current source inventory artifact schema version.
    /// </summary>
    public const string CurrentArtifactVersion = "1.0";

    /// <summary>
    /// Stable artifact kind identifier.
    /// </summary>
    [JsonPropertyName("artifactKind")]
    public string ArtifactKind { get; init; } = CurrentArtifactKind;

    /// <summary>
    /// Artifact schema version.
    /// </summary>
    [JsonPropertyName("artifactVersion")]
    public string ArtifactVersion { get; init; } = CurrentArtifactVersion;

    /// <summary>
    /// Canonical source kind identifier.
    /// </summary>
    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; init; } = string.Empty;

    /// <summary>
    /// Identity and version information for the scanned source.
    /// </summary>
    [JsonPropertyName("source")]
    public MigrationSourceIdentity Source { get; init; } = new();

    /// <summary>
    /// Authentication posture observed during the scan.
    /// </summary>
    [JsonPropertyName("authPosture")]
    public MigrationInventoryAuthPosture AuthPosture { get; init; } = new();

    /// <summary>
    /// Completeness information for the inventory result.
    /// </summary>
    [JsonPropertyName("scanCompleteness")]
    public MigrationInventoryCompleteness ScanCompleteness { get; init; } = new();

    /// <summary>
    /// Aggregate counts for the scanned inventory.
    /// </summary>
    [JsonPropertyName("summary")]
    public MigrationInventorySummary Summary { get; init; } = new();

    /// <summary>
    /// Overall compatibility assessment for the source.
    /// </summary>
    [JsonPropertyName("overallCompatibility")]
    public MigrationCompatibilityAssessment OverallCompatibility { get; init; } = new();

    /// <summary>
    /// Logical containers such as workspaces or services.
    /// </summary>
    [JsonPropertyName("containers")]
    public IReadOnlyList<MigrationInventoryContainer> Containers { get; init; } = [];

    /// <summary>
    /// Data resources such as layers, tables, or layer groups.
    /// </summary>
    [JsonPropertyName("resources")]
    public IReadOnlyList<MigrationInventoryResource> Resources { get; init; } = [];

    /// <summary>
    /// Styles or renderers discovered during the scan.
    /// </summary>
    [JsonPropertyName("styles")]
    public IReadOnlyList<MigrationInventoryStyle> Styles { get; init; } = [];

    /// <summary>
    /// External dependencies referenced by the inventory.
    /// </summary>
    [JsonPropertyName("externalDependencies")]
    public IReadOnlyList<MigrationExternalDependency> ExternalDependencies { get; init; } = [];
}

/// <summary>
/// Identifies a scanned source environment.
/// </summary>
public sealed class MigrationSourceIdentity
{
    /// <summary>
    /// Human-readable source name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Canonical source URL used for the scan.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Migration artifacts preserve the server JSON contract string value.")]
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Source product name, when reported.
    /// </summary>
    [JsonPropertyName("product")]
    public string? Product { get; init; }

    /// <summary>
    /// Source product version, when reported.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Source build or revision, when reported.
    /// </summary>
    [JsonPropertyName("build")]
    public string? Build { get; init; }

    /// <summary>
    /// Protocol subtype or service type, when reported.
    /// </summary>
    [JsonPropertyName("serviceType")]
    public string? ServiceType { get; init; }
}

/// <summary>
/// Authentication posture observed while scanning a source.
/// </summary>
public sealed class MigrationInventoryAuthPosture
{
    /// <summary>
    /// Posture label such as <c>anonymous</c>, <c>basic</c>, or <c>auth-required</c>.
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = string.Empty;

    /// <summary>
    /// Whether a complete credential set was supplied.
    /// </summary>
    [JsonPropertyName("credentialsSupplied")]
    public bool CredentialsSupplied { get; init; }

    /// <summary>
    /// Whether the scan confirmed access.
    /// </summary>
    [JsonPropertyName("accessConfirmed")]
    public bool AccessConfirmed { get; init; }

    /// <summary>
    /// Additional authentication notes.
    /// </summary>
    [JsonPropertyName("notes")]
    public IReadOnlyList<string> Notes { get; init; } = [];
}

/// <summary>
/// Describes how complete a migration inventory scan is.
/// </summary>
public sealed class MigrationInventoryCompleteness
{
    /// <summary>
    /// Completeness status such as <c>complete</c>, <c>partial</c>, or <c>failed</c>.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Warnings that affected completeness.
    /// </summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Inventory areas that could not be scanned.
    /// </summary>
    [JsonPropertyName("missingArtifacts")]
    public IReadOnlyList<string> MissingArtifacts { get; init; } = [];
}

/// <summary>
/// Aggregate counts for an inventory artifact.
/// </summary>
public sealed class MigrationInventorySummary
{
    /// <summary>
    /// Number of discovered containers.
    /// </summary>
    [JsonPropertyName("containerCount")]
    public int ContainerCount { get; init; }

    /// <summary>
    /// Number of discovered resources.
    /// </summary>
    [JsonPropertyName("resourceCount")]
    public int ResourceCount { get; init; }

    /// <summary>
    /// Number of discovered styles or renderers.
    /// </summary>
    [JsonPropertyName("styleCount")]
    public int StyleCount { get; init; }

    /// <summary>
    /// Number of discovered external dependencies.
    /// </summary>
    [JsonPropertyName("externalDependencyCount")]
    public int ExternalDependencyCount { get; init; }

    /// <summary>
    /// Number of compatible items.
    /// </summary>
    [JsonPropertyName("compatibleCount")]
    public int CompatibleCount { get; init; }

    /// <summary>
    /// Number of partially compatible items.
    /// </summary>
    [JsonPropertyName("partiallyCompatibleCount")]
    public int PartiallyCompatibleCount { get; init; }

    /// <summary>
    /// Number of incompatible items.
    /// </summary>
    [JsonPropertyName("incompatibleCount")]
    public int IncompatibleCount { get; init; }
}

/// <summary>
/// Compatibility assessment for a migration artifact item.
/// </summary>
public sealed class MigrationCompatibilityAssessment
{
    /// <summary>
    /// Compatibility level such as <c>compatible</c>, <c>partial</c>, or <c>incompatible</c>.
    /// </summary>
    [JsonPropertyName("level")]
    public string Level { get; init; } = string.Empty;

    /// <summary>
    /// Optional stable machine-readable compatibility code.
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    /// <summary>
    /// Primary explanation for the assigned level.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Compatibility warnings.
    /// </summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Manual steps needed to complete migration.
    /// </summary>
    [JsonPropertyName("manualSteps")]
    public IReadOnlyList<string> ManualSteps { get; init; } = [];
}

/// <summary>
/// Container entry such as a workspace or service.
/// </summary>
public sealed class MigrationInventoryContainer
{
    /// <summary>
    /// Stable artifact-local identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Container kind.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Container name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Optional display title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Optional description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Whether this is the source default container.
    /// </summary>
    [JsonPropertyName("isDefault")]
    public bool? IsDefault { get; init; }

    /// <summary>
    /// Compatibility assessment for the container.
    /// </summary>
    [JsonPropertyName("compatibility")]
    public MigrationCompatibilityAssessment Compatibility { get; init; } = new();
}

/// <summary>
/// Resource entry such as a layer, table, or layer group.
/// </summary>
public sealed class MigrationInventoryResource
{
    /// <summary>
    /// Stable artifact-local identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Parent container identifier.
    /// </summary>
    [JsonPropertyName("containerId")]
    public string ContainerId { get; init; } = string.Empty;

    /// <summary>
    /// Resource kind.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Resource name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Optional display title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Optional description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Geometry type, when the resource carries geometry.
    /// </summary>
    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    /// <summary>
    /// Reported feature count, when available.
    /// </summary>
    [JsonPropertyName("featureCount")]
    public int? FeatureCount { get; init; }

    /// <summary>
    /// Whether the resource advertises attachments.
    /// </summary>
    [JsonPropertyName("hasAttachments")]
    public bool? HasAttachments { get; init; }

    /// <summary>
    /// Advertised resource capabilities.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>
    /// CRS, datum, and unit details relevant to migration planning.
    /// </summary>
    [JsonPropertyName("spatialReferences")]
    public IReadOnlyList<MigrationSpatialReferenceInfo> SpatialReferences { get; init; } = [];

    /// <summary>
    /// Field metadata for resources that advertise a schema.
    /// </summary>
    [JsonPropertyName("fields")]
    public IReadOnlyList<MigrationInventoryField> Fields { get; init; } = [];

    /// <summary>
    /// Related style or renderer identifiers.
    /// </summary>
    [JsonPropertyName("styleIds")]
    public IReadOnlyList<string> StyleIds { get; init; } = [];

    /// <summary>
    /// Related external dependency identifiers.
    /// </summary>
    [JsonPropertyName("externalDependencyIds")]
    public IReadOnlyList<string> ExternalDependencyIds { get; init; } = [];

    /// <summary>
    /// Compatibility assessment for the resource.
    /// </summary>
    [JsonPropertyName("compatibility")]
    public MigrationCompatibilityAssessment Compatibility { get; init; } = new();
}

/// <summary>
/// Field schema entry surfaced for a migration inventory resource.
/// </summary>
public sealed class MigrationInventoryField
{
    /// <summary>
    /// Source-provided field name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Source-provided display alias.
    /// </summary>
    [JsonPropertyName("alias")]
    public string? Alias { get; init; }

    /// <summary>
    /// Source-provided field type token.
    /// </summary>
    [JsonPropertyName("fieldType")]
    public string FieldType { get; init; } = string.Empty;

    /// <summary>
    /// Whether the field is reported nullable.
    /// </summary>
    [JsonPropertyName("nullable")]
    public bool? Nullable { get; init; }

    /// <summary>
    /// Domain category when a domain is attached.
    /// </summary>
    [JsonPropertyName("domainType")]
    public string? DomainType { get; init; }

    /// <summary>
    /// Domain name when a domain is attached.
    /// </summary>
    [JsonPropertyName("domainName")]
    public string? DomainName { get; init; }

    /// <summary>
    /// Coded values for coded-value domains.
    /// </summary>
    [JsonPropertyName("domainValues")]
    public IReadOnlyList<MigrationInventoryCodedValue>? DomainValues { get; init; }
}

/// <summary>
/// Code and display name for a coded-value domain entry.
/// </summary>
public sealed class MigrationInventoryCodedValue
{
    /// <summary>
    /// Coded value as advertised by the source.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Display name for the coded value.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Style or renderer entry discovered from the source.
/// </summary>
public sealed class MigrationInventoryStyle
{
    /// <summary>
    /// Stable artifact-local identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Parent container identifier.
    /// </summary>
    [JsonPropertyName("containerId")]
    public string ContainerId { get; init; } = string.Empty;

    /// <summary>
    /// Style kind such as <c>style</c> or <c>renderer</c>.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Style or renderer name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Style or renderer format.
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }

    /// <summary>
    /// Resources associated with the style or renderer.
    /// </summary>
    [JsonPropertyName("resourceIds")]
    public IReadOnlyList<string> ResourceIds { get; init; } = [];

    /// <summary>
    /// Related external dependency identifiers.
    /// </summary>
    [JsonPropertyName("externalDependencyIds")]
    public IReadOnlyList<string> ExternalDependencyIds { get; init; } = [];

    /// <summary>
    /// Deterministic planning metadata. Raw style documents are not stored here.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// Compatibility assessment for the style or renderer.
    /// </summary>
    [JsonPropertyName("compatibility")]
    public MigrationCompatibilityAssessment Compatibility { get; init; } = new();
}

/// <summary>
/// External dependency discovered during a scan.
/// </summary>
public sealed class MigrationExternalDependency
{
    /// <summary>
    /// Stable artifact-local identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Parent container identifier.
    /// </summary>
    [JsonPropertyName("containerId")]
    public string ContainerId { get; init; } = string.Empty;

    /// <summary>
    /// Optional related resource, style, or renderer identifier.
    /// </summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    /// <summary>
    /// Dependency kind.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Dependency name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Dependency subtype or source-specific type label.
    /// </summary>
    [JsonPropertyName("dependencyType")]
    public string? DependencyType { get; init; }

    /// <summary>
    /// Dependency address or endpoint, when available.
    /// </summary>
    [JsonPropertyName("address")]
    public string? Address { get; init; }

    /// <summary>
    /// Deterministic planning metadata.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// CRS, datum, and unit details relevant to the dependency.
    /// </summary>
    [JsonPropertyName("spatialReferences")]
    public IReadOnlyList<MigrationSpatialReferenceInfo> SpatialReferences { get; init; } = [];

    /// <summary>
    /// Compatibility assessment for the dependency.
    /// </summary>
    [JsonPropertyName("compatibility")]
    public MigrationCompatibilityAssessment Compatibility { get; init; } = new();
}

/// <summary>
/// Spatial reference details extracted for migration planning.
/// </summary>
public sealed class MigrationSpatialReferenceInfo
{
    /// <summary>
    /// Role of the spatial reference entry.
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    /// <summary>
    /// Source-provided CRS text or identifier.
    /// </summary>
    [JsonPropertyName("sourceValue")]
    public string? SourceValue { get; init; }

    /// <summary>
    /// Resolved SRID, when available.
    /// </summary>
    [JsonPropertyName("srid")]
    public int? Srid { get; init; }

    /// <summary>
    /// Canonical CRS URI, when resolved.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Migration artifacts preserve CRS identifiers as contract strings.")]
    [JsonPropertyName("crsUri")]
    public string? CrsUri { get; init; }

    /// <summary>
    /// Datum name, when inferred.
    /// </summary>
    [JsonPropertyName("datum")]
    public string? Datum { get; init; }

    /// <summary>
    /// Linear or angular unit name, when inferred.
    /// </summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    /// <summary>
    /// Axis order label, when known.
    /// </summary>
    [JsonPropertyName("axisOrder")]
    public string? AxisOrder { get; init; }

    /// <summary>
    /// Whether the CRS is geographic.
    /// </summary>
    [JsonPropertyName("isGeographic")]
    public bool? IsGeographic { get; init; }
}

/// <summary>
/// Deterministic artifact that translates inventory into target Honua intent.
/// </summary>
public sealed class MigrationManifestArtifact
{
    /// <summary>
    /// Current migration manifest artifact kind.
    /// </summary>
    public const string CurrentArtifactKind = "honua.migration.manifest";

    /// <summary>
    /// Current migration manifest artifact schema version.
    /// </summary>
    public const string CurrentArtifactVersion = "1.0";

    /// <summary>
    /// Stable artifact kind identifier.
    /// </summary>
    [JsonPropertyName("artifactKind")]
    public string ArtifactKind { get; init; } = CurrentArtifactKind;

    /// <summary>
    /// Artifact schema version.
    /// </summary>
    [JsonPropertyName("artifactVersion")]
    public string ArtifactVersion { get; init; } = CurrentArtifactVersion;

    /// <summary>
    /// Source artifact kind this manifest was translated from.
    /// </summary>
    [JsonPropertyName("sourceArtifactKind")]
    public string SourceArtifactKind { get; init; } = MigrationSourceInventoryArtifact.CurrentArtifactKind;

    /// <summary>
    /// Source artifact version this manifest was translated from.
    /// </summary>
    [JsonPropertyName("sourceArtifactVersion")]
    public string SourceArtifactVersion { get; init; } = MigrationSourceInventoryArtifact.CurrentArtifactVersion;

    /// <summary>
    /// Canonical source kind identifier.
    /// </summary>
    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; init; } = string.Empty;

    /// <summary>
    /// Identity and version information for the scanned source.
    /// </summary>
    [JsonPropertyName("source")]
    public MigrationSourceIdentity Source { get; init; } = new();

    /// <summary>
    /// Deterministic translation summary.
    /// </summary>
    [JsonPropertyName("summary")]
    public MigrationManifestSummary Summary { get; init; } = new();

    /// <summary>
    /// Target resources that can be published or staged.
    /// </summary>
    [JsonPropertyName("targetResources")]
    public IReadOnlyList<MigrationManifestTargetResource> TargetResources { get; init; } = [];

    /// <summary>
    /// Target style actions or manual-review style items.
    /// </summary>
    [JsonPropertyName("styleActions")]
    public IReadOnlyList<MigrationManifestStyleAction> StyleActions { get; init; } = [];

    /// <summary>
    /// Items that require operator review before migration proceeds.
    /// </summary>
    [JsonPropertyName("manualReviewItems")]
    public IReadOnlyList<MigrationManifestReviewItem> ManualReviewItems { get; init; } = [];

    /// <summary>
    /// Items that cannot be translated by this migration slice.
    /// </summary>
    [JsonPropertyName("unsupportedItems")]
    public IReadOnlyList<MigrationManifestReviewItem> UnsupportedItems { get; init; } = [];
}

/// <summary>
/// Aggregate counts for a migration manifest artifact.
/// </summary>
public sealed class MigrationManifestSummary
{
    /// <summary>
    /// Number of source resources considered for translation.
    /// </summary>
    [JsonPropertyName("sourceResourceCount")]
    public int SourceResourceCount { get; init; }

    /// <summary>
    /// Number of target resources emitted into the manifest.
    /// </summary>
    [JsonPropertyName("targetResourceCount")]
    public int TargetResourceCount { get; init; }

    /// <summary>
    /// Number of style actions emitted into the manifest.
    /// </summary>
    [JsonPropertyName("styleActionCount")]
    public int StyleActionCount { get; init; }

    /// <summary>
    /// Number of source items requiring manual review.
    /// </summary>
    [JsonPropertyName("manualReviewCount")]
    public int ManualReviewCount { get; init; }

    /// <summary>
    /// Number of unsupported source items.
    /// </summary>
    [JsonPropertyName("unsupportedCount")]
    public int UnsupportedCount { get; init; }
}

/// <summary>
/// Target resource intent translated from one source inventory resource.
/// </summary>
public sealed class MigrationManifestTargetResource
{
    /// <summary>
    /// Source inventory resource identifier.
    /// </summary>
    [JsonPropertyName("sourceResourceId")]
    public string SourceResourceId { get; init; } = string.Empty;

    /// <summary>
    /// Source inventory resource kind.
    /// </summary>
    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; init; } = string.Empty;

    /// <summary>
    /// Target migration action, such as <c>publish</c> or <c>manual-review</c>.
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Target service name suggested for this resource.
    /// </summary>
    [JsonPropertyName("targetServiceName")]
    public string TargetServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Target resource name suggested for this resource.
    /// </summary>
    [JsonPropertyName("targetResourceName")]
    public string TargetResourceName { get; init; } = string.Empty;

    /// <summary>
    /// Geometry type copied from the inventory, when available.
    /// </summary>
    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    /// <summary>
    /// Field schema copied from the source inventory.
    /// </summary>
    [JsonPropertyName("fields")]
    public IReadOnlyList<MigrationInventoryField> Fields { get; init; } = [];

    /// <summary>
    /// Source capabilities copied for operator review.
    /// </summary>
    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>
    /// Source spatial references copied for operator review.
    /// </summary>
    [JsonPropertyName("spatialReferences")]
    public IReadOnlyList<MigrationSpatialReferenceInfo> SpatialReferences { get; init; } = [];

    /// <summary>
    /// Related style identifiers from the source inventory.
    /// </summary>
    [JsonPropertyName("styleIds")]
    public IReadOnlyList<string> StyleIds { get; init; } = [];

    /// <summary>
    /// Related external dependency identifiers from the source inventory.
    /// </summary>
    [JsonPropertyName("externalDependencyIds")]
    public IReadOnlyList<string> ExternalDependencyIds { get; init; } = [];

    /// <summary>
    /// Compatibility assessment that justified the target action.
    /// </summary>
    [JsonPropertyName("compatibility")]
    public MigrationCompatibilityAssessment Compatibility { get; init; } = new();
}

/// <summary>
/// Target style action translated from a source style or renderer.
/// </summary>
public sealed class MigrationManifestStyleAction
{
    /// <summary>
    /// Source style identifier.
    /// </summary>
    [JsonPropertyName("sourceStyleId")]
    public string SourceStyleId { get; init; } = string.Empty;

    /// <summary>
    /// Target style action.
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; init; } = string.Empty;

    /// <summary>
    /// Source style format.
    /// </summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }

    /// <summary>
    /// Related source resource identifiers.
    /// </summary>
    [JsonPropertyName("resourceIds")]
    public IReadOnlyList<string> ResourceIds { get; init; } = [];

    /// <summary>
    /// External dependencies that must be resolved before style migration.
    /// </summary>
    [JsonPropertyName("externalDependencyIds")]
    public IReadOnlyList<string> ExternalDependencyIds { get; init; } = [];

    /// <summary>
    /// Compatibility assessment that justified the target action.
    /// </summary>
    [JsonPropertyName("compatibility")]
    public MigrationCompatibilityAssessment Compatibility { get; init; } = new();
}

/// <summary>
/// Review or unsupported item emitted during manifest translation.
/// </summary>
public sealed class MigrationManifestReviewItem
{
    /// <summary>
    /// Source artifact identifier for the item.
    /// </summary>
    [JsonPropertyName("sourceId")]
    public string SourceId { get; init; } = string.Empty;

    /// <summary>
    /// Source item kind.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Stable machine-readable code.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Review severity such as <c>manual-review</c> or <c>unsupported</c>.
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable reason.
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Operator remediation guidance.
    /// </summary>
    [JsonPropertyName("manualSteps")]
    public IReadOnlyList<string> ManualSteps { get; init; } = [];

    /// <summary>
    /// Warnings copied from compatibility assessment.
    /// </summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Options for translating source inventory into a migration manifest.
/// </summary>
public sealed class MigrationManifestTranslationOptions
{
    /// <summary>
    /// Optional target service name for translated resources.
    /// </summary>
    [JsonPropertyName("targetServiceName")]
    public string? TargetServiceName { get; init; }
}

/// <summary>
/// Stable state values used by migration parity and cutover-readiness artifacts.
/// </summary>
public static class MigrationEvidenceStates
{
    /// <summary>
    /// Evidence is present and satisfies the check.
    /// </summary>
    public const string Pass = "pass";

    /// <summary>
    /// Evidence is present and shows the check failed.
    /// </summary>
    public const string Fail = "fail";

    /// <summary>
    /// Evidence is missing or not yet reviewed.
    /// </summary>
    public const string Unknown = "unknown";

    /// <summary>
    /// The check is not applicable to this migration.
    /// </summary>
    public const string NotApplicable = "not-applicable";
}

/// <summary>
/// Technical signoff artifact for a migration pilot or cutover review.
/// </summary>
public sealed class MigrationParityEvidenceArtifact
{
    /// <summary>
    /// Current parity evidence pack artifact kind.
    /// </summary>
    public const string CurrentArtifactKind = "honua.migration.parity-evidence-pack";

    /// <summary>
    /// Current parity evidence pack artifact schema version.
    /// </summary>
    public const string CurrentArtifactVersion = "1.0";

    /// <summary>
    /// Stable artifact kind identifier.
    /// </summary>
    [JsonPropertyName("artifactKind")]
    public string ArtifactKind { get; init; } = CurrentArtifactKind;

    /// <summary>
    /// Artifact schema version.
    /// </summary>
    [JsonPropertyName("artifactVersion")]
    public string ArtifactVersion { get; init; } = CurrentArtifactVersion;

    /// <summary>
    /// Canonical source kind identifier.
    /// </summary>
    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; init; } = string.Empty;

    /// <summary>
    /// Identity and version information for the scanned source.
    /// </summary>
    [JsonPropertyName("source")]
    public MigrationSourceIdentity Source { get; init; } = new();

    /// <summary>
    /// Overall state across capability, style, data, and readiness sections.
    /// </summary>
    [JsonPropertyName("overallState")]
    public string OverallState { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable signoff summary.
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Whether a migration manifest was available as evidence input.
    /// </summary>
    [JsonPropertyName("manifestAvailable")]
    public bool ManifestAvailable { get; init; }

    /// <summary>
    /// Evidence sections grouped by review category.
    /// </summary>
    [JsonPropertyName("sections")]
    public IReadOnlyList<MigrationParityEvidenceSection> Sections { get; init; } = [];

    /// <summary>
    /// Cutover-readiness checklist and aggregate state.
    /// </summary>
    [JsonPropertyName("cutoverReadiness")]
    public MigrationCutoverReadinessSummary CutoverReadiness { get; init; } = new();
}

/// <summary>
/// Group of parity evidence items for one review category.
/// </summary>
public sealed class MigrationParityEvidenceSection
{
    /// <summary>
    /// Stable section identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Section display title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Aggregate state for the section.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Evidence items in deterministic order.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<MigrationParityEvidenceItem> Items { get; init; } = [];
}

/// <summary>
/// Individual evidence item inside a parity section.
/// </summary>
public sealed class MigrationParityEvidenceItem
{
    /// <summary>
    /// Stable item identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Item state: <c>pass</c>, <c>fail</c>, <c>unknown</c>, or <c>not-applicable</c>.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Short item summary.
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Evidence text supporting the assigned state.
    /// </summary>
    [JsonPropertyName("evidence")]
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>
    /// Remediation guidance for fail or unknown states.
    /// </summary>
    [JsonPropertyName("remediation")]
    public IReadOnlyList<string> Remediation { get; init; } = [];

    /// <summary>
    /// Related manifest or inventory identifiers.
    /// </summary>
    [JsonPropertyName("relatedIds")]
    public IReadOnlyList<string> RelatedIds { get; init; } = [];
}

/// <summary>
/// Cutover-readiness checklist and aggregate state.
/// </summary>
public sealed class MigrationCutoverReadinessSummary
{
    /// <summary>
    /// Aggregate readiness state.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Checklist items in deterministic order.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<MigrationCutoverReadinessItem> Items { get; init; } = [];
}

/// <summary>
/// Individual cutover-readiness checklist item.
/// </summary>
public sealed class MigrationCutoverReadinessItem
{
    /// <summary>
    /// Stable checklist item identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Checklist item title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Item state: <c>pass</c>, <c>fail</c>, <c>unknown</c>, or <c>not-applicable</c>.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Evidence supplied by the operator or generated report.
    /// </summary>
    [JsonPropertyName("evidence")]
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>
    /// Remediation guidance for fail or unknown states.
    /// </summary>
    [JsonPropertyName("remediation")]
    public IReadOnlyList<string> Remediation { get; init; } = [];

    /// <summary>
    /// Optional owner responsible for closing the item.
    /// </summary>
    [JsonPropertyName("owner")]
    public string? Owner { get; init; }
}

/// <summary>
/// Operator-supplied readiness attestations.
/// </summary>
public sealed class MigrationReadinessAttestation
{
    /// <summary>
    /// Checklist item attestations.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<MigrationReadinessAttestationItem> Items { get; init; } = [];
}

/// <summary>
/// Operator-supplied state for one readiness checklist item.
/// </summary>
public sealed class MigrationReadinessAttestationItem
{
    /// <summary>
    /// Stable checklist item identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Attested state.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    /// <summary>
    /// Evidence supporting the attested state.
    /// </summary>
    [JsonPropertyName("evidence")]
    public IReadOnlyList<string> Evidence { get; init; } = [];

    /// <summary>
    /// Optional owner responsible for the item.
    /// </summary>
    [JsonPropertyName("owner")]
    public string? Owner { get; init; }
}
