using System.Text.Json;
using Honua.Sdk.Abstractions.Routing;

namespace Honua.Sdk.ProtocolIntegration.Tests;

public sealed class RoutingQualificationAssertionTests
{
    [Fact]
    public void NonemptyPolygonAndRouteGeometrySatisfyQualificationAssertions()
    {
        // Harness-only shapes verify the assertions; they are not candidate evidence.
        using var polygon = JsonDocument.Parse("""
            {"saPolygons":{"geometryType":"esriGeometryPolygon","features":[
              {"geometry":{"rings":[[[0,0],[0.02,0],[0.02,0.02],[0,0.02],[0,0]]]}}
            ]}}
            """);
        RoutingQualificationAssertions.RequireServiceArea(new ServiceAreaResult(polygon.RootElement));
        using var route = JsonDocument.Parse("""
            {"routes":{"geometryType":"esriGeometryPolyline","features":[
              {"attributes":{"IncidentID":1,"FacilityRank":1,"FacilityID":2},
               "geometry":{"paths":[[[0,0],[0.02,0]]]}}
            ]}}
            """);
        RoutingQualificationAssertions.RequireClosestFacility(new ClosestFacilityResult(
            route.RootElement, [new RouteSummary("route", 1, TimeSpan.FromMinutes(1))], []));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"saPolygons\":{\"geometryType\":\"esriGeometryPolygon\",\"features\":[]}}")]
    [InlineData("{\"saPolygons\":{\"geometryType\":\"esriGeometryPolygon\",\"features\":[{\"geometry\":{\"rings\":[]}}]}}")]
    public void EmptyServiceAreaPayloadCannotQualify(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var result = new ServiceAreaResult(document.RootElement);
        Assert.NotNull(Record.Exception(() => RoutingQualificationAssertions.RequireServiceArea(result)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"routes\":{\"geometryType\":\"esriGeometryPolyline\",\"features\":[]}}")]
    [InlineData("{\"routes\":{\"geometryType\":\"esriGeometryPolyline\",\"features\":[{\"attributes\":{\"IncidentID\":1,\"FacilityRank\":1,\"FacilityID\":1},\"geometry\":{\"paths\":[]}}]}}")]
    public void EmptyClosestFacilityPayloadCannotQualify(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        // A parsed summary alone cannot substitute for real route geometry.
        var result = new ClosestFacilityResult(document.RootElement,
            [new RouteSummary("route", 1, TimeSpan.FromMinutes(1))], []);
        Assert.NotNull(Record.Exception(() => RoutingQualificationAssertions.RequireClosestFacility(result)));
    }

    [Fact]
    public void EmptyParsedClosestFacilityRoutesCannotQualify()
    {
        using var document = JsonDocument.Parse("{}");
        var result = new ClosestFacilityResult(document.RootElement, [], []);
        Assert.NotNull(Record.Exception(() => RoutingQualificationAssertions.RequireClosestFacility(result)));
    }
}
