// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Google.Protobuf;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Conversion;

internal static partial class ProtoAdapter
{
    public static Proto.ListAttachmentsRequest ToProtoListAttachmentsRequest(Models.ListAttachmentsRequest request)
    {
        var proto = new Proto.ListAttachmentsRequest
        {
            Parent = ToProtoAttachmentParent(request.Parent),
            PageSize = request.PageSize,
            PageToken = request.PageToken ?? string.Empty,
        };

        if (request.ContentTypes is not null)
        {
            proto.ContentTypes.AddRange(request.ContentTypes);
        }

        if (request.Keywords is not null)
        {
            proto.Keywords.AddRange(request.Keywords);
        }

        return proto;
    }

    public static Models.ListAttachmentsResponse FromProtoListAttachmentsResponse(Proto.ListAttachmentsResponse response)
    {
        return new Models.ListAttachmentsResponse
        {
            Attachments = response.Attachments.Select(ConvertAttachmentDescriptor).ToList(),
            NextPageToken = response.NextPageToken,
        };
    }

    public static Proto.GetAttachmentUrlRequest ToProtoGetAttachmentUrlRequest(Models.GetAttachmentUrlRequest request)
    {
        return new Proto.GetAttachmentUrlRequest
        {
            Parent = ToProtoAttachmentParent(request.Parent),
            AttachmentId = request.AttachmentId,
        };
    }

    public static Models.GetAttachmentUrlResponse FromProtoGetAttachmentUrlResponse(
        Proto.GetAttachmentUrlResponse response)
    {
        Uri? parsedUrl = null;
        if (!string.IsNullOrWhiteSpace(response.Url))
        {
            Uri.TryCreate(response.Url, UriKind.RelativeOrAbsolute, out parsedUrl);
        }

        return new Models.GetAttachmentUrlResponse
        {
            Url = parsedUrl,
            ExpiresAtUtcMs = response.ExpiresAtUtcMs,
            Attachment = response.Attachment is not null ? ConvertAttachmentDescriptor(response.Attachment) : null,
        };
    }

    public static Proto.UploadAttachmentRequest ToProtoUploadAttachmentRequest(Models.UploadAttachmentRequest request)
    {
        return new Proto.UploadAttachmentRequest
        {
            Upload = ToProtoAttachmentUpload(request.Upload),
            Content = ByteString.CopyFrom(request.Content.Span),
        };
    }

    public static Models.UploadAttachmentResponse FromProtoUploadAttachmentResponse(
        Proto.UploadAttachmentResponse response)
    {
        return new Models.UploadAttachmentResponse
        {
            Attachment = response.Attachment is not null ? ConvertAttachmentDescriptor(response.Attachment) : null,
        };
    }

    public static Proto.BeginAttachmentUploadRequest ToProtoBeginAttachmentUploadRequest(
        Models.BeginAttachmentUploadRequest request)
    {
        return new Proto.BeginAttachmentUploadRequest
        {
            Upload = ToProtoAttachmentUpload(request.Upload),
        };
    }

    public static Models.BeginAttachmentUploadResponse FromProtoBeginAttachmentUploadResponse(
        Proto.BeginAttachmentUploadResponse response)
    {
        return new Models.BeginAttachmentUploadResponse
        {
            Session = response.Session is not null ? ConvertAttachmentUploadSession(response.Session) : null,
        };
    }

    public static Proto.UploadAttachmentChunkRequest ToProtoUploadAttachmentChunkRequest(
        Models.UploadAttachmentChunkRequest request)
    {
        return new Proto.UploadAttachmentChunkRequest
        {
            SessionId = request.SessionId,
            OffsetBytes = request.OffsetBytes,
            Content = ByteString.CopyFrom(request.Content.Span),
            Sha256 = ByteString.CopyFrom(request.Sha256.Span),
        };
    }

    public static Models.UploadAttachmentChunkResponse FromProtoUploadAttachmentChunkResponse(
        Proto.UploadAttachmentChunkResponse response)
    {
        return new Models.UploadAttachmentChunkResponse
        {
            Session = response.Session is not null ? ConvertAttachmentUploadSession(response.Session) : null,
        };
    }

    public static Proto.CommitAttachmentUploadRequest ToProtoCommitAttachmentUploadRequest(
        Models.CommitAttachmentUploadRequest request)
    {
        return new Proto.CommitAttachmentUploadRequest
        {
            SessionId = request.SessionId,
        };
    }

    public static Models.CommitAttachmentUploadResponse FromProtoCommitAttachmentUploadResponse(
        Proto.CommitAttachmentUploadResponse response)
    {
        return new Models.CommitAttachmentUploadResponse
        {
            Attachment = response.Attachment is not null ? ConvertAttachmentDescriptor(response.Attachment) : null,
        };
    }

    public static Proto.AbortAttachmentUploadRequest ToProtoAbortAttachmentUploadRequest(
        Models.AbortAttachmentUploadRequest request)
    {
        return new Proto.AbortAttachmentUploadRequest
        {
            SessionId = request.SessionId,
        };
    }

    public static Models.AbortAttachmentUploadResponse FromProtoAbortAttachmentUploadResponse(
        Proto.AbortAttachmentUploadResponse _)
    {
        return new Models.AbortAttachmentUploadResponse();
    }

    public static Proto.DeleteAttachmentRequest ToProtoDeleteAttachmentRequest(Models.DeleteAttachmentRequest request)
    {
        return new Proto.DeleteAttachmentRequest
        {
            Parent = ToProtoAttachmentParent(request.Parent),
            AttachmentId = request.AttachmentId,
            IdempotencyKey = request.IdempotencyKey,
        };
    }

    public static Models.DeleteAttachmentResponse FromProtoDeleteAttachmentResponse(
        Proto.DeleteAttachmentResponse response)
    {
        return new Models.DeleteAttachmentResponse
        {
            AttachmentId = response.AttachmentId,
        };
    }

    private static Proto.AttachmentParent ToProtoAttachmentParent(Models.AttachmentParent parent)
    {
        ArgumentNullException.ThrowIfNull(parent);

        var hasObjectId = parent.ObjectId.HasValue;
        var hasGlobalId = !string.IsNullOrWhiteSpace(parent.GlobalId);
        if (hasObjectId == hasGlobalId)
        {
            throw new ArgumentException(
                "AttachmentParent must specify exactly one of ObjectId or GlobalId.",
                nameof(parent));
        }

        var proto = new Proto.AttachmentParent
        {
            ServiceId = parent.ServiceId,
            LayerId = parent.LayerId,
        };

        if (parent.ObjectId is long objectId)
        {
            proto.ObjectId = objectId;
        }
        else
        {
            proto.GlobalId = parent.GlobalId!;
        }

        return proto;
    }

    private static Proto.AttachmentUpload ToProtoAttachmentUpload(Models.AttachmentUpload upload)
    {
        ArgumentNullException.ThrowIfNull(upload);

        var proto = new Proto.AttachmentUpload
        {
            Parent = ToProtoAttachmentParent(upload.Parent),
            Name = upload.Name,
            ContentType = upload.ContentType,
            SizeBytes = upload.SizeBytes,
            Sha256 = ByteString.CopyFrom(upload.Sha256.Span),
            IdempotencyKey = upload.IdempotencyKey,
        };

        if (upload.Keywords is not null)
        {
            proto.Keywords.AddRange(upload.Keywords);
        }

        if (upload.ReplaceAttachmentId is long replaceId)
        {
            proto.ReplaceAttachmentId = replaceId;
        }

        return proto;
    }

    private static Models.AttachmentDescriptor ConvertAttachmentDescriptor(Proto.AttachmentDescriptor descriptor)
    {
        return new Models.AttachmentDescriptor
        {
            AttachmentId = descriptor.AttachmentId,
            Name = descriptor.Name,
            ContentType = descriptor.ContentType,
            SizeBytes = descriptor.SizeBytes,
            Keywords = descriptor.Keywords.ToList(),
            GlobalId = descriptor.GlobalId,
            Sha256 = descriptor.Sha256.Memory,
        };
    }

    private static Models.AttachmentUploadSession ConvertAttachmentUploadSession(Proto.AttachmentUploadSession session)
    {
        return new Models.AttachmentUploadSession
        {
            SessionId = session.SessionId,
            ExpiresAtUtcMs = session.ExpiresAtUtcMs,
            NextOffsetBytes = session.NextOffsetBytes,
            MaxChunkBytes = session.MaxChunkBytes,
        };
    }
}
