// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Conversion;

internal static partial class ProtoAdapter
{
    public static Proto.QueryRelatedRecordsRequest ToProtoQueryRelatedRecordsRequest(
        Models.QueryRelatedRecordsRequest request)
    {
        var proto = new Proto.QueryRelatedRecordsRequest
        {
            ServiceId = request.ServiceId,
            LayerId = request.LayerId,
            RelationshipId = request.RelationshipId,
            Where = request.Where ?? string.Empty,
            ReturnGeometry = request.ReturnGeometry,
            PageSize = request.PageSize,
            PageToken = request.PageToken ?? string.Empty,
        };

        if (request.ObjectIds is not null)
        {
            proto.ObjectIds.AddRange(request.ObjectIds);
        }

        if (request.OutFields is not null)
        {
            proto.OutFields.AddRange(request.OutFields);
        }

        if (request.OutSr is not null)
        {
            proto.OutSr = ToProtoSpatialReference(request.OutSr);
        }

        return proto;
    }

    public static Models.QueryRelatedRecordsResponse FromProtoQueryRelatedRecordsResponse(
        Proto.QueryRelatedRecordsResponse response)
    {
        var spatialReference = GrpcGeometryConverter.ReadSpatialReference(response.SpatialReference);
        var context = new FeatureDecodeContext(response.Fields, null, spatialReference);
        return new Models.QueryRelatedRecordsResponse
        {
            RelatedLayerId = response.RelatedLayerId,
            ObjectIdFieldName = response.ObjectIdFieldName,
            GeometryType = (Models.GeometryType)response.GeometryType,
            SpatialReference = response.SpatialReference is not null ? ConvertSpatialReference(response.SpatialReference) : null,
            Fields = response.Fields.Select(ConvertField).ToList(),
            Groups = response.Groups.Select(group => ConvertRelatedRecordGroup(group, context)).ToList(),
            NextPageToken = response.NextPageToken,
            GlobalIdFieldName = response.GlobalIdFieldName,
        };
    }

    public static Proto.ApplyServiceEditsRequest ToProtoApplyServiceEditsRequest(
        Models.ApplyServiceEditsRequest request)
    {
        var proto = new Proto.ApplyServiceEditsRequest
        {
            ServiceId = request.ServiceId,
            RollbackOnFailure = request.RollbackOnFailure,
            IdempotencyKey = request.IdempotencyKey ?? string.Empty,
        };

        foreach (var layerEdits in request.Edits)
        {
            proto.Edits.Add(ToProtoServiceLayerEdits(layerEdits));
        }

        return proto;
    }

    public static Models.ApplyServiceEditsResponse FromProtoApplyServiceEditsResponse(
        Proto.ApplyServiceEditsResponse response)
    {
        return new Models.ApplyServiceEditsResponse
        {
            Results = response.Results.Select(ConvertServiceLayerEditResults).ToList(),
            Error = response.Error is not null ? ConvertEditError(response.Error) : null,
        };
    }

    private static Models.RelatedRecordGroup ConvertRelatedRecordGroup(
        Proto.RelatedRecordGroup group,
        FeatureDecodeContext context)
    {
        return new Models.RelatedRecordGroup
        {
            ObjectId = group.ObjectId,
            Features = group.Features.Select(feature => ConvertFeature(feature, context)).ToList(),
        };
    }

    private static Proto.ServiceLayerEdits ToProtoServiceLayerEdits(Models.ServiceLayerEdits edits)
    {
        var proto = new Proto.ServiceLayerEdits
        {
            LayerId = edits.LayerId,
            ForceWrite = edits.ForceWrite,
            UseGlobalIds = edits.UseGlobalIds,
        };

        if (edits.Adds is not null)
        {
            foreach (var feature in edits.Adds)
            {
                proto.Adds.Add(ConvertFeatureToProto(feature));
            }
        }

        if (edits.Updates is not null)
        {
            foreach (var feature in edits.Updates)
            {
                proto.Updates.Add(ConvertFeatureToProto(feature));
            }
        }

        if (edits.Deletes is not null)
        {
            proto.Deletes.AddRange(edits.Deletes);
        }

        if (edits.DeleteGlobalIds is not null)
        {
            proto.DeleteGlobalIds.AddRange(edits.DeleteGlobalIds);
        }

        return proto;
    }

    private static Models.ServiceLayerEditResults ConvertServiceLayerEditResults(
        Proto.ServiceLayerEditResults results)
    {
        return new Models.ServiceLayerEditResults
        {
            LayerId = results.LayerId,
            Results = results.Results is not null
                ? FromProtoApplyEditsResponse(results.Results)
                : new Models.ApplyEditsResponse(),
        };
    }
}
