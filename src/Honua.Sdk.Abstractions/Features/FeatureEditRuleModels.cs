// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Structured field domain type.
/// </summary>
public enum FeatureFieldDomainType
{
    /// <summary>Domain type is unknown or provider-defined.</summary>
    Unknown = 0,

    /// <summary>Domain is a coded value list.</summary>
    CodedValue = 1,

    /// <summary>Domain is an inclusive range.</summary>
    Range = 2,

    /// <summary>Domain is inherited from a subtype or parent schema.</summary>
    Inherited = 3
}

/// <summary>
/// Attribute rule kind.
/// </summary>
public enum FeatureAttributeRuleType
{
    /// <summary>Rule type is unknown or provider-defined.</summary>
    Unknown = 0,

    /// <summary>Rule calculates or derives a field value.</summary>
    Calculation = 1,

    /// <summary>Rule constrains whether an edit can be applied.</summary>
    Constraint = 2,

    /// <summary>Rule validates data quality and may produce warnings or errors.</summary>
    Validation = 3
}

/// <summary>
/// Edit operation that can trigger an attribute rule.
/// </summary>
public enum FeatureAttributeRuleTrigger
{
    /// <summary>Trigger is unknown or provider-defined.</summary>
    Unknown = 0,

    /// <summary>Rule runs on insert.</summary>
    Insert = 1,

    /// <summary>Rule runs on update.</summary>
    Update = 2,

    /// <summary>Rule runs on delete.</summary>
    Delete = 3
}

/// <summary>
/// Relationship class cardinality.
/// </summary>
public enum FeatureRelationshipCardinality
{
    /// <summary>Cardinality is unknown or provider-defined.</summary>
    Unknown = 0,

    /// <summary>One origin feature can relate to one destination feature.</summary>
    OneToOne = 1,

    /// <summary>One origin feature can relate to many destination features.</summary>
    OneToMany = 2,

    /// <summary>Many origin features can relate to many destination features.</summary>
    ManyToMany = 3
}

/// <summary>
/// Relationship class ownership behavior.
/// </summary>
public enum FeatureRelationshipType
{
    /// <summary>Relationship type is unknown or provider-defined.</summary>
    Unknown = 0,

    /// <summary>Simple relationship without lifecycle ownership.</summary>
    Simple = 1,

    /// <summary>Composite relationship where origin lifecycle can affect destinations.</summary>
    Composite = 2,

    /// <summary>Attachment relationship.</summary>
    Attachment = 3
}

/// <summary>
/// Severity for client-visible edit validation findings.
/// </summary>
public enum FeatureEditValidationSeverity
{
    /// <summary>Informational validation finding.</summary>
    Information = 0,

    /// <summary>Validation warning that may still allow the edit.</summary>
    Warning = 1,

    /// <summary>Validation error returned by a rule or provider.</summary>
    Error = 2,

    /// <summary>Validation finding that blocks applying the edit.</summary>
    Blocking = 3
}

/// <summary>
/// Validation mode requested before applying edits.
/// </summary>
public enum FeatureEditValidationMode
{
    /// <summary>Use provider default validation behavior.</summary>
    ProviderDefault = 0,

    /// <summary>Run only client-safe checks exposed in SDK metadata.</summary>
    ClientSide = 1,

    /// <summary>Ask the provider to validate without committing edits.</summary>
    ServerSide = 2,

    /// <summary>Run both client-safe and provider-side validation when supported.</summary>
    ClientAndServer = 3
}

/// <summary>
/// Structured coded domain value.
/// </summary>
public sealed record FeatureFieldDomainCode
{
    /// <summary>Stored value.</summary>
    public required JsonElement Value { get; init; }

    /// <summary>Human-readable label.</summary>
    public string? Label { get; init; }

    /// <summary>Whether this code is retired or inactive.</summary>
    public bool Inactive { get; init; }
}

/// <summary>
/// Structured field domain metadata.
/// </summary>
public sealed record FeatureFieldDomain
{
    /// <summary>Stable domain identifier when the provider advertises one.</summary>
    public string? DomainId { get; init; }

    /// <summary>Domain display or lookup name.</summary>
    public string? Name { get; init; }

    /// <summary>Field name this domain applies to, when field-specific.</summary>
    public string? FieldName { get; init; }

    /// <summary>Structured domain type.</summary>
    public FeatureFieldDomainType Type { get; init; } = FeatureFieldDomainType.Unknown;

    /// <summary>Coded values for coded-value domains.</summary>
    public IReadOnlyList<FeatureFieldDomainCode> CodedValues { get; init; } = [];

    /// <summary>Inclusive minimum for range domains.</summary>
    public JsonElement? MinValue { get; init; }

    /// <summary>Inclusive maximum for range domains.</summary>
    public JsonElement? MaxValue { get; init; }

    /// <summary>Raw provider domain payload, when useful for provider-specific adapters.</summary>
    public JsonElement? Raw { get; init; }
}

/// <summary>
/// One field value participating in a contingent value set.
/// </summary>
public sealed record FeatureContingentFieldValue
{
    /// <summary>Field name participating in the contingent value set.</summary>
    public required string FieldName { get; init; }

    /// <summary>Allowed value for the field.</summary>
    public required JsonElement Value { get; init; }

    /// <summary>Whether this value represents an explicit null allowance.</summary>
    public bool AllowsNull { get; init; }
}

/// <summary>
/// Contingent values across a group of fields.
/// </summary>
public sealed record FeatureContingentValueSet
{
    /// <summary>Stable contingent value identifier when the provider advertises one.</summary>
    public string? ContingencyId { get; init; }

    /// <summary>Field group or provider grouping name.</summary>
    public string? FieldGroupName { get; init; }

    /// <summary>Field/value pairs that compose this contingent value set.</summary>
    public IReadOnlyList<FeatureContingentFieldValue> Values { get; init; } = [];

    /// <summary>Whether this contingent value set is retired or inactive.</summary>
    public bool Inactive { get; init; }

    /// <summary>Raw provider contingent value payload.</summary>
    public JsonElement? Raw { get; init; }
}

/// <summary>
/// Attribute rule metadata advertised by a provider.
/// </summary>
public sealed record FeatureAttributeRule
{
    /// <summary>Stable rule identifier.</summary>
    public required string RuleId { get; init; }

    /// <summary>Rule display or lookup name.</summary>
    public string? Name { get; init; }

    /// <summary>Rule kind.</summary>
    public FeatureAttributeRuleType Type { get; init; } = FeatureAttributeRuleType.Unknown;

    /// <summary>Field the rule targets, when field-specific.</summary>
    public string? FieldName { get; init; }

    /// <summary>Edit triggers that run the rule.</summary>
    public IReadOnlyList<FeatureAttributeRuleTrigger> Triggers { get; init; } = [];

    /// <summary>Provider expression language, such as Arcade or CQL.</summary>
    public string? ExpressionLanguage { get; init; }

    /// <summary>Provider expression body when it is safe to expose to clients.</summary>
    public string? Expression { get; init; }

    /// <summary>Whether the rule is enabled by the provider.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>Whether clients should avoid evaluating the rule locally.</summary>
    public bool ExcludeFromClientEvaluation { get; init; }

    /// <summary>Provider error number or code associated with the rule.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Provider validation message associated with the rule.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Raw provider rule payload.</summary>
    public JsonElement? Raw { get; init; }
}

/// <summary>
/// One key pair used by a relationship class.
/// </summary>
public sealed record FeatureRelationshipKey
{
    /// <summary>Origin/source field name.</summary>
    public required string OriginField { get; init; }

    /// <summary>Destination/related field name.</summary>
    public required string DestinationField { get; init; }
}

/// <summary>
/// Relationship class descriptor for related feature workflows.
/// </summary>
public sealed record FeatureRelationshipClassDescriptor
{
    /// <summary>Stable relationship identifier.</summary>
    public required string RelationshipId { get; init; }

    /// <summary>Relationship display or lookup name.</summary>
    public string? Name { get; init; }

    /// <summary>Origin/source feature source identifiers.</summary>
    public FeatureSource OriginSource { get; init; } = new();

    /// <summary>Destination/related feature source identifiers.</summary>
    public FeatureSource DestinationSource { get; init; } = new();

    /// <summary>Relationship cardinality.</summary>
    public FeatureRelationshipCardinality Cardinality { get; init; } = FeatureRelationshipCardinality.Unknown;

    /// <summary>Relationship ownership behavior.</summary>
    public FeatureRelationshipType Type { get; init; } = FeatureRelationshipType.Unknown;

    /// <summary>Key field pairs used by the relationship.</summary>
    public IReadOnlyList<FeatureRelationshipKey> Keys { get; init; } = [];

    /// <summary>Whether related records can be queried through the provider.</summary>
    public bool SupportsQueryRelated { get; init; }

    /// <summary>Whether related records can be edited through the provider.</summary>
    public bool SupportsEditRelated { get; init; }

    /// <summary>Raw provider relationship payload.</summary>
    public JsonElement? Raw { get; init; }
}

/// <summary>
/// Versioning capabilities for edit sessions.
/// </summary>
public sealed record FeatureEditVersioningCapabilities
{
    /// <summary>Whether named versions can be targeted by edit requests.</summary>
    public bool SupportsVersionName { get; init; }

    /// <summary>Whether branch versions are supported.</summary>
    public bool SupportsBranchVersioning { get; init; }

    /// <summary>Whether edit sessions can be started and later committed or rolled back.</summary>
    public bool SupportsEditSessions { get; init; }

    /// <summary>Whether optimistic conflict detection metadata is available.</summary>
    public bool SupportsConflictDetection { get; init; }

    /// <summary>Default version name advertised by the provider.</summary>
    public string? DefaultVersionName { get; init; }
}

/// <summary>
/// Editing-rule metadata discovered for a source.
/// </summary>
public sealed record FeatureEditingRulesMetadata
{
    /// <summary>Source identifiers associated with the metadata.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Structured field domains advertised by the provider.</summary>
    public IReadOnlyList<FeatureFieldDomain> FieldDomains { get; init; } = [];

    /// <summary>Contingent value sets advertised by the provider.</summary>
    public IReadOnlyList<FeatureContingentValueSet> ContingentValues { get; init; } = [];

    /// <summary>Attribute rules advertised by the provider.</summary>
    public IReadOnlyList<FeatureAttributeRule> AttributeRules { get; init; } = [];

    /// <summary>Relationship class descriptors advertised by the provider.</summary>
    public IReadOnlyList<FeatureRelationshipClassDescriptor> Relationships { get; init; } = [];

    /// <summary>Versioning and edit-session capabilities advertised by the provider.</summary>
    public FeatureEditVersioningCapabilities? Versioning { get; init; }

    /// <summary>Raw provider metadata payload.</summary>
    public JsonElement? Raw { get; init; }
}

/// <summary>
/// Request for editing-rule metadata discovery.
/// </summary>
public sealed record FeatureEditingRulesRequest
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Whether inactive or retired rule metadata should be included.</summary>
    public bool IncludeInactive { get; init; }

    /// <summary>Optional version name used by version-aware providers.</summary>
    public string? VersionName { get; init; }
}

/// <summary>
/// Branch or version-aware edit session.
/// </summary>
public sealed record FeatureEditSession
{
    /// <summary>Stable session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Version name targeted by the session.</summary>
    public string? VersionName { get; init; }

    /// <summary>Branch name targeted by the session.</summary>
    public string? BranchName { get; init; }

    /// <summary>Parent version or branch name.</summary>
    public string? ParentVersionName { get; init; }

    /// <summary>Timestamp when the provider opened the session.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Timestamp when the provider will expire the session.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Provider state token for optimistic concurrency or resume behavior.</summary>
    public string? StateToken { get; init; }
}

/// <summary>
/// Request to start a branch or version-aware edit session.
/// </summary>
public sealed record FeatureEditSessionStartRequest
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Version name to target.</summary>
    public string? VersionName { get; init; }

    /// <summary>Branch name to target.</summary>
    public string? BranchName { get; init; }

    /// <summary>Parent version used when creating a new version or branch.</summary>
    public string? ParentVersionName { get; init; }

    /// <summary>Optional operator or workflow description.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the provider should acquire an edit lock when supported.</summary>
    public bool AcquireLock { get; init; }
}

/// <summary>
/// Request to complete an edit session.
/// </summary>
public sealed record FeatureEditSessionCompleteRequest
{
    /// <summary>Edit session to complete.</summary>
    public required FeatureEditSession Session { get; init; }

    /// <summary>Optional provider validation state token.</summary>
    public string? StateToken { get; init; }
}

/// <summary>
/// One validation finding for a feature edit.
/// </summary>
public sealed record FeatureEditValidationResult
{
    /// <summary>Field associated with the finding, when field-specific.</summary>
    public string? FieldName { get; init; }

    /// <summary>Rule identifier associated with the finding.</summary>
    public string? RuleId { get; init; }

    /// <summary>Rule display or lookup name associated with the finding.</summary>
    public string? RuleName { get; init; }

    /// <summary>Validation severity.</summary>
    public FeatureEditValidationSeverity Severity { get; init; } = FeatureEditValidationSeverity.Error;

    /// <summary>Human-readable validation message.</summary>
    public required string Message { get; init; }

    /// <summary>Optional suggested fix text.</summary>
    public string? SuggestedFix { get; init; }

    /// <summary>Provider feature identifier associated with the finding.</summary>
    public string? FeatureId { get; init; }

    /// <summary>Provider object identifier associated with the finding.</summary>
    public long? ObjectId { get; init; }

    /// <summary>Edit operation associated with the finding.</summary>
    public string? Operation { get; init; }

    /// <summary>Whether this finding blocks applying the edit.</summary>
    public bool BlocksApply => Severity is FeatureEditValidationSeverity.Error or FeatureEditValidationSeverity.Blocking;
}

/// <summary>
/// Request to validate edits without necessarily committing them.
/// </summary>
public sealed record FeatureEditValidationRequest
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Features to validate for add operations.</summary>
    public IReadOnlyList<FeatureEditFeature> Adds { get; init; } = [];

    /// <summary>Features to validate for update operations.</summary>
    public IReadOnlyList<FeatureEditFeature> Updates { get; init; } = [];

    /// <summary>Features to validate for patch operations.</summary>
    public IReadOnlyList<FeatureEditPatch> Patches { get; init; } = [];

    /// <summary>Edit session context for version-aware providers.</summary>
    public FeatureEditSession? Session { get; init; }

    /// <summary>Validation mode to request.</summary>
    public FeatureEditValidationMode Mode { get; init; } = FeatureEditValidationMode.ProviderDefault;

    /// <summary>Whether warnings should be returned when the provider can filter them.</summary>
    public bool ReturnWarnings { get; init; } = true;
}

/// <summary>
/// Response from edit validation.
/// </summary>
public sealed record FeatureEditValidationResponse
{
    /// <summary>Provider name that handled validation.</summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>Validation findings returned by client-side rules or the provider.</summary>
    public IReadOnlyList<FeatureEditValidationResult> Results { get; init; } = [];

    /// <summary>Whether validation returned no blocking findings.</summary>
    public bool IsValid => Results.All(result => !result.BlocksApply);
}
