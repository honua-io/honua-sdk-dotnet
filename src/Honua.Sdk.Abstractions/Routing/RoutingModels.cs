// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Abstractions.Routing;

/// <summary>
/// WGS84 coordinate used by routing operations. Longitude maps to X and latitude maps to Y.
/// </summary>
public readonly record struct RoutingCoordinate(double Longitude, double Latitude)
{
    /// <summary>
    /// Creates a coordinate from latitude/longitude ordered values, which is common for GPS APIs.
    /// </summary>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <param name="longitude">Longitude in decimal degrees.</param>
    /// <returns>A coordinate in longitude/latitude order.</returns>
    public static RoutingCoordinate FromLatitudeLongitude(double latitude, double longitude) => new(longitude, latitude);
}

/// <summary>
/// Optional arrival time window associated with a routing stop.
/// </summary>
public sealed record RouteTimeWindow
{
    /// <summary>Inclusive start of the time window.</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>Inclusive end of the time window.</summary>
    public DateTimeOffset? End { get; init; }
}

/// <summary>
/// Point input for route stops, facilities, incidents, barriers, and service-area centers.
/// </summary>
public sealed class RoutingLocation
{
    /// <summary>WGS84 point coordinate.</summary>
    public required RoutingCoordinate Coordinate { get; init; }

    /// <summary>Optional display name sent as the route stop/facility name.</summary>
    public string? Name { get; init; }

    /// <summary>Optional stable caller identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Optional arrival window for stops when the provider supports time-window routing.</summary>
    public RouteTimeWindow? TimeWindow { get; init; }

    /// <summary>Additional provider-neutral attributes included with the network-analysis feature.</summary>
    public IReadOnlyDictionary<string, JsonElement>? Attributes { get; init; }

    /// <summary>
    /// Creates a location from longitude/latitude ordered values.
    /// </summary>
    /// <param name="longitude">Longitude in decimal degrees.</param>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <param name="name">Optional stop name.</param>
    /// <returns>A routing location.</returns>
    public static RoutingLocation FromLongitudeLatitude(double longitude, double latitude, string? name = null) => new()
    {
        Coordinate = new RoutingCoordinate(longitude, latitude),
        Name = name,
    };

    /// <summary>
    /// Creates a location from latitude/longitude ordered values, which is common for GPS APIs.
    /// </summary>
    /// <param name="latitude">Latitude in decimal degrees.</param>
    /// <param name="longitude">Longitude in decimal degrees.</param>
    /// <param name="name">Optional stop name.</param>
    /// <returns>A routing location.</returns>
    public static RoutingLocation FromLatitudeLongitude(double latitude, double longitude, string? name = null) => new()
    {
        Coordinate = RoutingCoordinate.FromLatitudeLongitude(latitude, longitude),
        Name = name,
    };
}

/// <summary>
/// Routing capabilities advertised by a provider implementation.
/// </summary>
public sealed record RoutingCapabilities
{
    /// <summary>Whether point-to-point and multi-stop directions are supported.</summary>
    public bool SupportsDirections { get; init; }

    /// <summary>Whether best-sequence route optimization is supported.</summary>
    public bool SupportsRouteOptimization { get; init; }

    /// <summary>Whether service-area or isochrone solving is supported.</summary>
    public bool SupportsServiceAreas { get; init; }

    /// <summary>Whether closest-facility solving is supported.</summary>
    public bool SupportsClosestFacility { get; init; }

    /// <summary>Whether provider metadata can advertise travel modes.</summary>
    public bool SupportsTravelModes { get; init; }

    /// <summary>Whether point barriers can be sent with solve requests.</summary>
    public bool SupportsPointBarriers { get; init; }

    /// <summary>Whether localized directions language can be requested.</summary>
    public bool SupportsDirectionsLanguage { get; init; }

    /// <summary>Native provider surface backing the implementation.</summary>
    public string? NativeSurface { get; init; }

    /// <summary>Reason the capability set is unavailable, when applicable.</summary>
    public string? UnsupportedReason { get; init; }
}

/// <summary>
/// Request used to discover routing service metadata such as travel modes and directions languages.
/// </summary>
public sealed record RouteServiceMetadataRequest
{
    /// <summary>Override for the routing service id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Override for the route layer name.</summary>
    public string? RouteLayerName { get; init; }
}

/// <summary>
/// Travel mode advertised by a routing provider.
/// </summary>
public sealed record RouteTravelMode
{
    /// <summary>Travel mode display or lookup name.</summary>
    public string? Name { get; init; }

    /// <summary>Provider-specific travel mode type.</summary>
    public string? Type { get; init; }

    /// <summary>Provider-specific description.</summary>
    public string? Description { get; init; }

    /// <summary>Raw provider travel mode payload.</summary>
    public JsonElement Raw { get; init; }
}

/// <summary>
/// Routing service metadata discovered from a provider.
/// </summary>
public sealed record RouteServiceMetadata
{
    /// <summary>Provider service id used for discovery.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Route layer used for discovery.</summary>
    public required string RouteLayerName { get; init; }

    /// <summary>Supported travel modes, when advertised.</summary>
    public IReadOnlyList<RouteTravelMode> TravelModes { get; init; } = [];

    /// <summary>Default travel mode name, id, or JSON summary, when advertised.</summary>
    public string? DefaultTravelMode { get; init; }

    /// <summary>Supported directions languages, when advertised.</summary>
    public IReadOnlyList<string> SupportedDirectionsLanguages { get; init; } = [];

    /// <summary>Raw provider metadata response.</summary>
    public JsonElement RawResponse { get; init; }
}

/// <summary>
/// Options shared by route solve requests.
/// </summary>
public sealed class RouteSolveOptions
{
    /// <summary>Override for the routing service id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Override for the route layer name.</summary>
    public string? RouteLayerName { get; init; }

    /// <summary>REST response format. Defaults to JSON.</summary>
    public string ResponseFormat { get; init; } = "json";

    /// <summary>Requests turn-by-turn directions in the response.</summary>
    public bool ReturnDirections { get; init; } = true;

    /// <summary>Requests route geometry in the response.</summary>
    public bool ReturnRoutes { get; init; } = true;

    /// <summary>Requests traffic-aware routing when the server/network supports it.</summary>
    public bool UseTraffic { get; init; }

    /// <summary>Requests toll-road restrictions when the server/network supports them.</summary>
    public bool AvoidTolls { get; init; }

    /// <summary>Requests highway restrictions when the server/network supports them.</summary>
    public bool AvoidHighways { get; init; }

    /// <summary>Optional server travel mode name or JSON travel-mode payload.</summary>
    public string? TravelMode { get; init; }

    /// <summary>Optional route start time.</summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>Optional language code for turn-by-turn directions.</summary>
    public string? DirectionsLanguage { get; init; }

    /// <summary>Direction length units sent to GeoServices-compatible NAServer endpoints.</summary>
    public string DirectionsLengthUnits { get; init; } = "esriMeters";

    /// <summary>Route line shape type sent to GeoServices-compatible NAServer endpoints.</summary>
    public string OutputLines { get; init; } = "esriNAOutputLineTrueShape";

    /// <summary>Optional point barriers used by providers that support barrier feature sets.</summary>
    public IReadOnlyList<RoutingLocation>? PointBarriers { get; init; }

    /// <summary>Additional raw provider parameters for server-specific routing extensions.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Directions request for point-to-point and multi-stop routing.
/// </summary>
public sealed class RouteDirectionsRequest
{
    /// <summary>Origin stop.</summary>
    public required RoutingLocation Origin { get; init; }

    /// <summary>Destination stop.</summary>
    public required RoutingLocation Destination { get; init; }

    /// <summary>Optional intermediate stops.</summary>
    public IReadOnlyList<RoutingLocation>? Waypoints { get; init; }

    /// <summary>Routing options.</summary>
    public RouteSolveOptions Options { get; init; } = new();
}

/// <summary>
/// Options for optimized multi-stop route requests.
/// </summary>
public sealed class RouteOptimizationOptions
{
    /// <summary>Override for the routing service id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Override for the route layer name.</summary>
    public string? RouteLayerName { get; init; }

    /// <summary>REST response format. Defaults to JSON.</summary>
    public string ResponseFormat { get; init; } = "json";

    /// <summary>Requests turn-by-turn directions in the response.</summary>
    public bool ReturnDirections { get; init; } = true;

    /// <summary>Requests route geometry in the response.</summary>
    public bool ReturnRoutes { get; init; } = true;

    /// <summary>Requests traffic-aware routing when the server/network supports it.</summary>
    public bool UseTraffic { get; init; }

    /// <summary>Requests toll-road restrictions when the server/network supports them.</summary>
    public bool AvoidTolls { get; init; }

    /// <summary>Requests highway restrictions when the server/network supports them.</summary>
    public bool AvoidHighways { get; init; }

    /// <summary>Optional server travel mode name or JSON travel-mode payload.</summary>
    public string? TravelMode { get; init; }

    /// <summary>Optional route start time.</summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>Whether the first stop must remain first when optimizing sequence.</summary>
    public bool PreserveFirstStop { get; init; } = true;

    /// <summary>Whether the last stop must remain last when optimizing sequence.</summary>
    public bool PreserveLastStop { get; init; } = true;

    /// <summary>Optional language code for turn-by-turn directions.</summary>
    public string? DirectionsLanguage { get; init; }

    /// <summary>Direction length units sent to GeoServices-compatible NAServer endpoints.</summary>
    public string DirectionsLengthUnits { get; init; } = "esriMeters";

    /// <summary>Route line shape type sent to GeoServices-compatible NAServer endpoints.</summary>
    public string OutputLines { get; init; } = "esriNAOutputLineTrueShape";

    /// <summary>Optional point barriers used by providers that support barrier feature sets.</summary>
    public IReadOnlyList<RoutingLocation>? PointBarriers { get; init; }

    /// <summary>Additional raw provider parameters for server-specific routing extensions.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Optimizes a sequence of stops using best-sequence routing.
/// </summary>
public sealed class RouteOptimizationRequest
{
    /// <summary>Stops to sequence and solve.</summary>
    public required IReadOnlyList<RoutingLocation> Stops { get; init; }

    /// <summary>Optimization options.</summary>
    public RouteOptimizationOptions Options { get; init; } = new();
}

/// <summary>
/// Options for service-area / isochrone requests.
/// </summary>
public sealed class ServiceAreaOptions
{
    /// <summary>Override for the routing service id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Override for the service-area layer name.</summary>
    public string? ServiceAreaLayerName { get; init; }

    /// <summary>REST response format. Defaults to JSON.</summary>
    public string ResponseFormat { get; init; } = "json";

    /// <summary>Provider travel direction parameter.</summary>
    public string TravelDirection { get; init; } = "esriNATravelDirectionFromFacility";

    /// <summary>Provider output polygon detail parameter.</summary>
    public string OutputPolygons { get; init; } = "esriNAOutputPolygonSimplified";

    /// <summary>Whether similar polygon ranges should be merged.</summary>
    public bool MergeSimilarPolygonRanges { get; init; } = true;

    /// <summary>Optional server travel mode name or JSON travel-mode payload.</summary>
    public string? TravelMode { get; init; }

    /// <summary>Optional route start time.</summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>Optional point barriers used by providers that support barrier feature sets.</summary>
    public IReadOnlyList<RoutingLocation>? PointBarriers { get; init; }

    /// <summary>Additional raw provider parameters for server-specific routing extensions.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Service-area / isochrone request centered on one location.
/// </summary>
public sealed class ServiceAreaRequest
{
    /// <summary>Center location.</summary>
    public required RoutingLocation Center { get; init; }

    /// <summary>Travel time break.</summary>
    public required TimeSpan TravelTime { get; init; }

    /// <summary>Service-area options.</summary>
    public ServiceAreaOptions Options { get; init; } = new();
}

/// <summary>
/// Options for closest-facility network-analysis requests.
/// </summary>
public sealed class ClosestFacilityOptions
{
    /// <summary>Override for the routing service id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Override for the closest-facility layer name.</summary>
    public string? ClosestFacilityLayerName { get; init; }

    /// <summary>REST response format. Defaults to JSON.</summary>
    public string ResponseFormat { get; init; } = "json";

    /// <summary>Maximum number of facilities returned for each incident.</summary>
    public int? TargetFacilityCount { get; init; }

    /// <summary>Provider travel direction parameter.</summary>
    public string TravelDirection { get; init; } = "esriNATravelDirectionToFacility";

    /// <summary>Requests turn-by-turn directions in the response.</summary>
    public bool ReturnDirections { get; init; } = true;

    /// <summary>Requests route geometry in the response.</summary>
    public bool ReturnRoutes { get; init; } = true;

    /// <summary>Optional server travel mode name or JSON travel-mode payload.</summary>
    public string? TravelMode { get; init; }

    /// <summary>Optional route start time.</summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>Optional language code for turn-by-turn directions.</summary>
    public string? DirectionsLanguage { get; init; }

    /// <summary>Optional point barriers used by providers that support barrier feature sets.</summary>
    public IReadOnlyList<RoutingLocation>? PointBarriers { get; init; }

    /// <summary>Additional raw provider parameters for server-specific routing extensions.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Finds the nearest facilities for one or more incident locations.
/// </summary>
public sealed class ClosestFacilityRequest
{
    /// <summary>Incident locations.</summary>
    public required IReadOnlyList<RoutingLocation> Incidents { get; init; }

    /// <summary>Candidate facility locations.</summary>
    public required IReadOnlyList<RoutingLocation> Facilities { get; init; }

    /// <summary>Closest-facility options.</summary>
    public ClosestFacilityOptions Options { get; init; } = new();
}

/// <summary>
/// Summary information parsed from a routing response when present.
/// </summary>
public sealed record RouteSummary(string? Name, double? TotalDistance, TimeSpan? TotalTime);

/// <summary>
/// Turn-by-turn instruction parsed from a routing response when present.
/// </summary>
public sealed record RouteDirectionStep(string? Text, double? Distance, TimeSpan? Time, string? ManeuverType);

/// <summary>
/// Result returned from directions and optimized-route requests.
/// </summary>
public sealed record RouteResult(JsonElement RawResponse, IReadOnlyList<RouteSummary> Routes, IReadOnlyList<RouteDirectionStep> Directions);

/// <summary>
/// Result returned from service-area requests.
/// </summary>
public sealed record ServiceAreaResult(JsonElement RawResponse);

/// <summary>
/// Result returned from closest-facility requests.
/// </summary>
public sealed record ClosestFacilityResult(
    JsonElement RawResponse,
    IReadOnlyList<RouteSummary> Routes,
    IReadOnlyList<RouteDirectionStep> Directions);
