// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Grpc.Conversion;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc;

public sealed partial class HonuaGrpcClient
{
    /// <inheritdoc />
    public Task<Models.QueryRelatedRecordsResponse> QueryRelatedRecordsAsync(
        Models.QueryRelatedRecordsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoQueryRelatedRecordsRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.QueryRelatedRecordsResponse, Models.QueryRelatedRecordsResponse>(
                "QueryRelatedRecords",
                (metadata, deadline, token) => _client.QueryRelatedRecordsAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoQueryRelatedRecordsResponse),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<Models.ApplyServiceEditsResponse> ApplyServiceEditsAsync(
        Models.ApplyServiceEditsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var protoRequest = ProtoAdapter.ToProtoApplyServiceEditsRequest(request);
        return ExecuteUnaryAsync(
            new UnaryRpcSpec<Proto.ApplyServiceEditsResponse, Models.ApplyServiceEditsResponse>(
                "ApplyServiceEdits",
                (metadata, deadline, token) => _client.ApplyServiceEditsAsync(protoRequest, metadata, deadline, token),
                ProtoAdapter.FromProtoApplyServiceEditsResponse),
            cancellationToken);
    }
}
