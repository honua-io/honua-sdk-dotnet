// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Grpc.Core;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Grpc.Conversion;
using Honua.Sdk.Grpc.Models;
using Moq;
using NetTopologySuite.Geometries;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Tests;

public class GrpcV1RpcsTests
{
    [Fact]
    public async Task DescribeServiceAsync_MapsServiceDescriptorAndLayers()
    {
        var protoResponse = new Proto.DescribeServiceResponse
        {
            Service = new Proto.ServiceDescriptor
            {
                ServiceId = "parks",
                Name = "City Parks",
                Description = "Public parks dataset",
                SpatialReference = new Proto.SpatialReference { Wkid = 4326, VcsWkid = 5773 },
                MaxRecordCount = 2000,
                Capabilities = new Proto.FeatureCapabilities
                {
                    Query = true,
                    QueryStream = true,
                    QueryRelated = true,
                    ApplyServiceEdits = true,
                    UseGlobalIds = true,
                    ListAttachments = true,
                },
            },
            NextPageToken = "next-2",
        };
        protoResponse.Layers.Add(new Proto.LayerSummary
        {
            LayerId = 0,
            Name = "Parks",
            GeometryType = Proto.GeometryType.Polygon,
        });

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.DescribeServiceAsync(
                It.IsAny<Proto.DescribeServiceRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateUnary(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object);
        var response = await client.DescribeServiceAsync(new DescribeServiceRequest
        {
            ServiceId = "parks",
            PageSize = 25,
        });

        Assert.Equal("next-2", response.NextPageToken);
        Assert.Equal("City Parks", response.Service?.Name);
        Assert.Equal(5773, response.Service?.SpatialReference?.VcsWkid);
        Assert.True(response.Service?.Capabilities?.ApplyServiceEdits);
        Assert.True(response.Service?.Capabilities?.QueryRelated);
        Assert.Single(response.Layers);
        Assert.Equal(GeometryType.Polygon, response.Layers[0].GeometryType);
    }

    [Fact]
    public async Task DescribeLayerAsync_AndSharedGetDescriptor_MapFullMetadata()
    {
        var layer = new Proto.LayerDescriptor
        {
            ServiceId = "parks",
            LayerId = 2,
            Name = "Trees",
            GeometryType = Proto.GeometryType.Point,
            SpatialReference = new Proto.SpatialReference { Wkid = 4326, VcsWkid = 5773 },
            ObjectIdField = "OBJECTID",
            GlobalIdField = "GLOBALID",
            DisplayField = "SPECIES",
            MaxRecordCount = 1000,
            HasZ = true,
            Capabilities = new Proto.FeatureCapabilities
            {
                Query = true,
                Create = true,
                Update = true,
                Delete = false,
                QueryRelated = true,
                ListAttachments = true,
                UploadAttachments = true,
                DeleteAttachments = false,
            },
            TimeInfo = new Proto.LayerTimeInfo
            {
                StartField = "PLANTED_AT",
                TimeZone = "Pacific/Honolulu",
                RespectsDaylightSaving = false,
                IntervalUnit = Proto.TimeIntervalUnit.Days,
            },
            EditingInfo = new Proto.LayerEditingInfo
            {
                CreatorField = "CREATED_BY",
                EditorTrackingEnabled = true,
                AllowUpdateOthers = false,
            },
            AttachmentLimits = new Proto.AttachmentLimits
            {
                MaxFileBytes = 10_485_760,
                MaxUnaryUploadBytes = 2_097_152,
                MaxChunkBytes = 524_288,
                AllowedContentTypes = { "image/jpeg", "application/pdf" },
            },
        };
        layer.Fields.Add(new Proto.LayerFieldDescriptor
        {
            Definition = new Proto.FieldDefinition
            {
                Name = "STATUS",
                Alias = "Tree Status",
                FieldType = Proto.FieldType.Integer,
                Nullable = false,
            },
            Editable = true,
            DefaultValue = new Proto.AttributeValue { Int32Value = 1 },
            Domain = new Proto.FieldDomain
            {
                Name = "StatusDomain",
                CodedValues = new Proto.CodedValueDomain
                {
                    Values =
                    {
                        new Proto.DomainCodedValue
                        {
                            Code = new Proto.AttributeValue { Int32Value = 1 },
                            Name = "Healthy",
                        },
                    },
                },
            },
            Precision = 10,
            Scale = 0,
        });
        layer.Relationships.Add(new Proto.RelationshipDescriptor
        {
            RelationshipId = 7,
            Name = "TreeInspections",
            RelatedLayerId = 3,
            OriginKeyField = "GLOBALID",
            DestinationKeyField = "TREE_GID",
            Cardinality = Proto.RelationshipCardinality.OneToMany,
            Role = Proto.RelationshipRole.Origin,
            Composite = true,
        });
        layer.Subtypes = new Proto.SubtypeDefinition
        {
            Field = "TYPE_CODE",
            DefaultCode = new Proto.AttributeValue { Int32Value = 1 },
            Subtypes =
            {
                new Proto.LayerSubtype
                {
                    Code = new Proto.AttributeValue { Int32Value = 1 },
                    Name = "Palm",
                    FieldOverrides =
                    {
                        ["STATUS"] = new Proto.SubtypeFieldOverride { ClearDomain = true },
                    },
                },
            },
        };

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.DescribeLayerAsync(
                It.IsAny<Proto.DescribeLayerRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateUnary(new Proto.DescribeLayerResponse { Layer = layer }));

        var client = new HonuaGrpcClient(mockClient.Object);
        var describeResponse = await client.DescribeLayerAsync(new DescribeLayerRequest
        {
            ServiceId = "parks",
            LayerId = 2,
        });

        Assert.NotNull(describeResponse.Layer);
        Assert.Equal("GLOBALID", describeResponse.Layer.GlobalIdField);
        Assert.True(describeResponse.Layer.HasZ);
        Assert.Equal(1, describeResponse.Layer.Fields[0].DefaultValue);
        Assert.True(describeResponse.Layer.Fields[0].HasDefaultValue);
        Assert.Equal("Healthy", describeResponse.Layer.Fields[0].Domain?.CodedValues?[0].Name);
        Assert.True(describeResponse.Layer.Subtypes?.Subtypes[0].FieldOverrides["STATUS"].ClearDomain);
        Assert.Equal(RelationshipCardinality.OneToMany, describeResponse.Layer.Relationships[0].Cardinality);
        Assert.Equal(TimeIntervalUnit.Days, describeResponse.Layer.TimeInfo?.IntervalUnit);
        Assert.Equal(2_097_152, describeResponse.Layer.AttachmentLimits?.MaxUnaryUploadBytes);

        var descriptor = await ((IHonuaFeatureDescriptorClient)client).GetDescriptorAsync(new SourceDescriptor
        {
            Id = "trees",
            Protocol = FeatureProtocolIds.Grpc,
            Locator = new SourceLocator { ServiceId = "parks", LayerId = 2 },
        });

        Assert.Equal("GLOBALID", descriptor.Schema?.GlobalIdField);
        Assert.Equal("EPSG:4326+5773", descriptor.Schema?.SpatialReference);
        Assert.Equal("Tree Status", descriptor.Schema?.Fields[0].Alias);
        Assert.True(descriptor.Schema?.Fields[0].Editable);
        Assert.Equal("PLANTED_AT", descriptor.Schema?.TimeField);
        Assert.True(descriptor.Schema?.EditCapabilities?.SupportsCreateOrUpdate());
        Assert.False(descriptor.Schema?.EditCapabilities?.SupportsDeletes);
        Assert.True(descriptor.Schema?.AttachmentCapabilities?.SupportsList);
        Assert.Contains(FeatureCapabilities.QueryRelated, descriptor.Capabilities);
        Assert.Contains(FeatureCapabilities.Attachments, descriptor.Capabilities);
    }

    [Fact]
    public async Task QueryRelatedRecordsAsync_MapsRequestGroupsAndSrid()
    {
        Proto.QueryRelatedRecordsRequest? captured = null;
        var protoResponse = new Proto.QueryRelatedRecordsResponse
        {
            RelatedLayerId = 3,
            ObjectIdFieldName = "INSPECTION_ID",
            GlobalIdFieldName = "GLOBALID",
            GeometryType = Proto.GeometryType.Point,
            SpatialReference = new Proto.SpatialReference { Wkid = 4326 },
            NextPageToken = "page-2",
        };
        protoResponse.Fields.Add(new Proto.FieldDefinition { Name = "RATING", FieldType = Proto.FieldType.Integer });
        var relatedFeature = new Proto.Feature
        {
            Id = 501,
            GlobalId = "33333333-3333-3333-3333-333333333333",
            Geometry = new Proto.Geometry { Point = new Proto.PointGeometry { X = -157.8, Y = 21.3 } },
        };
        relatedFeature.Attributes["RATING"] = new Proto.AttributeValue { Int32Value = 5 };
        protoResponse.Groups.Add(new Proto.RelatedRecordGroup
        {
            ObjectId = 10,
            Features = { relatedFeature },
        });

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryRelatedRecordsAsync(
                It.IsAny<Proto.QueryRelatedRecordsRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryRelatedRecordsRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => captured = req)
            .Returns(CreateUnary(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object);
        var response = await client.QueryRelatedRecordsAsync(new QueryRelatedRecordsRequest
        {
            ServiceId = "parks",
            LayerId = 2,
            RelationshipId = 7,
            ObjectIds = [10, 11],
            OutFields = ["RATING"],
            Where = "RATING >= 3",
            ReturnGeometry = true,
            OutSr = new SpatialReference { Wkid = 4326 },
            PageSize = 50,
        });

        Assert.NotNull(captured);
        Assert.Equal([10L, 11L], captured.ObjectIds);
        Assert.Equal(7, captured.RelationshipId);
        Assert.Equal(3, response.RelatedLayerId);
        Assert.Equal("page-2", response.NextPageToken);
        Assert.Equal("GLOBALID", response.GlobalIdFieldName);
        var group = Assert.Single(response.Groups);
        Assert.Equal(10, group.ObjectId);
        var point = Assert.IsType<Point>(group.Features[0].NtsGeometry);
        Assert.Equal(4326, point.SRID);
    }

    [Fact]
    public async Task ApplyServiceEditsAsync_MapsMultiLayerRequestAndResponse()
    {
        Proto.ApplyServiceEditsRequest? captured = null;
        var protoResponse = new Proto.ApplyServiceEditsResponse
        {
            Results =
            {
                new Proto.ServiceLayerEditResults
                {
                    LayerId = 0,
                    Results = new Proto.ApplyEditsResponse
                    {
                        AddResults = { new Proto.EditResult { ObjectId = 1, GlobalId = "gid-1", Success = true } },
                    },
                },
                new Proto.ServiceLayerEditResults
                {
                    LayerId = 1,
                    Results = new Proto.ApplyEditsResponse
                    {
                        DeleteResults = { new Proto.EditResult { ObjectId = 9, Success = true } },
                    },
                },
            },
        };

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.ApplyServiceEditsAsync(
                It.IsAny<Proto.ApplyServiceEditsRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.ApplyServiceEditsRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => captured = req)
            .Returns(CreateUnary(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object);
        var response = await client.ApplyServiceEditsAsync(new ApplyServiceEditsRequest
        {
            ServiceId = "parks",
            RollbackOnFailure = true,
            IdempotencyKey = "svc-edit-1",
            Edits =
            [
                new ServiceLayerEdits
                {
                    LayerId = 0,
                    Adds = [new Feature { GlobalId = "gid-1", Attributes = new Dictionary<string, object?> { ["name"] = "A" } }],
                },
                new ServiceLayerEdits
                {
                    LayerId = 1,
                    UseGlobalIds = true,
                    DeleteGlobalIds = ["gid-9"],
                },
            ],
        });

        Assert.NotNull(captured);
        Assert.True(captured.RollbackOnFailure);
        Assert.Equal("svc-edit-1", captured.IdempotencyKey);
        Assert.Equal(2, captured.Edits.Count);
        Assert.True(captured.Edits[1].UseGlobalIds);
        Assert.Equal(["gid-9"], captured.Edits[1].DeleteGlobalIds);
        Assert.Equal(2, response.Results.Count);
        Assert.Equal("gid-1", response.Results[0].Results.AddResults[0].GlobalId);
    }

    [Fact]
    public async Task NativeAttachmentRpcs_AndChunkedUploadLifecycle_MapCorrectly()
    {
        var payload = Encoding.UTF8.GetBytes("hello-attachment");
        var sha256 = SHA256.HashData(payload);
        var descriptor = new Proto.AttachmentDescriptor
        {
            AttachmentId = 42,
            Name = "photo.jpg",
            ContentType = "image/jpeg",
            SizeBytes = payload.Length,
            GlobalId = "att-gid",
            Sha256 = ByteString.CopyFrom(sha256),
            Keywords = { "field", "inspection" },
        };

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient.Setup(c => c.GetAttachmentUrlAsync(It.IsAny<Proto.GetAttachmentUrlRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateUnary(new Proto.GetAttachmentUrlResponse { Url = "https://storage.example/signed", ExpiresAtUtcMs = 1700000000000, Attachment = descriptor }));
        mockClient.Setup(c => c.BeginAttachmentUploadAsync(It.IsAny<Proto.BeginAttachmentUploadRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateUnary(new Proto.BeginAttachmentUploadResponse { Session = new Proto.AttachmentUploadSession { SessionId = "sess-1", NextOffsetBytes = 0, MaxChunkBytes = 65536, ExpiresAtUtcMs = 1700000005000 } }));
        mockClient.Setup(c => c.UploadAttachmentChunkAsync(It.IsAny<Proto.UploadAttachmentChunkRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateUnary(new Proto.UploadAttachmentChunkResponse { Session = new Proto.AttachmentUploadSession { SessionId = "sess-1", NextOffsetBytes = payload.Length, MaxChunkBytes = 65536 } }));
        mockClient.Setup(c => c.CommitAttachmentUploadAsync(It.IsAny<Proto.CommitAttachmentUploadRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateUnary(new Proto.CommitAttachmentUploadResponse { Attachment = descriptor }));
        mockClient.Setup(c => c.AbortAttachmentUploadAsync(It.IsAny<Proto.AbortAttachmentUploadRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateUnary(new Proto.AbortAttachmentUploadResponse()));

        var client = new HonuaGrpcClient(mockClient.Object);
        var parent = new AttachmentParent { ServiceId = "parks", LayerId = 0, GlobalId = "feat-gid" };

        var grant = await client.GetAttachmentUrlAsync(new GetAttachmentUrlRequest { Parent = parent, AttachmentId = 42 });
        Assert.Equal(new Uri("https://storage.example/signed"), grant.Url);
        Assert.Equal(sha256, grant.Attachment!.Sha256.ToArray());

        var begin = await client.BeginAttachmentUploadAsync(new BeginAttachmentUploadRequest
        {
            Upload = new AttachmentUpload
            {
                Parent = parent,
                Name = "photo.jpg",
                ContentType = "image/jpeg",
                SizeBytes = payload.Length,
                Sha256 = sha256,
                IdempotencyKey = "idem-1",
            },
        });
        Assert.Equal("sess-1", begin.Session?.SessionId);

        var chunk = await client.UploadAttachmentChunkAsync(new UploadAttachmentChunkRequest
        {
            SessionId = "sess-1",
            OffsetBytes = 0,
            Content = payload,
            Sha256 = sha256,
        });
        Assert.Equal(payload.Length, chunk.Session?.NextOffsetBytes);

        var committed = await client.CommitAttachmentUploadAsync(new CommitAttachmentUploadRequest { SessionId = "sess-1" });
        Assert.Equal(42, committed.Attachment?.AttachmentId);

        var aborted = await client.AbortAttachmentUploadAsync(new AbortAttachmentUploadRequest { SessionId = "sess-2" });
        Assert.NotNull(aborted);

        Assert.Throws<ArgumentException>(() => HonuaGrpcProtoConverter.ToProto(new ListAttachmentsRequest
        {
            Parent = new AttachmentParent { ServiceId = "parks", LayerId = 0 },
        }));
        Assert.Throws<ArgumentException>(() => HonuaGrpcProtoConverter.ToProto(new ListAttachmentsRequest
        {
            Parent = new AttachmentParent { ServiceId = "parks", LayerId = 0, ObjectId = 1, GlobalId = "gid" },
        }));
    }

    [Fact]
    public async Task SharedAttachmentClient_ListAddUpdateDelete_DelegatesToNativeRpcs()
    {
        var listCalls = 0;
        Proto.UploadAttachmentRequest? lastUpload = null;
        Proto.DeleteAttachmentRequest? lastDelete = null;

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient.Setup(c => c.ListAttachmentsAsync(It.IsAny<Proto.ListAttachmentsRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                listCalls++;
                return listCalls == 1
                    ? CreateUnary(new Proto.ListAttachmentsResponse
                    {
                        Attachments = { new Proto.AttachmentDescriptor { AttachmentId = 1, Name = "a.jpg", ContentType = "image/jpeg", SizeBytes = 5, Keywords = { "k1", "k2" } } },
                        NextPageToken = "p2",
                    })
                    : CreateUnary(new Proto.ListAttachmentsResponse
                    {
                        Attachments = { new Proto.AttachmentDescriptor { AttachmentId = 2, Name = "b.jpg", ContentType = "image/jpeg", SizeBytes = 7 } },
                    });
            });
        mockClient.Setup(c => c.UploadAttachmentAsync(It.IsAny<Proto.UploadAttachmentRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<Proto.UploadAttachmentRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => lastUpload = req)
            .Returns(CreateUnary(new Proto.UploadAttachmentResponse
            {
                Attachment = new Proto.AttachmentDescriptor { AttachmentId = 99, GlobalId = "att-99" },
            }));
        mockClient.Setup(c => c.DeleteAttachmentAsync(It.IsAny<Proto.DeleteAttachmentRequest>(), It.IsAny<Metadata>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Callback<Proto.DeleteAttachmentRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => lastDelete = req)
            .Returns(CreateUnary(new Proto.DeleteAttachmentResponse { AttachmentId = 99 }));

        IHonuaFeatureAttachmentClient client = new HonuaGrpcClient(mockClient.Object);
        var source = new FeatureSource { ServiceId = "parks", LayerId = 0 };

        var listed = await client.ListAttachmentsAsync(new FeatureAttachmentListRequest { Source = source, ObjectId = 10 });
        Assert.Equal(2, listed.Count);
        Assert.Equal("k1,k2", listed[0].Keywords);

        var contentBytes = Encoding.UTF8.GetBytes("file-body");
        using var addStream = new MemoryStream(contentBytes);
        var added = await client.AddAttachmentAsync(new FeatureAttachmentAddRequest
        {
            Source = source,
            ObjectId = 10,
            Name = "note.txt",
            ContentType = "text/plain",
            Content = addStream,
            Keywords = "tag1, tag2",
        });
        Assert.True(added.Succeeded);
        Assert.Equal(99, added.AttachmentId);
        Assert.NotNull(lastUpload);
        Assert.Equal(SHA256.HashData(contentBytes), lastUpload.Upload.Sha256.ToByteArray());
        Assert.False(lastUpload.Upload.HasReplaceAttachmentId);
        Assert.False(string.IsNullOrWhiteSpace(lastUpload.Upload.IdempotencyKey));

        using var updateStream = new MemoryStream(contentBytes);
        var updated = await client.UpdateAttachmentAsync(new FeatureAttachmentUpdateRequest
        {
            Source = source,
            ObjectId = 10,
            AttachmentId = 99,
            Name = "note-v2.txt",
            ContentType = "text/plain",
            Content = updateStream,
        });
        Assert.True(updated.Succeeded);
        Assert.Equal(99, lastUpload!.Upload.ReplaceAttachmentId);

        var deleted = await client.DeleteAttachmentAsync(new FeatureAttachmentDeleteRequest
        {
            Source = source,
            ObjectId = 10,
            AttachmentId = 99,
        });
        Assert.True(deleted.Succeeded);
        Assert.Equal(99, deleted.AttachmentId);
        Assert.False(string.IsNullOrWhiteSpace(lastDelete?.IdempotencyKey));

        await Assert.ThrowsAsync<NotSupportedException>(() => client.DownloadAttachmentAsync(
            new FeatureAttachmentDownloadRequest { Source = source, ObjectId = 10, AttachmentId = 99 }));
    }

    private static AsyncUnaryCall<T> CreateUnary<T>(T response)
    {
        return new AsyncUnaryCall<T>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }
}

file static class CapabilityTestExtensions
{
    public static bool SupportsCreateOrUpdate(this FeatureEditCapabilities capabilities)
        => capabilities.SupportsAdds && capabilities.SupportsUpdates;
}
