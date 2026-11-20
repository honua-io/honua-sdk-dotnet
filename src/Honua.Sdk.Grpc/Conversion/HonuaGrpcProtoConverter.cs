// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Grpc.Models;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Conversion;

/// <summary>
/// Converts between Honua SDK gRPC models and canonical <c>geospatial.v1</c> protobuf messages.
/// </summary>
public static class HonuaGrpcProtoConverter
{
    /// <summary>
    /// Converts a feature query request to the canonical protobuf request.
    /// </summary>
    /// <param name="request">SDK feature query request.</param>
    /// <returns>Canonical protobuf feature query request.</returns>
    public static Proto.QueryFeaturesRequest ToProto(QueryFeaturesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoRequest(request);
    }

    /// <summary>
    /// Converts a canonical protobuf query response to the SDK query response model.
    /// </summary>
    /// <param name="response">Canonical protobuf feature query response.</param>
    /// <returns>SDK feature query response.</returns>
    public static QueryFeaturesResponse FromProto(Proto.QueryFeaturesResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoResponse(response);
    }

    /// <summary>
    /// Converts a canonical protobuf streaming feature page to the SDK feature page model.
    /// </summary>
    /// <param name="page">Canonical protobuf feature page.</param>
    /// <returns>SDK feature page.</returns>
    public static FeaturePage FromProto(Proto.FeaturePage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return ProtoAdapter.FromProtoPage(page);
    }

    /// <summary>
    /// Converts a feature edit request to the canonical protobuf edit request.
    /// </summary>
    /// <param name="request">SDK feature edit request.</param>
    /// <returns>Canonical protobuf feature edit request.</returns>
    public static Proto.ApplyEditsRequest ToProto(ApplyEditsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoApplyEditsRequest(request);
    }

    /// <summary>
    /// Converts a canonical protobuf edit response to the SDK edit response model.
    /// </summary>
    /// <param name="response">Canonical protobuf feature edit response.</param>
    /// <returns>SDK feature edit response.</returns>
    public static ApplyEditsResponse FromProto(Proto.ApplyEditsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoApplyEditsResponse(response);
    }

    /// <summary>Converts a service metadata request to the canonical protobuf request.</summary>
    public static Proto.DescribeServiceRequest ToProto(DescribeServiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoDescribeServiceRequest(request);
    }

    /// <summary>Converts a canonical protobuf service metadata response to the SDK model.</summary>
    public static DescribeServiceResponse FromProto(Proto.DescribeServiceResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoDescribeServiceResponse(response);
    }

    /// <summary>Converts a layer metadata request to the canonical protobuf request.</summary>
    public static Proto.DescribeLayerRequest ToProto(DescribeLayerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoDescribeLayerRequest(request);
    }

    /// <summary>Converts a canonical protobuf layer metadata response to the SDK model.</summary>
    public static DescribeLayerResponse FromProto(Proto.DescribeLayerResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoDescribeLayerResponse(response);
    }

    /// <summary>Converts a related-records query request to the canonical protobuf request.</summary>
    public static Proto.QueryRelatedRecordsRequest ToProto(QueryRelatedRecordsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoQueryRelatedRecordsRequest(request);
    }

    /// <summary>Converts a canonical protobuf related-records query response to the SDK model.</summary>
    public static QueryRelatedRecordsResponse FromProto(Proto.QueryRelatedRecordsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoQueryRelatedRecordsResponse(response);
    }

    /// <summary>Converts a service-wide edit request to the canonical protobuf request.</summary>
    public static Proto.ApplyServiceEditsRequest ToProto(ApplyServiceEditsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoApplyServiceEditsRequest(request);
    }

    /// <summary>Converts a canonical protobuf service-wide edit response to the SDK model.</summary>
    public static ApplyServiceEditsResponse FromProto(Proto.ApplyServiceEditsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoApplyServiceEditsResponse(response);
    }

    /// <summary>Converts a list-attachments request to the canonical protobuf request.</summary>
    public static Proto.ListAttachmentsRequest ToProto(ListAttachmentsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoListAttachmentsRequest(request);
    }

    /// <summary>Converts a canonical protobuf list-attachments response to the SDK model.</summary>
    public static ListAttachmentsResponse FromProto(Proto.ListAttachmentsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoListAttachmentsResponse(response);
    }

    /// <summary>Converts a get-attachment-URL request to the canonical protobuf request.</summary>
    public static Proto.GetAttachmentUrlRequest ToProto(GetAttachmentUrlRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoGetAttachmentUrlRequest(request);
    }

    /// <summary>Converts a canonical protobuf get-attachment-URL response to the SDK model.</summary>
    public static GetAttachmentUrlResponse FromProto(Proto.GetAttachmentUrlResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoGetAttachmentUrlResponse(response);
    }

    /// <summary>Converts an attachment upload request to the canonical protobuf request.</summary>
    public static Proto.UploadAttachmentRequest ToProto(UploadAttachmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoUploadAttachmentRequest(request);
    }

    /// <summary>Converts a canonical protobuf attachment upload response to the SDK model.</summary>
    public static UploadAttachmentResponse FromProto(Proto.UploadAttachmentResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoUploadAttachmentResponse(response);
    }

    /// <summary>Converts a begin-attachment-upload request to the canonical protobuf request.</summary>
    public static Proto.BeginAttachmentUploadRequest ToProto(BeginAttachmentUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoBeginAttachmentUploadRequest(request);
    }

    /// <summary>Converts a canonical protobuf begin-attachment-upload response to the SDK model.</summary>
    public static BeginAttachmentUploadResponse FromProto(Proto.BeginAttachmentUploadResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoBeginAttachmentUploadResponse(response);
    }

    /// <summary>Converts an attachment chunk upload request to the canonical protobuf request.</summary>
    public static Proto.UploadAttachmentChunkRequest ToProto(UploadAttachmentChunkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoUploadAttachmentChunkRequest(request);
    }

    /// <summary>Converts a canonical protobuf attachment chunk upload response to the SDK model.</summary>
    public static UploadAttachmentChunkResponse FromProto(Proto.UploadAttachmentChunkResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoUploadAttachmentChunkResponse(response);
    }

    /// <summary>Converts a commit-attachment-upload request to the canonical protobuf request.</summary>
    public static Proto.CommitAttachmentUploadRequest ToProto(CommitAttachmentUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoCommitAttachmentUploadRequest(request);
    }

    /// <summary>Converts a canonical protobuf commit-attachment-upload response to the SDK model.</summary>
    public static CommitAttachmentUploadResponse FromProto(Proto.CommitAttachmentUploadResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoCommitAttachmentUploadResponse(response);
    }

    /// <summary>Converts an abort-attachment-upload request to the canonical protobuf request.</summary>
    public static Proto.AbortAttachmentUploadRequest ToProto(AbortAttachmentUploadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoAbortAttachmentUploadRequest(request);
    }

    /// <summary>Converts a canonical protobuf abort-attachment-upload response to the SDK model.</summary>
    public static AbortAttachmentUploadResponse FromProto(Proto.AbortAttachmentUploadResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoAbortAttachmentUploadResponse(response);
    }

    /// <summary>Converts a delete-attachment request to the canonical protobuf request.</summary>
    public static Proto.DeleteAttachmentRequest ToProto(DeleteAttachmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ProtoAdapter.ToProtoDeleteAttachmentRequest(request);
    }

    /// <summary>Converts a canonical protobuf delete-attachment response to the SDK model.</summary>
    public static DeleteAttachmentResponse FromProto(Proto.DeleteAttachmentResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return ProtoAdapter.FromProtoDeleteAttachmentResponse(response);
    }
}
