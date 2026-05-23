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

    /// <summary>Media duration for audio/video attachments.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Optional GPS track reference for timed media.</summary>
    public FieldGpsTrackReference? GpsTrack { get; init; }

    /// <summary>Lowercase hexadecimal SHA-256 digest, when known.</summary>
    public string? Sha256 { get; init; }

    /// <summary>Whether the host should blur faces before upload or export.</summary>
    public bool RequiresFaceBlur { get; init; }
}

/// <summary>
/// Portable structured address value.
/// </summary>
public sealed record FieldAddressValue
{
    /// <summary>Single-line formatted address.</summary>
    public string? Formatted { get; init; }

    /// <summary>Street address lines.</summary>
    public IReadOnlyList<string> Lines { get; init; } = [];

    /// <summary>City or locality.</summary>
    public string? Locality { get; init; }

    /// <summary>State, province, or region.</summary>
    public string? Region { get; init; }

    /// <summary>Postal or ZIP code.</summary>
    public string? PostalCode { get; init; }

    /// <summary>Country code or country name.</summary>
    public string? Country { get; init; }

    /// <summary>Geocoded location for the address, when available.</summary>
    public FieldGeoPoint? Location { get; init; }
}

/// <summary>
/// Portable record-link value.
/// </summary>
public sealed record FieldRecordLinkValue
{
    /// <summary>Referenced form identifier.</summary>
    public string? FormId { get; init; }

    /// <summary>Referenced source identifier.</summary>
    public string? SourceId { get; init; }

    /// <summary>Referenced record identifier.</summary>
    public required string RecordId { get; init; }

    /// <summary>Display label captured at link time.</summary>
    public string? Label { get; init; }
}

/// <summary>
/// Portable barcode or QR scan value.
/// </summary>
public sealed record FieldBarcodeValue
{
    /// <summary>Decoded barcode or QR value.</summary>
    public required string Value { get; init; }

    /// <summary>Barcode format, such as QR_CODE or CODE_128, when known.</summary>
    public string? Format { get; init; }

    /// <summary>UTC scan time.</summary>
    public DateTimeOffset ScannedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Portable GPS track reference associated with audio or video capture.
/// </summary>
public sealed record FieldGpsTrackReference
{
    /// <summary>Stable track identifier.</summary>
    public required string TrackId { get; init; }

    /// <summary>Track file name without host-specific local path.</summary>
    public string? FileName { get; init; }

    /// <summary>Track content type, for example application/geo+json or application/gpx+xml.</summary>
    public string? ContentType { get; init; }

    /// <summary>Number of positions in the track, when known.</summary>
    public int? PointCount { get; init; }

    /// <summary>UTC track start time.</summary>
    public DateTimeOffset? StartedAtUtc { get; init; }

    /// <summary>UTC track end time.</summary>
    public DateTimeOffset? EndedAtUtc { get; init; }
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

    /// <summary>Record is valid locally and ready to submit or sync.</summary>
    ReadyToSubmit,

    /// <summary>Record has been submitted for review or sync.</summary>
    Submitted,

    /// <summary>Record has been approved.</summary>
    Approved,

    /// <summary>Record has been rejected and may be resubmitted.</summary>
    Rejected,

    /// <summary>Record has been reopened after review.</summary>
    Reopened,

    /// <summary>Record has been locally deleted or marked for delete sync.</summary>
    Deleted
}
