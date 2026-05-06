using System.Globalization;
using System.Text.Json;
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.Geometry;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace RoutingGeofenceConsole;

public static class RoutingGeofenceDemo
{
    private static readonly GeometryFactory ProjectedFactory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 3857);

    public static async Task<RoutingGeofenceRunSummary> RunAsync(
        TextWriter output,
        IHonuaRoutingClient routingClient,
        string mode,
        string? serviceId = null,
        string? routeLayerName = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(routingClient);

        await output.WriteLineAsync($"Mode: {mode}");
        await output.WriteLineAsync($"Routing provider: {routingClient.ProviderName}");
        await output.WriteLineAsync();

        var route = await routingClient.GetDirectionsAsync(CreateDirectionsRequest(serviceId, routeLayerName), ct);
        var routeSummary = route.Routes.Count > 0 ? route.Routes[0] : null;
        await output.WriteLineAsync("Route:");
        await output.WriteLineAsync(
            $"  {routeSummary?.Name ?? "route"} distance={FormatDouble(routeSummary?.TotalDistance)}m time={FormatTime(routeSummary?.TotalTime)} steps={route.Directions.Count}");
        foreach (var step in route.Directions.Select((value, index) => new { Step = value, Index = index + 1 }))
        {
            await output.WriteLineAsync($"  {step.Index}. {step.Step.Text} ({FormatDouble(step.Step.Distance)}m)");
        }

        var evaluations = RunGeofenceFixture();
        await output.WriteLineAsync();
        await output.WriteLineAsync("Geofence:");
        foreach (var evaluation in evaluations)
        {
            var timestamp = evaluation.Position.Timestamp?.ToString("HH:mm:ss", CultureInfo.InvariantCulture) ?? "--:--:--";
            await output.WriteLineAsync(
                $"  {timestamp} {evaluation.Position.TrackId} {evaluation.Status} {evaluation.Transition} distance={FormatDouble(evaluation.Distance)}m");
        }

        return new RoutingGeofenceRunSummary(
            RouteCount: route.Routes.Count,
            DirectionCount: route.Directions.Count,
            RouteDistanceMeters: routeSummary?.TotalDistance,
            RouteTime: routeSummary?.TotalTime,
            GeofenceTransitions: evaluations.Select(evaluation => evaluation.Transition).ToArray());
    }

    public static RouteDirectionsRequest CreateDirectionsRequest(
        string? serviceId = null,
        string? routeLayerName = null) => new()
    {
        Origin = RoutingLocation.FromLongitudeLatitude(-157.8651, 21.3060, "Honolulu Harbor"),
        Destination = RoutingLocation.FromLongitudeLatitude(-157.8460, 21.3193, "Dispatch Yard"),
        Options = new RouteSolveOptions
        {
            ServiceId = string.IsNullOrWhiteSpace(serviceId) ? "Routing" : serviceId,
            RouteLayerName = string.IsNullOrWhiteSpace(routeLayerName) ? "Route" : routeLayerName,
            ReturnDirections = true,
            ReturnRoutes = true,
            DirectionsLanguage = "en"
        }
    };

    public static IReadOnlyList<HonuaGeofenceEvaluation> RunGeofenceFixture()
    {
        var evaluator = new HonuaGeofenceEvaluator(
        [
            new HonuaGeofenceDefinition
            {
                GeofenceId = "operations-yard",
                Geometry = ProjectedFactory.CreatePolygon(
                [
                    new Coordinate(0, 0),
                    new Coordinate(10, 0),
                    new Coordinate(10, 10),
                    new Coordinate(0, 10),
                    new Coordinate(0, 0)
                ]),
                ProximityDistance = 5,
                Metadata = new Dictionary<string, string> { ["fixture"] = "routing-geofence-demo" }
            }
        ]);

        return evaluator.Evaluate(
            [
                CreatePosition(14, 5, second: 0),
                CreatePosition(5, 5, second: 10),
                CreatePosition(14, 5, second: 20),
                CreatePosition(25, 5, second: 30)
            ])
            .ToArray();
    }

    private static HonuaGeofencePosition CreatePosition(double x, double y, int second)
        => new()
        {
            Location = ProjectedFactory.CreatePoint(new Coordinate(x, y)),
            TrackId = "truck-7",
            Timestamp = new DateTimeOffset(2026, 5, 6, 12, 0, second, TimeSpan.Zero)
        };

    private static string FormatDouble(double? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "unknown";

    private static string FormatTime(TimeSpan? value) =>
        value is null ? "unknown" : $"{value.Value.TotalMinutes.ToString("0.#", CultureInfo.InvariantCulture)}m";
}

public sealed record RoutingGeofenceOptions(
    bool UseServer,
    Uri ServerUri,
    string? ApiKey,
    string? BearerToken,
    string ServiceId,
    string RouteLayerName)
{
    public static RoutingGeofenceOptions FromEnvironment()
    {
        var mode = Environment.GetEnvironmentVariable("HONUA_ROUTE_MODE") ?? "simulated";
        var serverUri = new Uri(Environment.GetEnvironmentVariable("HONUA_ROUTE_SERVER_URL") ?? "https://localhost:5001");

        return new RoutingGeofenceOptions(
            UseServer: string.Equals(mode, "server", StringComparison.OrdinalIgnoreCase),
            ServerUri: serverUri,
            ApiKey: Environment.GetEnvironmentVariable("HONUA_ROUTE_API_KEY"),
            BearerToken: Environment.GetEnvironmentVariable("HONUA_ROUTE_BEARER_TOKEN"),
            ServiceId: Environment.GetEnvironmentVariable("HONUA_ROUTE_SERVICE_ID") ?? "Routing",
            RouteLayerName: Environment.GetEnvironmentVariable("HONUA_ROUTE_ROUTE_LAYER") ?? "Route");
    }
}

public sealed record RoutingGeofenceRunSummary(
    int RouteCount,
    int DirectionCount,
    double? RouteDistanceMeters,
    TimeSpan? RouteTime,
    IReadOnlyList<HonuaGeofenceTransition> GeofenceTransitions);

public sealed class SimulatedRoutingClient : IHonuaRoutingClient
{
    public string ProviderName => "simulated-geoservices-naserver";

    public RoutingCapabilities Capabilities { get; } = new()
    {
        SupportsDirections = true,
        SupportsRouteOptimization = false,
        SupportsServiceAreas = false,
        SupportsClosestFacility = false,
        SupportsTravelModes = true,
        NativeSurface = "deterministic route fixture"
    };

    public Task<RouteServiceMetadata> GetServiceMetadataAsync(
        RouteServiceMetadataRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(new RouteServiceMetadata
        {
            ServiceId = request.ServiceId ?? "Routing",
            RouteLayerName = request.RouteLayerName ?? "Route",
            DefaultTravelMode = "Driving Time",
            SupportedDirectionsLanguages = ["en"],
            RawResponse = JsonSerializer.SerializeToElement(new { defaultTravelMode = "Driving Time" })
        });
    }

    public Task<RouteResult> GetDirectionsAsync(RouteDirectionsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult(new RouteResult(
            JsonSerializer.SerializeToElement(new { fixture = "routing-geofence-demo" }),
            [new RouteSummary("Honolulu Harbor to Dispatch Yard", 4200, TimeSpan.FromMinutes(9.5))],
            [
                new RouteDirectionStep("Leave Honolulu Harbor", 600, TimeSpan.FromMinutes(1.5), "depart"),
                new RouteDirectionStep("Arrive at Dispatch Yard", 3600, TimeSpan.FromMinutes(8), "arrive")
            ]));
    }

    public Task<RouteResult> OptimizeRouteAsync(RouteOptimizationRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("The simulated routing client only supports directions.");

    public Task<ServiceAreaResult> GetServiceAreaAsync(ServiceAreaRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("The simulated routing client only supports directions.");

    public Task<ClosestFacilityResult> FindClosestFacilityAsync(ClosestFacilityRequest request, CancellationToken ct = default)
        => throw new NotSupportedException("The simulated routing client only supports directions.");
}
