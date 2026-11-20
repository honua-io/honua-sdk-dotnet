// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Grpc.Models;

namespace Honua.Sdk.Grpc;

/// <summary>Extended gRPC client interface exposing the full geospatial.v1 FeatureService RPC surface.</summary>
public interface IHonuaGrpcFeatureClient : IHonuaGrpcClient
{
    /// <summary>Returns authorized service metadata and a bounded page of accessible layers.</summary>
    Task<DescribeServiceResponse> DescribeServiceAsync(
        DescribeServiceRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the authoritative schema and effective capabilities of one layer or table.</summary>
    Task<DescribeLayerResponse> DescribeLayerAsync(
        DescribeLayerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Queries related destination records grouped by origin feature identity.</summary>
    Task<QueryRelatedRecordsResponse> QueryRelatedRecordsAsync(
        QueryRelatedRecordsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Applies ordered multi-layer edits within a single feature service.</summary>
    Task<ApplyServiceEditsResponse> ApplyServiceEditsAsync(
        ApplyServiceEditsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Lists a bounded page of attachment descriptors for a single feature.</summary>
    Task<ListAttachmentsResponse> ListAttachmentsAsync(
        ListAttachmentsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Requests a short-lived signed download URL for an attachment.</summary>
    Task<GetAttachmentUrlResponse> GetAttachmentUrlAsync(
        GetAttachmentUrlRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Uploads and publishes an attachment in a single bounded unary call.</summary>
    Task<UploadAttachmentResponse> UploadAttachmentAsync(
        UploadAttachmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Starts or resumes a durable chunked attachment upload session.</summary>
    Task<BeginAttachmentUploadResponse> BeginAttachmentUploadAsync(
        BeginAttachmentUploadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Appends or replays one chunk in an active attachment upload session.</summary>
    Task<UploadAttachmentChunkResponse> UploadAttachmentChunkAsync(
        UploadAttachmentChunkRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Validates and atomically publishes a completed chunked attachment upload.</summary>
    Task<CommitAttachmentUploadResponse> CommitAttachmentUploadAsync(
        CommitAttachmentUploadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Aborts an uncommitted attachment upload session and discards temporary content.</summary>
    Task<AbortAttachmentUploadResponse> AbortAttachmentUploadAsync(
        AbortAttachmentUploadRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes one attachment with caller-scoped idempotency protection.</summary>
    Task<DeleteAttachmentResponse> DeleteAttachmentAsync(
        DeleteAttachmentRequest request,
        CancellationToken cancellationToken = default);
}
