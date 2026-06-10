// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.GeometryServer;
using Honua.Sdk.GeoServices.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.GeoServices.Tests.GeometryServer;

public sealed class HonuaGeometryServerClientTests
{
    [Fact]
    public async Task ProjectAsync_SendsGeometriesAndSpatialReferences()
    {
        Dictionary<string, string>? form = null;
        Uri? uri = null;
        HttpMethod? method = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            uri = request.RequestUri;
            method = request.Method;
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("""
            {
              "geometries": [
                { "x": -17563470.0, "y": 2426417.0 }
              ]
            }
            """);
        });

        var result = await client.ProjectAsync(new ProjectGeometriesRequest
        {
            ServiceId = "Geometry",
            GeometryType = "esriGeometryPoint",
            InSpatialReference = 4326,
            OutSpatialReference = 3857,
            Geometries = [Point(-157.8, 21.3)],
        });

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("http://localhost:5000/rest/services/Geometry/GeometryServer/project", uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("json", form["f"]);
        Assert.Equal("4326", form["inSR"]);
        Assert.Equal("3857", form["outSR"]);
        using var geometries = JsonDocument.Parse(form["geometries"]);
        Assert.Equal("esriGeometryPoint", geometries.RootElement.GetProperty("geometryType").GetString());
        Assert.Equal(-157.8, geometries.RootElement.GetProperty("geometries")[0].GetProperty("x").GetDouble(), precision: 4);

        var geometry = Assert.Single(result.Geometries);
        Assert.Equal(-17563470.0, geometry.GetProperty("x").GetDouble(), precision: 1);
    }

    [Fact]
    public async Task BufferAsync_SendsDistancesUnitAndGeodesicFlag()
    {
        Dictionary<string, string>? form = null;
        Uri? uri = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            uri = request.RequestUri;
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("""{ "geometries": [ { "rings": [] } ] }""");
        });

        var result = await client.BufferAsync(new BufferGeometriesRequest
        {
            ServiceId = "Geometry",
            GeometryType = "esriGeometryPoint",
            InSpatialReference = 4326,
            OutSpatialReference = 4326,
            BufferSpatialReference = 3857,
            Distances = [100, 250],
            Unit = "esriSRUnit_Meter",
            UnionResults = true,
            Geodesic = true,
            Geometries = [Point(-157.8, 21.3)],
        });

        Assert.Equal("http://localhost:5000/rest/services/Geometry/GeometryServer/buffer", uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("100,250", form["distances"]);
        Assert.Equal("esriSRUnit_Meter", form["unit"]);
        Assert.Equal("3857", form["bufferSR"]);
        Assert.Equal("true", form["unionResults"]);
        Assert.Equal("true", form["geodesic"]);
        Assert.Single(result.Geometries);
    }

    [Fact]
    public async Task LengthsAsync_SendsPolylinesAndParsesLengths()
    {
        Dictionary<string, string>? form = null;
        Uri? uri = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            uri = request.RequestUri;
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("""{ "lengths": [ 1234.5, 678.9 ] }""");
        });

        var result = await client.LengthsAsync(new LengthsRequest
        {
            ServiceId = "Geometry",
            SpatialReference = 4326,
            LengthUnit = "esriSRUnit_Meter",
            CalculationType = "geodesic",
            Polylines = [Polyline()],
        });

        Assert.Equal("http://localhost:5000/rest/services/Geometry/GeometryServer/lengths", uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("4326", form["sr"]);
        Assert.Equal("geodesic", form["calculationType"]);
        Assert.Equal("esriSRUnit_Meter", form["lengthUnit"]);
        using var polylines = JsonDocument.Parse(form["polylines"]);
        Assert.Equal(JsonValueKind.Array, polylines.RootElement.ValueKind);
        Assert.Equal([1234.5, 678.9], result.Lengths);
    }

    [Fact]
    public async Task AreasAndLengthsAsync_ParsesAreasAndLengths()
    {
        Dictionary<string, string>? form = null;
        Uri? uri = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            uri = request.RequestUri;
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("""{ "areas": [ 5000.0 ], "lengths": [ 300.0 ] }""");
        });

        var result = await client.AreasAndLengthsAsync(new AreasAndLengthsRequest
        {
            ServiceId = "Geometry",
            SpatialReference = 4326,
            AreaUnit = "esriSquareMeters",
            LengthUnit = "esriSRUnit_Meter",
            CalculationType = "geodesic",
            Polygons = [Polygon()],
        });

        Assert.Equal("http://localhost:5000/rest/services/Geometry/GeometryServer/areasAndLengths", uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("esriSquareMeters", form["areaUnit"]);
        Assert.Equal([5000.0], result.Areas);
        Assert.Equal([300.0], result.Lengths);
    }

    [Fact]
    public async Task ProjectAsync_GeoServicesErrorSurfacesAsHonuaFeatureServerException()
    {
        var client = CreateClient((_, _) => Task.FromResult(JsonResponse("""
        {
          "error": {
            "code": 500,
            "message": "Projection failed.",
            "details": ["Unsupported transformation."]
          }
        }
        """)));

        var exception = await Assert.ThrowsAsync<HonuaFeatureServerException>(() =>
            client.ProjectAsync(new ProjectGeometriesRequest
            {
                ServiceId = "Geometry",
                GeometryType = "esriGeometryPoint",
                InSpatialReference = 4326,
                OutSpatialReference = 3857,
                Geometries = [Point(-157.8, 21.3)],
            }));

        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
        Assert.Equal(500, exception.GeoServicesErrorCode);
        Assert.Equal("Projection failed.", exception.Message);
        Assert.Equal(["Unsupported transformation."], exception.Details!);
    }

    [Fact]
    public void AddHonuaGeometryServer_RegistersGeometryServerClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaGeometryServer(options =>
        {
            options.BaseAddress = new Uri("http://localhost:5000");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<HonuaGeometryServerClient>());
    }

    [Fact]
    public async Task ProjectAsync_UsesConfiguredDefaultServiceIdWhenNotProvided()
    {
        Uri? uri = null;
        var options = new HonuaGeoServicesClientOptions
        {
            BaseAddress = new Uri("http://localhost:5000"),
            GeometryServiceId = "Utilities/Geometry",
        };
        var client = CreateClient(
            (request, _) =>
            {
                uri = request.RequestUri;
                return Task.FromResult(JsonResponse("""{ "geometries": [] }"""));
            },
            options);

        await client.ProjectAsync(new ProjectGeometriesRequest
        {
            GeometryType = "esriGeometryPoint",
            InSpatialReference = 4326,
            OutSpatialReference = 3857,
            Geometries = [Point(-157.8, 21.3)],
        });

        Assert.Equal(
            "http://localhost:5000/rest/services/Utilities%2FGeometry/GeometryServer/project",
            uri?.ToString());
    }

    private static JsonElement Point(double x, double y)
    {
        using var doc = JsonDocument.Parse(
            $"{{\"x\":{x.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"y\":{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}");
        return doc.RootElement.Clone();
    }

    private static JsonElement Polyline()
    {
        using var doc = JsonDocument.Parse("""{ "paths": [ [ [-157.8, 21.3], [-157.7, 21.4] ] ] }""");
        return doc.RootElement.Clone();
    }

    private static JsonElement Polygon()
    {
        using var doc = JsonDocument.Parse("""{ "rings": [ [ [-157.8, 21.3], [-157.7, 21.3], [-157.7, 21.4], [-157.8, 21.3] ] ] }""");
        return doc.RootElement.Clone();
    }

    private static HonuaGeometryServerClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        HonuaGeoServicesClientOptions? options = null)
    {
        var mockHandler = new MockHttpHandler(request => handler(request, CancellationToken.None));
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000"),
        };

        options ??= new HonuaGeoServicesClientOptions
        {
            BaseAddress = new Uri("http://localhost:5000"),
            GeometryServiceId = "Geometry",
        };

        return new HonuaGeometryServerClient(httpClient, options);
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
