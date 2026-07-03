// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Studio.Packages;

/// <summary>
/// Shared package envelope for every Studio-authored artifact family. Mirrors the
/// server <c>StudioPackageEnvelope</c>. The <see cref="Bindings"/>,
/// <see cref="Dependencies"/>, and <see cref="Provenance"/> collections are
/// always serialized as arrays (never <see langword="null"/>) — the server
/// rejects a null for any of them.
/// </summary>
public sealed record StudioPackageEnvelope
{
    /// <summary>Package family.</summary>
    public required StudioPackageFamily Family { get; init; }

    /// <summary>Schema version for the family envelope.</summary>
    public required string SchemaVersion { get; init; }

    /// <summary>Family-specific package format advertised by package-family capabilities.</summary>
    public string? Format { get; init; }

    /// <summary>Data, service, content, field, CRS, unit, or permission bindings.</summary>
    public IReadOnlyList<StudioPackageBinding> Bindings { get; init; } = Array.Empty<StudioPackageBinding>();

    /// <summary>Lineage, runtime, item, version, job, or artifact dependencies.</summary>
    public IReadOnlyList<StudioPackageDependency> Dependencies { get; init; } = Array.Empty<StudioPackageDependency>();

    /// <summary>Last validation summary known for the package envelope.</summary>
    public StudioValidationSummary Validation { get; init; } = StudioValidationSummary.NotValidated;

    /// <summary>Publication route, visibility, embed, service, schedule, or job intent.</summary>
    public StudioPublicationIntent? PublicationIntent { get; init; }

    /// <summary>Prompt, tool, source, audit, or generated-artifact provenance references.</summary>
    public IReadOnlyList<StudioProvenanceRef> Provenance { get; init; } = Array.Empty<StudioProvenanceRef>();

    /// <summary>Family-specific JSON payload.</summary>
    public JsonElement? Body { get; init; }
}

/// <summary>One binding declared by a Studio package.</summary>
public sealed record StudioPackageBinding
{
    /// <summary>Stable binding key within the package.</summary>
    public required string Key { get; init; }

    /// <summary>Binding kind, such as data-source, content-item, layer, field, route, or permission.</summary>
    public required string Kind { get; init; }

    /// <summary>Referenced item, resource, field, route, or permission identifier.</summary>
    public required string Ref { get; init; }

    /// <summary>Optional CRS identifier, such as EPSG:4326.</summary>
    public string? Crs { get; init; }

    /// <summary>Optional integer spatial reference identifier.</summary>
    public int? Srid { get; init; }

    /// <summary>Optional units associated with this binding.</summary>
    public string? Units { get; init; }

    /// <summary>Permissions required to use this binding.</summary>
    public IReadOnlyList<string> RequiredPermissions { get; init; } = Array.Empty<string>();

    /// <summary>Additional binding metadata.</summary>
    public JsonElement? Metadata { get; init; }
}

/// <summary>One dependency declared by a Studio package.</summary>
public sealed record StudioPackageDependency
{
    /// <summary>Referenced dependency identifier.</summary>
    public required string Ref { get; init; }

    /// <summary>Dependency kind, such as content-item, content-version, service, job, or artifact.</summary>
    public required string Kind { get; init; }

    /// <summary>Optional immutable dependency version identifier.</summary>
    public string? VersionId { get; init; }

    /// <summary>True when the dependency is required at runtime.</summary>
    public bool Required { get; init; } = true;

    /// <summary>Additional dependency metadata.</summary>
    public JsonElement? Metadata { get; init; }
}

/// <summary>
/// Validation summary attached to drafts, versions, and publication requests.
/// The .NET analogue of the honua-sdk-js <c>StudioPackageValidationResponse</c>
/// envelope.
/// </summary>
public sealed record StudioValidationSummary
{
    /// <summary>Shared not-validated summary.</summary>
    public static StudioValidationSummary NotValidated { get; } = new()
    {
        Status = StudioPackageValidationStatus.NotValidated,
        GeneratedAt = null,
    };

    /// <summary>Validation status.</summary>
    public required StudioPackageValidationStatus Status { get; init; }

    /// <summary>Validation diagnostics.</summary>
    public IReadOnlyList<StudioValidationDiagnostic> Diagnostics { get; init; } = Array.Empty<StudioValidationDiagnostic>();

    /// <summary>Capabilities that are unsupported or limited for the package.</summary>
    public IReadOnlyList<string> UnsupportedCapabilities { get; init; } = Array.Empty<string>();

    /// <summary>Timestamp when the summary was generated.</summary>
    public DateTimeOffset? GeneratedAt { get; init; }
}

/// <summary>One validation diagnostic produced for a package envelope.</summary>
public sealed record StudioValidationDiagnostic
{
    /// <summary>Machine-friendly diagnostic code.</summary>
    public required string Code { get; init; }

    /// <summary>Diagnostic severity.</summary>
    public required StudioPackageDiagnosticSeverity Severity { get; init; }

    /// <summary>JSON pointer or field path associated with the diagnostic.</summary>
    public string? Path { get; init; }

    /// <summary>Human-readable diagnostic message.</summary>
    public required string Message { get; init; }
}

/// <summary>Publication intent declared by a Studio package.</summary>
public sealed record StudioPublicationIntent
{
    /// <summary>Optional target route key.</summary>
    public string? Route { get; init; }

    /// <summary>Optional visibility target.</summary>
    public string? Visibility { get; init; }

    /// <summary>True when embedding should be enabled for the published package.</summary>
    public bool? Embed { get; init; }

    /// <summary>Optional service publication hint.</summary>
    public string? Service { get; init; }

    /// <summary>Optional schedule expression or key.</summary>
    public string? Schedule { get; init; }

    /// <summary>Optional job publication hint.</summary>
    public string? Job { get; init; }

    /// <summary>Additional publication metadata.</summary>
    public JsonElement? Metadata { get; init; }
}

/// <summary>Provenance reference attached to a Studio package.</summary>
public sealed record StudioProvenanceRef
{
    /// <summary>Provenance kind, such as prompt, tool, source, audit, or generated-by.</summary>
    public required string Kind { get; init; }

    /// <summary>Referenced provenance identifier.</summary>
    public required string Ref { get; init; }

    /// <summary>Relationship to the package.</summary>
    public required string Rel { get; init; }

    /// <summary>Actor associated with the provenance reference.</summary>
    public string? ActorId { get; init; }

    /// <summary>Timestamp associated with the provenance reference.</summary>
    public DateTimeOffset? Timestamp { get; init; }
}

/// <summary>Capability descriptor for one Studio package family.</summary>
public sealed record StudioPackageFamilyDescriptor
{
    /// <summary>Package family.</summary>
    public required StudioPackageFamily Family { get; init; }

    /// <summary>Current schema version for the family.</summary>
    public required string CurrentSchemaVersion { get; init; }

    /// <summary>Canonical family-specific format identifier.</summary>
    public required string Format { get; init; }

    /// <summary>Support level in the current deployment context.</summary>
    public required StudioPackageSupportLevel SupportLevel { get; init; }

    /// <summary>Operations supported by this family.</summary>
    public IReadOnlyList<StudioPackageOperation> SupportedOperations { get; init; } = Array.Empty<StudioPackageOperation>();

    /// <summary>Validation depth advertised for this family.</summary>
    public required string ValidationDepth { get; init; }

    /// <summary>Limitations that clients should surface as disabled or limited states.</summary>
    public IReadOnlyList<string> Limitations { get; init; } = Array.Empty<string>();

    /// <summary>Maximum serialized package size in bytes.</summary>
    public int MaxPackageBytes { get; init; }

    /// <summary>True when preview planning is available.</summary>
    public bool PreviewSupported { get; init; }

    /// <summary>True when publication requests are available.</summary>
    public bool PublishSupported { get; init; }
}

/// <summary>Response payload for package family capability discovery.</summary>
public sealed record StudioPackageFamilyCapabilities
{
    /// <summary>Persistence mode backing package lifecycle operations.</summary>
    public required StudioPackagePersistenceMode PersistenceMode { get; init; }

    /// <summary>True when the backing store is durable.</summary>
    public required bool Durable { get; init; }

    /// <summary>Package families, including unsupported or limited families.</summary>
    public IReadOnlyList<StudioPackageFamilyDescriptor> Families { get; init; } = Array.Empty<StudioPackageFamilyDescriptor>();
}

/// <summary>Mutable Studio package draft.</summary>
public sealed record StudioPackageDraft
{
    /// <summary>Draft identifier.</summary>
    public required Guid DraftId { get; init; }

    /// <summary>Content item identifier owned by the Studio lifecycle.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Machine-friendly package key.</summary>
    public required string PackageKey { get; init; }

    /// <summary>Workspace identifier.</summary>
    public string? WorkspaceId { get; init; }

    /// <summary>Owner principal identifier.</summary>
    public string? OwnerId { get; init; }

    /// <summary>Package family.</summary>
    public required StudioPackageFamily Family { get; init; }

    /// <summary>Package envelope.</summary>
    public required StudioPackageEnvelope Envelope { get; init; }

    /// <summary>Last validation summary known for the draft.</summary>
    public StudioValidationSummary Validation { get; init; } = StudioValidationSummary.NotValidated;

    /// <summary>Version this draft was reopened from, when applicable.</summary>
    public Guid? BaseVersionId { get; init; }

    /// <summary>Optimistic concurrency generation.</summary>
    public long Generation { get; init; }

    /// <summary>Identifier of the actor that created the draft.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Identifier of the actor that last updated the draft.</summary>
    public string? UpdatedBy { get; init; }

    /// <summary>Timestamp when the draft was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Timestamp when the draft was last updated.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Immutable Studio content version.</summary>
public sealed record StudioContentVersion
{
    /// <summary>Content item identifier.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Machine-friendly package key captured when the version was created.</summary>
    public required string PackageKey { get; init; }

    /// <summary>Workspace identifier captured when the version was created.</summary>
    public string? WorkspaceId { get; init; }

    /// <summary>Owner principal identifier captured when the version was created.</summary>
    public string? OwnerId { get; init; }

    /// <summary>Version identifier.</summary>
    public required Guid VersionId { get; init; }

    /// <summary>Monotonic version number within an item.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>SHA-256 hash of the immutable package envelope with volatile validation timestamps excluded.</summary>
    public required string ContentHash { get; init; }

    /// <summary>Immutable package envelope.</summary>
    public required StudioPackageEnvelope Envelope { get; init; }

    /// <summary>Validation summary captured at version creation.</summary>
    public required StudioValidationSummary Validation { get; init; }

    /// <summary>Dependency sidecars captured from the immutable package envelope.</summary>
    public IReadOnlyList<StudioPackageDependency> Dependencies { get; init; } = Array.Empty<StudioPackageDependency>();

    /// <summary>Provenance captured from the immutable package envelope.</summary>
    public IReadOnlyList<StudioProvenanceRef> Provenance { get; init; } = Array.Empty<StudioProvenanceRef>();

    /// <summary>Source draft identifier.</summary>
    public Guid? SourceDraftId { get; init; }

    /// <summary>Base version identifier when this version came from a reopened draft.</summary>
    public Guid? BaseVersionId { get; init; }

    /// <summary>Optional author change note.</summary>
    public string? ChangeNote { get; init; }

    /// <summary>Identifier of the actor that created the immutable version.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Timestamp when the immutable version was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Response body for listing immutable content versions.</summary>
public sealed record StudioContentVersionList
{
    /// <summary>Content item identifier.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Immutable versions ordered by version number.</summary>
    public IReadOnlyList<StudioContentVersion> Versions { get; init; } = Array.Empty<StudioContentVersion>();
}

/// <summary>Current and published version pointers for a Studio content item.</summary>
public sealed record StudioContentItemPointers
{
    /// <summary>Content item identifier.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Current version identifier.</summary>
    public Guid? CurrentVersionId { get; init; }

    /// <summary>Published version identifier.</summary>
    public Guid? PublishedVersionId { get; init; }
}

/// <summary>Durable publication request for an immutable Studio content version.</summary>
public sealed record StudioPublicationRequest
{
    /// <summary>Publication request identifier.</summary>
    public required Guid RequestId { get; init; }

    /// <summary>Content item identifier.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Immutable version identifier requested for publication.</summary>
    public required Guid VersionId { get; init; }

    /// <summary>Publication intent.</summary>
    public StudioPublicationIntent? Intent { get; init; }

    /// <summary>Persisted request status.</summary>
    public required StudioPublicationRequestStatus Status { get; init; }

    /// <summary>Validation evidence captured for the request.</summary>
    public StudioValidationSummary Validation { get; init; } = StudioValidationSummary.NotValidated;

    /// <summary>Optional acknowledgement text for validation warnings.</summary>
    public string? WarningAcknowledgement { get; init; }

    /// <summary>Identifier of the actor that requested publication.</summary>
    public string? RequestedBy { get; init; }

    /// <summary>Timestamp when the request was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Result of a deterministic comparison between two immutable content versions.</summary>
public sealed record StudioVersionComparison
{
    /// <summary>Source version identifier.</summary>
    public required Guid LeftVersionId { get; init; }

    /// <summary>Target version identifier.</summary>
    public required Guid RightVersionId { get; init; }

    /// <summary>True when the version content hashes match.</summary>
    public required bool ContentEqual { get; init; }

    /// <summary>True when dependency sets match.</summary>
    public required bool DependenciesEqual { get; init; }

    /// <summary>True when validation status and diagnostics match.</summary>
    public required bool ValidationEqual { get; init; }

    /// <summary>True when provenance sets match.</summary>
    public required bool ProvenanceEqual { get; init; }

    /// <summary>Deterministic change labels for clients to render.</summary>
    public IReadOnlyList<string> Changes { get; init; } = Array.Empty<string>();
}

/// <summary>Stable preview plan for a mutable Studio package draft.</summary>
public sealed record StudioPreviewPlan
{
    /// <summary>Draft identifier.</summary>
    public required Guid DraftId { get; init; }

    /// <summary>Package family.</summary>
    public required StudioPackageFamily Family { get; init; }

    /// <summary>True when preview can run synchronously.</summary>
    public required bool Synchronous { get; init; }

    /// <summary>True when preview requires a background job.</summary>
    public required bool RequiresJob { get; init; }

    /// <summary>Preview steps.</summary>
    public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();

    /// <summary>Validation summary used for the preview plan.</summary>
    public required StudioValidationSummary Validation { get; init; }
}

/// <summary>Durable rollback request and resulting pointer state.</summary>
public sealed record StudioRollbackRequest
{
    /// <summary>Rollback request identifier.</summary>
    public required Guid RequestId { get; init; }

    /// <summary>Content item identifier.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>Version identifier selected as the rollback target.</summary>
    public required Guid TargetVersionId { get; init; }

    /// <summary>Pointer updated by the rollback.</summary>
    [System.Text.Json.Serialization.JsonPropertyName("pointer")]
    public required StudioRollbackPointer Target { get; init; }

    /// <summary>Resulting current/published pointers.</summary>
    public required StudioContentItemPointers Pointers { get; init; }

    /// <summary>Identifier of the actor that requested rollback.</summary>
    public string? RequestedBy { get; init; }

    /// <summary>Reason supplied by the actor.</summary>
    public string? Reason { get; init; }

    /// <summary>Timestamp when the request was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
