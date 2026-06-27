// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Abstractions.Data;
using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.ImageServer;
using Honua.Sdk.GeoServices.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.GeoServices.Tests.ImageServer;

public sealed class HonuaImageServerRasterDataClientTests
{
    [Fact]
    public void AddHonuaImageServer_RegistersRasterDataClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaImageServer(options =>
        {
            options.BaseAddress = new Uri("http://localhost:5000");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();

        var raster = provider.GetService<IHonuaRasterDataClient>();
        Assert.NotNull(raster);
        Assert.Equal("honua.geoservices.imageserver", raster!.ProviderName);
        Assert.True(raster.RasterCapabilities.SupportsWindowReads);
        Assert.True(raster.RasterCapabilities.SupportsCoverageStatistics);
        Assert.Equal("GeoServices/ImageServer", raster.RasterCapabilities.NativeSurface);
    }

    [Fact]
    public async Task GetRasterMetadataAsync_MapsServiceMetadataToDatasetMetadata()
    {
        var client = CreateClient((_, _) => Task.FromResult(JsonResponse("""
        {
          "serviceDescription": "Elevation",
          "name": "Elevation",
          "pixelType": "F32",
          "bandCount": 1,
          "minValues": [0.0],
          "maxValues": [4207.5],
          "extent": {
            "xmin": -160.3, "ymin": 18.9, "xmax": -154.8, "ymax": 22.3,
            "spatialReference": { "wkid": 4326, "latestWkid": 4326 }
          },
          "hasHistograms": true
        }
        """)));

        var metadata = await client.GetRasterMetadataAsync(new RasterMetadataRequest
        {
            Source = new SpatialDataSource { ServiceId = "Elevation" },
        });

        Assert.Equal("Elevation", metadata.DatasetId);
        Assert.Equal("Elevation", metadata.Name);
        Assert.Equal(RasterPixelType.SinglePrecision, metadata.PixelType);
        Assert.NotNull(metadata.Extent);
        Assert.Equal(-160.3, metadata.Extent!.MinX, precision: 4);
        Assert.Equal("4326", metadata.SpatialReference);
        Assert.Single(metadata.Bands);
        Assert.Equal(0, metadata.Bands[0].BandIndex);
        Assert.Equal(0.0, metadata.Bands[0].Minimum);
        Assert.Equal(4207.5, metadata.Bands[0].Maximum);
        Assert.Contains("Histograms", metadata.Capabilities);
    }

    [Fact]
    public async Task GetRasterMetadataAsync_WithoutServiceId_Throws()
    {
        var client = CreateClient((_, _) => Task.FromResult(JsonResponse("{}")));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetRasterMetadataAsync(new RasterMetadataRequest
            {
                Source = new SpatialDataSource(),
            }));
    }

    [Fact]
    public async Task GetCoverageStatisticsAsync_SendsEnvelopeAndParsesBandStatistics()
    {
        Dictionary<string, string>? form = null;
        Uri? uri = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            uri = request.RequestUri;
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("""
            {
              "statistics": [
                { "min": 1.0, "max": 9.0, "mean": 5.0, "standardDeviation": 2.0, "count": 100 }
              ],
              "histograms": []
            }
            """);
        });

        var response = await client.GetCoverageStatisticsAsync(new RasterCoverageStatisticsRequest
        {
            Source = new SpatialDataSource { ServiceId = "Elevation" },
            Extent = new FeatureBoundingBox { MinX = -158, MinY = 21, MaxX = -157, MaxY = 22, Crs = "4326" },
            CellSize = 30,
        });

        Assert.Equal(
            "http://localhost:5000/rest/services/Elevation/ImageServer/computeStatisticsHistograms",
            uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("json", form!["f"]);
        Assert.Equal("esriGeometryEnvelope", form["geometryType"]);
        Assert.Equal("30,30", form["pixelSize"]);
        using var geometry = JsonDocument.Parse(form["geometry"]);
        Assert.Equal(-158, geometry.RootElement.GetProperty("xmin").GetDouble(), precision: 4);
        Assert.Equal(4326, geometry.RootElement.GetProperty("spatialReference").GetProperty("wkid").GetInt32());

        var band = Assert.Single(response.Bands);
        Assert.Equal(0, band.BandIndex);
        Assert.Equal(1.0, band.Minimum);
        Assert.Equal(9.0, band.Maximum);
        Assert.Equal(5.0, band.Mean);
        Assert.Equal(2.0, band.StandardDeviation);
        Assert.Equal(100, band.Count);
        Assert.True(response.Succeeded);
    }

    [Fact]
    public async Task ReadWindowAsync_BuildsExportRequestAndReturnsWindowStream()
    {
        Dictionary<string, string>? form = null;
        Uri? uri = null;
        HttpMethod? method = null;
        var raster = new byte[] { 0x49, 0x49, 0x2A, 0x00 }; // little-endian TIFF magic
        var client = CreateClient(async (request, cancellationToken) =>
        {
            uri = request.RequestUri;
            method = request.Method;
            form = await ReadFormAsync(request, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(raster)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/tiff") },
                },
            };
        });

        await using var window = await client.ReadWindowAsync(new RasterWindowReadRequest
        {
            Source = new SpatialDataSource { ServiceId = "Elevation" },
            Extent = new FeatureBoundingBox { MinX = -158, MinY = 21, MaxX = -157, MaxY = 22, Crs = "4326" },
            OutputSpatialReference = "EPSG:3857",
            Width = 512,
            Height = 256,
            Format = RasterWindowFormat.GeoTiff,
            BandIndexes = [0, 1, 2],
            ResamplingMethod = RasterResamplingMethod.Bilinear,
        });

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("http://localhost:5000/rest/services/Elevation/ImageServer/exportImage", uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("image", form!["f"]);
        Assert.Equal("-158,21,-157,22", form["bbox"]);
        Assert.Equal("4326", form["bboxSR"]);
        Assert.Equal("3857", form["imageSR"]);
        Assert.Equal("512,256", form["size"]);
        Assert.Equal("tiff", form["format"]);
        Assert.Equal("RSP_BilinearInterpolation", form["interpolation"]);
        Assert.Equal("0,1,2", form["bandIds"]);

        Assert.Equal("image/tiff", window.ContentType);
        Assert.Equal(512, window.Width);
        Assert.Equal(256, window.Height);
        Assert.Equal("Elevation", window.Source.ServiceId);
        using var memory = new MemoryStream();
        await window.Content.CopyToAsync(memory);
        Assert.Equal(raster, memory.ToArray());
    }

    [Fact]
    public async Task ReadWindowAsync_NonPositiveSize_Throws()
    {
        var client = CreateClient((_, _) => Task.FromResult(JsonResponse("{}")));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.ReadWindowAsync(new RasterWindowReadRequest
            {
                Source = new SpatialDataSource { ServiceId = "Elevation" },
                Extent = new FeatureBoundingBox { MinX = 0, MinY = 0, MaxX = 1, MaxY = 1 },
                Width = 0,
                Height = 256,
            }));
    }

    private static HonuaImageServerRasterDataClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpHandler(request => handler(request, CancellationToken.None));
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000"),
        };

        return new HonuaImageServerRasterDataClient(new HonuaImageServerClient(httpClient));
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static async Task<Dictionary<string, string>> ReadFormAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        return body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(
                pair => Decode(pair[0]),
                pair => pair.Length == 2 ? Decode(pair[1]) : string.Empty,
                StringComparer.Ordinal);
    }

    private static string Decode(string value) => Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
}
