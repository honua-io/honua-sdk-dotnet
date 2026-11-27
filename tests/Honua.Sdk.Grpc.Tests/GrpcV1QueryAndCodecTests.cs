// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Grpc.Core;
using Honua.Sdk.Grpc.Conversion;
using Honua.Sdk.Grpc.Models;
using Moq;
using NetTopologySuite.Geometries;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Tests;

public class GrpcV1QueryAndCodecTests
{
    [Fact]
    public void QueryFeaturesRequest_MapsV1SpecExtensions()
    {
        var request = new QueryFeaturesRequest
        {
            ServiceId = "geodesy",
            LayerId = 4,
            GlobalIds = ["11111111-1111-1111-1111-111111111111"],
            OutSr = new SpatialReference
            {
                Wkid = 4326,
                LatestWkid = 4326,
                VcsWkid = 5773,
                LatestVcsWkid = 5773,
            },
            DatumTransformationWkid = 1188,
            UsePositionalAttributes = true,
            UsePackedCoordinates = true,
            SpatialFilter = new SpatialFilter
            {
                Geometry = new Dictionary<string, object?>
                {
                    ["x"] = -157.85,
                    ["y"] = 21.30,
                    ["spatialReference"] = new Dictionary<string, object?>
                    {
                        ["wkid"] = 4326,
                        ["vcsWkid"] = 5703,
                        ["latestVcsWkid"] = 5703,
                    },
                },
                SpatialRelationship = SpatialRelationship.WithinDistance,
                Distance = 2.5,
                DistanceUnit = DistanceUnit.NauticalMiles,
            },
        };

        var proto = HonuaGrpcProtoConverter.ToProto(request);

        Assert.Equal(["11111111-1111-1111-1111-111111111111"], proto.GlobalIds);
        Assert.Equal(4326, proto.OutSr.Wkid);
        Assert.Equal(5773, proto.OutSr.VcsWkid);
        Assert.Equal(5773, proto.OutSr.LatestVcsWkid);
        Assert.Equal(1188, proto.DatumTransformationWkid);
        Assert.True(proto.UsePositionalAttributes);
        Assert.True(proto.UsePackedCoordinates);
        Assert.Equal(Proto.DistanceUnit.NauticalMiles, proto.SpatialFilter.DistanceUnit);
        Assert.Equal(5703, proto.SpatialFilter.SpatialReference.VcsWkid);
        Assert.Equal(5703, proto.SpatialFilter.SpatialReference.LatestVcsWkid);
    }

    [Fact]
    public void QueryFeaturesResponse_DecodesPositionalAttributesQuantizedCoordsAndSrid()
    {
        var transform = new Proto.CoordinateTransform
        {
            ScaleX = 0.001,
            ScaleY = 0.001,
            TranslateX = -158.0,
            TranslateY = 21.0,
        };
        var path = new Proto.CoordinateSequence { Stride = 2 };
        path.DeltaCoords.AddRange([150, 300, 50, -100]);

        var protoFeature = new Proto.Feature
        {
            Id = 77,
            GlobalId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            Geometry = new Proto.Geometry
            {
                Polyline = new Proto.PolylineGeometry { Paths = { path } },
            },
        };
        protoFeature.AttributeValues.Add(new Proto.AttributeValue { StringValue = "Waikiki" });
        protoFeature.AttributeValues.Add(new Proto.AttributeValue { DecimalValue = "12345.67890" });
        protoFeature.AttributeValues.Add(new Proto.AttributeValue { NullValue = Proto.NullValue.NullValue });

        var protoResponse = new Proto.QueryFeaturesResponse
        {
            ObjectIdFieldName = "OBJECTID",
            GlobalIdFieldName = "GLOBALID",
            GeometryType = Proto.GeometryType.LineString,
            SpatialReference = new Proto.SpatialReference { Wkid = 4326, VcsWkid = 5773, LatestVcsWkid = 5773 },
            CoordinateTransform = transform,
        };
        protoResponse.Fields.Add(new Proto.FieldDefinition { Name = "NAME", Alias = "Site Name", FieldType = Proto.FieldType.String });
        protoResponse.Fields.Add(new Proto.FieldDefinition { Name = "BUDGET", FieldType = Proto.FieldType.Decimal });
        protoResponse.Fields.Add(new Proto.FieldDefinition { Name = "NOTES", FieldType = Proto.FieldType.String, Nullable = true });
        protoResponse.Features.Add(protoFeature);

        var response = HonuaGrpcProtoConverter.FromProto(protoResponse);

        Assert.Equal("GLOBALID", response.GlobalIdFieldName);
        Assert.Equal(5773, response.SpatialReference?.VcsWkid);
        Assert.Equal("Site Name", response.Fields[0].Alias);
        Assert.Equal(FieldType.Decimal, response.Fields[1].FieldType);
        Assert.NotNull(response.CoordinateTransform);
        Assert.Equal(0.001, response.CoordinateTransform.ScaleX);

        var feature = Assert.Single(response.Features);
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", feature.GlobalId);
        Assert.Equal("Waikiki", feature.Attributes["NAME"]);
        Assert.Equal(12345.67890m, feature.Attributes["BUDGET"]);
        Assert.Null(feature.Attributes["NOTES"]);

        var line = Assert.IsType<LineString>(feature.NtsGeometry);
        Assert.Equal(4326, line.SRID);
        Assert.Equal(-157.85, line.GetCoordinateN(0).X, 6);
        Assert.Equal(21.30, line.GetCoordinateN(0).Y, 6);
        Assert.Equal(-157.80, line.GetCoordinateN(1).X, 6);
        Assert.Equal(21.20, line.GetCoordinateN(1).Y, 6);
    }

    [Fact]
    public async Task QueryFeaturesStreamAsync_PropagatesFirstPageFieldsTransformAndSpatialReference()
    {
        var transform = new Proto.CoordinateTransform
        {
            ScaleX = 0.01,
            ScaleY = 0.01,
            TranslateX = 10.0,
            TranslateY = 20.0,
        };
        var path1 = new Proto.CoordinateSequence { Stride = 2 };
        path1.DeltaCoords.AddRange([0, 0, 100, 100]);
        var path2 = new Proto.CoordinateSequence { Stride = 2 };
        path2.DeltaCoords.AddRange([200, 300, 50, 50]);

        var page1 = new Proto.FeaturePage
        {
            ObjectIdFieldName = "OID",
            GlobalIdFieldName = "GID",
            SpatialReference = new Proto.SpatialReference { Wkid = 3857 },
            CoordinateTransform = transform,
            IsLastPage = false,
        };
        page1.Fields.Add(new Proto.FieldDefinition { Name = "LABEL", FieldType = Proto.FieldType.String });
        var feature1 = new Proto.Feature
        {
            Id = 1,
            Geometry = new Proto.Geometry { Polyline = new Proto.PolylineGeometry { Paths = { path1 } } },
        };
        feature1.AttributeValues.Add(new Proto.AttributeValue { StringValue = "Page One" });
        page1.Features.Add(feature1);

        var page2 = new Proto.FeaturePage { IsLastPage = true };
        var feature2 = new Proto.Feature
        {
            Id = 2,
            Geometry = new Proto.Geometry { Polyline = new Proto.PolylineGeometry { Paths = { path2 } } },
        };
        feature2.AttributeValues.Add(new Proto.AttributeValue { StringValue = "Page Two" });
        page2.Features.Add(feature2);

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryFeaturesStream(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateStream([page1, page2]));

        var client = new HonuaGrpcClient(mockClient.Object);
        var pages = new List<FeaturePage>();
        await foreach (var page in client.QueryFeaturesStreamAsync(new QueryFeaturesRequest
        {
            ServiceId = "svc",
            LayerId = 0,
            UsePositionalAttributes = true,
            UsePackedCoordinates = true,
        }))
        {
            pages.Add(page);
        }

        Assert.Equal(2, pages.Count);
        Assert.Equal("GID", pages[0].GlobalIdFieldName);
        Assert.Equal("Page One", pages[0].Features[0].Attributes["LABEL"]);
        Assert.Equal("Page Two", pages[1].Features[0].Attributes["LABEL"]);

        var line2 = Assert.IsType<LineString>(pages[1].Features[0].NtsGeometry);
        Assert.Equal(3857, line2.SRID);
        Assert.Equal(12.0, line2.GetCoordinateN(0).X, 6);
        Assert.Equal(23.0, line2.GetCoordinateN(0).Y, 6);
        Assert.Equal(12.5, line2.GetCoordinateN(1).X, 6);
        Assert.Equal(23.5, line2.GetCoordinateN(1).Y, 6);
    }

    [Fact]
    public void TypedAttributeValues_DateTimeTimestampOffsetAndDecimal_RoundTripLosslessly()
    {
        var date = new DateOnly(2026, 11, 20);
        var time = new TimeOnly(14, 35, 22, 123).Add(TimeSpan.FromTicks(4567));
        var offset = new DateTimeOffset(2026, 11, 20, 14, 35, 22, TimeSpan.FromHours(-10));
        var amount = 9876543210.123456m;

        var dateProto = ProtoAdapter.ConvertAttributeToProto(date);
        var timeProto = ProtoAdapter.ConvertAttributeToProto(time);
        var offsetProto = ProtoAdapter.ConvertAttributeToProto(new TimestampOffsetValue(offset));
        var decimalProto = ProtoAdapter.ConvertAttributeToProto(amount);

        Assert.Equal(Proto.AttributeValue.ValueOneofCase.DateValue, dateProto.ValueCase);
        Assert.Equal("2026-11-20", dateProto.DateValue);
        Assert.Equal(date, ProtoAdapter.ConvertAttribute(dateProto));

        Assert.Equal(Proto.AttributeValue.ValueOneofCase.TimeValue, timeProto.ValueCase);
        Assert.Equal(time, ProtoAdapter.ConvertAttribute(timeProto));

        Assert.Equal(Proto.AttributeValue.ValueOneofCase.TimestampOffsetValue, offsetProto.ValueCase);
        Assert.Equal("2026-11-20T14:35:22-10:00", offsetProto.TimestampOffsetValue);
        var decodedOffset = Assert.IsType<DateTimeOffset>(ProtoAdapter.ConvertAttribute(offsetProto));
        Assert.Equal(offset, decodedOffset);
        Assert.Equal(TimeSpan.FromHours(-10), decodedOffset.Offset);

        Assert.Equal(Proto.AttributeValue.ValueOneofCase.DecimalValue, decimalProto.ValueCase);
        Assert.Equal("9876543210.123456", decimalProto.DecimalValue);
        Assert.Equal(amount, ProtoAdapter.ConvertAttribute(decimalProto));
    }

    [Fact]
    public void ApplyEdits_MapsIdempotencyKeyAndGlobalIds()
    {
        var request = new ApplyEditsRequest
        {
            ServiceId = "parcels",
            LayerId = 1,
            IdempotencyKey = "req-uuid-1",
            UseGlobalIds = true,
            Updates =
            [
                new Feature
                {
                    GlobalId = "11111111-1111-1111-1111-111111111111",
                    Attributes = new Dictionary<string, object?> { ["status"] = "verified" },
                },
            ],
            DeleteGlobalIds = ["22222222-2222-2222-2222-222222222222"],
            RollbackOnFailure = true,
        };

        var proto = HonuaGrpcProtoConverter.ToProto(request);

        Assert.Equal("req-uuid-1", proto.IdempotencyKey);
        Assert.True(proto.UseGlobalIds);
        Assert.Equal("11111111-1111-1111-1111-111111111111", proto.Updates[0].GlobalId);
        Assert.Equal(["22222222-2222-2222-2222-222222222222"], proto.DeleteGlobalIds);

        var protoResponse = new Proto.ApplyEditsResponse();
        protoResponse.UpdateResults.Add(new Proto.EditResult
        {
            ObjectId = 42,
            GlobalId = "11111111-1111-1111-1111-111111111111",
            Success = true,
        });
        var response = HonuaGrpcProtoConverter.FromProto(protoResponse);
        Assert.Equal("11111111-1111-1111-1111-111111111111", response.UpdateResults[0].GlobalId);
    }

    [Fact]
    public void PositionalAttributes_ExceedingFieldCount_Throws()
    {
        var protoResponse = new Proto.QueryFeaturesResponse();
        protoResponse.Fields.Add(new Proto.FieldDefinition { Name = "ONLY_ONE" });
        var feature = new Proto.Feature { Id = 1 };
        feature.AttributeValues.Add(new Proto.AttributeValue { StringValue = "a" });
        feature.AttributeValues.Add(new Proto.AttributeValue { StringValue = "b" });
        protoResponse.Features.Add(feature);

        Assert.Throws<InvalidOperationException>(() => HonuaGrpcProtoConverter.FromProto(protoResponse));
    }

    private static AsyncServerStreamingCall<T> CreateStream<T>(IEnumerable<T> items)
    {
        return new AsyncServerStreamingCall<T>(
            new SimpleStreamReader<T>(items),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    private sealed class SimpleStreamReader<T>(IEnumerable<T> items) : IAsyncStreamReader<T>
    {
        private readonly IEnumerator<T> _enumerator = items.GetEnumerator();

        public T Current => _enumerator.Current;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
            => Task.FromResult(_enumerator.MoveNext());
    }
}
