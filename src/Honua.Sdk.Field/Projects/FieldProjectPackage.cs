// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Field.Forms;
using Honua.Sdk.Field.Records;

namespace Honua.Sdk.Field.Projects;

/// <summary>
/// Portable, no-cloud package describing a field project that a runtime can import locally.
/// </summary>
public sealed record FieldProjectPackage
{
    /// <summary>Current schema version for local field project package manifests.</summary>
    public const string CurrentSchemaVersion = "honua.field-project-package.v1";

    /// <summary>Manifest schema version.</summary>
    public required string SchemaVersion { get; init; }

    /// <summary>Stable project identifier.</summary>
    public required string ProjectId { get; init; }

    /// <summary>Human-readable project name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional package/project version.</summary>
    public string? Version { get; init; }

    /// <summary>Optional package description.</summary>
    public string? Description { get; init; }

    /// <summary>UTC time the package was generated.</summary>
    public DateTimeOffset? GeneratedAtUtc { get; init; }

    /// <summary>Feature sources included in the project package.</summary>
    public IReadOnlyList<SourceDescriptor> Sources { get; init; } = [];

    /// <summary>Field forms included in the project package.</summary>
    public IReadOnlyList<FormDefinition> Forms { get; init; } = [];

    /// <summary>Bindings that connect forms to feature sources and offline package entries.</summary>
    public IReadOnlyList<FieldProjectBinding> Bindings { get; init; } = [];

    /// <summary>Offline feature/scene package artifacts bundled or referenced by this project.</summary>
    public IReadOnlyList<FieldOfflinePackageReference> OfflinePackages { get; init; } = [];

    /// <summary>Package-wide media capture/export policy.</summary>
    public FieldProjectMediaPolicy MediaPolicy { get; init; } = new();

    /// <summary>Package-wide record lifecycle policy.</summary>
    public FieldRecordLifecyclePolicy LifecyclePolicy { get; init; } = FieldRecordLifecyclePolicy.Default;

    /// <summary>Optional task packets that seed local field assignments.</summary>
    public IReadOnlyList<FieldTaskPacket> TaskPackets { get; init; } = [];

    /// <summary>Additional non-UI package metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses a package manifest JSON payload using the package contract defaults.</summary>
    /// <param name="json">Manifest JSON.</param>
    /// <returns>Parsed package manifest.</returns>
    public static FieldProjectPackage ParseJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        return JsonSerializer.Deserialize<FieldProjectPackage>(json, FieldProjectPackageJson.Options)
            ?? throw new JsonException("Field project package manifest was empty.");
    }

    /// <summary>Serializes this package manifest using the package contract defaults.</summary>
    /// <returns>Manifest JSON.</returns>
    public string ToJson()
        => JsonSerializer.Serialize(this, FieldProjectPackageJson.Options);

    /// <summary>Validates this package manifest.</summary>
    /// <returns>Validation result with warnings and blocking errors.</returns>
    public FieldProjectPackageValidationResult Validate()
        => FieldProjectPackageValidator.Validate(this);
}

/// <summary>
/// Connects one form to one portable source within a local field project package.
/// </summary>
public sealed record FieldProjectBinding
{
    /// <summary>Stable binding identifier.</summary>
    public required string BindingId { get; init; }

    /// <summary>Form identifier from <see cref="FieldProjectPackage.Forms"/>.</summary>
    public required string FormId { get; init; }

    /// <summary>Source descriptor identifier from <see cref="FieldProjectPackage.Sources"/>.</summary>
    public required string SourceId { get; init; }

    /// <summary>Optional offline artifact identifier that backs this binding.</summary>
    public string? OfflinePackageId { get; init; }

    /// <summary>Optional query used to seed the local package or filter assigned records.</summary>
    public SourceQuery? SourceQuery { get; init; }

    /// <summary>Whether records for this binding are expected to include geometry.</summary>
    public bool RequiresGeometry { get; init; } = true;

    /// <summary>Whether local create/update/delete operations are allowed for this binding.</summary>
    public bool Editable { get; init; } = true;

    /// <summary>Field id used as the primary display label in host UIs.</summary>
    public string? DisplayFieldId { get; init; }

    /// <summary>Field id used for local duplicate checks, when different from display field.</summary>
    public string? DuplicateKeyFieldId { get; init; }
}

/// <summary>
/// Portable reference to an offline artifact used by a field project package.
/// </summary>
public sealed record FieldOfflinePackageReference
{
    /// <summary>Stable offline package identifier.</summary>
    public required string PackageId { get; init; }

    /// <summary>Artifact kind.</summary>
    public FieldOfflinePackageKind Kind { get; init; } = FieldOfflinePackageKind.FeatureData;

    /// <summary>Relative path or file name inside the local package archive.</summary>
    public string? RelativePath { get; init; }

    /// <summary>Content type of the artifact, when known.</summary>
    public string? ContentType { get; init; }

    /// <summary>Declared artifact size in bytes.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Lowercase hexadecimal SHA-256 digest, when supplied.</summary>
    public string? Sha256 { get; init; }

    /// <summary>Source ids represented by this artifact.</summary>
    public IReadOnlyList<string> SourceIds { get; init; } = [];

    /// <summary>UTC time after which the artifact should be considered stale.</summary>
    public DateTimeOffset? StaleAfterUtc { get; init; }

    /// <summary>UTC time after which offline use should be blocked or explicitly degraded.</summary>
    public DateTimeOffset? OfflineUseExpiresAtUtc { get; init; }
}

/// <summary>
/// Offline artifact kinds understood by field package consumers.
/// </summary>
public enum FieldOfflinePackageKind
{
    /// <summary>Feature data package such as GeoPackage, SQLite, or an SDK feature bundle.</summary>
    FeatureData,

    /// <summary>Map tile/style package used for local display.</summary>
    MapTiles,

    /// <summary>3D scene package used for local scene/AR workflows.</summary>
    Scene,

    /// <summary>Media seed package used for local reference attachments.</summary>
    Media,

    /// <summary>Opaque runtime-specific artifact.</summary>
    Other
}

/// <summary>
/// Package-level media capture and export policy.
/// </summary>
public sealed record FieldProjectMediaPolicy
{
    /// <summary>Allowed media content types. Empty means runtime default.</summary>
    public IReadOnlyList<string> AllowedContentTypes { get; init; } = [];

    /// <summary>Maximum single attachment size in bytes.</summary>
    public long? MaxAttachmentBytes { get; init; }

    /// <summary>Whether photo capture should request face blurring before export/upload.</summary>
    public bool RequiresFaceBlurByDefault { get; init; }

    /// <summary>Whether media capture should include GPS metadata when available.</summary>
    public bool CaptureLocationByDefault { get; init; } = true;

    /// <summary>Whether video/audio capture may include track metadata when available.</summary>
    public bool CaptureGpsTrackForTimedMedia { get; init; }

    /// <summary>Per-field media requirements and overrides.</summary>
    public IReadOnlyList<FieldMediaRequirement> Requirements { get; init; } = [];
}

/// <summary>
/// Per-field media requirement declared by a local field package.
/// </summary>
public sealed record FieldMediaRequirement
{
    /// <summary>Form identifier that owns the field.</summary>
    public required string FormId { get; init; }

    /// <summary>Field identifier for the media field.</summary>
    public required string FieldId { get; init; }

    /// <summary>Expected media type.</summary>
    public FieldMediaType MediaType { get; init; }

    /// <summary>Minimum attachment count for this field.</summary>
    public int? MinCount { get; init; }

    /// <summary>Maximum attachment count for this field.</summary>
    public int? MaxCount { get; init; }

    /// <summary>Allowed content types for this field. Empty means package default.</summary>
    public IReadOnlyList<string> AllowedContentTypes { get; init; } = [];
}

/// <summary>
/// Portable record lifecycle policy for local field packages.
/// </summary>
public sealed record FieldRecordLifecyclePolicy
{
    /// <summary>Default lifecycle policy for no-cloud local field workflows.</summary>
    public static FieldRecordLifecyclePolicy Default { get; } = new()
    {
        AllowedStatuses =
        [
            RecordStatus.Draft,
            RecordStatus.ReadyToSubmit,
            RecordStatus.Submitted,
            RecordStatus.Rejected,
            RecordStatus.Approved,
            RecordStatus.Reopened,
            RecordStatus.Deleted
        ],
        AllowedTransitions =
        [
            new FieldRecordLifecycleTransition { From = RecordStatus.Draft, To = RecordStatus.ReadyToSubmit },
            new FieldRecordLifecycleTransition { From = RecordStatus.ReadyToSubmit, To = RecordStatus.Submitted },
            new FieldRecordLifecycleTransition { From = RecordStatus.Draft, To = RecordStatus.Submitted },
            new FieldRecordLifecycleTransition { From = RecordStatus.Submitted, To = RecordStatus.Approved },
            new FieldRecordLifecycleTransition { From = RecordStatus.Submitted, To = RecordStatus.Rejected },
            new FieldRecordLifecycleTransition { From = RecordStatus.Submitted, To = RecordStatus.Deleted },
            new FieldRecordLifecycleTransition { From = RecordStatus.Rejected, To = RecordStatus.Reopened },
            new FieldRecordLifecycleTransition { From = RecordStatus.Reopened, To = RecordStatus.ReadyToSubmit },
            new FieldRecordLifecycleTransition { From = RecordStatus.Reopened, To = RecordStatus.Submitted },
            new FieldRecordLifecycleTransition { From = RecordStatus.Approved, To = RecordStatus.Reopened },
            new FieldRecordLifecycleTransition { From = RecordStatus.Draft, To = RecordStatus.Deleted },
            new FieldRecordLifecycleTransition { From = RecordStatus.Rejected, To = RecordStatus.Deleted },
            new FieldRecordLifecycleTransition { From = RecordStatus.Reopened, To = RecordStatus.Deleted }
        ]
    };

    /// <summary>Status values the package expects clients to support.</summary>
    public IReadOnlyList<RecordStatus> AllowedStatuses { get; init; } = [];

    /// <summary>Allowed status transitions.</summary>
    public IReadOnlyList<FieldRecordLifecycleTransition> AllowedTransitions { get; init; } = [];

    /// <summary>Whether approved/submitted records should be read-only unless reopened.</summary>
    public bool ProtectSubmittedRecords { get; init; } = true;

    /// <summary>Whether rejected records may be edited locally.</summary>
    public bool AllowRejectedEdit { get; init; } = true;
}

/// <summary>
/// One allowed field record lifecycle transition.
/// </summary>
public sealed record FieldRecordLifecycleTransition
{
    /// <summary>Source status.</summary>
    public RecordStatus From { get; init; }

    /// <summary>Destination status.</summary>
    public RecordStatus To { get; init; }

    /// <summary>Optional role, group, or policy token required for this transition.</summary>
    public string? RequiredActorRole { get; init; }
}

/// <summary>
/// Local task packet bundled with a field project package.
/// </summary>
public sealed record FieldTaskPacket
{
    /// <summary>Stable task packet identifier.</summary>
    public required string TaskPacketId { get; init; }

    /// <summary>Human-readable task packet name.</summary>
    public string? Name { get; init; }

    /// <summary>Assignments included in this packet.</summary>
    public IReadOnlyList<FieldAssignment> Assignments { get; init; } = [];
}

/// <summary>
/// Local assignment for a field user or crew.
/// </summary>
public sealed record FieldAssignment
{
    /// <summary>Stable assignment identifier.</summary>
    public required string AssignmentId { get; init; }

    /// <summary>Binding this assignment applies to.</summary>
    public required string BindingId { get; init; }

    /// <summary>Assigned user identifier, when known.</summary>
    public string? AssigneeUserId { get; init; }

    /// <summary>Assigned crew identifier, when known.</summary>
    public string? CrewId { get; init; }

    /// <summary>Assignment priority.</summary>
    public FieldAssignmentPriority Priority { get; init; } = FieldAssignmentPriority.Normal;

    /// <summary>Current assignment status.</summary>
    public FieldAssignmentStatus Status { get; init; } = FieldAssignmentStatus.NotStarted;

    /// <summary>UTC due date for this assignment.</summary>
    public DateTimeOffset? DueAtUtc { get; init; }

    /// <summary>Optional source query narrowing the work packet.</summary>
    public SourceQuery? WorkQuery { get; init; }

    /// <summary>Linked record identifiers, when tasks are pre-created.</summary>
    public IReadOnlyList<string> RecordIds { get; init; } = [];

    /// <summary>Additional non-UI assignment metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Assignment priority values.
/// </summary>
public enum FieldAssignmentPriority
{
    /// <summary>Low priority.</summary>
    Low,

    /// <summary>Normal priority.</summary>
    Normal,

    /// <summary>High priority.</summary>
    High,

    /// <summary>Urgent priority.</summary>
    Urgent
}

/// <summary>
/// Assignment progress values.
/// </summary>
public enum FieldAssignmentStatus
{
    /// <summary>Work has not started.</summary>
    NotStarted,

    /// <summary>Work is currently in progress.</summary>
    InProgress,

    /// <summary>Work is blocked by a local or field condition.</summary>
    Blocked,

    /// <summary>Work is complete locally.</summary>
    Complete,

    /// <summary>Work is canceled or no longer required.</summary>
    Canceled
}

/// <summary>
/// Validation result for field project packages.
/// </summary>
public sealed record FieldProjectPackageValidationResult
{
    /// <summary>Validation findings.</summary>
    public IReadOnlyList<FieldProjectPackageValidationIssue> Issues { get; init; } = [];

    /// <summary>Whether the package has no blocking validation errors.</summary>
    public bool IsValid => Issues.All(issue => issue.Severity != FieldProjectPackageValidationSeverity.Error);

    /// <summary>Whether the package has warning findings.</summary>
    public bool HasWarnings => Issues.Any(issue => issue.Severity == FieldProjectPackageValidationSeverity.Warning);
}

/// <summary>
/// One validation finding for a field project package.
/// </summary>
public sealed record FieldProjectPackageValidationIssue
{
    /// <summary>Machine-readable validation code.</summary>
    public required string Code { get; init; }

    /// <summary>JSON-style path to the affected value.</summary>
    public required string Path { get; init; }

    /// <summary>Human-readable validation message.</summary>
    public required string Message { get; init; }

    /// <summary>Finding severity.</summary>
    public FieldProjectPackageValidationSeverity Severity { get; init; } = FieldProjectPackageValidationSeverity.Error;
}

/// <summary>
/// Field project package validation severities.
/// </summary>
public enum FieldProjectPackageValidationSeverity
{
    /// <summary>Warning that should be shown but does not block local import.</summary>
    Warning,

    /// <summary>Error that blocks local import.</summary>
    Error
}

/// <summary>
/// Validation codes for field project package manifests.
/// </summary>
public static class FieldProjectPackageValidationCodes
{
    /// <summary>Manifest schema version is unsupported.</summary>
    public const string UnsupportedSchemaVersion = "unsupported-schema-version";

    /// <summary>Manifest is missing a required value.</summary>
    public const string MissingRequiredValue = "missing-required-value";

    /// <summary>Manifest references a missing form, source, binding, or package.</summary>
    public const string MissingReference = "missing-reference";

    /// <summary>Manifest contains a duplicate identifier.</summary>
    public const string DuplicateIdentifier = "duplicate-identifier";

    /// <summary>Manifest contains an invalid value.</summary>
    public const string InvalidValue = "invalid-value";
}

/// <summary>
/// Validator for no-cloud field project package manifests.
/// </summary>
public static class FieldProjectPackageValidator
{
    /// <summary>Validates a field project package manifest.</summary>
    /// <param name="package">Package to validate.</param>
    /// <returns>Validation result.</returns>
    public static FieldProjectPackageValidationResult Validate(FieldProjectPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var issues = new List<FieldProjectPackageValidationIssue>();

        ValidatePackageIdentity(issues, package);
        ValidatePackageIdentifiers(issues, package);

        var identifiers = FieldProjectPackageIdentifierSets.Create(package);

        ValidateBindings(issues, package.Bindings, identifiers);
        ValidateOfflinePackages(issues, package.OfflinePackages, identifiers.SourceIds);
        ValidateMediaPolicy(issues, package.MediaPolicy, identifiers.FormIds);
        ValidateTaskPackets(issues, package.TaskPackets, identifiers.BindingIds);
        ValidateLifecyclePolicy(issues, package.LifecyclePolicy);

        return new FieldProjectPackageValidationResult { Issues = issues };
    }

    private static void ValidatePackageIdentity(
        ICollection<FieldProjectPackageValidationIssue> issues,
        FieldProjectPackage package)
    {
        Require(issues, package.SchemaVersion, "$.schemaVersion", "Package schemaVersion is required.");
        if (!string.IsNullOrWhiteSpace(package.SchemaVersion) &&
            !string.Equals(package.SchemaVersion, FieldProjectPackage.CurrentSchemaVersion, StringComparison.Ordinal))
        {
            AddError(
                issues,
                FieldProjectPackageValidationCodes.UnsupportedSchemaVersion,
                "$.schemaVersion",
                $"Unsupported package schemaVersion '{package.SchemaVersion}'.");
        }

        Require(issues, package.ProjectId, "$.projectId", "Package projectId is required.");
        Require(issues, package.Name, "$.name", "Package name is required.");
    }

    private static void ValidatePackageIdentifiers(
        ICollection<FieldProjectPackageValidationIssue> issues,
        FieldProjectPackage package)
    {
        RequireAll(
            issues,
            package.Forms.Select((form, index) => (Value: form.FormId, Path: $"$.forms[{index}].formId")),
            "Form id is required.");
        RequireAll(
            issues,
            package.Sources.Select((source, index) => (Value: source.Id, Path: $"$.sources[{index}].id")),
            "Source id is required.");

        ValidateUnique(
            issues,
            package.Forms.Select(form => form.FormId),
            "$.forms",
            "Form identifiers must be unique.");
        ValidateUnique(
            issues,
            package.Sources.Select(source => source.Id),
            "$.sources",
            "Source identifiers must be unique.");
        ValidateUnique(
            issues,
            package.Bindings.Select(binding => binding.BindingId),
            "$.bindings",
            "Binding identifiers must be unique.");
        ValidateUnique(
            issues,
            package.OfflinePackages.Select(offlinePackage => offlinePackage.PackageId),
            "$.offlinePackages",
            "Offline package identifiers must be unique.");
    }

    private static void ValidateBindings(
        ICollection<FieldProjectPackageValidationIssue> issues,
        IReadOnlyList<FieldProjectBinding> bindings,
        FieldProjectPackageIdentifierSets identifiers)
    {
        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            var path = $"$.bindings[{i}]";
            Require(issues, binding.BindingId, $"{path}.bindingId", "Binding id is required.");
            Require(issues, binding.FormId, $"{path}.formId", "Binding formId is required.");
            Require(issues, binding.SourceId, $"{path}.sourceId", "Binding sourceId is required.");

            if (!string.IsNullOrWhiteSpace(binding.FormId) && !identifiers.FormIds.Contains(binding.FormId))
            {
                AddError(
                    issues,
                    FieldProjectPackageValidationCodes.MissingReference,
                    $"{path}.formId",
                    $"Binding references missing form '{binding.FormId}'.");
            }

            if (!string.IsNullOrWhiteSpace(binding.SourceId) && !identifiers.SourceIds.Contains(binding.SourceId))
            {
                AddError(
                    issues,
                    FieldProjectPackageValidationCodes.MissingReference,
                    $"{path}.sourceId",
                    $"Binding references missing source '{binding.SourceId}'.");
            }

            if (!string.IsNullOrWhiteSpace(binding.OfflinePackageId) &&
                !identifiers.OfflinePackageIds.Contains(binding.OfflinePackageId))
            {
                AddError(
                    issues,
                    FieldProjectPackageValidationCodes.MissingReference,
                    $"{path}.offlinePackageId",
                    $"Binding references missing offline package '{binding.OfflinePackageId}'.");
            }
        }
    }

    private static void ValidateOfflinePackages(
        ICollection<FieldProjectPackageValidationIssue> issues,
        IReadOnlyList<FieldOfflinePackageReference> offlinePackages,
        IReadOnlySet<string> sourceIds)
    {
        for (var i = 0; i < offlinePackages.Count; i++)
        {
            var offlinePackage = offlinePackages[i];
            var path = $"$.offlinePackages[{i}]";
            Require(issues, offlinePackage.PackageId, $"{path}.packageId", "Offline package id is required.");
            foreach (var sourceId in offlinePackage.SourceIds.Where(sourceId => !sourceIds.Contains(sourceId)))
            {
                AddError(
                    issues,
                    FieldProjectPackageValidationCodes.MissingReference,
                    $"{path}.sourceIds",
                    $"Offline package references missing source '{sourceId}'.");
            }

            if (offlinePackage.SizeBytes is < 0)
            {
                AddError(
                    issues,
                    FieldProjectPackageValidationCodes.InvalidValue,
                    $"{path}.sizeBytes",
                    "Offline package sizeBytes must be zero or greater.");
            }
        }
    }

    private static void ValidateMediaPolicy(
        ICollection<FieldProjectPackageValidationIssue> issues,
        FieldProjectMediaPolicy mediaPolicy,
        IReadOnlySet<string> formIds)
    {
        for (var i = 0; i < mediaPolicy.Requirements.Count; i++)
        {
            var requirement = mediaPolicy.Requirements[i];
            var path = $"$.mediaPolicy.requirements[{i}]";
            Require(issues, requirement.FormId, $"{path}.formId", "Media requirement formId is required.");
            Require(issues, requirement.FieldId, $"{path}.fieldId", "Media requirement fieldId is required.");

            if (!string.IsNullOrWhiteSpace(requirement.FormId) && !formIds.Contains(requirement.FormId))
            {
                AddError(
                    issues,
                    FieldProjectPackageValidationCodes.MissingReference,
                    $"{path}.formId",
                    $"Media requirement references missing form '{requirement.FormId}'.");
            }

            if (requirement.MinCount is < 0 || requirement.MaxCount is < 0)
            {
                AddError(
                    issues,
                    FieldProjectPackageValidationCodes.InvalidValue,
                    path,
                    "Media requirement counts must be zero or greater.");
            }

            if (requirement.MinCount is { } min && requirement.MaxCount is { } max && min > max)
            {
                AddError(
                    issues,
                    FieldProjectPackageValidationCodes.InvalidValue,
                    path,
                    "Media requirement minCount cannot exceed maxCount.");
            }
        }
    }

    private static void ValidateTaskPackets(
        ICollection<FieldProjectPackageValidationIssue> issues,
        IReadOnlyList<FieldTaskPacket> taskPackets,
        IReadOnlySet<string> bindingIds)
    {
        for (var i = 0; i < taskPackets.Count; i++)
        {
            var packet = taskPackets[i];
            var path = $"$.taskPackets[{i}]";
            Require(issues, packet.TaskPacketId, $"{path}.taskPacketId", "Task packet id is required.");

            for (var j = 0; j < packet.Assignments.Count; j++)
            {
                var assignment = packet.Assignments[j];
                var assignmentPath = $"{path}.assignments[{j}]";
                Require(issues, assignment.AssignmentId, $"{assignmentPath}.assignmentId", "Assignment id is required.");
                Require(issues, assignment.BindingId, $"{assignmentPath}.bindingId", "Assignment bindingId is required.");

                if (!string.IsNullOrWhiteSpace(assignment.BindingId) && !bindingIds.Contains(assignment.BindingId))
                {
                    AddError(
                        issues,
                        FieldProjectPackageValidationCodes.MissingReference,
                        $"{assignmentPath}.bindingId",
                        $"Assignment references missing binding '{assignment.BindingId}'.");
                }
            }
        }
    }

    private static void ValidateLifecyclePolicy(
        ICollection<FieldProjectPackageValidationIssue> issues,
        FieldRecordLifecyclePolicy lifecyclePolicy)
    {
        var statuses = lifecyclePolicy.AllowedStatuses.Count > 0
            ? lifecyclePolicy.AllowedStatuses.ToHashSet()
            : Enum.GetValues<RecordStatus>().ToHashSet();

        for (var i = 0; i < lifecyclePolicy.AllowedTransitions.Count; i++)
        {
            var transition = lifecyclePolicy.AllowedTransitions[i];
            var path = $"$.lifecyclePolicy.allowedTransitions[{i}]";
            if (!statuses.Contains(transition.From))
            {
                AddError(
                    issues,
                    FieldProjectPackageValidationCodes.InvalidValue,
                    $"{path}.from",
                    $"Transition source status '{transition.From}' is not allowed by the lifecycle policy.");
            }

            if (!statuses.Contains(transition.To))
            {
                AddError(
                    issues,
                    FieldProjectPackageValidationCodes.InvalidValue,
                    $"{path}.to",
                    $"Transition destination status '{transition.To}' is not allowed by the lifecycle policy.");
            }
        }
    }

    private static void ValidateUnique(
        ICollection<FieldProjectPackageValidationIssue> issues,
        IEnumerable<string?> values,
        string path,
        string message)
    {
        var hasDuplicate = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Skip(1).Any());

        if (hasDuplicate)
        {
            AddError(issues, FieldProjectPackageValidationCodes.DuplicateIdentifier, path, message);
        }
    }

    private static void RequireAll(
        ICollection<FieldProjectPackageValidationIssue> issues,
        IEnumerable<(string Value, string Path)> values,
        string message)
    {
        foreach (var value in values.Where(value => string.IsNullOrWhiteSpace(value.Value)))
        {
            AddError(issues, FieldProjectPackageValidationCodes.MissingRequiredValue, value.Path, message);
        }
    }

    private static void Require(
        ICollection<FieldProjectPackageValidationIssue> issues,
        string? value,
        string path,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(issues, FieldProjectPackageValidationCodes.MissingRequiredValue, path, message);
        }
    }

    private static void AddError(
        ICollection<FieldProjectPackageValidationIssue> issues,
        string code,
        string path,
        string message)
        => issues.Add(new FieldProjectPackageValidationIssue
        {
            Code = code,
            Path = path,
            Message = message,
            Severity = FieldProjectPackageValidationSeverity.Error
        });

    private sealed record FieldProjectPackageIdentifierSets(
        IReadOnlySet<string> FormIds,
        IReadOnlySet<string> SourceIds,
        IReadOnlySet<string> BindingIds,
        IReadOnlySet<string> OfflinePackageIds)
    {
        public static FieldProjectPackageIdentifierSets Create(FieldProjectPackage package)
            => new(
                CreateSet(package.Forms.Select(form => form.FormId)),
                CreateSet(package.Sources.Select(source => source.Id)),
                CreateSet(package.Bindings.Select(binding => binding.BindingId)),
                CreateSet(package.OfflinePackages.Select(offlinePackage => offlinePackage.PackageId)));

        private static HashSet<string> CreateSet(IEnumerable<string?> values)
            => values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}

internal static class FieldProjectPackageJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };
}
