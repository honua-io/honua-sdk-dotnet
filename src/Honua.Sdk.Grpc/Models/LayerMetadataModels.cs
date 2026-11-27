// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>Layer field descriptor combining schema definition and editing constraints.</summary>
public sealed class LayerFieldDescriptor
{
    /// <summary>Shared field schema definition.</summary>
    public FieldDefinition Definition { get; init; } = new();

    /// <summary>Whether the field is editable; null when unknown.</summary>
    public bool? Editable { get; init; }

    /// <summary>Whether a default value was explicitly supplied (including explicit null).</summary>
    public bool HasDefaultValue { get; init; }

    /// <summary>Default attribute value when HasDefaultValue is true.</summary>
    public object? DefaultValue { get; init; }

    /// <summary>Optional domain constraining field values.</summary>
    public FieldDomain? Domain { get; init; }

    /// <summary>Optional numeric precision.</summary>
    public int? Precision { get; init; }

    /// <summary>Optional numeric scale.</summary>
    public int? Scale { get; init; }
}

/// <summary>Attribute value domain constraint for a field.</summary>
public sealed class FieldDomain
{
    /// <summary>Domain name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Allowed coded values when this is a coded-value domain.</summary>
    public IReadOnlyList<DomainCodedValue>? CodedValues { get; init; }

    /// <summary>Inclusive value range when this is a range domain.</summary>
    public RangeDomain? Range { get; init; }

    /// <summary>Merge policy name.</summary>
    public string MergePolicy { get; init; } = string.Empty;

    /// <summary>Split policy name.</summary>
    public string SplitPolicy { get; init; } = string.Empty;
}

/// <summary>Typed coded value entry within a coded-value domain.</summary>
public sealed class DomainCodedValue
{
    /// <summary>Typed code value.</summary>
    public object? Code { get; init; }

    /// <summary>Human-readable label.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional description.</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>Inclusive range constraint in a field's native attribute type.</summary>
public sealed class RangeDomain
{
    /// <summary>Inclusive minimum value.</summary>
    public object? Minimum { get; init; }

    /// <summary>Inclusive maximum value.</summary>
    public object? Maximum { get; init; }
}

/// <summary>Subtype definition for a layer.</summary>
public sealed class SubtypeDefinition
{
    /// <summary>Name of the subtype discriminator field.</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>Default subtype code value.</summary>
    public object? DefaultCode { get; init; }

    /// <summary>Declared layer subtypes.</summary>
    public IReadOnlyList<LayerSubtype> Subtypes { get; init; } = [];
}

/// <summary>Single subtype entry with field overrides and templates.</summary>
public sealed class LayerSubtype
{
    /// <summary>Typed subtype discriminator code.</summary>
    public object? Code { get; init; }

    /// <summary>Subtype display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Field-level default and domain overrides keyed by field name.</summary>
    public IReadOnlyDictionary<string, SubtypeFieldOverride> FieldOverrides { get; init; } =
        new Dictionary<string, SubtypeFieldOverride>();

    /// <summary>Feature templates specific to this subtype.</summary>
    public IReadOnlyList<FeatureTemplate> Templates { get; init; } = [];
}

/// <summary>Subtype-specific default value and domain override for one field.</summary>
public sealed class SubtypeFieldOverride
{
    /// <summary>Whether a default value override was explicitly supplied.</summary>
    public bool HasDefaultValue { get; init; }

    /// <summary>Overridden default value when HasDefaultValue is true.</summary>
    public object? DefaultValue { get; init; }

    /// <summary>Replacement domain when the subtype sets a domain.</summary>
    public FieldDomain? Domain { get; init; }

    /// <summary>True when the subtype explicitly clears the inherited field domain.</summary>
    public bool ClearDomain { get; init; }
}

/// <summary>Feature attribute template for creating new features.</summary>
public sealed class FeatureTemplate
{
    /// <summary>Template name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Template description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Default attribute values supplied by the template.</summary>
    public IReadOnlyDictionary<string, object?> DefaultAttributes { get; init; } =
        new Dictionary<string, object?>();
}

/// <summary>Metadata describing a relationship between two layers in a service.</summary>
public sealed class RelationshipDescriptor
{
    /// <summary>Relationship identifier.</summary>
    public int RelationshipId { get; init; }

    /// <summary>Relationship name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Related destination or origin layer identifier.</summary>
    public int RelatedLayerId { get; init; }

    /// <summary>Origin key field name.</summary>
    public string OriginKeyField { get; init; } = string.Empty;

    /// <summary>Destination key field name.</summary>
    public string DestinationKeyField { get; init; } = string.Empty;

    /// <summary>Relationship cardinality.</summary>
    public RelationshipCardinality Cardinality { get; init; }

    /// <summary>Described layer's role in the relationship.</summary>
    public RelationshipRole Role { get; init; }

    /// <summary>Whether the relationship is composite, when known.</summary>
    public bool? Composite { get; init; }

    /// <summary>Optional relationship description.</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>Temporal metadata for a layer.</summary>
public sealed class LayerTimeInfo
{
    /// <summary>Start time field name.</summary>
    public string StartField { get; init; } = string.Empty;

    /// <summary>End time field name, empty for instant observations.</summary>
    public string EndField { get; init; } = string.Empty;

    /// <summary>IANA time zone identifier, empty when unknown.</summary>
    public string TimeZone { get; init; } = string.Empty;

    /// <summary>Whether the time zone observes daylight saving time, when known.</summary>
    public bool? RespectsDaylightSaving { get; init; }

    /// <summary>Start of the temporal extent in UTC milliseconds since epoch.</summary>
    public long? ExtentStartUtcMs { get; init; }

    /// <summary>End of the temporal extent in UTC milliseconds since epoch.</summary>
    public long? ExtentEndUtcMs { get; init; }

    /// <summary>Default temporal navigation interval.</summary>
    public double? DefaultInterval { get; init; }

    /// <summary>Unit of the default temporal navigation interval.</summary>
    public TimeIntervalUnit IntervalUnit { get; init; }
}

/// <summary>Server-owned editor tracking and ownership restrictions for a layer.</summary>
public sealed class LayerEditingInfo
{
    /// <summary>Creator tracking field name.</summary>
    public string CreatorField { get; init; } = string.Empty;

    /// <summary>Creation timestamp field name.</summary>
    public string CreatedAtField { get; init; } = string.Empty;

    /// <summary>Last editor tracking field name.</summary>
    public string EditorField { get; init; } = string.Empty;

    /// <summary>Last edit timestamp field name.</summary>
    public string EditedAtField { get; init; } = string.Empty;

    /// <summary>Whether editor tracking is enabled.</summary>
    public bool? EditorTrackingEnabled { get; init; }

    /// <summary>Whether ownership-based access control is enabled.</summary>
    public bool? OwnershipAccessControlEnabled { get; init; }

    /// <summary>Whether callers may update features created by other users.</summary>
    public bool? AllowUpdateOthers { get; init; }

    /// <summary>Whether callers may delete features created by other users.</summary>
    public bool? AllowDeleteOthers { get; init; }

    /// <summary>Timestamp of the last edit in UTC milliseconds since epoch.</summary>
    public long? LastEditUtcMs { get; init; }
}

/// <summary>Effective attachment byte and count limits advertised for a layer.</summary>
public sealed class AttachmentLimits
{
    /// <summary>Maximum single attachment size in bytes; zero means unspecified.</summary>
    public long MaxFileBytes { get; init; }

    /// <summary>Maximum attachment count per feature; zero means unspecified.</summary>
    public int MaxAttachmentsPerFeature { get; init; }

    /// <summary>Maximum total attachment bytes per feature; zero means unspecified.</summary>
    public long MaxTotalBytesPerFeature { get; init; }

    /// <summary>Allowed MIME content types.</summary>
    public IReadOnlyList<string> AllowedContentTypes { get; init; } = [];

    /// <summary>Maximum bytes permitted in a single unary UploadAttachment call; zero means unspecified.</summary>
    public long MaxUnaryUploadBytes { get; init; }

    /// <summary>Maximum bytes permitted in a single UploadAttachmentChunk call; zero means unspecified.</summary>
    public long MaxChunkBytes { get; init; }
}
