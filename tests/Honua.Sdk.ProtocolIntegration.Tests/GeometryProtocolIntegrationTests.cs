using System.Text.Json;
using Honua.Sdk.GeoServices.GeometryServer;

namespace Honua.Sdk.ProtocolIntegration.Tests;

[Collection(ProtocolIntegrationCollection.Name)]
[Trait("Category", "ProtocolIntegration")]
public sealed class GeometryProtocolIntegrationTests(ProtocolIntegrationFixture fixture)
{
    private readonly ProtocolIntegrationFixture _fixture = fixture;

    [ProtocolIntegrationFact]
    public async Task Project_TransformsPointToWebMercator()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var result = await _fixture.GeometryServerClient.ProjectAsync(new ProjectGeometriesRequest
        {
            GeometryType = "esriGeometryPoint",
            Geometries = [JsonSerializer.SerializeToElement(new { x = 1, y = 1 })],
            InSpatialReference = 4326,
            OutSpatialReference = 3857,
        }, timeout.Token).ConfigureAwait(false);

        var point = Assert.Single(result.Geometries);
        Assert.Equal(111319.490793, point.GetProperty("x").GetDouble(), precision: 5);
        Assert.Equal(111325.142866, point.GetProperty("y").GetDouble(), precision: 5);
    }

    [ProtocolIntegrationFact]
    public async Task Buffer_ReturnsPolygonAroundPoint()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var result = await _fixture.GeometryServerClient.BufferAsync(new BufferGeometriesRequest
        {
            GeometryType = "esriGeometryPoint",
            Geometries = [JsonSerializer.SerializeToElement(new { x = 0, y = 0 })],
            InSpatialReference = 3857,
            OutSpatialReference = 3857,
            BufferSpatialReference = 3857,
            Distances = [10],
            Unit = "esriSRUnit_Meter",
        }, timeout.Token).ConfigureAwait(false);

        var polygon = Assert.Single(result.Geometries);
        var ring = Assert.Single(polygon.GetProperty("rings").EnumerateArray());
        Assert.True(ring.GetArrayLength() >= 4);
        var points = ring.EnumerateArray().ToArray();
        Assert.Equal(points[0].GetRawText(), points[^1].GetRawText());
        Assert.Contains(points, point => point[0].GetDouble() > 9);
        Assert.Contains(points, point => point[0].GetDouble() < -9);
        Assert.Contains(points, point => point[1].GetDouble() > 9);
        Assert.Contains(points, point => point[1].GetDouble() < -9);
    }

    [ProtocolIntegrationFact]
    public async Task Lengths_ReturnsPlanarDistance()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var result = await _fixture.GeometryServerClient.LengthsAsync(new LengthsRequest
        {
            Polylines = [JsonSerializer.SerializeToElement(new { paths = new[] { new[] { new[] { 0, 0 }, new[] { 3, 4 } } } })],
            SpatialReference = 3857,
            CalculationType = "planar",
            LengthUnit = "esriSRUnit_Meter",
        }, timeout.Token).ConfigureAwait(false);

        Assert.Equal(5, Assert.Single(result.Lengths), precision: 6);
    }

    [ProtocolIntegrationFact]
    public async Task AreasAndLengths_ReturnsPlanarSquareMeasurements()
    {
        using var timeout = _fixture.CreateTimeoutScope();
        var result = await _fixture.GeometryServerClient.AreasAndLengthsAsync(new AreasAndLengthsRequest
        {
            Polygons = [JsonSerializer.SerializeToElement(new
            {
                rings = new[] { new[] { new[] { 0, 0 }, new[] { 0, 10 }, new[] { 10, 10 }, new[] { 10, 0 }, new[] { 0, 0 } } },
            })],
            SpatialReference = 3857,
            CalculationType = "planar",
            LengthUnit = "esriSRUnit_Meter",
            AreaUnit = "esriSquareMeters",
        }, timeout.Token).ConfigureAwait(false);

        Assert.Equal(100, Assert.Single(result.Areas), precision: 6);
        Assert.Equal(40, Assert.Single(result.Lengths), precision: 6);
    }
}
