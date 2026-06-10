// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.ImageServer;
using Honua.Sdk.GeoServices.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.GeoServices.Tests.ImageServer;

public sealed class HonuaImageServerClientTests
{
    [Fact]
    public async Task GetServiceMetadataAsync_ParsesImageServiceDescription()
    {
        Uri? uri = null;
        var client = CreateClient((request, _) =>
        {
            uri = request.RequestUri;
            return Task.FromResult(JsonResponse("""
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
              "supportedQueryFormats": "JSON, geoJSON",
              "supportedImageFormatTypes": "PNG,PNG8,PNG24,JPG,TIFF",
              "allowedMosaicMethods": "Center,NorthWest",
              "defaultMosaicMethod": "Center",
              "hasRasterAttributeTable": false,
              "hasHistograms": true
            }
            """));
        });

        var metadata = await client.GetServiceMetadataAsync("Elevation");

        Assert.Equal("http://localhost:5000/rest/services/Elevation/ImageServer?f=json", uri?.ToString());
        Assert.Equal("Elevation", metadata.ServiceId);
        Assert.Equal("F32", metadata.PixelType);
        Assert.Equal(1, metadata.BandCount);
        Assert.Equal([0.0], metadata.MinValues!);
        Assert.Equal([4207.5], metadata.MaxValues!);
        Assert.NotNull(metadata.Extent);
        Assert.Equal(-160.3, metadata.Extent!.XMin, precision: 4);
        Assert.Equal(4326, metadata.Extent.SpatialReferenceWkid);
        Assert.Equal(["JSON", "geoJSON"], metadata.SupportedQueryFormats!);
        Assert.Equal(["PNG", "PNG8", "PNG24", "JPG", "TIFF"], metadata.SupportedImageFormatTypes!);
        Assert.Equal("Center", metadata.DefaultMosaicMethod);
        Assert.False(metadata.HasRasterAttributeTable);
        Assert.True(metadata.HasHistograms);
    }

    [Fact]
    public async Task ExportImageAsync_SendsExportFormAndReturnsRasterStream()
    {
        Dictionary<string, string>? form = null;
        Uri? uri = null;
        HttpMethod? method = null;
        var raster = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
        var client = CreateClient(async (request, cancellationToken) =>
        {
            uri = request.RequestUri;
            method = request.Method;
            form = await ReadFormAsync(request, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(raster)
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png") },
                },
            };
        });

        await using var result = await client.ExportImageAsync(new ExportImageRequest
        {
            ServiceId = "Elevation",
            BoundingBox = new ImageServerExtent { XMin = -158, YMin = 21, XMax = -157, YMax = 22 },
            BoundingBoxSpatialReference = 4326,
            ImageSpatialReference = 3857,
            Width = 512,
            Height = 256,
            Format = "png24",
            Interpolation = "RSP_BilinearInterpolation",
        });

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("http://localhost:5000/rest/services/Elevation/ImageServer/exportImage", uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("image", form["f"]);
        Assert.Equal("-158,21,-157,22", form["bbox"]);
        Assert.Equal("4326", form["bboxSR"]);
        Assert.Equal("3857", form["imageSR"]);
        Assert.Equal("512,256", form["size"]);
        Assert.Equal("png24", form["format"]);
        Assert.Equal("RSP_BilinearInterpolation", form["interpolation"]);

        Assert.Equal("image/png", result.ContentType);
        using var memory = new MemoryStream();
        await result.Content.CopyToAsync(memory);
        Assert.Equal(raster, memory.ToArray());
    }

    [Fact]
    public async Task ExportImageMetadataAsync_ParsesHrefAndExtent()
    {
        Dictionary<string, string>? form = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("""
            {
              "href": "https://honua.example/exports/elevation.png",
              "width": 512,
              "height": 256,
              "scale": 12345.6,
              "extent": {
                "xmin": -158, "ymin": 21, "xmax": -157, "ymax": 22,
                "spatialReference": { "wkid": 3857 }
              }
            }
            """);
        });

        var metadata = await client.ExportImageMetadataAsync(new ExportImageRequest
        {
            ServiceId = "Elevation",
            BoundingBox = new ImageServerExtent { XMin = -158, YMin = 21, XMax = -157, YMax = 22 },
            Width = 512,
            Height = 256,
        });

        Assert.NotNull(form);
        Assert.Equal("json", form["f"]);
        Assert.Equal("https://honua.example/exports/elevation.png", metadata.Href);
        Assert.Equal(512, metadata.Width);
        Assert.Equal(256, metadata.Height);
        Assert.Equal(12345.6, metadata.Scale);
        Assert.Equal(3857, metadata.Extent!.SpatialReferenceWkid);
    }

    [Fact]
    public async Task IdentifyAsync_SendsPointGeometryAndParsesValue()
    {
        Dictionary<string, string>? form = null;
        Uri? uri = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            uri = request.RequestUri;
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("""
            {
              "objectId": 42,
              "name": "DEM",
              "value": "1234.5",
              "location": { "x": -157.8, "y": 21.3 },
              "catalogItems": { "features": [] }
            }
            """);
        });

        var result = await client.IdentifyAsync(new IdentifyImageRequest
        {
            ServiceId = "Elevation",
            X = -157.8,
            Y = 21.3,
            SpatialReferenceWkid = 4326,
        });

        Assert.Equal("http://localhost:5000/rest/services/Elevation/ImageServer/identify", uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("json", form["f"]);
        Assert.Equal("esriGeometryPoint", form["geometryType"]);
        using var geometry = JsonDocument.Parse(form["geometry"]);
        Assert.Equal(-157.8, geometry.RootElement.GetProperty("x").GetDouble(), precision: 4);
        Assert.Equal(4326, geometry.RootElement.GetProperty("spatialReference").GetProperty("wkid").GetInt32());

        Assert.Equal(42, result.ObjectId);
        Assert.Equal("DEM", result.Name);
        Assert.Equal("1234.5", result.Value);
    }

    [Fact]
    public async Task ExportImageAsync_GeoServicesErrorSurfacesAsHonuaFeatureServerException()
    {
        var client = CreateClient((_, _) => Task.FromResult(JsonResponse("""
        {
          "error": {
            "code": 400,
            "message": "Unable to complete operation.",
            "details": ["Invalid bounding box."]
          }
        }
        """)));

        var exception = await Assert.ThrowsAsync<HonuaFeatureServerException>(() =>
            client.ExportImageAsync(new ExportImageRequest
            {
                ServiceId = "Elevation",
                BoundingBox = new ImageServerExtent { XMin = 0, YMin = 0, XMax = 1, YMax = 1 },
                Width = 256,
                Height = 256,
            }));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(400, exception.GeoServicesErrorCode);
        Assert.Equal("Unable to complete operation.", exception.Message);
        Assert.Equal(["Invalid bounding box."], exception.Details!);
    }

    [Fact]
    public void AddHonuaImageServer_RegistersImageServerClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaImageServer(options =>
        {
            options.BaseAddress = new Uri("http://localhost:5000");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<HonuaImageServerClient>());
    }

    private static HonuaImageServerClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        var mockHandler = new MockHttpHandler(request => handler(request, CancellationToken.None));
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000"),
        };

        return new HonuaImageServerClient(httpClient);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static async Task<Dictionary<string, string>> ReadFormAsync(HttpRequestMessage request, CancellationToken cancellationToken)
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
