// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.GeoServices.GeometryServer;

/// <summary>
/// GeoServices GeometryServer client: <c>project</c>, <c>buffer</c>, <c>lengths</c>, and
/// <c>areasAndLengths</c>.
///
/// Per AGENTS.md this is the remote fallback for transforms/buffers/geodesic measurement the
/// local NetTopologySuite/ProjNet engine cannot perform offline; prefer local computation where
/// possible.
/// </summary>
public sealed class HonuaGeometryServerClient
{
    private readonly HttpClient _http;
    private readonly HonuaGeoServicesClientOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaGeometryServerClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and authentication handlers.</param>
    /// <param name="options">GeoServices options used for the default GeometryServer service id.</param>
    [ActivatorUtilitiesConstructor]
    public HonuaGeometryServerClient(HttpClient httpClient, IOptions<HonuaGeoServicesClientOptions> options)
        : this(httpClient, GetOptionsValue(options))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaGeometryServerClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and authentication handlers.</param>
    /// <param name="options">GeoServices options used for the default GeometryServer service id.</param>
    public HonuaGeometryServerClient(HttpClient httpClient, HonuaGeoServicesClientOptions options)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Projects geometries between spatial references (<c>POST .../GeometryServer/project</c>).
    /// </summary>
    /// <param name="request">Project parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The projected geometries.</returns>
    public async Task<GeometryCollectionResult> ProjectAsync(
        ProjectGeometriesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Geometries);

        var form = new List<(string Key, string? Value)>
        {
            ("f", "json"),
            ("geometries", SerializeGeometries(request.GeometryType, request.Geometries)),
            ("inSR", request.InSpatialReference.ToString(CultureInfo.InvariantCulture)),
            ("outSR", request.OutSpatialReference.ToString(CultureInfo.InvariantCulture)),
            ("transformation", request.Transformation),
        };

        var body = await PostAsync("project", request.ServiceId, form, cancellationToken).ConfigureAwait(false);
        return ParseGeometryCollection(body);
    }

    /// <summary>
    /// Buffers geometries (<c>POST .../GeometryServer/buffer</c>).
    /// </summary>
    /// <param name="request">Buffer parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The buffer polygons.</returns>
    public async Task<GeometryCollectionResult> BufferAsync(
        BufferGeometriesRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Geometries);
        ArgumentNullException.ThrowIfNull(request.Distances);
        if (request.Distances.Count == 0)
        {
            throw new ArgumentException("At least one buffer distance is required.", nameof(request));
        }

        var form = new List<(string Key, string? Value)>
        {
            ("f", "json"),
            ("geometries", SerializeGeometries(request.GeometryType, request.Geometries)),
            ("inSR", request.InSpatialReference.ToString(CultureInfo.InvariantCulture)),
            ("outSR", request.OutSpatialReference.ToString(CultureInfo.InvariantCulture)),
            ("bufferSR", request.BufferSpatialReference?.ToString(CultureInfo.InvariantCulture)),
            ("distances", string.Join(',', request.Distances.Select(d => d.ToString("R", CultureInfo.InvariantCulture)))),
            ("unit", request.Unit),
            ("unionResults", request.UnionResults ? "true" : "false"),
            ("geodesic", request.Geodesic ? "true" : "false"),
        };

        var body = await PostAsync("buffer", request.ServiceId, form, cancellationToken).ConfigureAwait(false);
        return ParseGeometryCollection(body);
    }

    /// <summary>
    /// Computes polyline lengths (<c>POST .../GeometryServer/lengths</c>).
    /// </summary>
    /// <param name="request">Lengths parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The computed lengths.</returns>
    public async Task<LengthsResult> LengthsAsync(
        LengthsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Polylines);

        var form = new List<(string Key, string? Value)>
        {
            ("f", "json"),
            ("polylines", SerializeGeometryArray(request.Polylines)),
            ("sr", request.SpatialReference.ToString(CultureInfo.InvariantCulture)),
            ("lengthUnit", request.LengthUnit),
            ("calculationType", request.CalculationType),
        };

        var body = await PostAsync("lengths", request.ServiceId, form, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(body);
        return new LengthsResult
        {
            Lengths = ReadDoubleArray(document.RootElement, "lengths"),
            RawResponse = document.RootElement.Clone(),
        };
    }

    /// <summary>
    /// Computes polygon areas and perimeter lengths (<c>POST .../GeometryServer/areasAndLengths</c>).
    /// </summary>
    /// <param name="request">Areas-and-lengths parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The computed areas and lengths.</returns>
    public async Task<AreasAndLengthsResult> AreasAndLengthsAsync(
        AreasAndLengthsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Polygons);

        var form = new List<(string Key, string? Value)>
        {
            ("f", "json"),
            ("polygons", SerializeGeometryArray(request.Polygons)),
            ("sr", request.SpatialReference.ToString(CultureInfo.InvariantCulture)),
            ("lengthUnit", request.LengthUnit),
            ("areaUnit", request.AreaUnit),
            ("calculationType", request.CalculationType),
        };

        var body = await PostAsync("areasAndLengths", request.ServiceId, form, cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(body);
        return new AreasAndLengthsResult
        {
            Areas = ReadDoubleArray(document.RootElement, "areas"),
            Lengths = ReadDoubleArray(document.RootElement, "lengths"),
            RawResponse = document.RootElement.Clone(),
        };
    }

    private Task<string> PostAsync(
        string operation,
        string? serviceId,
        List<(string Key, string? Value)> form,
        CancellationToken cancellationToken)
    {
        var resolved = string.IsNullOrWhiteSpace(serviceId) ? _options.GeometryServiceId : serviceId;
        if (string.IsNullOrWhiteSpace(resolved))
        {
            throw new InvalidOperationException("Honua GeoServices GeometryServer service id must be configured.");
        }

        var path = $"/rest/services/{Uri.EscapeDataString(resolved)}/GeometryServer/{operation}";
        return GeoServicesHttp.PostFormAsync(_http, path, form, cancellationToken);
    }

    private static HonuaGeoServicesClientOptions GetOptionsValue(IOptions<HonuaGeoServicesClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Value;
    }

    private static string SerializeGeometries(string geometryType, IReadOnlyList<JsonElement> geometries)
    {
        if (string.IsNullOrWhiteSpace(geometryType))
        {
            throw new ArgumentException("Geometry type is required.", nameof(geometryType));
        }

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("geometryType", geometryType);
            writer.WritePropertyName("geometries");
            writer.WriteStartArray();
            foreach (var geometry in geometries)
            {
                geometry.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string SerializeGeometryArray(IReadOnlyList<JsonElement> geometries)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var geometry in geometries)
            {
                geometry.WriteTo(writer);
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static GeometryCollectionResult ParseGeometryCollection(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var geometries = new List<JsonElement>();
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("geometries", out var array) &&
            array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                geometries.Add(item.Clone());
            }
        }

        return new GeometryCollectionResult
        {
            Geometries = geometries,
            RawResponse = root.Clone(),
        };
    }

    private static List<double> ReadDoubleArray(JsonElement element, string name)
    {
        var list = new List<double>();
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var array) &&
            array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var number))
                {
                    list.Add(number);
                }
                else if (item.ValueKind == JsonValueKind.String &&
                         double.TryParse(item.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                {
                    list.Add(number);
                }
            }
        }

        return list;
    }
}
