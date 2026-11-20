// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>Identifies the parent feature of an attachment by object ID or global ID.</summary>
public sealed record AttachmentParent
{
    /// <summary>Service identifier.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Layer identifier within the service.</summary>
    public required int LayerId { get; init; }

    /// <summary>Integer object ID of the parent feature; mutually exclusive with GlobalId.</summary>
    public long? ObjectId { get; init; }

    /// <summary>Global UUID of the parent feature; mutually exclusive with ObjectId.</summary>
    public string? GlobalId { get; init; }
}

/// <summary>Metadata describing one stored feature attachment.</summary>
public sealed class AttachmentDescriptor
{
    /// <summary>Server-assigned attachment identifier.</summary>
    public long AttachmentId { get; init; }

    /// <summary>Display file name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Validated MIME content type.</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>Attachment size in bytes.</summary>
    public long SizeBytes { get; init; }

    /// <summary>Keywords associated with the attachment.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>Stable global identifier when available.</summary>
    public string GlobalId { get; init; } = string.Empty;

    /// <summary>SHA-256 content digest (32 bytes) when available, or empty.</summary>
    public ReadOnlyMemory<byte> Sha256 { get; init; }
}

/// <summary>Request parameters for listing attachments of a feature.</summary>
public sealed record ListAttachmentsRequest
{
    /// <summary>Parent feature identity.</summary>
    public required AttachmentParent Parent { get; init; }

    /// <summary>Optional exact MIME content-type filters.</summary>
    public IReadOnlyList<string>? ContentTypes { get; init; }

    /// <summary>Optional keyword filters matching any listed keyword.</summary>
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>Maximum number of attachments to return; zero selects the server default.</summary>
    public int PageSize { get; init; }

    /// <summary>Opaque continuation token from a preceding response.</summary>
    public string PageToken { get; init; } = string.Empty;
}

/// <summary>Response page containing feature attachment descriptors.</summary>
public sealed class ListAttachmentsResponse
{
    /// <summary>Attachment descriptors in this page.</summary>
    public IReadOnlyList<AttachmentDescriptor> Attachments { get; init; } = [];

    /// <summary>Opaque continuation token, empty when no more attachments remain.</summary>
    public string NextPageToken { get; init; } = string.Empty;
}

/// <summary>Request parameters for obtaining a short-lived signed attachment download URL.</summary>
public sealed record GetAttachmentUrlRequest
{
    /// <summary>Parent feature identity.</summary>
    public required AttachmentParent Parent { get; init; }

    /// <summary>Attachment identifier.</summary>
    public required long AttachmentId { get; init; }
}

/// <summary>Response carrying a short-lived signed download URL and attachment metadata.</summary>
public sealed class GetAttachmentUrlResponse
{
    /// <summary>Short-lived bearer download URL; do not persist or log.</summary>
    public Uri? Url { get; init; }

    /// <summary>Grant expiration timestamp in UTC milliseconds since epoch.</summary>
    public long ExpiresAtUtcMs { get; init; }

    /// <summary>Attachment descriptor.</summary>
    public AttachmentDescriptor? Attachment { get; init; }
}

/// <summary>Metadata describing an attachment creation or atomic replacement upload.</summary>
public sealed record AttachmentUpload
{
    /// <summary>Parent feature identity.</summary>
    public required AttachmentParent Parent { get; init; }

    /// <summary>Display file name.</summary>
    public required string Name { get; init; }

    /// <summary>MIME content type.</summary>
    public required string ContentType { get; init; }

    /// <summary>Optional keywords associated with the attachment.</summary>
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>Total attachment content size in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Required 32-byte SHA-256 digest of the complete attachment content.</summary>
    public required ReadOnlyMemory<byte> Sha256 { get; init; }

    /// <summary>Optional attachment ID to replace atomically; null creates a new attachment.</summary>
    public long? ReplaceAttachmentId { get; init; }

    /// <summary>Required client-generated idempotency key.</summary>
    public required string IdempotencyKey { get; init; }
}

/// <summary>Request parameters for a bounded unary attachment upload.</summary>
public sealed record UploadAttachmentRequest
{
    /// <summary>Upload metadata, size, digest, and idempotency key.</summary>
    public required AttachmentUpload Upload { get; init; }

    /// <summary>Complete attachment content bytes.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }
}

/// <summary>Response confirming a published attachment upload.</summary>
public sealed class UploadAttachmentResponse
{
    /// <summary>Published attachment descriptor.</summary>
    public AttachmentDescriptor? Attachment { get; init; }
}

/// <summary>Request parameters for starting or resuming a chunked attachment upload session.</summary>
public sealed record BeginAttachmentUploadRequest
{
    /// <summary>Upload metadata, total size, complete SHA-256 digest, and idempotency key.</summary>
    public required AttachmentUpload Upload { get; init; }
}

/// <summary>Durable state of a chunked attachment upload session.</summary>
public sealed class AttachmentUploadSession
{
    /// <summary>Server-assigned upload session identifier.</summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>Session expiration timestamp in UTC milliseconds since epoch.</summary>
    public long ExpiresAtUtcMs { get; init; }

    /// <summary>Next expected byte offset for chunk uploads.</summary>
    public long NextOffsetBytes { get; init; }

    /// <summary>Maximum bytes accepted in a single UploadAttachmentChunk call.</summary>
    public long MaxChunkBytes { get; init; }
}

/// <summary>Response returning the created or resumed upload session.</summary>
public sealed class BeginAttachmentUploadResponse
{
    /// <summary>Upload session state.</summary>
    public AttachmentUploadSession? Session { get; init; }
}

/// <summary>Request parameters for appending one chunk to an upload session.</summary>
public sealed record UploadAttachmentChunkRequest
{
    /// <summary>Upload session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Byte offset matching the session's next expected offset or an already accepted offset.</summary>
    public required long OffsetBytes { get; init; }

    /// <summary>Chunk content bytes.</summary>
    public required ReadOnlyMemory<byte> Content { get; init; }

    /// <summary>Required 32-byte SHA-256 digest of this chunk's bytes.</summary>
    public required ReadOnlyMemory<byte> Sha256 { get; init; }
}

/// <summary>Response reporting updated upload session progress after a chunk upload.</summary>
public sealed class UploadAttachmentChunkResponse
{
    /// <summary>Updated upload session state.</summary>
    public AttachmentUploadSession? Session { get; init; }
}

/// <summary>Request parameters for atomically publishing a completed chunked upload.</summary>
public sealed record CommitAttachmentUploadRequest
{
    /// <summary>Upload session identifier.</summary>
    public required string SessionId { get; init; }
}

/// <summary>Response returning the atomically published attachment descriptor.</summary>
public sealed class CommitAttachmentUploadResponse
{
    /// <summary>Published attachment descriptor.</summary>
    public AttachmentDescriptor? Attachment { get; init; }
}

/// <summary>Request parameters for discarding an uncommitted upload session.</summary>
public sealed record AbortAttachmentUploadRequest
{
    /// <summary>Upload session identifier.</summary>
    public required string SessionId { get; init; }
}

/// <summary>Response confirming an upload session has been aborted.</summary>
public sealed class AbortAttachmentUploadResponse
{
}

/// <summary>Request parameters for deleting one attachment with idempotency protection.</summary>
public sealed record DeleteAttachmentRequest
{
    /// <summary>Parent feature identity.</summary>
    public required AttachmentParent Parent { get; init; }

    /// <summary>Attachment identifier to delete.</summary>
    public required long AttachmentId { get; init; }

    /// <summary>Required client-generated idempotency key.</summary>
    public required string IdempotencyKey { get; init; }
}

/// <summary>Response confirming deletion of an attachment.</summary>
public sealed class DeleteAttachmentResponse
{
    /// <summary>Deleted attachment identifier.</summary>
    public long AttachmentId { get; init; }
}
