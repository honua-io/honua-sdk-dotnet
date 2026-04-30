// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Collections.ObjectModel;

namespace Honua.Sdk.Field.Records;

/// <summary>
/// Portable field record captured against a form definition.
/// </summary>
public sealed class FieldRecord
{
    /// <summary>Stable record identifier.</summary>
    public required string RecordId { get; init; }

    /// <summary>Form identifier used for capture.</summary>
    public required string FormId { get; init; }

    /// <summary>Captured field values keyed by form field identifier.</summary>
    public Dictionary<string, object?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Portable media attachment metadata. Host-specific local paths stay outside the SDK contract.</summary>
    public Collection<FieldMediaAttachment> Media { get; init; } = [];

    /// <summary>Capture location, when available.</summary>
    public FieldGeoPoint? Location { get; set; }

    /// <summary>Current workflow status.</summary>
    public RecordStatus Status { get; set; } = RecordStatus.Draft;

    /// <summary>User assigned to this record, when applicable.</summary>
    public string? AssignedUserId { get; set; }

    /// <summary>UTC time the record was created.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>UTC time the record was submitted.</summary>
    public DateTimeOffset? SubmittedAtUtc { get; set; }

    /// <summary>UTC time the record was completed.</summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Elapsed time from creation to completion.</summary>
    public TimeSpan? Duration => CompletedAtUtc.HasValue ? CompletedAtUtc.Value - CreatedAtUtc : null;
}

/// <summary>
/// Portable media metadata attached to a field record.
/// </summary>
public sealed record FieldMediaAttachment
{
    /// <summary>Stable attachment identifier.</summary>
    public required string AttachmentId { get; init; }

    /// <summary>Field that owns this media attachment, when known.</summary>
    public string? FieldId { get; init; }

    /// <summary>Media type.</summary>
    public FieldMediaType MediaType { get; init; }

    /// <summary>File name without host-specific local path.</summary>
    public string? FileName { get; init; }

    /// <summary>Media content type.</summary>
    public string? ContentType { get; init; }

    /// <summary>Media size in bytes.</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Location where the media was captured.</summary>
    public FieldGeoPoint? CaptureLocation { get; init; }

    /// <summary>UTC time the media was captured.</summary>
    public DateTimeOffset CapturedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Whether the host should blur faces before upload or export.</summary>
    public bool RequiresFaceBlur { get; init; }
}

/// <summary>
/// Geographic point captured with a field record.
/// </summary>
/// <param name="Latitude">Latitude in decimal degrees.</param>
/// <param name="Longitude">Longitude in decimal degrees.</param>
/// <param name="AccuracyMeters">Horizontal accuracy in meters.</param>
public sealed record FieldGeoPoint(double Latitude, double Longitude, double? AccuracyMeters = null);

/// <summary>
/// Portable media types.
/// </summary>
public enum FieldMediaType
{
    /// <summary>Photograph.</summary>
    Photo,

    /// <summary>Video recording.</summary>
    Video,

    /// <summary>Audio recording.</summary>
    Audio,

    /// <summary>Digital signature.</summary>
    Signature,

    /// <summary>Sketch or annotation.</summary>
    Sketch,

    /// <summary>Generic file attachment.</summary>
    File
}

/// <summary>
/// Workflow status of a field record.
/// </summary>
public enum RecordStatus
{
    /// <summary>Record is being edited.</summary>
    Draft,

    /// <summary>Record has been submitted for review or sync.</summary>
    Submitted,

    /// <summary>Record has been approved.</summary>
    Approved,

    /// <summary>Record has been rejected and may be resubmitted.</summary>
    Rejected
}
