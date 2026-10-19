using System.Text.Json;
using Honua.Sdk.Abstractions.Routing;

namespace Honua.Sdk.ProtocolIntegration.Tests;

internal static class RoutingQualificationAssertions
{
    public static void RequireServiceArea(ServiceAreaResult result)
    {
        var polygons = result.RawResponse.GetProperty("saPolygons");
        Assert.Equal("esriGeometryPolygon", polygons.GetProperty("geometryType").GetString());
        var features = polygons.GetProperty("features");
        Assert.NotEmpty(features.EnumerateArray());
        foreach (var rings in features.EnumerateArray()
            .Select(feature => feature.GetProperty("geometry").GetProperty("rings")))
        {
            Assert.NotEmpty(rings.EnumerateArray());
            foreach (var ring in rings.EnumerateArray())
            {
                Assert.InRange(ring.GetArrayLength(), 4, int.MaxValue);
                RequireFinitePoints(ring);
                double twiceArea = 0;
                for (var index = 1; index < ring.GetArrayLength(); index++)
                {
                    twiceArea += ring[index - 1][0].GetDouble() * ring[index][1].GetDouble()
                        - ring[index][0].GetDouble() * ring[index - 1][1].GetDouble();
                }

                Assert.True(double.IsFinite(twiceArea) && Math.Abs(twiceArea) > 0);
                Assert.Equal(ring[0][0].GetDouble(), ring[ring.GetArrayLength() - 1][0].GetDouble());
                Assert.Equal(ring[0][1].GetDouble(), ring[ring.GetArrayLength() - 1][1].GetDouble());
            }
        }
    }

    public static void RequireClosestFacility(ClosestFacilityResult result)
    {
        Assert.Single(result.Routes);
        Assert.True(result.Routes[0].TotalDistance > 0);
        Assert.True(result.Routes[0].TotalTime > TimeSpan.Zero);
        var routes = result.RawResponse.GetProperty("routes");
        Assert.Equal("esriGeometryPolyline", routes.GetProperty("geometryType").GetString());
        var feature = Assert.Single(routes.GetProperty("features").EnumerateArray());
        var attributes = feature.GetProperty("attributes");
        Assert.Equal(1, attributes.GetProperty("IncidentID").GetInt32());
        Assert.Equal(1, attributes.GetProperty("FacilityRank").GetInt32());
        Assert.InRange(attributes.GetProperty("FacilityID").GetInt32(), 1, 2);
        var paths = feature.GetProperty("geometry").GetProperty("paths");
        Assert.NotEmpty(paths.EnumerateArray());
        foreach (var path in paths.EnumerateArray())
        {
            Assert.InRange(path.GetArrayLength(), 2, int.MaxValue);
            RequireFinitePoints(path);
            Assert.Contains(path.EnumerateArray(), point =>
                Math.Abs(point[0].GetDouble() - path[0][0].GetDouble()) > 1e-12 ||
                Math.Abs(point[1].GetDouble() - path[0][1].GetDouble()) > 1e-12);
        }
    }

    private static void RequireFinitePoints(JsonElement points)
    {
        foreach (var point in points.EnumerateArray())
        {
            Assert.InRange(point.GetArrayLength(), 2, int.MaxValue);
            Assert.True(double.IsFinite(point[0].GetDouble()));
            Assert.True(double.IsFinite(point[1].GetDouble()));
        }
    }
}
