// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Honua.Sdk.GeoServices.ImageServer;

/// <summary>
/// GeoServices ImageServer client: service metadata, raster <c>exportImage</c>, and pixel <c>identify</c>.
/// </summary>
public sealed class HonuaImageServerClient
{
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaImageServerClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and authentication handlers.</param>
    /// <param name="options">GeoServices options.</param>
    [ActivatorUtilitiesConstructor]
    public HonuaImageServerClient(HttpClient httpClient, IOptions<HonuaGeoServicesClientOptions> options)
        : this(httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        _ = options.Value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaImageServerClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client configured with base address and authentication handlers.</param>
    public HonuaImageServerClient(HttpClient httpClient)
    {
        _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Gets the ImageServer service metadata (<c>GET .../ImageServer?f=json</c>).
    /// </summary>
    /// <param name="serviceId">The ImageServer service id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed image-service metadata.</returns>
    public async Task<ImageServerMetadata> GetServiceMetadataAsync(
        string serviceId, CancellationToken cancellationToken = default)
    {
        var resolved = RequireServiceId(serviceId);
        var body = await GeoServicesHttp.GetStringAsync(
            _http, $"{ServicePath(resolved)}?f=json", cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return new ImageServerMetadata
        {
            ServiceId = resolved,
            ServiceDescription = ReadString(root, "serviceDescription"),
            Name = ReadString(root, "name"),
            PixelType = ReadString(root, "pixelType"),
            Extent = ReadExtent(root, "extent") ?? ReadExtent(root, "fullExtent"),
            BandCount = ReadInt(root, "bandCount"),
            MinValues = ReadDoubleArray(root, "minValues"),
            MaxValues = ReadDoubleArray(root, "maxValues"),
            SupportedQueryFormats = ReadStringList(root, "supportedQueryFormats"),
            SupportedImageFormatTypes = ReadStringList(root, "supportedImageFormatTypes"),
            AllowedMosaicMethods = ReadStringList(root, "allowedMosaicMethods"),
            DefaultMosaicMethod = ReadString(root, "defaultMosaicMethod"),
            HasRasterAttributeTable = ReadBool(root, "hasRasterAttributeTable"),
            HasHistograms = ReadBool(root, "hasHistograms"),
            RawResponse = root.Clone(),
        };
    }

    /// <summary>
    /// Exports a raster image (<c>POST .../ImageServer/exportImage</c> with <c>f=image</c>).
    /// The returned <see cref="ExportImageResult"/> owns the response stream; dispose it when done.
    /// </summary>
    /// <param name="request">Export parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raster image result.</returns>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Response ownership is transferred to ExportImageResult via ResponseOwningStream.")]
    public async Task<ExportImageResult> ExportImageAsync(
        ExportImageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var form = BuildExportForm(request, "image");
        var path = $"{ServicePath(RequireServiceId(request.ServiceId))}/exportImage";

        using var content = CreateFormContent(form);
        var response = await _http.PostAsync(
            GeoServicesHttp.CreateRequestUri(path), content, cancellationToken).ConfigureAwait(false);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (!response.IsSuccessStatusCode || IsJsonContent(contentType))
        {
            // Either an HTTP error, or a GeoServices error envelope returned with 200 + JSON.
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            response.Dispose();
            GeoServicesHttp.EnsureSuccess(CreateStatusOnly(response.StatusCode), body);

            // No error envelope but JSON content: treat as a contract violation for f=image.
            throw new FeatureServer.Exceptions.HonuaFeatureServerException(
                response.StatusCode,
                "ImageServer exportImage returned JSON instead of a raster image stream.",
                body);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var length = response.Content.Headers.ContentLength;
        return new ExportImageResult(new ResponseOwningStream(stream, response), contentType, length);
    }

    /// <summary>
    /// Exports image metadata (<c>POST .../ImageServer/exportImage</c> with <c>f=json</c>):
    /// returns the href and extent rather than the raster bytes.
    /// </summary>
    /// <param name="request">Export parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export metadata (href, size, extent, scale).</returns>
    public async Task<ExportImageMetadata> ExportImageMetadataAsync(
        ExportImageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var form = BuildExportForm(request, "json");
        var path = $"{ServicePath(RequireServiceId(request.ServiceId))}/exportImage";
        var body = await GeoServicesHttp.PostFormAsync(_http, path, form, cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return new ExportImageMetadata
        {
            Href = ReadString(root, "href"),
            Width = ReadInt(root, "width"),
            Height = ReadInt(root, "height"),
            Extent = ReadExtent(root, "extent"),
            Scale = ReadDouble(root, "scale"),
            RawResponse = root.Clone(),
        };
    }

    /// <summary>
    /// Computes raster statistics and histograms over an optional area of interest
    /// (<c>POST .../ImageServer/computeStatisticsHistograms</c> with <c>f=json</c>).
    /// Returns the raw GeoServices statistics/histograms JSON payload.
    /// </summary>
    /// <param name="serviceId">The ImageServer service id.</param>
    /// <param name="parameters">Form parameters (geometry, mosaicRule, pixelSize, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed statistics/histograms JSON document; the caller owns and disposes it.</returns>
    public async Task<JsonDocument> ComputeStatisticsHistogramsAsync(
        string serviceId,
        IEnumerable<(string Key, string? Value)> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var resolved = RequireServiceId(serviceId);
        var path = $"{ServicePath(resolved)}/computeStatisticsHistograms";
        var body = await GeoServicesHttp.PostFormAsync(_http, path, parameters, cancellationToken).ConfigureAwait(false);
        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// Identifies the pixel value at a point (<c>GET .../ImageServer/identify</c>).
    /// </summary>
    /// <param name="request">Identify parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identify result.</returns>
    public async Task<IdentifyImageResult> IdentifyAsync(
        IdentifyImageRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var form = new List<(string Key, string? Value)>
        {
            ("f", "json"),
            ("geometry", BuildPointGeometry(request.X, request.Y, request.SpatialReferenceWkid)),
            ("geometryType", "esriGeometryPoint"),
            ("mosaicRule", request.MosaicRule),
            ("renderingRule", request.RenderingRule),
            ("pixelSize", request.PixelSize),
        };
        AddAdditional(form, request.AdditionalParameters);

        var path = $"{ServicePath(RequireServiceId(request.ServiceId))}/identify";
        var body = await GeoServicesHttp.PostFormAsync(_http, path, form, cancellationToken).ConfigureAwait(false);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return new IdentifyImageResult
        {
            ObjectId = ReadLong(root, "objectId"),
            Name = ReadString(root, "name"),
            Value = ReadString(root, "value"),
            Location = ReadLocation(root),
            RawResponse = root.Clone(),
        };
    }

    private static List<(string Key, string? Value)> BuildExportForm(ExportImageRequest request, string format)
    {
        ArgumentNullException.ThrowIfNull(request.BoundingBox);
        if (request.Width <= 0 || request.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Export image size must be positive.");
        }

        var bbox = request.BoundingBox;
        var form = new List<(string Key, string? Value)>
        {
            ("f", format),
            ("bbox", string.Join(',', new[]
            {
                Num(bbox.XMin), Num(bbox.YMin), Num(bbox.XMax), Num(bbox.YMax),
            })),
            ("bboxSR", (request.BoundingBoxSpatialReference ?? bbox.SpatialReferenceWkid)?.ToString(CultureInfo.InvariantCulture)),
            ("imageSR", request.ImageSpatialReference?.ToString(CultureInfo.InvariantCulture)),
            ("size", $"{request.Width.ToString(CultureInfo.InvariantCulture)},{request.Height.ToString(CultureInfo.InvariantCulture)}"),
            ("format", request.Format),
            ("pixelType", request.PixelType),
            ("noData", request.NoData),
            ("noDataInterpretation", request.NoDataInterpretation),
            ("interpolation", request.Interpolation),
            ("mosaicRule", request.MosaicRule),
            ("renderingRule", request.RenderingRule),
        };
        AddAdditional(form, request.AdditionalParameters);
        return form;
    }

    private static FormUrlEncodedContent CreateFormContent(IEnumerable<(string Key, string? Value)> parameters)
        => new(parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .Select(parameter => new KeyValuePair<string, string>(parameter.Key, parameter.Value!)));

    private static string BuildPointGeometry(double x, double y, int? wkid)
    {
        var sb = new StringBuilder();
        sb.Append("{\"x\":").Append(Num(x)).Append(",\"y\":").Append(Num(y));
        if (wkid is { } w)
        {
            sb.Append(",\"spatialReference\":{\"wkid\":").Append(w.ToString(CultureInfo.InvariantCulture)).Append('}');
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static void AddAdditional(
        List<(string Key, string? Value)> form, IReadOnlyDictionary<string, string?>? additional)
    {
        if (additional is null)
        {
            return;
        }

        foreach (var pair in additional)
        {
            form.Add((pair.Key, pair.Value));
        }
    }

    private static string ServicePath(string serviceId)
        => $"/rest/services/{Uri.EscapeDataString(serviceId)}/ImageServer";

    private static string RequireServiceId(string? serviceId)
        => string.IsNullOrWhiteSpace(serviceId)
            ? throw new InvalidOperationException("Honua GeoServices ImageServer service id must be provided.")
            : serviceId;

    private static HttpResponseMessage CreateStatusOnly(System.Net.HttpStatusCode status) => new(status);

    private static bool IsJsonContent(string? contentType)
        => contentType is not null && contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string? ReadString(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? ReadLocation(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty("location", out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static int? ReadInt(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number &&
           value.TryGetInt32(out var number)
            ? number
            : null;

    private static long? ReadLong(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number &&
           value.TryGetInt64(out var number)
            ? number
            : null;

    private static double? ReadDouble(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number &&
           value.TryGetDouble(out var number)
            ? number
            : null;

    private static bool? ReadBool(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static List<double>? ReadDoubleArray(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<double>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var number))
            {
                list.Add(number);
            }
        }

        return list;
    }

    private static List<string>? ReadStringList(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()!)
                .ToList();
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return null;
    }

    private static ImageServerExtent? ReadExtent(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(name, out var extent) ||
            extent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int? wkid = null;
        if (extent.TryGetProperty("spatialReference", out var sr) &&
            sr.ValueKind == JsonValueKind.Object &&
            (sr.TryGetProperty("latestWkid", out var latest) && latest.TryGetInt32(out var latestWkid) ||
             sr.TryGetProperty("wkid", out latest) && latest.TryGetInt32(out latestWkid)))
        {
            wkid = latestWkid;
        }

        return new ImageServerExtent
        {
            XMin = ReadDouble(extent, "xmin") ?? 0,
            YMin = ReadDouble(extent, "ymin") ?? 0,
            XMax = ReadDouble(extent, "xmax") ?? 0,
            YMax = ReadDouble(extent, "ymax") ?? 0,
            SpatialReferenceWkid = wkid,
        };
    }
}
