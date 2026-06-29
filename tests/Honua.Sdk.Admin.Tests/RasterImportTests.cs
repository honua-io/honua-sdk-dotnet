// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using Honua.Sdk.Admin.Exceptions;
using Honua.Sdk.Admin.Extensions;
using Honua.Sdk.Admin.Models;
using Honua.Sdk.Admin.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.Admin.Tests;

public sealed class RasterImportTests
{
    [Fact]
    public async Task ImportRasterAsync_PostsMultipartToImportEndpoint()
    {
        string? capturedPath = null;
        HttpMethod? capturedMethod = null;
        string? capturedBody = null;

        var client = TestHelpers.CreateClient(async req =>
        {
            capturedPath = req.RequestUri!.AbsolutePath;
            capturedMethod = req.Method;
            capturedBody = await req.Content!.ReadAsStringAsync();

            return TestHelpers.CreateRawJsonResponse(new
            {
                success = true,
                rasterId = 42L,
                layerId = 7,
                name = "elevation",
                format = "GeoTiff",
                srid = 4326,
                width = 256,
                height = 256,
                bandCount = 1,
                tilesGenerated = 9,
                duration = "00:00:01.5",
            });
        });

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("fake-geotiff-bytes"));
        var result = await client.ImportRasterAsync(
            new RasterImportRequest
            {
                Content = content,
                FileName = "elevation.tif",
                LayerId = 7,
                Name = "elevation",
                Srid = 4326,
                TileZoomLevels = [0, 1, 2],
            });

        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Equal("/api/v1/admin/import/raster/", capturedPath);
        Assert.NotNull(capturedBody);
        Assert.Contains("name=file; filename=elevation.tif", capturedBody);
        Assert.Contains("name=layerId", capturedBody);
        Assert.Contains("name=name", capturedBody);
        Assert.Contains("name=srid", capturedBody);
        Assert.Contains("name=tileZoomLevels", capturedBody);
        Assert.Contains("0,1,2", capturedBody);

        Assert.True(result.Success);
        Assert.Equal(42L, result.RasterId);
        Assert.Equal(7, result.LayerId);
        Assert.Equal("GeoTiff", result.Format);
        Assert.Equal(TimeSpan.FromSeconds(1.5), result.Duration);
    }

    [Fact]
    public async Task ImportRasterAsync_IncludesSidecarsAsFileParts()
    {
        string? capturedBody = null;
        var client = TestHelpers.CreateClient(async req =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            return TestHelpers.CreateRawJsonResponse(new { success = true, layerId = 3, name = "ortho", format = "PngWorldFile" });
        });

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("fake-png-bytes"));
        await client.ImportRasterAsync(
            new RasterImportRequest
            {
                Content = content,
                FileName = "ortho.png",
                LayerId = 3,
                Name = "ortho",
                WorldFileContent = "1.0\n0.0\n0.0\n-1.0\n0.0\n0.0\n",
                ProjectionContent = "GEOGCS[\"WGS 84\"]",
            });

        Assert.NotNull(capturedBody);
        Assert.Contains("filename=ortho.wld", capturedBody);
        Assert.Contains("filename=ortho.prj", capturedBody);
    }

    [Fact]
    public async Task ImportRasterAsync_ErrorResponse_ThrowsApiException()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateErrorResponse(HttpStatusCode.BadRequest, "Unsupported raster format: .bmp")));

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("nope"));
        var exception = await Assert.ThrowsAsync<HonuaAdminApiException>(
            () => client.ImportRasterAsync(
                new RasterImportRequest
                {
                    Content = content,
                    FileName = "image.bmp",
                    LayerId = 1,
                    Name = "image",
                }));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("Unsupported raster format", exception.Message);
    }

    [Fact]
    public async Task ImportRasterAsync_NullRequest_Throws()
    {
        var client = TestHelpers.CreateClient(_ =>
            Task.FromResult(TestHelpers.CreateRawJsonResponse(new { success = true })));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => client.ImportRasterAsync(null!));
    }

    [Fact]
    public async Task GetSupportedRasterFormatsAsync_ParsesRawResponse()
    {
        var client = TestHelpers.CreateClient(req =>
        {
            Assert.Equal("/api/v1/admin/import/raster/formats", req.RequestUri!.AbsolutePath);
            Assert.Equal(HttpMethod.Get, req.Method);
            return Task.FromResult(TestHelpers.CreateRawJsonResponse(new
            {
                supportedExtensions = new[] { ".tif", ".tiff", ".png" },
                formatDescriptions = new Dictionary<string, string>
                {
                    [".tif"] = "GeoTIFF",
                    [".png"] = "PNG with world file",
                },
            }));
        });

        var formats = await client.GetSupportedRasterFormatsAsync();

        Assert.Equal(3, formats.SupportedExtensions.Count);
        Assert.Contains(".tif", formats.SupportedExtensions);
        Assert.Equal("GeoTIFF", formats.FormatDescriptions[".tif"]);
    }

    [Fact]
    public void AddHonuaAdmin_ResolvesRasterImportClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaAdmin(options =>
        {
            options.BaseAddress = new Uri("https://localhost:5001");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();

        var rasterClient = provider.GetRequiredService<IHonuaAdminRasterImportClient>();
        Assert.NotNull(rasterClient);
        Assert.IsType<HonuaAdminClient>(rasterClient);
    }
}
