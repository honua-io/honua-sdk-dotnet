// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Grpc.Conversion;
using GrpcModels = Honua.Sdk.Grpc.Models;

namespace Honua.Sdk.Grpc.Tests.Conversion;


public class MobileRequestConvertersTests
{
    [Fact]
    public void ToGrpcQueryRequest_MapsAllFields()
    {
        var request = new QueryFeaturesRequest
        {
            ServiceId = "svc",
            LayerId = 3,
            Where = "STATUS='Open'",
            ObjectIds = [1L, 2L, 3L],
            OutFields = ["NAME", "STATUS"],
            ReturnGeometry = false,
            ResultOffset = 50,
            ResultRecordCount = 100,
            OrderBy = "NAME ASC",
            ReturnDistinct = true,
            ReturnCountOnly = true,
            ReturnIdsOnly = true,
            ReturnExtentOnly = true,
        };

        var grpc = MobileRequestConverters.ToGrpcQueryRequest(request);

        Assert.Equal("svc", grpc.ServiceId);
        Assert.Equal(3, grpc.LayerId);
        Assert.Equal("STATUS='Open'", grpc.Where);
        Assert.Equal([1L, 2L, 3L], grpc.ObjectIds);
        Assert.Equal(["NAME", "STATUS"], grpc.OutFields);
        Assert.False(grpc.ReturnGeometry);
        Assert.Equal(50, grpc.ResultOffset);
        Assert.Equal(100, grpc.ResultRecordCount);
        Assert.Equal("NAME ASC", grpc.OrderBy);
        Assert.True(grpc.ReturnDistinct);
        Assert.True(grpc.ReturnCountOnly);
        Assert.True(grpc.ReturnIdsOnly);
        Assert.True(grpc.ReturnExtentOnly);
    }

    [Fact]
    public void ToGrpcQueryRequest_DefaultsForOptionalFields()
    {
        var request = new QueryFeaturesRequest { ServiceId = "svc", LayerId = 0 };

        var grpc = MobileRequestConverters.ToGrpcQueryRequest(request);

        Assert.Equal(0, grpc.ResultOffset);
        Assert.Null(grpc.ResultRecordCount);
        Assert.Null(grpc.OrderBy);
        Assert.True(grpc.ReturnGeometry);
    }

    [Fact]
    public void ToGrpcApplyEditsRequest_MapsAddsUpdatesDeletes()
    {
        var add = new FeatureEditFeature
        {
            ObjectId = 10,
            Attributes = new Dictionary<string, JsonElement>
            {
                ["NAME"] = JsonSerializer.SerializeToElement("Alpha"),
            },
            Geometry = JsonSerializer.SerializeToElement(new { x = 1.0, y = 2.0 }),
        };
        var update = new FeatureEditFeature
        {
            Attributes = new Dictionary<string, JsonElement>
            {
                ["OBJECTID"] = JsonSerializer.SerializeToElement(20),
                ["NAME"] = JsonSerializer.SerializeToElement("Beta"),
            },
        };

        var request = new ApplyEditsRequest
        {
            ServiceId = "svc",
            LayerId = 1,
            Adds = [add],
            Updates = [update],
            Deletes = [100L, 101L],
            RollbackOnFailure = true,
            ForceWrite = true,
        };

        var grpc = MobileRequestConverters.ToGrpcApplyEditsRequest(request);

        Assert.Equal("svc", grpc.ServiceId);
        Assert.Equal(1, grpc.LayerId);
        Assert.True(grpc.RollbackOnFailure);
        Assert.True(grpc.ForceWrite);

        Assert.NotNull(grpc.Adds);
        Assert.Single(grpc.Adds);
        Assert.Equal(10L, grpc.Adds[0].Id);
        Assert.Equal("Alpha", grpc.Adds[0].Attributes["NAME"]);

        Assert.NotNull(grpc.Updates);
        Assert.Single(grpc.Updates);
        Assert.Equal(20L, grpc.Updates[0].Id);

        Assert.Equal([100L, 101L], grpc.Deletes);
    }

    [Fact]
    public void ToGrpcApplyEditsRequest_ParsesJsonFallbacks()
    {
        var request = new ApplyEditsRequest
        {
            ServiceId = "svc",
            LayerId = 0,
            AddsJson = """[{"attributes":{"OBJECTID":1,"NAME":"X"},"geometry":{"x":1,"y":2}}]""",
            UpdatesJson = """{"attributes":{"OBJECTID":"42","NAME":"Y"}}""",
            DeletesCsv = "1, 2 ,3",
        };

        var grpc = MobileRequestConverters.ToGrpcApplyEditsRequest(request);

        Assert.NotNull(grpc.Adds);
        Assert.Single(grpc.Adds);
        Assert.Equal(1L, grpc.Adds[0].Id);

        Assert.NotNull(grpc.Updates);
        Assert.Single(grpc.Updates);
        Assert.Equal(42L, grpc.Updates[0].Id);

        Assert.Equal([1L, 2L, 3L], grpc.Deletes);
    }

    [Fact]
    public void ToGrpcApplyEditsRequest_OmitsNullCollectionsWhenEmpty()
    {
        var request = new ApplyEditsRequest { ServiceId = "svc", LayerId = 0 };

        var grpc = MobileRequestConverters.ToGrpcApplyEditsRequest(request);

        Assert.Null(grpc.Adds);
        Assert.Null(grpc.Updates);
        Assert.Null(grpc.Deletes);
    }

    [Fact]
    public void ToJsonDocument_QueryResponse_SerializesCoreFields()
    {
        var response = new GrpcModels.QueryFeaturesResponse
        {
            ObjectIdFieldName = "OBJECTID",
            GeometryType = GrpcModels.GeometryType.Point,
            SpatialReference = new GrpcModels.SpatialReference { Wkid = 4326, LatestWkid = 4326 },
            Fields =
            [
                new GrpcModels.FieldDefinition { Name = "NAME", FieldType = GrpcModels.FieldType.String, Length = 50, Nullable = true },
            ],
            Features =
            [
                new GrpcModels.Feature { Id = 1, Attributes = new Dictionary<string, object?> { ["NAME"] = "A" } },
            ],
            ExceededTransferLimit = true,
            Count = 1,
            ObjectIds = [1L],
            Extent = new GrpcModels.Extent { Xmin = 0, Ymin = 0, Xmax = 1, Ymax = 1 },
        };

        using var doc = MobileRequestConverters.ToJsonDocument(response);
        var root = doc.RootElement;
        Assert.Equal("OBJECTID", root.GetProperty("objectIdFieldName").GetString());
        Assert.True(root.GetProperty("exceededTransferLimit").GetBoolean());
        Assert.Equal(1L, root.GetProperty("count").GetInt64());
        Assert.Equal(4326, root.GetProperty("spatialReference").GetProperty("wkid").GetInt32());
        Assert.Equal("A", root.GetProperty("features")[0].GetProperty("attributes").GetProperty("NAME").GetString());
        Assert.Equal(1L, root.GetProperty("features")[0].GetProperty("id").GetInt64());
        Assert.Equal(0.0, root.GetProperty("extent").GetProperty("xmin").GetDouble());
    }

    [Fact]
    public void ToJsonDocument_FeaturePage_SerializesIsLastPage()
    {
        var page = new GrpcModels.FeaturePage
        {
            ObjectIdFieldName = "OBJECTID",
            GeometryType = GrpcModels.GeometryType.Polygon,
            IsLastPage = true,
        };

        using var doc = MobileRequestConverters.ToJsonDocument(page);
        Assert.True(doc.RootElement.GetProperty("isLastPage").GetBoolean());
        Assert.Equal("Polygon", doc.RootElement.GetProperty("geometryType").GetString());
    }

    [Fact]
    public void ToJsonDocument_ApplyEditsResponse_SerializesResults()
    {
        var response = new GrpcModels.ApplyEditsResponse
        {
            AddResults = [new GrpcModels.EditResult { ObjectId = 1, Success = true }],
            UpdateResults = [new GrpcModels.EditResult { ObjectId = 2, Success = false, Error = new GrpcModels.EditError { Code = 500, Message = "boom" } }],
            DeleteResults = [],
            Error = null,
        };

        using var doc = MobileRequestConverters.ToJsonDocument(response);
        var root = doc.RootElement;
        Assert.Equal(1L, root.GetProperty("addResults")[0].GetProperty("objectId").GetInt64());
        Assert.True(root.GetProperty("addResults")[0].GetProperty("success").GetBoolean());

        var updateError = root.GetProperty("updateResults")[0].GetProperty("error");
        Assert.Equal(500, updateError.GetProperty("code").GetInt32());
        Assert.Equal("boom", updateError.GetProperty("message").GetString());

        Assert.Equal(JsonValueKind.Null, root.GetProperty("error").ValueKind);
    }

    [Fact]
    public void ToGrpcApplyEditsRequest_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => MobileRequestConverters.ToGrpcApplyEditsRequest(null!));
    }

    [Fact]
    public void ToGrpcQueryRequest_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => MobileRequestConverters.ToGrpcQueryRequest(null!));
    }
}

