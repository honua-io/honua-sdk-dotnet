// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Security.Cryptography;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Grpc.Conversion;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc;

public sealed partial class HonuaGrpcClient
{
    private const string UnsupportedDirectDownloadReason =
        "gRPC FeatureService issues short-lived signed download URLs via GetAttachmentUrlAsync " +
        "rather than streaming attachment bytes over gRPC.";

    /// <inheritdoc />
    public Task<Models.ListAttachmentsResponse> ListAttachmentsAsync(
        Models.ListAttachmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoListAttachmentsRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.ListAttachmentsResponse, Models.ListAttachmentsResponse>(
                "ListAttachments",
                (metadata, deadline, token) => _client.ListAttachmentsAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoListAttachmentsResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Models.GetAttachmentUrlResponse> GetAttachmentUrlAsync(
        Models.GetAttachmentUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoGetAttachmentUrlRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.GetAttachmentUrlResponse, Models.GetAttachmentUrlResponse>(
                "GetAttachmentUrl",
                (metadata, deadline, token) => _client.GetAttachmentUrlAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoGetAttachmentUrlResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Models.UploadAttachmentResponse> UploadAttachmentAsync(
        Models.UploadAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoUploadAttachmentRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.UploadAttachmentResponse, Models.UploadAttachmentResponse>(
                "UploadAttachment",
                (metadata, deadline, token) => _client.UploadAttachmentAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoUploadAttachmentResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Models.BeginAttachmentUploadResponse> BeginAttachmentUploadAsync(
        Models.BeginAttachmentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoBeginAttachmentUploadRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.BeginAttachmentUploadResponse, Models.BeginAttachmentUploadResponse>(
                "BeginAttachmentUpload",
                (metadata, deadline, token) => _client.BeginAttachmentUploadAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoBeginAttachmentUploadResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Models.UploadAttachmentChunkResponse> UploadAttachmentChunkAsync(
        Models.UploadAttachmentChunkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoUploadAttachmentChunkRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.UploadAttachmentChunkResponse, Models.UploadAttachmentChunkResponse>(
                "UploadAttachmentChunk",
                (metadata, deadline, token) => _client.UploadAttachmentChunkAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoUploadAttachmentChunkResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Models.CommitAttachmentUploadResponse> CommitAttachmentUploadAsync(
        Models.CommitAttachmentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoCommitAttachmentUploadRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.CommitAttachmentUploadResponse, Models.CommitAttachmentUploadResponse>(
                "CommitAttachmentUpload",
                (metadata, deadline, token) => _client.CommitAttachmentUploadAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoCommitAttachmentUploadResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Models.AbortAttachmentUploadResponse> AbortAttachmentUploadAsync(
        Models.AbortAttachmentUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoAbortAttachmentUploadRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.AbortAttachmentUploadResponse, Models.AbortAttachmentUploadResponse>(
                "AbortAttachmentUpload",
                (metadata, deadline, token) => _client.AbortAttachmentUploadAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoAbortAttachmentUploadResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Models.DeleteAttachmentResponse> DeleteAttachmentAsync(
        Models.DeleteAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoDeleteAttachmentRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.DeleteAttachmentResponse, Models.DeleteAttachmentResponse>(
                "DeleteAttachment",
                (metadata, deadline, token) => _client.DeleteAttachmentAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoDeleteAttachmentResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FeatureAttachmentInfo>> ListAttachmentsAsync(
        FeatureAttachmentListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parent = BuildAttachmentParent(request.Source, request.ObjectId);
        var items = new List<FeatureAttachmentInfo>();
        var pageToken = string.Empty;

        while (true)
        {
            var page = await ListAttachmentsAsync(
                new Models.ListAttachmentsRequest
                {
                    Parent = parent,
                    PageToken = pageToken,
                },
                cancellationToken).ConfigureAwait(false);
            foreach (var attachment in page.Attachments)
            {
                items.Add(ToFeatureAttachmentInfo(request.Source, request.ObjectId, attachment));
            }

            if (string.IsNullOrEmpty(page.NextPageToken))
            {
                return items;
            }

            pageToken = page.NextPageToken;
        }
    }

    /// <inheritdoc />
    public Task<FeatureAttachmentContent> DownloadAttachmentAsync(
        FeatureAttachmentDownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(UnsupportedDirectDownloadReason);
    }

    /// <inheritdoc />
    public async Task<FeatureAttachmentResult> AddAttachmentAsync(
        FeatureAttachmentAddRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uploadRequest = await BuildUnaryUploadRequestAsync(
            new SharedAttachmentUploadInput(
                request.Source,
                request.ObjectId,
                request.Name,
                request.ContentType,
                request.Content,
                request.Keywords,
                ReplaceAttachmentId: null),
            cancellationToken).ConfigureAwait(false);
        var response = await UploadAttachmentAsync(uploadRequest, cancellationToken).ConfigureAwait(false);
        return ToFeatureAttachmentResult(response.Attachment);
    }

    /// <inheritdoc />
    public async Task<FeatureAttachmentResult> UpdateAttachmentAsync(
        FeatureAttachmentUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uploadRequest = await BuildUnaryUploadRequestAsync(
            new SharedAttachmentUploadInput(
                request.Source,
                request.ObjectId,
                request.Name,
                request.ContentType,
                request.Content,
                request.Keywords,
                ReplaceAttachmentId: request.AttachmentId),
            cancellationToken).ConfigureAwait(false);
        var response = await UploadAttachmentAsync(uploadRequest, cancellationToken).ConfigureAwait(false);
        return ToFeatureAttachmentResult(response.Attachment);
    }

    /// <inheritdoc />
    public async Task<FeatureAttachmentResult> DeleteAttachmentAsync(
        FeatureAttachmentDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await DeleteAttachmentAsync(
            new Models.DeleteAttachmentRequest
            {
                Parent = BuildAttachmentParent(request.Source, request.ObjectId),
                AttachmentId = request.AttachmentId,
                IdempotencyKey = Guid.NewGuid().ToString("D"),
            },
            cancellationToken).ConfigureAwait(false);
        return new FeatureAttachmentResult
        {
            AttachmentId = response.AttachmentId,
            Succeeded = true,
        };
    }

    private sealed record SharedAttachmentUploadInput(
        FeatureSource Source,
        long ObjectId,
        string Name,
        string ContentType,
        Stream Content,
        string? Keywords,
        long? ReplaceAttachmentId);

    private static async Task<Models.UploadAttachmentRequest> BuildUnaryUploadRequestAsync(
        SharedAttachmentUploadInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input.Content);
        using var buffer = new MemoryStream();
        await input.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var bytes = buffer.ToArray();
        var digest = SHA256.HashData(bytes);

        return new Models.UploadAttachmentRequest
        {
            Upload = new Models.AttachmentUpload
            {
                Parent = BuildAttachmentParent(input.Source, input.ObjectId),
                Name = input.Name,
                ContentType = input.ContentType,
                Keywords = ParseAttachmentKeywords(input.Keywords),
                SizeBytes = bytes.Length,
                Sha256 = digest,
                ReplaceAttachmentId = input.ReplaceAttachmentId,
                IdempotencyKey = Guid.NewGuid().ToString("D"),
            },
            Content = bytes,
        };
    }

    private static Models.AttachmentParent BuildAttachmentParent(FeatureSource source, long objectId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.ServiceId))
        {
            throw new ArgumentException("A service ID is required for gRPC attachment operations.", nameof(source));
        }

        if (!source.LayerId.HasValue)
        {
            throw new ArgumentException("A layer ID is required for gRPC attachment operations.", nameof(source));
        }

        return new Models.AttachmentParent
        {
            ServiceId = source.ServiceId,
            LayerId = source.LayerId.Value,
            ObjectId = objectId,
        };
    }

    private static List<string>? ParseAttachmentKeywords(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
        {
            return null;
        }

        return keywords
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static FeatureAttachmentInfo ToFeatureAttachmentInfo(
        FeatureSource source,
        long parentObjectId,
        Models.AttachmentDescriptor attachment)
    {
        return new FeatureAttachmentInfo
        {
            Source = source,
            ParentObjectId = parentObjectId,
            AttachmentId = attachment.AttachmentId,
            GlobalId = string.IsNullOrWhiteSpace(attachment.GlobalId) ? null : attachment.GlobalId,
            Name = attachment.Name,
            ContentType = attachment.ContentType,
            Size = attachment.SizeBytes,
            Keywords = attachment.Keywords.Count > 0 ? string.Join(",", attachment.Keywords) : null,
        };
    }

    private static FeatureAttachmentResult ToFeatureAttachmentResult(Models.AttachmentDescriptor? attachment)
    {
        return new FeatureAttachmentResult
        {
            AttachmentId = attachment?.AttachmentId,
            GlobalId = string.IsNullOrWhiteSpace(attachment?.GlobalId) ? null : attachment.GlobalId,
            Succeeded = attachment is not null,
        };
    }
}
