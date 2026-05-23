// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Field.Forms;

/// <summary>
/// Describes a portable field data-collection form.
/// </summary>
public sealed record FormDefinition
{
    /// <summary>Stable form identifier.</summary>
    public required string FormId { get; init; }

    /// <summary>Human-readable form name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional form version.</summary>
    public string? Version { get; init; }

    /// <summary>Optional form description.</summary>
    public string? Description { get; init; }

    /// <summary>Target source or layer that receives submissions from this form.</summary>
    public FormTarget? Target { get; init; }

    /// <summary>Ordered sections that compose the form.</summary>
    public IReadOnlyList<FormSection> Sections { get; init; } = [];

    /// <summary>Additional non-UI form metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Source target for form submissions.
/// </summary>
public sealed record FormTarget
{
    /// <summary>SDK source descriptor identifier, when known.</summary>
    public string? SourceId { get; init; }

    /// <summary>Honua or GeoServices service identifier.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Feature layer identifier within the target service.</summary>
    public int? LayerId { get; init; }

    /// <summary>OGC API Features collection identifier.</summary>
    public string? CollectionId { get; init; }

    /// <summary>WFS feature type name.</summary>
    public string? TypeName { get; init; }
}

/// <summary>
/// Logical grouping of fields within a form.
/// </summary>
public sealed record FormSection
{
    /// <summary>Stable section identifier.</summary>
    public required string SectionId { get; init; }

    /// <summary>Human-readable section label.</summary>
    public required string Label { get; init; }

    /// <summary>Optional section description or help text.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the section can be repeated to capture multiple entries.</summary>
    public bool Repeatable { get; init; }

    /// <summary>Whether host UI may collapse this section.</summary>
    public bool Collapsible { get; init; }

    /// <summary>Whether host UI should initially collapse this section.</summary>
    public bool InitiallyCollapsed { get; init; }

    /// <summary>Ordered fields in this section.</summary>
    public IReadOnlyList<FormField> Fields { get; init; } = [];
}

/// <summary>
/// Single portable input field definition.
/// </summary>
public sealed record FormField
{
    /// <summary>Stable field identifier used as a record value key.</summary>
    public required string FieldId { get; init; }

    /// <summary>Human-readable field label.</summary>
    public required string Label { get; init; }

    /// <summary>Portable field data type.</summary>
    public FormFieldType Type { get; init; }

    /// <summary>Provider source field name, when different from <see cref="FieldId"/>.</summary>
    public string? SourceFieldName { get; init; }

    /// <summary>Whether a value is required before submission.</summary>
    public bool Required { get; init; }

    /// <summary>Choice values for single- or multi-choice fields.</summary>
    public IReadOnlyList<FieldChoice> Choices { get; init; } = [];

    /// <summary>Shared choice-set identifier when choices are resolved from a catalog.</summary>
    public string? ChoiceSetId { get; init; }

    /// <summary>Validation constraints for this field.</summary>
    public FieldValidationRule Validation { get; init; } = new();

    /// <summary>Media capture policy for photo, video, audio, signature, sketch, or file fields.</summary>
    public FieldMediaCapturePolicy? MediaPolicy { get; init; }

    /// <summary>Referenced form identifier for record-link fields.</summary>
    public string? ReferencedFormId { get; init; }

    /// <summary>Optional visibility rule evaluated against the current record.</summary>
    public FieldVisibilityRule? VisibilityRule { get; init; }

    /// <summary>Optional calculated expression, such as <c>concat($first, ' ', $last)</c>.</summary>
    public string? CalculatedExpression { get; init; }

    /// <summary>Optional help text for host UI.</summary>
    public string? HelpText { get; init; }
}

/// <summary>
/// Supported portable form field types.
/// </summary>
public enum FormFieldType
{
    /// <summary>Free-form text input.</summary>
    Text,

    /// <summary>Numeric input with optional range validation.</summary>
    Numeric,

    /// <summary>Date-only input.</summary>
    Date,

    /// <summary>Time-only input.</summary>
    Time,

    /// <summary>Combined date and time input.</summary>
    DateTime,

    /// <summary>Boolean yes/no input.</summary>
    YesNo,

    /// <summary>Single selection from predefined choices.</summary>
    SingleChoice,

    /// <summary>Multiple selections from predefined choices.</summary>
    MultipleChoice,

    /// <summary>Hierarchical or coded classification picker.</summary>
    Classification,

    /// <summary>Postal or structured address input.</summary>
    Address,

    /// <summary>Absolute URL input.</summary>
    Hyperlink,

    /// <summary>Link to another record.</summary>
    RecordLink,

    /// <summary>Auto-computed field driven by an expression.</summary>
    Calculated,

    /// <summary>Photo capture or upload input.</summary>
    Photo,

    /// <summary>Video capture or upload input.</summary>
    Video,

    /// <summary>Audio capture or upload input.</summary>
    Audio,

    /// <summary>Digital signature capture input.</summary>
    Signature,

    /// <summary>Freehand sketch or annotation input.</summary>
    Sketch,

    /// <summary>Barcode or QR code scan input.</summary>
    Barcode,

    /// <summary>Generic file attachment input.</summary>
    File,

    /// <summary>GPS or map-picked location input.</summary>
    Location
}

/// <summary>
/// One allowed choice for a field.
/// </summary>
public sealed record FieldChoice
{
    /// <summary>Stored choice value.</summary>
    public required string Value { get; init; }

    /// <summary>Display label for host UI.</summary>
    public string? Label { get; init; }

    /// <summary>Parent choice value for hierarchical classifications.</summary>
    public string? ParentValue { get; init; }

    /// <summary>Nested child choices for classification or cascading choice fields.</summary>
    public IReadOnlyList<FieldChoice> Children { get; init; } = [];
}

/// <summary>
/// Validation constraints applied to a field.
/// </summary>
public sealed record FieldValidationRule
{
    /// <summary>Regular expression pattern the value must match.</summary>
    public string? RegexPattern { get; init; }

    /// <summary>Minimum numeric value, inclusive.</summary>
    public double? MinNumericValue { get; init; }

    /// <summary>Maximum numeric value, inclusive.</summary>
    public double? MaxNumericValue { get; init; }

    /// <summary>Minimum string length.</summary>
    public int? MinLength { get; init; }

    /// <summary>Maximum string length.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Minimum media attachment count required for media fields.</summary>
    public int? MinMediaCount { get; init; }

    /// <summary>Maximum media attachment count allowed for media fields.</summary>
    public int? MaxMediaCount { get; init; }
}

/// <summary>
/// Capture and export policy for media-like form fields.
/// </summary>
public sealed record FieldMediaCapturePolicy
{
    /// <summary>Allowed content types. Empty means runtime default.</summary>
    public IReadOnlyList<string> AllowedContentTypes { get; init; } = [];

    /// <summary>Maximum attachment size in bytes.</summary>
    public long? MaxAttachmentBytes { get; init; }

    /// <summary>Whether captured media should include location metadata when available.</summary>
    public bool CaptureLocation { get; init; } = true;

    /// <summary>Whether timed media should include a GPS track reference when available.</summary>
    public bool CaptureGpsTrack { get; init; }

    /// <summary>Whether captured photos should request face blurring before export/upload.</summary>
    public bool RequiresFaceBlur { get; init; }
}

/// <summary>
/// Rule that conditionally shows or hides a field based on another field value.
/// </summary>
public sealed record FieldVisibilityRule
{
    /// <summary>Field whose value controls visibility.</summary>
    public required string DependsOnFieldId { get; init; }

    /// <summary>Comparison operator used for the visibility check.</summary>
    public ComparisonOperator Operator { get; init; }

    /// <summary>Value to compare against.</summary>
    public object? MatchValue { get; init; }
}

/// <summary>
/// Comparison operators for field visibility.
/// </summary>
public enum ComparisonOperator
{
    /// <summary>Values must be equal.</summary>
    Equals,

    /// <summary>Values must not be equal.</summary>
    NotEquals,

    /// <summary>Actual value must be greater than the match value.</summary>
    GreaterThan,

    /// <summary>Actual value must be less than the match value.</summary>
    LessThan,

    /// <summary>Actual value must contain the match value.</summary>
    Contains
}
