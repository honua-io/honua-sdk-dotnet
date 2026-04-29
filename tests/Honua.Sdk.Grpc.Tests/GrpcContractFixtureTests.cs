// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Google.Protobuf;
using Honua.Sdk.Grpc.Conversion;
using Honua.Sdk.Grpc.Models;
using Honua.Sdk.Grpc.Tests.Fixtures;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Tests;

public class GrpcContractFixtureTests
{
    [Fact]
    public void QueryFeaturesRequestFixture_MatchesPublicProtoConverter()
    {
        var sdkRequest = new QueryFeaturesRequest
        {
            ServiceId = "parks",
            LayerId = 2,
            Where = "status = 'open'",
            ObjectIds = [101, 102],
            OutFields = ["name", "status", "opened_at"],
            ReturnGeometry = true,
            OutSr = new SpatialReference { Wkid = 3857, LatestWkid = 3857 },
            ResultOffset = 5,
            ResultRecordCount = 25,
            OrderBy = "name ASC",
            ReturnDistinct = true,
            OutStatistics =
            [
                new StatisticDefinition
                {
                    OnStatisticField = "visitors",
                    StatisticType = StatisticType.Sum,
                    OutStatisticFieldName = "total_visitors",
                },
            ],
            GroupBy = ["status"],
            GeometryPrecision = 6,
            MaxAllowableOffset = 0.25,
            SpatialFilter = new SpatialFilter
            {
                Geometry = new Dictionary<string, object?>
                {
                    ["rings"] = new List<object?>
                    {
                        new List<object?>
                        {
                            new List<object?> { -123.5, 37.5 },
                            new List<object?> { -122.5, 37.5 },
                            new List<object?> { -122.5, 38.5 },
                            new List<object?> { -123.5, 38.5 },
                            new List<object?> { -123.5, 37.5 },
                        },
                    },
                },
                SpatialRelationship = SpatialRelationship.Intersects,
                SpatialReference = new SpatialReference { Wkid = 4326 },
                DistanceUnit = DistanceUnit.Meters,
            },
        };

        var expected = ParseProto<Proto.QueryFeaturesRequest>("query-features-request.json");

        Assert.Equal(expected, HonuaGrpcProtoConverter.ToProto(sdkRequest));
    }

    [Fact]
    public void QueryFeaturesResponseFixture_MatchesPublicProtoConverter()
    {
        var protoResponse = ParseProto<Proto.QueryFeaturesResponse>("query-features-response.json");

        var response = HonuaGrpcProtoConverter.FromProto(protoResponse);

        Assert.Equal("OBJECTID", response.ObjectIdFieldName);
        Assert.Equal(GeometryType.Point, response.GeometryType);
        Assert.Equal(4326, response.SpatialReference?.Wkid);
        Assert.Equal(4326, response.SpatialReference?.LatestWkid);
        Assert.Equal(3, response.Fields.Count);
        Assert.Equal(FieldType.DateTime, response.Fields[2].FieldType);
        Assert.Single(response.Features);
        Assert.Equal(101, response.Features[0].Id);
        Assert.Equal("Ala Moana Regional Park", response.Features[0].Attributes["name"]);
        Assert.Equal(2500L, response.Features[0].Attributes["visitors"]);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000L).DateTime, response.Features[0].Attributes["opened_at"]);
        Assert.Equal(Proto.Geometry.ShapeOneofCase.Point, protoResponse.Features[0].Geometry.ShapeCase);
        Assert.True(response.ExceededTransferLimit);
        Assert.Equal(1, response.Count);
    }

    [Fact]
    public void ApplyEditsRequestFixture_MatchesPublicProtoConverter()
    {
        var openedAt = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000L);
        var sdkRequest = new ApplyEditsRequest
        {
            ServiceId = "parks",
            LayerId = 2,
            Adds =
            [
                new Feature
                {
                    Attributes = new Dictionary<string, object?>
                    {
                        ["name"] = "New Park",
                        ["active"] = true,
                        ["opened_at"] = openedAt,
                    },
                    Geometry = new Dictionary<string, object?>
                    {
                        ["x"] = -157.85,
                        ["y"] = 21.3,
                    },
                },
            ],
            Updates =
            [
                new Feature
                {
                    Id = 77,
                    Attributes = new Dictionary<string, object?>
                    {
                        ["name"] = "Renamed Park",
                    },
                },
            ],
            Deletes = [88, 89],
            RollbackOnFailure = true,
            ForceWrite = true,
        };

        var expected = ParseProto<Proto.ApplyEditsRequest>("apply-edits-request.json");

        Assert.Equal(expected, HonuaGrpcProtoConverter.ToProto(sdkRequest));
    }

    [Fact]
    public void ApplyEditsResponseFixture_MatchesPublicProtoConverter()
    {
        var protoResponse = ParseProto<Proto.ApplyEditsResponse>("apply-edits-response.json");

        var response = HonuaGrpcProtoConverter.FromProto(protoResponse);

        Assert.Single(response.AddResults);
        Assert.True(response.AddResults[0].Success);
        Assert.Equal(101, response.AddResults[0].ObjectId);
        Assert.Single(response.UpdateResults);
        Assert.False(response.UpdateResults[0].Success);
        Assert.Equal(409, response.UpdateResults[0].Error?.Code);
        Assert.Equal("Edit conflict", response.UpdateResults[0].Error?.Message);
        Assert.Single(response.DeleteResults);
        Assert.True(response.DeleteResults[0].Success);
        Assert.Equal(400, response.Error?.Code);
        Assert.Equal("Batch rejected", response.Error?.Message);
    }

    private static T ParseProto<T>(string fileName)
        where T : IMessage<T>, new()
    {
        var parser = new JsonParser(JsonParser.Settings.Default.WithIgnoreUnknownFields(false));
        return parser.Parse<T>(GrpcFixtureReader.ReadJson(fileName));
    }
}
