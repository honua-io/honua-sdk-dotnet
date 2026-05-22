// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Data;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Tests;

public sealed class RasterElevationEnrichmentContractTests
{
    [Fact]
    public void RasterMetadata_ModelsBandsExtentsAndCoverageStatistics()
    {
        var source = RasterSource();
        var metadata = new RasterDatasetMetadata
        {
            Source = source,
            DatasetId = "landcover-2025",
            Name = "Landcover 2025",
            SpatialReference = "EPSG:3857",
            Extent = new FeatureBoundingBox
            {
                MinX = -158.25,
                MinY = 21.20,
                MaxX = -157.60,
                MaxY = 21.75,
                Crs = "EPSG:4326",
            },
            CellSizeX = 10,
            CellSizeY = 10,
            Width = 4096,
            Height = 2048,
            PixelType = RasterPixelType.UnsignedShort,
            Bands =
            [
                new RasterBandMetadata
                {
                    BandIndex = 1,
                    Name = "Class",
                    PixelType = RasterPixelType.UnsignedShort,
                    Unit = "class",
                    NoDataValue = 0,
                    Minimum = 1,
                    Maximum = 12,
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        ["colorTable"] = JsonValue("[1,2,3]"),
                    },
                },
            ],
            Capabilities = ["metadata", "statistics"],
            RawMetadata = JsonValue("""{"dataset":"landcover-2025"}"""),
        };

        var request = new RasterCoverageStatisticsRequest
        {
            Source = source,
            BandIndexes = [1],
            Extent = metadata.Extent,
            StatisticTypes =
            [
                SpatialDataStatisticType.Count,
                SpatialDataStatisticType.Mean,
                SpatialDataStatisticType.Percentile,
            ],
            Percentiles = [50, 95],
            ResamplingMethod = RasterResamplingMethod.Nearest,
        };
        var response = new RasterCoverageStatisticsResponse
        {
            Source = source,
            Extent = request.Extent,
            Bands =
            [
                new RasterBandStatistics
                {
                    BandIndex = 1,
                    Count = 2048,
                    NoDataCount = 3,
                    Minimum = 1,
                    Maximum = 12,
                    Mean = 4.25,
                    Percentiles = new Dictionary<string, double>
                    {
                        ["p50"] = 4,
                        ["p95"] = 11,
                    },
                },
            ],
        };

        Assert.Equal("landcover-2025", metadata.DatasetId);
        Assert.Equal(RasterPixelType.UnsignedShort, metadata.Bands[0].PixelType);
        Assert.Equal(RasterResamplingMethod.Nearest, request.ResamplingMethod);
        Assert.True(response.Succeeded);
        Assert.Equal(2048, response.Bands[0].Count);
        Assert.Equal(11, response.Bands[0].Percentiles?["p95"]);
    }

    [Fact]
    public void ElevationSampling_ModelsPointAndProfileSamples()
    {
        var point = SpatialDataPoint.FromLatitudeLongitude(21.3045, -157.8557, "honolulu");
        var request = new ElevationSamplingRequest
        {
            Source = new SpatialDataSource
            {
                ServiceId = "terrain",
                DatasetId = "dem-1m",
            },
            Points = [point],
            PathGeometry = JsonValue("""{"paths":[[[-157.9,21.3],[-157.8,21.35]]]}"""),
            PathCrs = "EPSG:4326",
            SampleDistance = 25,
            Unit = "meters",
            VerticalDatum = "EGM96",
            IncludeNoData = true,
        };
        var response = new ElevationSamplingResponse
        {
            Source = request.Source,
            Samples =
            [
                new ElevationSample
                {
                    Location = point,
                    Elevation = 5.8,
                    Unit = request.Unit,
                    VerticalDatum = request.VerticalDatum,
                    DistanceAlong = 0,
                    Resolution = 1,
                },
            ],
        };

        Assert.Equal("EPSG:4326", point.Crs);
        Assert.Equal(-157.8557, point.X);
        Assert.Equal(21.3045, point.Y);
        Assert.Equal(25, request.SampleDistance);
        Assert.True(response.Succeeded);
        Assert.Equal(5.8, response.Samples[0].Elevation);
    }

    [Fact]
    public void Enrichment_ModelsMetadataAttributesAndBlockingMessages()
    {
        var population = new EnrichmentAttributeDefinition
        {
            AttributeId = "population_total",
            Name = "Total population",
            Category = "demographics",
            ValueType = SpatialDataValueType.IntegralNumber,
            Unit = "people",
            AggregationMethod = "apportioned",
        };
        var metadata = new EnrichmentMetadata
        {
            Source = new SpatialDataSource
            {
                ServiceId = "enrichment",
                DatasetId = "acs",
            },
            Attributes = [population],
            Categories = ["demographics"],
        };
        var request = new EnrichmentRequest
        {
            Source = metadata.Source,
            AttributeIds = [population.AttributeId],
            Geometry = JsonValue("""{"rings":[[[-158,21],[-157,21],[-157,22],[-158,21]]]}"""),
            GeometryType = FeatureSpatialGeometryType.Polygon,
            GeometryCrs = "EPSG:4326",
            ReturnGeometry = false,
        };
        var response = new EnrichmentResponse
        {
            Source = metadata.Source,
            Attributes = [population],
            Records =
            [
                new EnrichmentRecord
                {
                    RecordId = "aoI-1",
                    Attributes = new Dictionary<string, JsonElement>
                    {
                        [population.AttributeId] = JsonValue("973000"),
                    },
                },
            ],
        };
        var failed = response with
        {
            Messages =
            [
                new SpatialDataMessage
                {
                    Severity = SpatialDataMessageSeverity.Error,
                    Code = "missing-variable",
                    Message = "Requested enrichment variable is not available.",
                    SuggestedFix = "Refresh enrichment metadata.",
                },
            ],
        };

        Assert.Single(metadata.Attributes);
        Assert.Equal(FeatureSpatialGeometryType.Polygon, request.GeometryType);
        Assert.True(response.Succeeded);
        Assert.False(failed.Succeeded);
        Assert.Equal(973000, response.Records[0].Attributes[population.AttributeId].GetInt32());
    }

    [Fact]
    public async Task Interfaces_ModelProviderCapabilitiesAndWorkflows()
    {
        var client = new FakeSpatialDataClient();

        var metadata = await client.GetRasterMetadataAsync(new RasterMetadataRequest
        {
            Source = RasterSource(),
            IncludeBands = true,
        });
        var statistics = await client.GetCoverageStatisticsAsync(new RasterCoverageStatisticsRequest
        {
            Source = RasterSource(),
            BandIndexes = [1],
        });
        var elevation = await client.SampleElevationAsync(new ElevationSamplingRequest
        {
            Source = new SpatialDataSource { ServiceId = "terrain" },
            Points = [SpatialDataPoint.FromLongitudeLatitude(-157.8557, 21.3045, "honolulu")],
        });
        var enrichmentMetadata = await client.GetEnrichmentMetadataAsync(new EnrichmentMetadataRequest
        {
            Source = new SpatialDataSource { ServiceId = "enrichment" },
        });
        var enrichment = await client.EnrichAsync(new EnrichmentRequest
        {
            Source = enrichmentMetadata.Source,
            AttributeIds = ["population_total"],
            FeatureSource = new FeatureSource { ServiceId = "parcels", LayerId = 0 },
            ObjectIds = [42],
        });

        Assert.Equal("fake-spatial-data", client.ProviderName);
        Assert.True(client.RasterCapabilities.SupportsCoverageStatistics);
        Assert.True(client.ElevationCapabilities.SupportsProfileSampling);
        Assert.True(client.EnrichmentCapabilities.SupportsFeatureEnrichment);
        Assert.Equal("landcover-2025", metadata.DatasetId);
        Assert.True(statistics.Succeeded);
        Assert.Single(elevation.Samples);
        Assert.Single(enrichmentMetadata.Attributes);
        Assert.True(enrichment.Succeeded);
    }

    private static SpatialDataSource RasterSource()
        => new()
        {
            ServiceId = "imagery",
            DatasetId = "landcover-2025",
            RasterId = "mosaic-1",
        };

    private static JsonElement JsonValue(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class FakeSpatialDataClient :
        IHonuaRasterDataClient,
        IHonuaElevationDataClient,
        IHonuaEnrichmentDataClient
    {
        public string ProviderName => "fake-spatial-data";

        public RasterDataCapabilities RasterCapabilities { get; } = new()
        {
            SupportsMetadata = true,
            SupportsBandMetadata = true,
            SupportsCoverageStatistics = true,
            SupportsNoDataMasks = true,
            NativeSurface = "honua-server-raster",
        };

        public ElevationDataCapabilities ElevationCapabilities { get; } = new()
        {
            SupportsPointSampling = true,
            SupportsBatchSampling = true,
            SupportsProfileSampling = true,
            SupportsOutputUnits = true,
            SupportsVerticalDatum = true,
            NativeSurface = "honua-server-elevation",
        };

        public EnrichmentDataCapabilities EnrichmentCapabilities { get; } = new()
        {
            SupportsMetadata = true,
            SupportsFeatureEnrichment = true,
            SupportsGeometryEnrichment = true,
            SupportsBatchEnrichment = true,
            SupportsDemographicVariables = true,
            NativeSurface = "honua-server-enrichment",
        };

        public Task<RasterDatasetMetadata> GetRasterMetadataAsync(RasterMetadataRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new RasterDatasetMetadata
            {
                Source = request.Source,
                DatasetId = request.Source.DatasetId ?? "landcover-2025",
                Bands =
                [
                    new RasterBandMetadata
                    {
                        BandIndex = 1,
                        Name = "Class",
                        PixelType = RasterPixelType.UnsignedShort,
                    },
                ],
                Capabilities = ["metadata", "statistics"],
            });
        }

        public Task<RasterCoverageStatisticsResponse> GetCoverageStatisticsAsync(
            RasterCoverageStatisticsRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new RasterCoverageStatisticsResponse
            {
                Source = request.Source,
                Bands =
                [
                    new RasterBandStatistics
                    {
                        BandIndex = request.BandIndexes?[0] ?? 1,
                        Count = 12,
                        Minimum = 1,
                        Maximum = 9,
                        Mean = 4.5,
                    },
                ],
            });
        }

        public Task<ElevationSamplingResponse> SampleElevationAsync(ElevationSamplingRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ElevationSamplingResponse
            {
                Source = request.Source,
                Samples = request.Points.Select(static point => new ElevationSample
                {
                    Location = point,
                    Elevation = 5.8,
                    Unit = "meters",
                    VerticalDatum = "EGM96",
                }).ToList(),
            });
        }

        public Task<EnrichmentMetadata> GetEnrichmentMetadataAsync(EnrichmentMetadataRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new EnrichmentMetadata
            {
                Source = request.Source,
                Categories = ["demographics"],
                Attributes =
                [
                    new EnrichmentAttributeDefinition
                    {
                        AttributeId = "population_total",
                        Name = "Total population",
                        Category = "demographics",
                        ValueType = SpatialDataValueType.IntegralNumber,
                    },
                ],
            });
        }

        public Task<EnrichmentResponse> EnrichAsync(EnrichmentRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new EnrichmentResponse
            {
                Source = request.Source,
                Records =
                [
                    new EnrichmentRecord
                    {
                        RecordId = request.ObjectIds?[0].ToString(System.Globalization.CultureInfo.InvariantCulture),
                        FeatureSource = request.FeatureSource,
                        Attributes = new Dictionary<string, JsonElement>
                        {
                            ["population_total"] = JsonValue("973000"),
                        },
                    },
                ],
            });
        }
    }
}
