// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Tests.Features;

/// <summary>
/// Tests the unified <see cref="HonuaFeatureGateway"/> that lets a geoprocessing
/// tool reach attachments and temporal/having queries regardless of the feature
/// transport. The fakes stand in for the gRPC and GeoServices clients.
/// </summary>
public class HonuaFeatureGatewayTests
{
    [Fact]
    public void Constructor_WithNoQueryClients_Throws()
    {
        Assert.Throws<ArgumentException>(() => new HonuaFeatureGateway(
            Array.Empty<IHonuaFeatureQueryClient>(),
            Array.Empty<IHonuaFeatureAttachmentClient>()));
    }

    [Fact]
    public async Task ListAttachments_WithGrpcPrimary_RoutesToGeoServicesProvider()
    {
        // gRPC is the primary feature transport but exposes no attachment RPCs.
        var grpc = new FakeFeatureClient("grpc", FakeFeatureClient.GrpcQueryCapabilities, attachmentSupported: false);
        var geoServices = new FakeFeatureClient(
            "geoservices",
            FakeFeatureClient.FullQueryCapabilities,
            attachmentSupported: true);

        var gateway = new HonuaFeatureGateway(
            new IHonuaFeatureQueryClient[] { grpc, geoServices },
            new IHonuaFeatureAttachmentClient[] { grpc, geoServices });

        var result = await gateway.ListAttachmentsAsync(new FeatureAttachmentListRequest
        {
            Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
            ObjectId = 1,
        });

        Assert.Single(result);
        Assert.Equal("geoservices", geoServices.LastAttachmentProvider);
        Assert.Null(grpc.LastAttachmentProvider);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("update")]
    [InlineData("delete")]
    [InlineData("download")]
    public async Task AttachmentWrites_WithGrpcPrimary_RouteToCapableProvider(string operation)
    {
        var grpc = new FakeFeatureClient("grpc", FakeFeatureClient.GrpcQueryCapabilities, attachmentSupported: false);
        var geoServices = new FakeFeatureClient("geoservices", FakeFeatureClient.FullQueryCapabilities, attachmentSupported: true);

        IHonuaFeatureGateway gateway = new HonuaFeatureGateway(
            new IHonuaFeatureQueryClient[] { grpc },
            new IHonuaFeatureAttachmentClient[] { grpc, geoServices });

        var source = new FeatureSource { ServiceId = "svc", LayerId = 0 };
        switch (operation)
        {
            case "add":
                await gateway.AddAttachmentAsync(new FeatureAttachmentAddRequest
                {
                    Source = source,
                    ObjectId = 1,
                    Name = "photo.jpg",
                    ContentType = "image/jpeg",
                    Content = Stream.Null,
                });
                break;
            case "update":
                await gateway.UpdateAttachmentAsync(new FeatureAttachmentUpdateRequest
                {
                    Source = source,
                    ObjectId = 1,
                    AttachmentId = 2,
                    Name = "photo.jpg",
                    ContentType = "image/jpeg",
                    Content = Stream.Null,
                });
                break;
            case "delete":
                await gateway.DeleteAttachmentAsync(new FeatureAttachmentDeleteRequest { Source = source, ObjectId = 1, AttachmentId = 2 });
                break;
            case "download":
                await gateway.DownloadAttachmentAsync(new FeatureAttachmentDownloadRequest { Source = source, ObjectId = 1, AttachmentId = 2 });
                break;
        }

        Assert.Equal("geoservices", geoServices.LastAttachmentProvider);
        Assert.Null(grpc.LastAttachmentProvider);
    }

    [Fact]
    public async Task ListAttachments_WithNoAttachmentProvider_ThrowsClearGuidance()
    {
        var grpc = new FakeFeatureClient("grpc", FakeFeatureClient.GrpcQueryCapabilities, attachmentSupported: false);
        var gateway = new HonuaFeatureGateway(
            new IHonuaFeatureQueryClient[] { grpc },
            new IHonuaFeatureAttachmentClient[] { grpc });

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => gateway.ListAttachmentsAsync(new FeatureAttachmentListRequest
            {
                Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
                ObjectId = 1,
            }));

        Assert.Contains("UseGeoServices", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AttachmentCapabilities_AggregateAcrossProviders()
    {
        var grpc = new FakeFeatureClient("grpc", FakeFeatureClient.GrpcQueryCapabilities, attachmentSupported: false);
        var geoServices = new FakeFeatureClient("geoservices", FakeFeatureClient.FullQueryCapabilities, attachmentSupported: true);
        var gateway = new HonuaFeatureGateway(
            new IHonuaFeatureQueryClient[] { grpc },
            new IHonuaFeatureAttachmentClient[] { grpc, geoServices });

        var caps = gateway.AttachmentCapabilities;

        Assert.True(caps.SupportsList);
        Assert.True(caps.SupportsDownload);
        Assert.True(caps.SupportsAdd);
        Assert.True(caps.SupportsUpdate);
        Assert.True(caps.SupportsDelete);
        Assert.Null(caps.UnsupportedReason);
    }

    [Fact]
    public async Task Query_PlainRequest_StaysOnPrimaryProvider()
    {
        var grpc = new FakeFeatureClient("grpc", FakeFeatureClient.GrpcQueryCapabilities, attachmentSupported: false);
        var geoServices = new FakeFeatureClient("geoservices", FakeFeatureClient.FullQueryCapabilities, attachmentSupported: true);
        var gateway = new HonuaFeatureGateway(
            new IHonuaFeatureQueryClient[] { grpc, geoServices },
            new IHonuaFeatureAttachmentClient[] { geoServices });

        await gateway.QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
        });

        Assert.Equal("grpc", grpc.LastQueryProvider);
        Assert.Null(geoServices.LastQueryProvider);
    }

    [Fact]
    public async Task Query_WithTimeFilter_RoutesAwayFromGrpcToTimeCapableProvider()
    {
        var grpc = new FakeFeatureClient("grpc", FakeFeatureClient.GrpcQueryCapabilities, attachmentSupported: false);
        var geoServices = new FakeFeatureClient("geoservices", FakeFeatureClient.FullQueryCapabilities, attachmentSupported: true);
        var gateway = new HonuaFeatureGateway(
            new IHonuaFeatureQueryClient[] { grpc, geoServices },
            new IHonuaFeatureAttachmentClient[] { geoServices });

        await gateway.QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
            TimeFilter = new FeatureTimeFilter
            {
                Start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            },
        });

        Assert.Equal("geoservices", geoServices.LastQueryProvider);
        Assert.Null(grpc.LastQueryProvider);
    }

    [Fact]
    public async Task Query_WithHaving_RoutesAwayFromGrpcToHavingCapableProvider()
    {
        var grpc = new FakeFeatureClient("grpc", FakeFeatureClient.GrpcQueryCapabilities, attachmentSupported: false);
        var geoServices = new FakeFeatureClient("geoservices", FakeFeatureClient.FullQueryCapabilities, attachmentSupported: true);
        var gateway = new HonuaFeatureGateway(
            new IHonuaFeatureQueryClient[] { grpc, geoServices },
            new IHonuaFeatureAttachmentClient[] { geoServices });

        await gateway.QueryAsync(new FeatureQueryRequest
        {
            Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
            GroupBy = new[] { "category" },
            Having = "COUNT(*) > 5",
        });

        Assert.Equal("geoservices", geoServices.LastQueryProvider);
        Assert.Null(grpc.LastQueryProvider);
    }

    [Fact]
    public async Task Query_WithTimeFilter_AndNoCapableProvider_ThrowsClearGuidance()
    {
        var grpc = new FakeFeatureClient("grpc", FakeFeatureClient.GrpcQueryCapabilities, attachmentSupported: false);
        var gateway = new HonuaFeatureGateway(
            new IHonuaFeatureQueryClient[] { grpc },
            new IHonuaFeatureAttachmentClient[] { grpc });

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => gateway.QueryAsync(new FeatureQueryRequest
            {
                Source = new FeatureSource { ServiceId = "svc", LayerId = 0 },
                TimeFilter = new FeatureTimeFilter
                {
                    Start = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                },
            }));

        Assert.Contains("time filter", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UseGeoServices", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryCapabilities_AggregateAcrossProviders()
    {
        var grpc = new FakeFeatureClient("grpc", FakeFeatureClient.GrpcQueryCapabilities, attachmentSupported: false);
        var geoServices = new FakeFeatureClient("geoservices", FakeFeatureClient.FullQueryCapabilities, attachmentSupported: true);
        var gateway = new HonuaFeatureGateway(
            new IHonuaFeatureQueryClient[] { grpc, geoServices },
            new IHonuaFeatureAttachmentClient[] { geoServices });

        var caps = gateway.QueryCapabilities;

        Assert.True(caps.SupportsTimeFilter);
        Assert.True(caps.SupportsHaving);
        Assert.True(caps.SupportsStatistics);
        Assert.True(caps.SupportsGroupBy);
    }

    [Fact]
    public void DefaultQueryCapabilities_ReportEverythingUnsupported()
    {
        // A provider that does not override QueryCapabilities falls back to the
        // conservative default-implemented interface member.
        IHonuaFeatureQueryClient bare = new BareQueryClient();

        Assert.False(bare.QueryCapabilities.SupportsTimeFilter);
        Assert.False(bare.QueryCapabilities.SupportsHaving);
        Assert.False(bare.QueryCapabilities.SupportsStatistics);
        Assert.False(bare.QueryCapabilities.SupportsGroupBy);
        Assert.NotNull(bare.QueryCapabilities.UnsupportedReason);
    }

    private sealed class BareQueryClient : IHonuaFeatureQueryClient
    {
        public string ProviderName => "bare";

        public Task<FeatureQueryResult> QueryAsync(FeatureQueryRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new FeatureQueryResult { ProviderName = ProviderName });

        public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
            FeatureQueryRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return new FeatureQueryResult { ProviderName = ProviderName };
        }
    }

    private sealed class FakeFeatureClient : IHonuaFeatureQueryClient, IHonuaFeatureAttachmentClient
    {
        public static readonly FeatureQueryCapabilities GrpcQueryCapabilities = new()
        {
            SupportsTimeFilter = false,
            SupportsStatistics = true,
            SupportsGroupBy = true,
            SupportsHaving = false,
        };

        public static readonly FeatureQueryCapabilities FullQueryCapabilities = new()
        {
            SupportsTimeFilter = true,
            SupportsStatistics = true,
            SupportsGroupBy = true,
            SupportsHaving = true,
        };

        private readonly bool _attachmentSupported;

        public FakeFeatureClient(string providerName, FeatureQueryCapabilities queryCapabilities, bool attachmentSupported)
        {
            ProviderName = providerName;
            QueryCapabilities = queryCapabilities;
            _attachmentSupported = attachmentSupported;
            AttachmentCapabilities = new FeatureAttachmentCapabilities
            {
                SupportsList = attachmentSupported,
                SupportsDownload = attachmentSupported,
                SupportsAdd = attachmentSupported,
                SupportsUpdate = attachmentSupported,
                SupportsDelete = attachmentSupported,
                UnsupportedReason = attachmentSupported ? null : "fake: no attachment RPCs",
            };
        }

        public string ProviderName { get; }

        public FeatureQueryCapabilities QueryCapabilities { get; }

        public FeatureAttachmentCapabilities AttachmentCapabilities { get; }

        public string? LastQueryProvider { get; private set; }

        public string? LastAttachmentProvider { get; private set; }

        public Task<FeatureQueryResult> QueryAsync(FeatureQueryRequest request, CancellationToken cancellationToken = default)
        {
            LastQueryProvider = ProviderName;
            return Task.FromResult(new FeatureQueryResult { ProviderName = ProviderName });
        }

        public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
            FeatureQueryRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastQueryProvider = ProviderName;
            await Task.CompletedTask;
            yield return new FeatureQueryResult { ProviderName = ProviderName };
        }

        public Task<IReadOnlyList<FeatureAttachmentInfo>> ListAttachmentsAsync(
            FeatureAttachmentListRequest request,
            CancellationToken cancellationToken = default)
        {
            Guard();
            LastAttachmentProvider = ProviderName;
            return Task.FromResult<IReadOnlyList<FeatureAttachmentInfo>>(new[] { new FeatureAttachmentInfo { Name = "photo.jpg" } });
        }

        public Task<FeatureAttachmentContent> DownloadAttachmentAsync(
            FeatureAttachmentDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            Guard();
            LastAttachmentProvider = ProviderName;
            return Task.FromResult(new FeatureAttachmentContent { Content = Stream.Null });
        }

        public Task<FeatureAttachmentResult> AddAttachmentAsync(
            FeatureAttachmentAddRequest request,
            CancellationToken cancellationToken = default)
        {
            Guard();
            LastAttachmentProvider = ProviderName;
            return Task.FromResult(new FeatureAttachmentResult { Succeeded = true });
        }

        public Task<FeatureAttachmentResult> UpdateAttachmentAsync(
            FeatureAttachmentUpdateRequest request,
            CancellationToken cancellationToken = default)
        {
            Guard();
            LastAttachmentProvider = ProviderName;
            return Task.FromResult(new FeatureAttachmentResult { Succeeded = true });
        }

        public Task<FeatureAttachmentResult> DeleteAttachmentAsync(
            FeatureAttachmentDeleteRequest request,
            CancellationToken cancellationToken = default)
        {
            Guard();
            LastAttachmentProvider = ProviderName;
            return Task.FromResult(new FeatureAttachmentResult { Succeeded = true });
        }

        private void Guard()
        {
            if (!_attachmentSupported)
            {
                throw new NotSupportedException("fake: no attachment RPCs");
            }
        }
    }
}
