// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using System.Text.Json;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class SourceFacadeTests
{
    [Theory]
    [InlineData("grpc", FeatureProtocolIds.Grpc)]
    [InlineData("geoservices-featureserver", FeatureProtocolIds.GeoServicesFeatureService)]
    [InlineData("FeatureServer", FeatureProtocolIds.GeoServicesFeatureService)]
    [InlineData("ogc-api-features", FeatureProtocolIds.OgcFeatures)]
    [InlineData("wfs", FeatureProtocolIds.Wfs)]
    public void Normalize_MapsProviderAliasesToCanonicalProtocolIds(string alias, string expected)
    {
        Assert.Equal(expected, FeatureProtocolIds.Normalize(alias));
        Assert.True(FeatureProtocolIds.Matches(alias, expected));
    }

    [Fact]
    public void DefaultsFor_ReturnsCanonicalCapabilitySet()
    {
        var geoservices = FeatureProtocolCapabilities.DefaultsFor("geoservices-featureserver");
        var wfs = FeatureProtocolCapabilities.DefaultsFor(FeatureProtocolIds.Wfs);

        Assert.Contains(FeatureCapabilities.Query, geoservices);
        Assert.Contains(FeatureCapabilities.ApplyEdits, geoservices);
        Assert.Contains(FeatureCapabilities.QueryObjectIds, wfs);
        Assert.DoesNotContain(FeatureCapabilities.ApplyEdits, wfs);
    }

    [Fact]
    public async Task HonuaSource_QueryAllAsync_DrainsPagesAndSuppliesDescriptorSource()
    {
        FeatureQueryRequest? capturedRequest = null;
        var queryClient = new FakeQueryClient(
            "grpc",
            request =>
            {
                capturedRequest = request;
                return
                [
                    new FeatureQueryResult
                    {
                        ProviderName = "grpc",
                        Features =
                        [
                            new FeatureRecord
                            {
                                Id = "1",
                                Attributes = new Dictionary<string, JsonElement>
                                {
                                    ["name"] = JsonSerializer.SerializeToElement("One")
                                }
                            }
                        ],
                        NumberMatched = 2,
                        NumberReturned = 1,
                        HasMoreResults = true
                    },
                    new FeatureQueryResult
                    {
                        ProviderName = "grpc",
                        Features =
                        [
                            new FeatureRecord
                            {
                                Id = "2",
                                Attributes = new Dictionary<string, JsonElement>
                                {
                                    ["name"] = JsonSerializer.SerializeToElement("Two")
                                }
                            }
                        ],
                        NumberMatched = 2,
                        NumberReturned = 1
                    }
                ];
            });
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "parks",
                Protocol = FeatureProtocolIds.Grpc,
                Locator = new SourceLocator { ServiceId = "svc", LayerId = 0 }
            },
            queryClient,
            nativeClient: queryClient);

        var result = await source.QueryAllAsync(new SourceQuery { Where = "1=1", Limit = 100 });

        Assert.NotNull(capturedRequest);
        Assert.Equal("svc", capturedRequest.Source.ServiceId);
        Assert.Equal(0, capturedRequest.Source.LayerId);
        Assert.Equal("1=1", capturedRequest.Filter);
        Assert.Equal(100, capturedRequest.Limit);
        Assert.Equal(2, result.Features.Count);
        Assert.Equal(2, result.NumberMatched);
        Assert.Equal(2, result.NumberReturned);
    }

    [Fact]
    public async Task HonuaSource_QueryAllAsync_RespectsSourceQueryLimit()
    {
        var queryClient = new FakeQueryClient(
            "grpc",
            _ =>
            [
                new FeatureQueryResult
                {
                    ProviderName = "grpc",
                    Features =
                    [
                        new FeatureRecord { Id = "1" },
                        new FeatureRecord { Id = "2" }
                    ],
                    NumberMatched = 3,
                    NumberReturned = 2,
                    HasMoreResults = true
                },
                new FeatureQueryResult
                {
                    ProviderName = "grpc",
                    Features = [new FeatureRecord { Id = "3" }],
                    NumberMatched = 3,
                    NumberReturned = 1
                }
            ]);
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "parks",
                Protocol = FeatureProtocolIds.Grpc,
                Locator = new SourceLocator { ServiceId = "svc", LayerId = 0 }
            },
            queryClient);

        var result = await source.QueryAllAsync(new SourceQuery { Limit = 1 });

        Assert.Equal(["1"], result.Features.Select(feature => feature.Id));
        Assert.Equal(3, result.NumberMatched);
        Assert.Equal(1, result.NumberReturned);
    }

    [Fact]
    public async Task HonuaSource_QueryObjectIdsAsync_UsesFeatureIdsAndPrimaryKeyFallback()
    {
        var queryClient = new FakeQueryClient(
            "wfs",
            _ =>
            [
                new FeatureQueryResult
                {
                    ProviderName = "wfs",
                    Features =
                    [
                        new FeatureRecord { Id = "parcels.1" },
                        new FeatureRecord
                        {
                            Attributes = new Dictionary<string, JsonElement>
                            {
                                ["parcel_id"] = JsonSerializer.SerializeToElement("APN-002")
                            }
                        }
                    ],
                    NumberReturned = 2
                }
            ]);
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "parcels",
                Protocol = FeatureProtocolIds.Wfs,
                Locator = new SourceLocator { TypeName = "parcels" },
                Schema = new SourceSchema { PrimaryKey = "parcel_id" }
            },
            queryClient);

        var ids = await source.QueryObjectIdsAsync();

        Assert.Equal(["parcels.1", "APN-002"], ids);
    }

    [Fact]
    public async Task HonuaSource_QueryObjectIdsAsync_ZeroLimitReturnsEmpty()
    {
        var queryClient = new FakeQueryClient(
            "wfs",
            _ =>
            [
                new FeatureQueryResult
                {
                    ProviderName = "wfs",
                    Features = [new FeatureRecord { Id = "parcels.1" }],
                    NumberReturned = 1
                }
            ]);
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "parcels",
                Protocol = FeatureProtocolIds.Wfs,
                Locator = new SourceLocator { TypeName = "parcels" }
            },
            queryClient);

        var ids = await source.QueryObjectIdsAsync(new SourceQuery { Limit = 0 });

        Assert.Empty(ids);
    }

    [Fact]
    public async Task HonuaSource_QueryObjectIdsAsync_DoesNotRequireQueryCapability()
    {
        FeatureQueryRequest? capturedRequest = null;
        var queryClient = new FakeQueryClient(
            "wfs",
            request =>
            {
                capturedRequest = request;
                return
                [
                    new FeatureQueryResult
                    {
                        ProviderName = "wfs",
                        Features =
                        [
                            new FeatureRecord { Id = "parcels.1" },
                            new FeatureRecord { Id = "parcels.1" },
                            new FeatureRecord { Id = "parcels.2" }
                        ],
                        NumberReturned = 3
                    }
                ];
            });
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "ids-only",
                Protocol = FeatureProtocolIds.Wfs,
                Locator = new SourceLocator { TypeName = "parcels" },
                Capabilities = [FeatureCapabilities.QueryObjectIds]
            },
            queryClient);

        var ids = await source.QueryObjectIdsAsync(new SourceQuery { Limit = 10 });

        Assert.DoesNotContain(FeatureCapabilities.Query, source.Capabilities);
        Assert.Contains(FeatureCapabilities.QueryObjectIds, source.Capabilities);
        Assert.NotNull(capturedRequest);
        Assert.False(capturedRequest.ReturnGeometry);
        Assert.Equal(10, capturedRequest.Limit);
        Assert.Equal(["parcels.1", "parcels.2"], ids);
    }

    [Fact]
    public async Task HonuaSource_ApplyEditsAsync_RejectsUnsupportedEditCapability()
    {
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "parcels",
                Protocol = FeatureProtocolIds.Wfs,
                Locator = new SourceLocator { TypeName = "parcels" }
            },
            new FakeQueryClient("wfs", _ => []),
            new FakeEditClient("wfs", new FeatureEditCapabilities
            {
                NativeSurface = "WFS-T Transaction",
                UnsupportedReason = "WFS-T is not implemented."
            }));

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => source.ApplyEditsAsync(new FeatureEditRequest
            {
                Adds = [new FeatureEditFeature()]
            }));

        Assert.DoesNotContain(FeatureCapabilities.ApplyEdits, source.Capabilities);
        Assert.Contains("applyEdits", ex.Message);
    }

    [Fact]
    public async Task HonuaSource_ApplyEditsAsync_RejectsMismatchedEditClient()
    {
        var editClient = new FakeEditClient(
            "geoservices-featureserver",
            new FeatureEditCapabilities
            {
                SupportsAdds = true,
                SupportsUpdates = true,
                SupportsDeletes = true
            });
        var source = new HonuaSource(
            new SourceDescriptor
            {
                Id = "parks",
                Protocol = FeatureProtocolIds.Grpc,
                Locator = new SourceLocator { ServiceId = "parks", LayerId = 0 }
            },
            new FakeQueryClient("grpc", _ => []),
            editClient);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => source.ApplyEditsAsync(new FeatureEditRequest
            {
                DeleteObjectIds = [1]
            }));

        Assert.DoesNotContain(FeatureCapabilities.ApplyEdits, source.Capabilities);
        Assert.Contains("applyEdits", ex.Message);
        Assert.Equal(0, editClient.ApplyCalls);
    }

    private sealed class FakeQueryClient(
        string providerName,
        Func<FeatureQueryRequest, IReadOnlyList<FeatureQueryResult>> queryPages) : IHonuaFeatureQueryClient
    {
        public string ProviderName { get; } = providerName;

        public Task<FeatureQueryResult> QueryAsync(FeatureQueryRequest request, CancellationToken ct = default)
            => Task.FromResult(queryPages(request).FirstOrDefault() ?? new FeatureQueryResult { ProviderName = ProviderName });

        public async IAsyncEnumerable<FeatureQueryResult> QueryPagesAsync(
            FeatureQueryRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var page in queryPages(request))
            {
                ct.ThrowIfCancellationRequested();
                yield return page;
                await Task.Yield();
            }
        }
    }

    private sealed class FakeEditClient(
        string providerName,
        FeatureEditCapabilities capabilities) : IHonuaFeatureEditClient
    {
        public string ProviderName { get; } = providerName;

        public FeatureEditCapabilities EditCapabilities { get; } = capabilities;

        public int ApplyCalls { get; private set; }

        public Task<FeatureEditResponse> ApplyEditsAsync(FeatureEditRequest request, CancellationToken ct = default)
        {
            ApplyCalls++;
            return Task.FromResult(new FeatureEditResponse { ProviderName = ProviderName });
        }
    }
}
