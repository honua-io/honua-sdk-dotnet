// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Grpc.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Proto = Geospatial.V1;

namespace Honua.Sdk.Grpc.Tests;

public class HonuaGrpcClientTests
{
    [Fact]
    public async Task QueryFeaturesAsync_DelegatesToStub()
    {
        var protoResponse = new Proto.QueryFeaturesResponse
        {
            ObjectIdFieldName = "OBJECTID",
            GeometryType = Proto.GeometryType.Point,
        };

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncUnaryCall(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object);
        var request = new Models.QueryFeaturesRequest
        {
            ServiceId = "test-svc",
            LayerId = 0,
        };

        var result = await client.QueryFeaturesAsync(request);

        Assert.Equal("OBJECTID", result.ObjectIdFieldName);
        Assert.Equal(Models.GeometryType.Point, result.GeometryType);
    }

    [Fact]
    public async Task QueryFeaturesAsync_AppliesConfiguredDeadline()
    {
        var timeout = TimeSpan.FromSeconds(30);
        DateTime? capturedDeadline = null;
        var protoResponse = new Proto.QueryFeaturesResponse();

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryFeaturesRequest, Metadata, DateTime?, CancellationToken>(
                (_, _, deadline, _) => capturedDeadline = deadline)
            .Returns(CreateAsyncUnaryCall(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object, new HonuaGrpcClientOptions
        {
            EnableCompressionNegotiation = false,
            Timeout = timeout
        });
        var before = DateTime.UtcNow;

        await client.QueryFeaturesAsync(new Models.QueryFeaturesRequest { ServiceId = "test-svc" });

        Assert.NotNull(capturedDeadline);
        Assert.InRange(capturedDeadline.Value, before.Add(timeout).AddSeconds(-1), DateTime.UtcNow.Add(timeout).AddSeconds(1));
    }

    [Fact]
    public void Constructor_InvalidTimeout_Throws()
    {
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new HonuaGrpcClient(mockClient.Object, new HonuaGrpcClientOptions
            {
                Timeout = TimeSpan.FromMilliseconds(10)
            }));

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_SharedAbstraction_DelegatesToGrpcQuery()
    {
        Proto.QueryFeaturesRequest? capturedRequest = null;
        var protoResponse = new Proto.QueryFeaturesResponse
        {
            ObjectIdFieldName = "OBJECTID",
            GeometryType = Proto.GeometryType.Point,
            ExceededTransferLimit = true,
            Count = 12,
            Extent = new Proto.Extent
            {
                Xmin = -180,
                Ymin = -90,
                Xmax = 180,
                Ymax = 90,
                SpatialReference = new Proto.SpatialReference { Wkid = 4326 },
            },
        };
        protoResponse.ObjectIds.AddRange([42L, 43L]);
        var feature = new Proto.Feature { Id = 42 };
        feature.Attributes["name"] = new Proto.AttributeValue { StringValue = "Park" };
        protoResponse.Features.Add(feature);

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryFeaturesRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => capturedRequest = req)
            .Returns(CreateAsyncUnaryCall(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object);

        var result = await ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { ServiceId = "test-svc", LayerId = 0 },
            Filter = "status = 'open'",
            FilterLanguage = FeatureFilterLanguage.SqlWhere,
            ObjectIds = [42],
            OutFields = ["name"],
            ReturnGeometry = false,
            Offset = 5,
            Limit = 10,
            OrderBy = "name ASC",
            ReturnDistinct = true,
            ReturnCountOnly = true,
            ReturnIdsOnly = true,
            ReturnExtentOnly = true,
            OutStatistics =
            [
                new FeatureQueryStatistic
                {
                    OnField = "POP",
                    StatisticType = FeatureStatisticType.Average,
                    OutField = "AVG_POP"
                }
            ],
            GroupBy = ["STATE"],
            OutputCrs = "EPSG:3857",
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal("test-svc", capturedRequest.ServiceId);
        Assert.Equal(0, capturedRequest.LayerId);
        Assert.Equal("status = 'open'", capturedRequest.Where);
        Assert.Equal([42L], capturedRequest.ObjectIds);
        Assert.Equal(["name"], capturedRequest.OutFields);
        Assert.False(capturedRequest.ReturnGeometry);
        Assert.Equal(5, capturedRequest.ResultOffset);
        Assert.Equal(10, capturedRequest.ResultRecordCount);
        Assert.Equal("name ASC", capturedRequest.OrderBy);
        Assert.True(capturedRequest.ReturnDistinct);
        Assert.True(capturedRequest.ReturnCountOnly);
        Assert.True(capturedRequest.ReturnIdsOnly);
        Assert.True(capturedRequest.ReturnExtentOnly);
        Assert.Single(capturedRequest.OutStatistics);
        Assert.Equal("POP", capturedRequest.OutStatistics[0].OnStatisticField);
        Assert.Equal(Proto.StatisticType.Avg, capturedRequest.OutStatistics[0].StatisticType);
        Assert.Equal("AVG_POP", capturedRequest.OutStatistics[0].OutStatisticFieldName);
        Assert.Equal(["STATE"], capturedRequest.GroupBy);
        Assert.Equal(3857, capturedRequest.OutSr.Wkid);
        Assert.Equal("grpc", result.ProviderName);
        Assert.Equal(12, result.NumberMatched);
        Assert.Equal([42L, 43L], result.ObjectIds);
        Assert.NotNull(result.Extent);
        Assert.Equal("EPSG:4326", result.Extent.Crs);
        Assert.True(result.HasMoreResults);
        Assert.Single(result.Features);
        Assert.Equal("42", result.Features[0].Id);
        Assert.Equal("Park", result.Features[0].Attributes["name"].GetString());
    }

    [Fact]
    public async Task QueryAsync_SharedAbstraction_UnsupportedTimeFilterThrows()
    {
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        var client = new HonuaGrpcClient(mockClient.Object);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
            {
                Source = new FeatureSource { ServiceId = "test-svc", LayerId = 0 },
                TimeFilter = new FeatureTimeFilter
                {
                    Start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
                },
            }));

        Assert.Contains("time filters", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_SharedAbstraction_Crs84MapsToWkid4326()
    {
        Proto.QueryFeaturesRequest? capturedRequest = null;
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryFeaturesRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => capturedRequest = req)
            .Returns(CreateAsyncUnaryCall(new Proto.QueryFeaturesResponse()));

        var client = new HonuaGrpcClient(mockClient.Object);

        await ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { ServiceId = "test-svc", LayerId = 0 },
            Bbox = new FeatureBoundingBox
            {
                MinX = -118,
                MinY = 33,
                MaxX = -117,
                MaxY = 34,
                Crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
            },
            OutputCrs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal(4326, capturedRequest.OutSr.Wkid);
        Assert.Equal(4326, capturedRequest.SpatialFilter.SpatialReference.Wkid);
    }

    [Fact]
    public async Task QueryAsync_SharedAbstraction_MapsExplicitSpatialFilter()
    {
        Proto.QueryFeaturesRequest? capturedRequest = null;
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryFeaturesRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => capturedRequest = req)
            .Returns(CreateAsyncUnaryCall(new Proto.QueryFeaturesResponse()));

        var client = new HonuaGrpcClient(mockClient.Object);

        await ((IHonuaFeatureQueryClient)client).QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { ServiceId = "test-svc", LayerId = 0 },
            SpatialFilter = new FeatureSpatialFilter
            {
                Geometry = JsonSerializer.SerializeToElement(new { x = -118.0, y = 34.0 }),
                GeometryType = FeatureSpatialGeometryType.Point,
                Crs = "EPSG:4326",
                Relationship = FeatureSpatialRelationship.Within
            }
        });

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedRequest.SpatialFilter);
        Assert.Equal(Proto.SpatialRelationship.Within, capturedRequest.SpatialFilter.SpatialRelationship);
        Assert.Equal(4326, capturedRequest.SpatialFilter.SpatialReference.Wkid);
        Assert.Equal(Proto.Geometry.ShapeOneofCase.Point, capturedRequest.SpatialFilter.Geometry.ShapeCase);
        Assert.Equal(-118.0, capturedRequest.SpatialFilter.Geometry.Point.X);
        Assert.Equal(34.0, capturedRequest.SpatialFilter.Geometry.Point.Y);
    }

    [Fact]
    public async Task HonuaSourceFacade_QueriesGrpcClientAndExposesNativeProtocol()
    {
        Proto.QueryFeaturesRequest? capturedRequest = null;
        var protoResponse = new Proto.QueryFeaturesResponse();
        protoResponse.Features.Add(new Proto.Feature { Id = 42 });

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryFeaturesRequest, Metadata, DateTime?, CancellationToken>((req, _, _, _) => capturedRequest = req)
            .Returns(CreateAsyncUnaryCall(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object);
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "parks",
                Protocol = FeatureProtocolIds.Grpc,
                Locator = new SourceLocator { ServiceId = "svc", LayerId = 3 }
            },
            client,
            client,
            client);

        var query = new SourceQuery { Where = "1=1", Limit = 10 };
        var result = await source.QueryAsync(query);

        Assert.NotNull(capturedRequest);
        Assert.Equal("svc", capturedRequest.ServiceId);
        Assert.Equal(3, capturedRequest.LayerId);
        Assert.Equal("1=1", capturedRequest.Where);
        Assert.Equal(10, capturedRequest.ResultRecordCount);
        Assert.Equal("grpc", result.ProviderName);
        Assert.Equal("42", Assert.Single(result.Features).Id);
        Assert.Contains(FeatureCapabilities.ApplyEdits, source.Capabilities);
        Assert.Same(client, source.Protocol<HonuaGrpcClient>());
    }

    [Fact]
    public async Task ApplyEditsAsync_SharedAbstraction_DelegatesToGrpcApplyEdits()
    {
        Proto.ApplyEditsRequest? capturedRequest = null;
        var protoResponse = new Proto.ApplyEditsResponse
        {
            Error = null,
        };
        protoResponse.AddResults.Add(new Proto.EditResult { ObjectId = 101, Success = true });
        protoResponse.UpdateResults.Add(new Proto.EditResult
        {
            ObjectId = 102,
            Success = false,
            Error = new Proto.EditError { Code = 400, Message = "Update rejected" }
        });
        protoResponse.DeleteResults.Add(new Proto.EditResult { ObjectId = 103, Success = true });

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.ApplyEditsAsync(
                It.IsAny<Proto.ApplyEditsRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.ApplyEditsRequest, Metadata, DateTime?, CancellationToken>(
                (req, _, _, _) => capturedRequest = req)
            .Returns(CreateAsyncUnaryCall(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object);

        var response = await ((IHonuaFeatureEditClient)client).ApplyEditsAsync(new FeatureEditRequest
        {
            Source = new FeatureSource { ServiceId = "parks", LayerId = 2 },
            Adds =
            [
                new FeatureEditFeature
                {
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["name"] = JsonValue("New Park"),
                        ["active"] = JsonValue(true),
                        ["visitors"] = JsonValue(12),
                    },
                    Geometry = JsonObject("""{"x":1.25,"y":2.5,"spatialReference":{"wkid":4326}}"""),
                }
            ],
            Updates =
            [
                new FeatureEditFeature
                {
                    Id = "77",
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["name"] = JsonValue("Renamed Park"),
                    },
                }
            ],
            DeleteObjectIds = [88],
            DeleteIds = ["89"],
            RollbackOnFailure = true,
            ForceWrite = true,
        });

        Assert.NotNull(capturedRequest);
        Assert.Equal("parks", capturedRequest.ServiceId);
        Assert.Equal(2, capturedRequest.LayerId);
        Assert.True(capturedRequest.RollbackOnFailure);
        Assert.True(capturedRequest.ForceWrite);
        Assert.Single(capturedRequest.Adds);
        Assert.Equal("New Park", capturedRequest.Adds[0].Attributes["name"].StringValue);
        Assert.True(capturedRequest.Adds[0].Attributes["active"].BoolValue);
        Assert.Equal(12, capturedRequest.Adds[0].Attributes["visitors"].Int64Value);
        Assert.Equal(Proto.Geometry.ShapeOneofCase.Point, capturedRequest.Adds[0].Geometry.ShapeCase);
        Assert.Single(capturedRequest.Updates);
        Assert.Equal(77, capturedRequest.Updates[0].Id);
        Assert.Equal([88L, 89L], capturedRequest.Deletes);

        Assert.Equal("grpc", response.ProviderName);
        Assert.False(response.Succeeded);
        Assert.Equal(101, response.AddResults[0].ObjectId);
        Assert.True(response.AddResults[0].Succeeded);
        Assert.Equal(102, response.UpdateResults[0].ObjectId);
        Assert.False(response.UpdateResults[0].Succeeded);
        Assert.Equal(400, response.UpdateResults[0].Error?.Code);
        Assert.Equal("Update rejected", response.UpdateResults[0].Error?.Message);
        Assert.Equal(103, response.DeleteResults[0].ObjectId);
    }

    [Fact]
    public async Task ApplyEditsAsync_SharedAbstraction_NonNumericUpdateId_Throws()
    {
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        var client = new HonuaGrpcClient(mockClient.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IHonuaFeatureEditClient)client).ApplyEditsAsync(new FeatureEditRequest
            {
                Source = new FeatureSource { ServiceId = "parks", LayerId = 2 },
                Updates =
                [
                    new FeatureEditFeature { Id = "abc" }
                ],
            }));

        Assert.Contains("numeric", ex.Message);
    }

    [Fact]
    public void AddHonuaGrpc_RegistersSharedEditClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaGrpc(options => options.Address = "http://localhost:5000");

        using var provider = services.BuildServiceProvider();
        var editClient = Assert.Single(provider.GetServices<IHonuaFeatureEditClient>());

        Assert.Equal("grpc", editClient.ProviderName);
        Assert.True(editClient.EditCapabilities.SupportsAdds);
        Assert.True(editClient.EditCapabilities.SupportsUpdates);
        Assert.True(editClient.EditCapabilities.SupportsDeletes);
    }

    [Fact]
    public async Task QueryFeaturesStreamAsync_YieldsPagesAndStopsOnLastPage()
    {
        var page1 = new Proto.FeaturePage
        {
            ObjectIdFieldName = "FID",
            IsLastPage = false,
        };
        var feature1 = new Proto.Feature { Id = 1 };
        page1.Features.Add(feature1);

        var page2 = new Proto.FeaturePage
        {
            IsLastPage = true,
        };
        var feature2 = new Proto.Feature { Id = 2 };
        page2.Features.Add(feature2);

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryFeaturesStream(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncServerStreamingCall([page1, page2]));

        var client = new HonuaGrpcClient(mockClient.Object);
        var request = new Models.QueryFeaturesRequest
        {
            ServiceId = "test-svc",
            LayerId = 0,
        };

        var pages = new List<Models.FeaturePage>();
        await foreach (var page in client.QueryFeaturesStreamAsync(request))
        {
            pages.Add(page);
        }

        Assert.Equal(2, pages.Count);
        Assert.Equal("FID", pages[0].ObjectIdFieldName);
        Assert.False(pages[0].IsLastPage);
        Assert.True(pages[1].IsLastPage);
        Assert.Equal(1L, pages[0].Features[0].Id);
        Assert.Equal(2L, pages[1].Features[0].Id);
    }

    [Fact]
    public async Task QueryFeaturesAsync_RpcException_WrappedInHonuaGrpcException()
    {
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Throws(new RpcException(new Status(StatusCode.NotFound, "Layer not found")));

        var client = new HonuaGrpcClient(mockClient.Object);
        var request = new Models.QueryFeaturesRequest
        {
            ServiceId = "test-svc",
            LayerId = 999,
        };

        var ex = await Assert.ThrowsAsync<HonuaGrpcException>(() => client.QueryFeaturesAsync(request));

        Assert.Equal(StatusCode.NotFound, ex.StatusCode);
        Assert.Contains("Layer not found", ex.Message);
    }

    [Fact]
    public async Task QueryFeaturesStreamAsync_RpcException_WrappedInHonuaGrpcException()
    {
        var rpcException = new RpcException(new Status(StatusCode.Unavailable, "Stream unavailable"));

        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        mockClient
            .Setup(c => c.QueryFeaturesStream(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateFaultedAsyncServerStreamingCall<Proto.FeaturePage>(rpcException));

        var client = new HonuaGrpcClient(mockClient.Object);
        var request = new Models.QueryFeaturesRequest
        {
            ServiceId = "test-svc",
            LayerId = 0,
        };

        async Task Run()
        {
            await foreach (var _ in client.QueryFeaturesStreamAsync(request))
            {
            }
        }

        var ex = await Assert.ThrowsAsync<HonuaGrpcException>(Run);

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
        Assert.Contains("Stream unavailable", ex.Message);
    }

    [Fact]
    public async Task Metadata_IncludesApiKey_WhenConfigured()
    {
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        Metadata? capturedMetadata = null;

        var protoResponse = new Proto.QueryFeaturesResponse();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryFeaturesRequest, Metadata, DateTime?, CancellationToken>(
                (_, metadata, _, _) => capturedMetadata = metadata)
            .Returns(CreateAsyncUnaryCall(protoResponse));

        var metadata = new Metadata { { "x-api-key", "my-key" } };
        var client = new HonuaGrpcClient(mockClient.Object, metadata);

        await client.QueryFeaturesAsync(new Models.QueryFeaturesRequest { ServiceId = "svc" });

        Assert.NotNull(capturedMetadata);
        var apiKeyEntry = capturedMetadata!.FirstOrDefault(e => e.Key == "x-api-key");
        Assert.NotNull(apiKeyEntry);
        Assert.Equal("my-key", apiKeyEntry.Value);
    }

    [Fact]
    public async Task Metadata_IncludesBearerToken_WhenConfigured()
    {
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        Metadata? capturedMetadata = null;

        var protoResponse = new Proto.QueryFeaturesResponse();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryFeaturesRequest, Metadata, DateTime?, CancellationToken>(
                (_, metadata, _, _) => capturedMetadata = metadata)
            .Returns(CreateAsyncUnaryCall(protoResponse));

        var metadata = new Metadata { { "authorization", "Bearer my-token" } };
        var client = new HonuaGrpcClient(mockClient.Object, metadata);

        await client.QueryFeaturesAsync(new Models.QueryFeaturesRequest { ServiceId = "svc" });

        Assert.NotNull(capturedMetadata);
        var authEntry = capturedMetadata!.FirstOrDefault(e => e.Key == "authorization");
        Assert.NotNull(authEntry);
        Assert.Equal("Bearer my-token", authEntry.Value);
    }

    [Fact]
    public async Task Metadata_IncludesStaticCredentials_WhenConfiguredInOptions()
    {
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        Metadata? capturedMetadata = null;

        var protoResponse = new Proto.QueryFeaturesResponse();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryFeaturesRequest, Metadata, DateTime?, CancellationToken>(
                (_, metadata, _, _) => capturedMetadata = metadata)
            .Returns(CreateAsyncUnaryCall(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object, new HonuaGrpcClientOptions
        {
            EnableCompressionNegotiation = false,
            ApiKey = "my-key",
            BearerToken = "my-token"
        });

        await client.QueryFeaturesAsync(new Models.QueryFeaturesRequest { ServiceId = "svc" });

        Assert.NotNull(capturedMetadata);
        Assert.Equal("my-key", GetMetadataValue(capturedMetadata!, "x-api-key"));
        Assert.Equal("Bearer my-token", GetMetadataValue(capturedMetadata!, "authorization"));
    }

    [Fact]
    public async Task Metadata_UsesCredentialProvidersPerCall()
    {
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        var capturedMetadata = new List<Metadata>();
        var apiKeyCalls = 0;
        var bearerTokenCalls = 0;

        var protoResponse = new Proto.QueryFeaturesResponse();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryFeaturesRequest, Metadata, DateTime?, CancellationToken>(
                (_, metadata, _, _) => capturedMetadata.Add(metadata))
            .Returns(CreateAsyncUnaryCall(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object, new HonuaGrpcClientOptions
        {
            EnableCompressionNegotiation = false,
            ApiKeyProvider = _ => Task.FromResult<string?>($"grpc-key-{++apiKeyCalls}"),
            BearerTokenProvider = _ => Task.FromResult<string?>($"grpc-token-{++bearerTokenCalls}")
        });

        await client.QueryFeaturesAsync(new Models.QueryFeaturesRequest { ServiceId = "svc" });
        await client.QueryFeaturesAsync(new Models.QueryFeaturesRequest { ServiceId = "svc" });

        Assert.Collection(
            capturedMetadata,
            first =>
            {
                Assert.Equal("grpc-key-1", GetMetadataValue(first, "x-api-key"));
                Assert.Equal("Bearer grpc-token-1", GetMetadataValue(first, "authorization"));
            },
            second =>
            {
                Assert.Equal("grpc-key-2", GetMetadataValue(second, "x-api-key"));
                Assert.Equal("Bearer grpc-token-2", GetMetadataValue(second, "authorization"));
            });
    }

    [Fact]
    public async Task Metadata_ProviderReturningNullOrEmpty_OmitsCredentials()
    {
        var mockClient = new Mock<Proto.FeatureService.FeatureServiceClient>();
        Metadata? capturedMetadata = null;

        var protoResponse = new Proto.QueryFeaturesResponse();
        mockClient
            .Setup(c => c.QueryFeaturesAsync(
                It.IsAny<Proto.QueryFeaturesRequest>(),
                It.IsAny<Metadata>(),
                It.IsAny<DateTime?>(),
                It.IsAny<CancellationToken>()))
            .Callback<Proto.QueryFeaturesRequest, Metadata, DateTime?, CancellationToken>(
                (_, metadata, _, _) => capturedMetadata = metadata)
            .Returns(CreateAsyncUnaryCall(protoResponse));

        var client = new HonuaGrpcClient(mockClient.Object, new HonuaGrpcClientOptions
        {
            EnableCompressionNegotiation = false,
            ApiKey = "fallback-key",
            BearerToken = "fallback-token",
            ApiKeyProvider = _ => Task.FromResult<string?>(null),
            BearerTokenProvider = _ => Task.FromResult<string?>(string.Empty)
        });

        await client.QueryFeaturesAsync(new Models.QueryFeaturesRequest { ServiceId = "svc" });

        Assert.NotNull(capturedMetadata);
        Assert.Null(GetMetadataValue(capturedMetadata!, "x-api-key"));
        Assert.Null(GetMetadataValue(capturedMetadata!, "authorization"));
    }

    [Fact]
    public void Constructor_WithCredentialsAndRemoteHttpAddress_Throws()
    {
        var options = Options.Create(new HonuaGrpcClientOptions
        {
            Address = "http://example.com:5000",
            ApiKey = "my-key"
        });

        var ex = Assert.Throws<InvalidOperationException>(() => new HonuaGrpcClient(options));
        Assert.Contains("Refusing to send gRPC credentials over an insecure connection", ex.Message);
    }

    [Fact]
    public void Constructor_WithCredentialProviderAndRemoteHttpAddress_Throws()
    {
        var options = Options.Create(new HonuaGrpcClientOptions
        {
            Address = "http://example.com:5000",
            BearerTokenProvider = _ => Task.FromResult<string?>("my-token")
        });

        var ex = Assert.Throws<InvalidOperationException>(() => new HonuaGrpcClient(options));
        Assert.Contains("Refusing to send gRPC credentials over an insecure connection", ex.Message);
    }

    [Fact]
    public void ChannelConstructor_WithCredentialProviderAndRemoteHttpAddress_Throws()
    {
        using var channel = GrpcChannel.ForAddress("http://example.com:5000");
        var options = new HonuaGrpcClientOptions
        {
            BearerTokenProvider = _ => Task.FromResult<string?>("my-token")
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new HonuaGrpcClient(channel, options));
        Assert.Contains("Refusing to send gRPC credentials over an insecure connection", ex.Message);
    }

    [Fact]
    public void ChannelConstructor_WithCredentialProviderAndLoopbackHttpAddress_DoesNotThrow()
    {
        using var channel = GrpcChannel.ForAddress("http://localhost:5000");
        var options = new HonuaGrpcClientOptions
        {
            ApiKeyProvider = _ => Task.FromResult<string?>("my-key")
        };

        using var client = new HonuaGrpcClient(channel, options);
    }

    [Fact]
    public void Constructor_WithCredentialsAndLoopbackHttpAddress_DoesNotThrow()
    {
        var options = Options.Create(new HonuaGrpcClientOptions
        {
            Address = "http://localhost:5000",
            BearerToken = "my-token"
        });

        using var client = new HonuaGrpcClient(options);
    }

    [Fact]
    public void HonuaGrpcException_ContainsStatusCode()
    {
        var ex = new HonuaGrpcException(StatusCode.PermissionDenied, "Access denied");

        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
        Assert.Contains("PermissionDenied", ex.Message);
        Assert.Contains("Access denied", ex.Message);
    }

    private static AsyncUnaryCall<T> CreateAsyncUnaryCall<T>(T response)
    {
        return new AsyncUnaryCall<T>(
            Task.FromResult(response),
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    private static string? GetMetadataValue(Metadata metadata, string key)
        => metadata.FirstOrDefault(entry => entry.Key == key)?.Value;

    private static JsonElement JsonValue<T>(T value)
        => JsonSerializer.SerializeToElement(value);

    private static JsonElement JsonObject(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static AsyncServerStreamingCall<T> CreateAsyncServerStreamingCall<T>(IEnumerable<T> responses)
    {
        var stream = new TestAsyncStreamReader<T>(responses);
        return new AsyncServerStreamingCall<T>(
            stream,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => new Metadata(),
            () => { });
    }

    private static AsyncServerStreamingCall<T> CreateFaultedAsyncServerStreamingCall<T>(RpcException exception)
    {
        var stream = new ThrowingAsyncStreamReader<T>(exception);
        return new AsyncServerStreamingCall<T>(
            stream,
            Task.FromResult(new Metadata()),
            () => exception.Status,
            () => new Metadata(),
            () => { });
    }

    private sealed class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        private readonly IEnumerator<T> _enumerator;

        public TestAsyncStreamReader(IEnumerable<T> items)
        {
            _enumerator = items.GetEnumerator();
        }

        public T Current => _enumerator.Current;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return Task.FromResult(_enumerator.MoveNext());
        }
    }

    private sealed class ThrowingAsyncStreamReader<T>(RpcException exception) : IAsyncStreamReader<T>
    {
        public T Current => default!;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
            => Task.FromException<bool>(exception);
    }
}
