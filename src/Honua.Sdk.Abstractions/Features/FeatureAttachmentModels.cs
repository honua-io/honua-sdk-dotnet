// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Provider attachment capabilities exposed through the shared feature attachment abstraction.
/// </summary>
public sealed record FeatureAttachmentCapabilities
{
    /// <summary>Whether the provider can list attachments for a feature.</summary>
    public bool SupportsList { get; init; }

    /// <summary>Whether the provider can download attachment content.</summary>
    public bool SupportsDownload { get; init; }

    /// <summary>Whether the provider can add attachments.</summary>
    public bool SupportsAdd { get; init; }

    /// <summary>Whether the provider can update attachment content or metadata.</summary>
    public bool SupportsUpdate { get; init; }

    /// <summary>Whether the provider can delete attachments.</summary>
    public bool SupportsDelete { get; init; }

    /// <summary>Native protocol surface used by the provider, when useful for diagnostics.</summary>
    public string? NativeSurface { get; init; }

    /// <summary>Reason attachments are unsupported when no attachment operation is available.</summary>
    public string? UnsupportedReason { get; init; }
}

/// <summary>
/// Provider-neutral attachment metadata.
/// </summary>
public sealed record FeatureAttachmentInfo
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Object ID of the parent feature.</summary>
    public long? ParentObjectId { get; init; }

    /// <summary>Provider attachment identifier.</summary>
    public long? AttachmentId { get; init; }

    /// <summary>Provider global ID, when available.</summary>
    public string? GlobalId { get; init; }

    /// <summary>Attachment file name.</summary>
    public string? Name { get; init; }

    /// <summary>Attachment content type.</summary>
    public string? ContentType { get; init; }

    /// <summary>Attachment size in bytes.</summary>
    public long? Size { get; init; }

    /// <summary>Provider keywords or tags associated with the attachment.</summary>
    public string? Keywords { get; init; }

    /// <summary>Provider URL for direct attachment access, when advertised.</summary>
    public Uri? Url { get; init; }
}

/// <summary>
/// Request to list attachments for one feature.
/// </summary>
public sealed record FeatureAttachmentListRequest
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Object ID of the parent feature.</summary>
    public required long ObjectId { get; init; }
}

/// <summary>
/// Request to download one attachment.
/// </summary>
public sealed record FeatureAttachmentDownloadRequest
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Object ID of the parent feature.</summary>
    public required long ObjectId { get; init; }

    /// <summary>Provider attachment identifier.</summary>
    public required long AttachmentId { get; init; }
}

/// <summary>
/// Downloaded attachment content. Dispose <see cref="Content"/> when finished.
/// </summary>
public sealed record FeatureAttachmentContent
{
    /// <summary>Attachment metadata known at download time.</summary>
    public FeatureAttachmentInfo Info { get; init; } = new();

    /// <summary>Attachment content stream.</summary>
    public required Stream Content { get; init; }
}

/// <summary>
/// Request to add an attachment to one feature.
/// </summary>
public sealed record FeatureAttachmentAddRequest
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Object ID of the parent feature.</summary>
    public required long ObjectId { get; init; }

    /// <summary>Attachment file name.</summary>
    public required string Name { get; init; }

    /// <summary>Attachment content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>Attachment content stream.</summary>
    public required Stream Content { get; init; }

    /// <summary>Provider keywords or tags associated with the attachment.</summary>
    public string? Keywords { get; init; }
}

/// <summary>
/// Request to update an attachment.
/// </summary>
public sealed record FeatureAttachmentUpdateRequest
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Object ID of the parent feature.</summary>
    public required long ObjectId { get; init; }

    /// <summary>Provider attachment identifier.</summary>
    public required long AttachmentId { get; init; }

    /// <summary>Replacement attachment file name.</summary>
    public required string Name { get; init; }

    /// <summary>Replacement attachment content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>Replacement attachment content stream.</summary>
    public required Stream Content { get; init; }

    /// <summary>Provider keywords or tags associated with the attachment.</summary>
    public string? Keywords { get; init; }
}

/// <summary>
/// Request to delete one attachment.
/// </summary>
public sealed record FeatureAttachmentDeleteRequest
{
    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Object ID of the parent feature.</summary>
    public required long ObjectId { get; init; }

    /// <summary>Provider attachment identifier.</summary>
    public required long AttachmentId { get; init; }
}

/// <summary>
/// Provider-neutral outcome of a single attachment edit operation.
/// </summary>
public sealed record FeatureAttachmentResult
{
    /// <summary>Provider attachment identifier.</summary>
    public long? AttachmentId { get; init; }

    /// <summary>Provider global ID, when available.</summary>
    public string? GlobalId { get; init; }

    /// <summary>Whether the attachment operation succeeded.</summary>
    public bool Succeeded { get; init; }

    /// <summary>Error details when the operation failed.</summary>
    public FeatureEditError? Error { get; init; }
}
