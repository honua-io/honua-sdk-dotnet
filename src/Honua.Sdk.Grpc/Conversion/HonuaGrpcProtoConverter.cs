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
}
