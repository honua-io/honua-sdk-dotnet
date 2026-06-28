// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Honua.Sdk.Abstractions.Routing;
using Honua.Sdk.GeoServices.FeatureServer.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.GeoServices.Routing;

/// <summary>
/// GeoServices NAServer implementation of routing and network-analysis operations.
/// </summary>
public sealed class HonuaRoutingClient : IHonuaRoutingClient
{
    private static readonly RoutingCapabilities ProviderCapabilities = new()
    {
        SupportsDirections = true,
        SupportsRouteOptimization = true,
        SupportsServiceAreas = true,
        SupportsClosestFacility = true,
        SupportsTravelModes = true,
        SupportsPointBarriers = true,
        SupportsDirectionsLanguage = true,
        NativeSurface = "GeoServices NAServer"
    };

    private readonly HttpClient _http;
    private readonly HonuaGeoServicesClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaRoutingClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and authentication handlers.</param>
    /// <param name="options">GeoServices options used for default service and layer names.</param>
    [ActivatorUtilitiesConstructor]
    public HonuaRoutingClient(HttpClient httpClient, IOptions<HonuaGeoServicesClientOptions> options)
        : this(httpClient, GetOptionsValue(options))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaRoutingClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and authentication handlers.</param>
    /// <param name="options">GeoServices options used for default service and layer names.</param>
    public HonuaRoutingClient(HttpClient httpClient, HonuaGeoServicesClientOptions options)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string ProviderName => "geoservices-naserver";

    /// <inheritdoc />
    public RoutingCapabilities Capabilities => ProviderCapabilities;

    /// <inheritdoc />
    public async Task<RouteServiceMetadata> GetServiceMetadataAsync(
        RouteServiceMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var serviceId = ResolveServiceId(request.ServiceId);
        var layerName = ResolveLayerName(request.RouteLayerName, _options.RoutingRouteLayerName);
        var path = BuildNasLayerPath(serviceId, layerName);
        var body = await GetStringAsync($"{path}?f=json", cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return new RouteServiceMetadata
        {
            ServiceId = serviceId,
            RouteLayerName = layerName,
            TravelModes = ParseTravelModes(root),
            DefaultTravelMode = ParseDefaultTravelMode(root),
            SupportedDirectionsLanguages = ParseDirectionsLanguages(root),
            RawResponse = root.Clone(),
        };
    }

    /// <summary>
    /// Creates a fluent directions builder.
    /// </summary>
    /// <returns>A route directions builder.</returns>
    public RouteDirectionsBuilder Route() => new(this);

    /// <summary>
    /// Gets directions from an origin to a destination with optional waypoints.
    /// </summary>
    /// <param name="origin">Origin stop.</param>
    /// <param name="destination">Destination stop.</param>
    /// <param name="waypoints">Optional intermediate stops.</param>
    /// <param name="options">Routing options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Route result.</returns>
    public Task<RouteResult> GetDirectionsAsync(
        RoutingLocation origin,
        RoutingLocation destination,
        IReadOnlyList<RoutingLocation>? waypoints = null,
        RouteSolveOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        return GetDirectionsAsync(
            new RouteDirectionsRequest
            {
                Origin = origin,
                Destination = destination,
                Waypoints = waypoints,
                Options = options ?? new RouteSolveOptions(),
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RouteResult> GetDirectionsAsync(RouteDirectionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Origin);
        ArgumentNullException.ThrowIfNull(request.Destination);

        var stops = new List<RoutingLocation> { request.Origin };
        if (request.Waypoints is { Count: > 0 })
        {
            stops.AddRange(request.Waypoints);
        }

        stops.Add(request.Destination);
        ValidateLocations(stops, minCount: 2, nameof(request));

        var options = request.Options ?? new RouteSolveOptions();
        var form = CreateRouteSolveForm(options);
        form.Add(("stops", SerializeFeatureSet(stops)));
        form.Add(("findBestSequence", "false"));
        AddPointBarriers(form, options.PointBarriers);

        var body = await PostFormAsync(
            BuildNasOperationPath(options.ServiceId, options.RouteLayerName, "solve", _options.RoutingRouteLayerName),
            form,
            cancellationToken).ConfigureAwait(false);

        return RoutingResultParser.ParseRoute(body);
    }

    /// <summary>
    /// Gets the service area reachable from <paramref name="center"/> within <paramref name="travelTime"/>.
    /// </summary>
    /// <param name="center">Center location.</param>
    /// <param name="travelTime">Travel time break.</param>
    /// <param name="options">Service-area options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Service-area result.</returns>
    public Task<ServiceAreaResult> GetServiceAreaAsync(
        RoutingLocation center,
        TimeSpan travelTime,
        ServiceAreaOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(center);

        return GetServiceAreaAsync(
            new ServiceAreaRequest
            {
                Center = center,
                TravelTime = travelTime,
                Options = options ?? new ServiceAreaOptions(),
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ServiceAreaResult> GetServiceAreaAsync(ServiceAreaRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Center);

        if (request.TravelTime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Travel time must be greater than zero.");
        }

        ValidateLocations([request.Center], minCount: 1, nameof(request));

        var options = request.Options ?? new ServiceAreaOptions();
        var form = new List<(string Key, string? Value)>
        {
            ("f", options.ResponseFormat),
            ("facilities", SerializeFeatureSet([request.Center])),
            ("defaultBreaks", request.TravelTime.TotalMinutes.ToString("0.###", CultureInfo.InvariantCulture)),
            ("travelDirection", options.TravelDirection),
            ("outputPolygons", options.OutputPolygons),
            ("mergeSimilarPolygonRanges", options.MergeSimilarPolygonRanges ? "true" : "false"),
            ("travelMode", options.TravelMode),
            ("startTime", FormatStartTime(options.StartTime)),
        };
        AddPointBarriers(form, options.PointBarriers);
        AddAdditionalParameters(form, options.AdditionalParameters);

        var body = await PostFormAsync(
            BuildNasOperationPath(
                options.ServiceId,
                options.ServiceAreaLayerName,
                "solveServiceArea",
                _options.RoutingServiceAreaLayerName),
            form,
            cancellationToken).ConfigureAwait(false);

        return RoutingResultParser.ParseServiceArea(body);
    }

    /// <summary>
    /// Finds the closest facilities for one or more incident locations.
    /// </summary>
    /// <param name="incidents">Incident locations.</param>
    /// <param name="facilities">Candidate facility locations.</param>
    /// <param name="options">Closest-facility options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Closest-facility result.</returns>
    public Task<ClosestFacilityResult> FindClosestFacilityAsync(
        IReadOnlyList<RoutingLocation> incidents,
        IReadOnlyList<RoutingLocation> facilities,
        ClosestFacilityOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return FindClosestFacilityAsync(
            new ClosestFacilityRequest
            {
                Incidents = incidents,
                Facilities = facilities,
                Options = options ?? new ClosestFacilityOptions(),
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ClosestFacilityResult> FindClosestFacilityAsync(ClosestFacilityRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Incidents);
        ArgumentNullException.ThrowIfNull(request.Facilities);

        ValidateLocations(request.Incidents, minCount: 1, nameof(request.Incidents));
        ValidateLocations(request.Facilities, minCount: 1, nameof(request.Facilities));

        var options = request.Options ?? new ClosestFacilityOptions();
        var form = new List<(string Key, string? Value)>
        {
            ("f", options.ResponseFormat),
            ("incidents", SerializeFeatureSet(request.Incidents)),
            ("facilities", SerializeFeatureSet(request.Facilities)),
            ("defaultTargetFacilityCount", options.TargetFacilityCount?.ToString(CultureInfo.InvariantCulture)),
            ("travelDirection", options.TravelDirection),
            ("returnDirections", options.ReturnDirections ? "true" : "false"),
            ("returnRoutes", options.ReturnRoutes ? "true" : "false"),
            ("travelMode", options.TravelMode),
            ("startTime", FormatStartTime(options.StartTime)),
            ("directionsLanguage", options.DirectionsLanguage),
        };
        AddPointBarriers(form, options.PointBarriers);
        AddAdditionalParameters(form, options.AdditionalParameters);

        var body = await PostFormAsync(
            BuildNasOperationPath(
                options.ServiceId,
                options.ClosestFacilityLayerName,
                "solveClosestFacility",
                _options.RoutingClosestFacilityLayerName),
            form,
            cancellationToken).ConfigureAwait(false);

        return RoutingResultParser.ParseClosestFacility(body);
    }

    /// <summary>
    /// Optimizes the order of route stops using NAServer best-sequence routing.
    /// </summary>
    /// <param name="stops">Stops to sequence and solve.</param>
    /// <param name="options">Optimization options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Route result.</returns>
    public Task<RouteResult> OptimizeRouteAsync(
        IReadOnlyList<RoutingLocation> stops,
        RouteOptimizationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return OptimizeRouteAsync(
            new RouteOptimizationRequest
            {
                Stops = stops,
                Options = options ?? new RouteOptimizationOptions(),
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RouteResult> OptimizeRouteAsync(RouteOptimizationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Stops);
        ValidateLocations(request.Stops, minCount: 2, nameof(request.Stops));

        var options = request.Options ?? new RouteOptimizationOptions();
        var form = CreateRouteOptimizationForm(options);
        form.Add(("stops", SerializeFeatureSet(request.Stops)));
        AddPointBarriers(form, options.PointBarriers);

        var body = await PostFormAsync(
            BuildNasOperationPath(options.ServiceId, options.RouteLayerName, "solve", _options.RoutingRouteLayerName),
            form,
            cancellationToken).ConfigureAwait(false);

        return RoutingResultParser.ParseRoute(body);
    }

    private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(CreateRequestUri(url), cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    private async Task<string> PostFormAsync(
        string path,
        List<(string Key, string? Value)> parameters,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(
            parameters
                .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
                .Select(parameter => new KeyValuePair<string, string>(parameter.Key, parameter.Value!)));

        using var response = await _http.PostAsync(CreateRequestUri(path), content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body);
        return body;
    }

    private static HonuaGeoServicesClientOptions GetOptionsValue(IOptions<HonuaGeoServicesClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Value;
    }

    private static List<(string Key, string? Value)> CreateRouteSolveForm(RouteSolveOptions options)
    {
        var form = new List<(string Key, string? Value)>
        {
            ("f", options.ResponseFormat),
            ("returnDirections", options.ReturnDirections ? "true" : "false"),
            ("returnRoutes", options.ReturnRoutes ? "true" : "false"),
            ("useTraffic", options.UseTraffic ? "true" : null),
            ("avoidTolls", options.AvoidTolls ? "true" : null),
            ("avoidHighways", options.AvoidHighways ? "true" : null),
            ("travelMode", options.TravelMode),
            ("startTime", FormatStartTime(options.StartTime)),
            ("directionsLanguage", options.DirectionsLanguage),
            ("directionsLengthUnits", options.DirectionsLengthUnits),
            ("outputLines", options.OutputLines),
        };
        AddAdditionalParameters(form, options.AdditionalParameters);
        return form;
    }

    private static List<(string Key, string? Value)> CreateRouteOptimizationForm(RouteOptimizationOptions options)
    {
        var form = new List<(string Key, string? Value)>
        {
            ("f", options.ResponseFormat),
            ("returnDirections", options.ReturnDirections ? "true" : "false"),
            ("returnRoutes", options.ReturnRoutes ? "true" : "false"),
            ("findBestSequence", "true"),
            ("preserveFirstStop", options.PreserveFirstStop ? "true" : "false"),
            ("preserveLastStop", options.PreserveLastStop ? "true" : "false"),
            ("useTraffic", options.UseTraffic ? "true" : null),
            ("avoidTolls", options.AvoidTolls ? "true" : null),
            ("avoidHighways", options.AvoidHighways ? "true" : null),
            ("travelMode", options.TravelMode),
            ("startTime", FormatStartTime(options.StartTime)),
            ("directionsLanguage", options.DirectionsLanguage),
            ("directionsLengthUnits", options.DirectionsLengthUnits),
            ("outputLines", options.OutputLines),
        };
        AddAdditionalParameters(form, options.AdditionalParameters);
        return form;
    }

    private string BuildNasOperationPath(
        string? serviceId,
        string? layerName,
        string operation,
        string defaultLayerName)
    {
        var resolvedServiceId = ResolveServiceId(serviceId);
        var resolvedLayerName = ResolveLayerName(layerName, defaultLayerName);
        return $"{BuildNasLayerPath(resolvedServiceId, resolvedLayerName)}/{operation}";
    }

    private static string BuildNasLayerPath(string serviceId, string layerName)
        => $"/rest/services/{Uri.EscapeDataString(serviceId)}/NAServer/{Uri.EscapeDataString(layerName)}";

    private string ResolveServiceId(string? serviceId)
    {
        var resolved = string.IsNullOrWhiteSpace(serviceId) ? _options.RoutingServiceId : serviceId;
        return string.IsNullOrWhiteSpace(resolved)
            ? throw new InvalidOperationException("Honua GeoServices routing service id must be configured.")
            : resolved;
    }

    private static string ResolveLayerName(string? layerName, string defaultLayerName)
    {
        var resolved = string.IsNullOrWhiteSpace(layerName) ? defaultLayerName : layerName;
        return string.IsNullOrWhiteSpace(resolved)
            ? throw new InvalidOperationException("Honua GeoServices routing layer name must be configured.")
            : resolved;
    }

    private static void AddPointBarriers(
        List<(string Key, string? Value)> form,
        IReadOnlyList<RoutingLocation>? pointBarriers)
    {
        if (pointBarriers is not { Count: > 0 })
        {
            return;
        }

        ValidateLocations(pointBarriers, minCount: 1, nameof(pointBarriers));
        form.Add(("barriers", SerializeFeatureSet(pointBarriers)));
    }

    private static string SerializeFeatureSet(IReadOnlyList<RoutingLocation> locations)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("features");
            writer.WriteStartArray();
            foreach (var location in locations)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("geometry");
                WritePointGeometry(writer, location.Coordinate);

                if (HasAttributes(location))
                {
                    writer.WritePropertyName("attributes");
                    WriteAttributes(writer, location);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WritePropertyName("spatialReference");
            WriteSpatialReference(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WritePointGeometry(Utf8JsonWriter writer, RoutingCoordinate coordinate)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", coordinate.Longitude);
        writer.WriteNumber("y", coordinate.Latitude);
        writer.WritePropertyName("spatialReference");
        WriteSpatialReference(writer);
        writer.WriteEndObject();
    }

    private static void WriteSpatialReference(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("wkid", 4326);
        writer.WriteEndObject();
    }

    private static bool HasAttributes(RoutingLocation location)
        => !string.IsNullOrWhiteSpace(location.Id) ||
           !string.IsNullOrWhiteSpace(location.Name) ||
           location.TimeWindow is not null ||
           location.Attributes is { Count: > 0 };

    private static void WriteAttributes(Utf8JsonWriter writer, RoutingLocation location)
    {
        writer.WriteStartObject();
        if (!string.IsNullOrWhiteSpace(location.Id))
        {
            writer.WriteString("Id", location.Id);
        }

        if (!string.IsNullOrWhiteSpace(location.Name))
        {
            writer.WriteString("Name", location.Name);
        }

        if (location.TimeWindow?.Start is { } start)
        {
            writer.WriteString("TimeWindowStart", FormatStartTime(start));
        }

        if (location.TimeWindow?.End is { } end)
        {
            writer.WriteString("TimeWindowEnd", FormatStartTime(end));
        }

        if (location.Attributes is not null)
        {
            foreach (var attribute in location.Attributes)
            {
                writer.WritePropertyName(attribute.Key);
                attribute.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void ValidateLocations(IReadOnlyList<RoutingLocation> locations, int minCount, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(locations);

        if (locations.Count < minCount)
        {
            throw new ArgumentException($"At least {minCount} routing location(s) are required.", parameterName);
        }

        foreach (var location in locations)
        {
            ArgumentNullException.ThrowIfNull(location);
            if (!double.IsFinite(location.Coordinate.Longitude) || !double.IsFinite(location.Coordinate.Latitude))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Routing coordinates must be finite numbers.");
            }

            if (location.Coordinate.Latitude is < -90 or > 90)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Routing latitude must be between -90 and 90.");
            }

            if (location.Coordinate.Longitude is < -180 or > 180)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Routing longitude must be between -180 and 180.");
            }
        }
    }

    private static void AddAdditionalParameters(
        List<(string Key, string? Value)> form,
        IReadOnlyDictionary<string, string?>? additionalParameters)
    {
        if (additionalParameters is null)
        {
            return;
        }

        foreach (var parameter in additionalParameters)
        {
            form.Add((parameter.Key, parameter.Value));
        }
    }

    private static string? FormatStartTime(DateTimeOffset? startTime)
        => startTime?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static Uri CreateRequestUri(string url) => new(url, UriKind.RelativeOrAbsolute);

    private static void EnsureSuccess(HttpResponseMessage response, string body)
    {
        if (response.IsSuccessStatusCode)
        {
            if (!string.IsNullOrWhiteSpace(body) && TryExtractGeoServicesError(body, response.StatusCode) is { } error)
            {
                throw error;
            }

            return;
        }

        var errorMessage = TryExtractErrorMessage(body) ?? response.ReasonPhrase ?? "GeoServices routing request failed";
        throw new HonuaFeatureServerException(response.StatusCode, errorMessage, body);
    }

    private static HonuaFeatureServerException? TryExtractGeoServicesError(string body, HttpStatusCode fallbackStatus)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var errorElement) ||
                errorElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var message = "GeoServices routing returned an error.";
            int? geoServicesCode = null;
            IReadOnlyList<string>? details = null;
            var httpCode = fallbackStatus;
            if (errorElement.TryGetProperty("message", out var msgProp) &&
                msgProp.ValueKind == JsonValueKind.String)
            {
                message = msgProp.GetString() ?? message;
            }

            if (errorElement.TryGetProperty("code", out var codeProp) &&
                codeProp.TryGetInt32(out var errorCode))
            {
                geoServicesCode = errorCode;
                httpCode = GeoServicesHttp.MapErrorCodeToStatus(errorCode, fallbackStatus);
            }

            if (errorElement.TryGetProperty("details", out var detailsProp) &&
                detailsProp.ValueKind == JsonValueKind.Array)
            {
                var detailList = new List<string>();
                foreach (var item in detailsProp.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        detailList.Add(item.GetString()!);
                    }
                }

                details = detailList;
            }

            return new HonuaFeatureServerException(httpCode, message, body, geoServicesCode, details);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryExtractErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.Object &&
                errorElement.TryGetProperty("message", out var msg) &&
                msg.ValueKind == JsonValueKind.String)
            {
                return msg.GetString();
            }

            if (doc.RootElement.TryGetProperty("message", out var topMsg) &&
                topMsg.ValueKind == JsonValueKind.String)
            {
                return topMsg.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static List<RouteTravelMode> ParseTravelModes(JsonElement root)
    {
        if (!TryGetProperty(root, "supportedTravelModes", out var travelModes))
        {
            return [];
        }

        if (travelModes.ValueKind == JsonValueKind.String &&
            TryParseJson(travelModes.GetString(), out var parsedModes))
        {
            using (parsedModes)
            {
                return ParseTravelModes(parsedModes.RootElement);
            }
        }

        if (travelModes.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var modes = new List<RouteTravelMode>();
        foreach (var mode in travelModes.EnumerateArray())
        {
            modes.Add(new RouteTravelMode
            {
                Name = ReadString(mode, "name", "Name", "id", "Id"),
                Type = ReadString(mode, "type", "Type"),
                Description = ReadString(mode, "description", "Description"),
                Raw = mode.Clone(),
            });
        }

        return modes;
    }

    private static string? ParseDefaultTravelMode(JsonElement root)
    {
        if (!TryGetProperty(root, "defaultTravelMode", out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return ReadString(value, "name", "Name", "id", "Id") ?? value.GetRawText();
        }

        return value.GetRawText();
    }

    private static List<string> ParseDirectionsLanguages(JsonElement root)
    {
        foreach (var propertyName in new[] { "supportedDirectionsLanguages", "directionsLanguages" })
        {
            if (!TryGetProperty(root, propertyName, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .OfType<string>()
                    .ToList();
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                return value.GetString()?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList() ?? [];
            }
        }

        return [];
    }

    private static bool TryParseJson(string? json, out JsonDocument document)
    {
        document = default!;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}

/// <summary>
/// Fluent builder for common directions requests.
/// </summary>
public sealed class RouteDirectionsBuilder
{
    private readonly HonuaRoutingClient _client;
    private readonly List<RoutingLocation> _waypoints = [];
    private RoutingLocation? _origin;
    private RoutingLocation? _destination;
    private RouteSolveOptions _options = new();

    internal RouteDirectionsBuilder(HonuaRoutingClient client)
    {
        _client = client;
    }

    /// <summary>Sets the route origin.</summary>
    /// <param name="origin">Origin stop.</param>
    /// <returns>The current builder.</returns>
    public RouteDirectionsBuilder From(RoutingLocation origin)
    {
        _origin = origin ?? throw new ArgumentNullException(nameof(origin));
        return this;
    }

    /// <summary>Sets the route destination.</summary>
    /// <param name="destination">Destination stop.</param>
    /// <returns>The current builder.</returns>
    public RouteDirectionsBuilder To(RoutingLocation destination)
    {
        _destination = destination ?? throw new ArgumentNullException(nameof(destination));
        return this;
    }

    /// <summary>Adds an intermediate stop.</summary>
    /// <param name="waypoint">Intermediate stop.</param>
    /// <returns>The current builder.</returns>
    public RouteDirectionsBuilder Via(RoutingLocation waypoint)
    {
        _waypoints.Add(waypoint ?? throw new ArgumentNullException(nameof(waypoint)));
        return this;
    }

    /// <summary>Requests traffic-aware routing.</summary>
    /// <returns>The current builder.</returns>
    public RouteDirectionsBuilder WithTraffic()
    {
        _options = CopyOptions(useTraffic: true);
        return this;
    }

    /// <summary>Requests toll-road avoidance when supported.</summary>
    /// <returns>The current builder.</returns>
    public RouteDirectionsBuilder AvoidTolls()
    {
        _options = CopyOptions(avoidTolls: true);
        return this;
    }

    /// <summary>Requests highway avoidance when supported.</summary>
    /// <returns>The current builder.</returns>
    public RouteDirectionsBuilder AvoidHighways()
    {
        _options = CopyOptions(avoidHighways: true);
        return this;
    }

    /// <summary>Sets the provider travel mode.</summary>
    /// <param name="travelMode">Travel mode name or JSON payload.</param>
    /// <returns>The current builder.</returns>
    public RouteDirectionsBuilder WithTravelMode(string travelMode)
    {
        if (string.IsNullOrWhiteSpace(travelMode))
        {
            throw new ArgumentException("Travel mode cannot be empty.", nameof(travelMode));
        }

        _options = CopyOptions(travelMode: travelMode);
        return this;
    }

    /// <summary>Replaces the route solve options.</summary>
    /// <param name="options">Route solve options.</param>
    /// <returns>The current builder.</returns>
    public RouteDirectionsBuilder WithOptions(RouteSolveOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>Executes the built route solve request.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Route result.</returns>
    public Task<RouteResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (_origin is null)
        {
            throw new InvalidOperationException("Route origin has not been set.");
        }

        if (_destination is null)
        {
            throw new InvalidOperationException("Route destination has not been set.");
        }

        return _client.GetDirectionsAsync(_origin, _destination, _waypoints, _options, cancellationToken);
    }

    private RouteSolveOptions CopyOptions(
        bool? useTraffic = null,
        bool? avoidTolls = null,
        bool? avoidHighways = null,
        string? travelMode = null)
        => new()
        {
            ServiceId = _options.ServiceId,
            RouteLayerName = _options.RouteLayerName,
            ResponseFormat = _options.ResponseFormat,
            ReturnDirections = _options.ReturnDirections,
            ReturnRoutes = _options.ReturnRoutes,
            UseTraffic = useTraffic ?? _options.UseTraffic,
            AvoidTolls = avoidTolls ?? _options.AvoidTolls,
            AvoidHighways = avoidHighways ?? _options.AvoidHighways,
            TravelMode = travelMode ?? _options.TravelMode,
            StartTime = _options.StartTime,
            DirectionsLanguage = _options.DirectionsLanguage,
            DirectionsLengthUnits = _options.DirectionsLengthUnits,
            OutputLines = _options.OutputLines,
            PointBarriers = _options.PointBarriers,
            AdditionalParameters = _options.AdditionalParameters,
        };
}

internal static class RoutingResultParser
{
    public static RouteResult ParseRoute(string body)
    {
        using var document = JsonDocument.Parse(body);
        var directions = ParseDirections(document.RootElement);
        var routes = ParseRouteSummaries(document.RootElement);
        if (routes.Count == 0)
        {
            routes = ParseDirectionSummaries(document.RootElement);
        }

        return new RouteResult(document.RootElement.Clone(), routes, directions);
    }

    public static ServiceAreaResult ParseServiceArea(string body)
    {
        using var document = JsonDocument.Parse(body);
        return new ServiceAreaResult(document.RootElement.Clone());
    }

    public static ClosestFacilityResult ParseClosestFacility(string body)
    {
        using var document = JsonDocument.Parse(body);
        var routes = ParseRouteSummaries(document.RootElement);
        if (routes.Count == 0)
        {
            routes = ParseDirectionSummaries(document.RootElement);
        }

        return new ClosestFacilityResult(document.RootElement.Clone(), routes, ParseDirections(document.RootElement));
    }

    private static List<RouteDirectionStep> ParseDirections(JsonElement root)
    {
        var steps = new List<RouteDirectionStep>();
        if (!TryGetProperty(root, "directions", out var directions))
        {
            return steps;
        }

        foreach (var directionSet in EnumerateObjectOrArray(directions))
        {
            if (!TryGetProperty(directionSet, "features", out var features))
            {
                continue;
            }

            foreach (var feature in EnumerateObjectOrArray(features))
            {
                if (!TryGetProperty(feature, "attributes", out var attributes))
                {
                    continue;
                }

                steps.Add(new RouteDirectionStep(
                    ReadString(attributes, "text", "Text", "maneuverText", "driveInstruction"),
                    ReadDouble(attributes, "length", "Length", "distance", "Distance"),
                    ReadMinutes(attributes, "time", "Time", "minutes", "Minutes"),
                    ReadString(attributes, "maneuverType", "ManeuverType", "type")));
            }
        }

        return steps;
    }

    private static List<RouteSummary> ParseRouteSummaries(JsonElement root)
    {
        var summaries = new List<RouteSummary>();
        if (!TryGetProperty(root, "routes", out var routes) || !TryGetProperty(routes, "features", out var features))
        {
            return summaries;
        }

        foreach (var feature in EnumerateObjectOrArray(features))
        {
            if (!TryGetProperty(feature, "attributes", out var attributes))
            {
                continue;
            }

            summaries.Add(new RouteSummary(
                ReadString(attributes, "Name", "name", "RouteName"),
                ReadDouble(attributes, "Total_Length", "totalLength", "TotalDistance", "totalDistance"),
                ReadMinutes(attributes, "Total_Time", "totalTime", "Total_TravelTime", "travelTime")));
        }

        return summaries;
    }

    private static List<RouteSummary> ParseDirectionSummaries(JsonElement root)
    {
        var summaries = new List<RouteSummary>();
        if (!TryGetProperty(root, "directions", out var directions))
        {
            return summaries;
        }

        foreach (var directionSet in EnumerateObjectOrArray(directions))
        {
            if (!TryGetProperty(directionSet, "summary", out var summary))
            {
                continue;
            }

            summaries.Add(new RouteSummary(
                ReadString(summary, "routeName", "RouteName", "Name"),
                ReadDouble(summary, "totalLength", "Total_Length", "totalDistance"),
                ReadMinutes(summary, "totalTime", "Total_Time", "travelTime")));
        }

        return summaries;
    }

    private static IEnumerable<JsonElement> EnumerateObjectOrArray(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                yield return item;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
        }
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static double? ReadDouble(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String &&
                double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                return number;
            }
        }

        return null;
    }

    private static TimeSpan? ReadMinutes(JsonElement element, params string[] names)
    {
        var value = ReadDouble(element, names);
        return value is null ? null : TimeSpan.FromMinutes(value.Value);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
