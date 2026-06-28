// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.GeoServices.Extensions;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Honua.Sdk.GeoServices.Routing;
using Honua.Sdk.GeoServices.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.GeoServices.Tests.Routing;

public sealed class HonuaRoutingClientTests
{
    [Fact]
    public async Task GetDirectionsAsync_SendsNasServerSolvePayloadAndParsesDirections()
    {
        Dictionary<string, string>? form = null;
        HttpMethod? method = null;
        Uri? uri = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            method = request.Method;
            uri = request.RequestUri;
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("""
            {
              "routes": {
                "features": [
                  { "attributes": { "Name": "Route 1", "Total_Length": 10.5, "Total_Time": 25 } }
                ]
              },
              "directions": [
                {
                  "features": [
                    { "attributes": { "text": "Head east", "length": 0.1, "time": 1.2, "maneuverType": "esriDMTDepart" } }
                  ]
                }
              ]
            }
            """);
        });

        var result = await client.GetDirectionsAsync(
            RoutingLocation.FromLongitudeLatitude(-157.8583, 21.3069, "Start"),
            RoutingLocation.FromLongitudeLatitude(-157.8037, 21.2810, "Finish"),
            [RoutingLocation.FromLongitudeLatitude(-157.84, 21.29, "Waypoint")]);

        Assert.Equal(HttpMethod.Post, method);
        Assert.Equal("http://localhost:5000/rest/services/Routing/NAServer/Route/solve", uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("json", form["f"]);
        Assert.Equal("true", form["returnDirections"]);
        Assert.Equal("false", form["findBestSequence"]);

        using var stops = JsonDocument.Parse(form["stops"]);
        var features = stops.RootElement.GetProperty("features").EnumerateArray().ToArray();
        Assert.Equal(3, features.Length);
        Assert.Equal("Start", features[0].GetProperty("attributes").GetProperty("Name").GetString());
        Assert.Equal(-157.8583, features[0].GetProperty("geometry").GetProperty("x").GetDouble(), precision: 4);

        Assert.Single(result.Routes);
        Assert.Equal("Route 1", result.Routes[0].Name);
        Assert.Single(result.Directions);
        Assert.Equal("Head east", result.Directions[0].Text);
        Assert.Equal(TimeSpan.FromMinutes(1.2), result.Directions[0].Time);
    }

    [Fact]
    public async Task OptimizeRouteAsync_SendsBestSequenceParameters()
    {
        Dictionary<string, string>? form = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("{}");
        });

        await client.OptimizeRouteAsync(
            [
                RoutingLocation.FromLongitudeLatitude(-157.86, 21.30, "A"),
                RoutingLocation.FromLongitudeLatitude(-157.84, 21.31, "B"),
                RoutingLocation.FromLongitudeLatitude(-157.82, 21.32, "C"),
            ],
            new RouteOptimizationOptions
            {
                PreserveFirstStop = true,
                PreserveLastStop = false,
            });

        Assert.NotNull(form);
        Assert.Equal("true", form["findBestSequence"]);
        Assert.Equal("true", form["preserveFirstStop"]);
        Assert.Equal("false", form["preserveLastStop"]);
    }

    [Fact]
    public async Task GetServiceAreaAsync_SendsTravelTimeBreak()
    {
        Dictionary<string, string>? form = null;
        Uri? uri = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            uri = request.RequestUri;
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("{}");
        });

        await client.GetServiceAreaAsync(
            RoutingLocation.FromLongitudeLatitude(-157.8583, 21.3069, "Depot"),
            TimeSpan.FromMinutes(30));

        Assert.Equal("http://localhost:5000/rest/services/Routing/NAServer/ServiceArea/solveServiceArea", uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("30", form["defaultBreaks"]);
        Assert.Equal("esriNATravelDirectionFromFacility", form["travelDirection"]);
        using var facilities = JsonDocument.Parse(form["facilities"]);
        Assert.Equal("Depot", facilities.RootElement.GetProperty("features")[0].GetProperty("attributes").GetProperty("Name").GetString());
    }

    [Fact]
    public async Task FindClosestFacilityAsync_SendsIncidentAndFacilityFeatureSets()
    {
        Dictionary<string, string>? form = null;
        Uri? uri = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            uri = request.RequestUri;
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("{}");
        });

        await client.FindClosestFacilityAsync(
            [RoutingLocation.FromLongitudeLatitude(-157.85, 21.30, "Incident")],
            [
                RoutingLocation.FromLongitudeLatitude(-157.80, 21.28, "Facility A"),
                RoutingLocation.FromLongitudeLatitude(-157.82, 21.31, "Facility B"),
            ],
            new ClosestFacilityOptions { TargetFacilityCount = 1 });

        Assert.Equal("http://localhost:5000/rest/services/Routing/NAServer/ClosestFacility/solveClosestFacility", uri?.ToString());
        Assert.NotNull(form);
        Assert.Equal("1", form["defaultTargetFacilityCount"]);
        using var incidents = JsonDocument.Parse(form["incidents"]);
        using var facilities = JsonDocument.Parse(form["facilities"]);
        Assert.Single(incidents.RootElement.GetProperty("features").EnumerateArray());
        Assert.Equal(2, facilities.RootElement.GetProperty("features").EnumerateArray().Count());
    }

    [Fact]
    public async Task FindClosestFacilityAsync_UsesDirectionSummaryWhenRoutesAreOmitted()
    {
        var client = CreateClient((_, _) => Task.FromResult(JsonResponse("""
        {
          "directions": [
            {
              "summary": { "routeName": "Incident - Facility A", "totalLength": 2.5, "totalTime": 8 }
            }
          ]
        }
        """)));

        var result = await client.FindClosestFacilityAsync(
            [RoutingLocation.FromLongitudeLatitude(-157.85, 21.30, "Incident")],
            [RoutingLocation.FromLongitudeLatitude(-157.80, 21.28, "Facility A")],
            new ClosestFacilityOptions { ReturnRoutes = false });

        Assert.Single(result.Routes);
        Assert.Equal("Incident - Facility A", result.Routes[0].Name);
        Assert.Equal(2.5, result.Routes[0].TotalDistance);
        Assert.Equal(TimeSpan.FromMinutes(8), result.Routes[0].TotalTime);
    }

    [Fact]
    public async Task RouteBuilder_AppliesTrafficRestrictionsBarriersAndLanguage()
    {
        Dictionary<string, string>? form = null;
        var client = CreateClient(async (request, cancellationToken) =>
        {
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("{}");
        });

        await client.Route()
            .From(RoutingLocation.FromLongitudeLatitude(-157.8583, 21.3069))
            .Via(RoutingLocation.FromLongitudeLatitude(-157.84, 21.29))
            .To(RoutingLocation.FromLongitudeLatitude(-157.8037, 21.2810))
            .WithOptions(new RouteSolveOptions
            {
                DirectionsLanguage = "es",
                PointBarriers = [RoutingLocation.FromLongitudeLatitude(-157.83, 21.30, "Closure")],
            })
            .WithTraffic()
            .AvoidTolls()
            .ExecuteAsync();

        Assert.NotNull(form);
        Assert.Equal("true", form["useTraffic"]);
        Assert.Equal("true", form["avoidTolls"]);
        Assert.Equal("es", form["directionsLanguage"]);
        using var stops = JsonDocument.Parse(form["stops"]);
        using var barriers = JsonDocument.Parse(form["barriers"]);
        Assert.Equal(3, stops.RootElement.GetProperty("features").EnumerateArray().Count());
        Assert.Equal("Closure", barriers.RootElement.GetProperty("features")[0].GetProperty("attributes").GetProperty("Name").GetString());
    }

    [Fact]
    public async Task GetServiceMetadataAsync_ParsesTravelModesAndLanguages()
    {
        Uri? uri = null;
        var client = CreateClient((request, _) =>
        {
            uri = request.RequestUri;
            return Task.FromResult(JsonResponse("""
            {
              "defaultTravelMode": { "name": "Driving Time" },
              "supportedTravelModes": [
                { "name": "Driving Time", "type": "AUTOMOBILE", "description": "Fastest driving route" }
              ],
              "supportedDirectionsLanguages": ["en", "es"]
            }
            """));
        });

        var metadata = await client.GetServiceMetadataAsync(new RouteServiceMetadataRequest());

        Assert.Equal("http://localhost:5000/rest/services/Routing/NAServer/Route?f=json", uri?.ToString());
        Assert.Equal("Routing", metadata.ServiceId);
        Assert.Equal("Route", metadata.RouteLayerName);
        var mode = Assert.Single(metadata.TravelModes);
        Assert.Equal("Driving Time", mode.Name);
        Assert.Equal("AUTOMOBILE", mode.Type);
        Assert.Equal("Driving Time", metadata.DefaultTravelMode);
        Assert.Equal(["en", "es"], metadata.SupportedDirectionsLanguages);
    }

    [Fact]
    public async Task RoutingLocation_SerializesTimeWindowsAndJsonAttributes()
    {
        Dictionary<string, string>? form = null;
        using var priority = JsonDocument.Parse("\"high\"");
        var location = new RoutingLocation
        {
            Coordinate = new RoutingCoordinate(-157.8583, 21.3069),
            Name = "Timed stop",
            TimeWindow = new RouteTimeWindow
            {
                Start = new DateTimeOffset(2026, 4, 30, 20, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 4, 30, 21, 0, 0, TimeSpan.Zero),
            },
            Attributes = new Dictionary<string, JsonElement>
            {
                ["Priority"] = priority.RootElement.Clone(),
            },
        };
        var client = CreateClient(async (request, cancellationToken) =>
        {
            form = await ReadFormAsync(request, cancellationToken);
            return JsonResponse("{}");
        });

        await client.GetDirectionsAsync(location, RoutingLocation.FromLongitudeLatitude(-157.8037, 21.2810));

        Assert.NotNull(form);
        using var stops = JsonDocument.Parse(form["stops"]);
        var attributes = stops.RootElement.GetProperty("features")[0].GetProperty("attributes");
        Assert.Equal("Timed stop", attributes.GetProperty("Name").GetString());
        Assert.Equal("2026-04-30T20:00:00.0000000+00:00", attributes.GetProperty("TimeWindowStart").GetString());
        Assert.Equal("high", attributes.GetProperty("Priority").GetString());
    }

    [Fact]
    public void AddHonuaRouting_RegistersRoutingClient()
    {
        var services = new ServiceCollection();
        services.AddHonuaRouting(options =>
        {
            options.BaseAddress = new Uri("http://localhost:5000");
            options.EnableRetry = false;
        });

        using var provider = services.BuildServiceProvider();
        var routingClient = Assert.Single(provider.GetServices<IHonuaRoutingClient>());

        Assert.Equal("geoservices-naserver", routingClient.ProviderName);
        Assert.True(routingClient.Capabilities.SupportsDirections);
        Assert.True(routingClient.Capabilities.SupportsRouteOptimization);
        Assert.True(routingClient.Capabilities.SupportsServiceAreas);
        Assert.True(routingClient.Capabilities.SupportsClosestFacility);
    }

    [Fact]
    public async Task GetDirectionsAsync_GeoServicesErrorCode_DoesNotLeakIntoHttpStatus()
    {
        // GeoServices returns HTTP 200 with an error payload whose code is an Esri code
        // (1000), NOT an HTTP status. The error must keep the 200 transport status and
        // expose 1000 via GeoServicesErrorCode rather than casting 1000 into HttpStatus,
        // which would break consumers branching on status for retry/auth.
        var client = CreateClient((_, _) =>
            Task.FromResult(JsonResponse("""
            { "error": { "code": 1000, "message": "Unable to complete operation." } }
            """)));

        var ex = await Assert.ThrowsAsync<HonuaFeatureServerException>(() =>
            client.GetDirectionsAsync(
                RoutingLocation.FromLongitudeLatitude(-157.8583, 21.3069, "Start"),
                RoutingLocation.FromLongitudeLatitude(-157.8037, 21.2810, "Finish")));

        Assert.Equal(HttpStatusCode.OK, ex.StatusCode);
        Assert.Equal(1000, ex.GeoServicesErrorCode);
    }

    private static HonuaRoutingClient CreateClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler,
        HonuaGeoServicesClientOptions? options = null)
    {
        var mockHandler = new MockHttpHandler(request => handler(request, CancellationToken.None));
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("http://localhost:5000")
        };

        options ??= new HonuaGeoServicesClientOptions
        {
            BaseAddress = new Uri("http://localhost:5000"),
            RoutingServiceId = "Routing",
        };

        return new HonuaRoutingClient(httpClient, options);
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
