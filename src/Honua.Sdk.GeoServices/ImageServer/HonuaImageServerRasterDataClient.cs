// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using Honua.Sdk.Abstractions.Data;
using Honua.Sdk.Abstractions.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Sdk.GeoServices.ImageServer;

/// <summary>
/// <see cref="IHonuaRasterDataClient"/> implementation backed by the Honua
/// GeoServices ImageServer surface. Adapts the provider-neutral raster contract
/// (metadata, coverage statistics, windowed reads) onto the read-only ImageServer
/// endpoints (<c>?f=json</c> service metadata, <c>computeStatisticsHistograms</c>,
/// and <c>exportImage</c>). The windowed read (<see cref="ReadWindowAsync"/>) maps a
/// bbox extent plus a target pixel size onto an <c>exportImage</c> call so a raster
/// geoprocessing tool can pull a clipped window of a large raster rather than the
/// whole dataset.
/// </summary>
/// <remarks>
/// This client is read-only. The Honua server does expose a raster <i>write</i>
/// path — the admin multipart raster import endpoint
/// (<c>POST /api/v1/admin/import/raster</c>, GeoTIFF / world-file upload into
/// PostGIS) — but it lives on the privileged Admin surface and is not part of the
/// raster-data read contract. A geoprocessing tool that produces a raster should
/// write a GeoTIFF locally (e.g. from a <see cref="ReadWindowAsync"/> window or a
/// computed result) and register it through the admin raster import endpoint
/// exposed by the Admin client surface. See the package README for the raster
/// output stance.
/// </remarks>
public sealed class HonuaImageServerRasterDataClient : IHonuaRasterDataClient
{
    private static readonly RasterDataCapabilities CapabilitiesValue = new()
    {
        SupportsMetadata = true,
        SupportsBandMetadata = true,
        SupportsCoverageStatistics = true,
        SupportsHistograms = true,
        SupportsMosaicRules = true,
        SupportsTimeSlices = false,
        SupportsNoDataMasks = true,
        SupportsWindowReads = true,
        NativeSurface = "GeoServices/ImageServer",
    };

    private readonly HonuaImageServerClient _imageServer;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaImageServerRasterDataClient"/> class.
    /// </summary>
    /// <param name="imageServer">The underlying ImageServer client.</param>
    [ActivatorUtilitiesConstructor]
    public HonuaImageServerRasterDataClient(HonuaImageServerClient imageServer)
    {
        _imageServer = imageServer ?? throw new ArgumentNullException(nameof(imageServer));
    }

    /// <inheritdoc />
    public string ProviderName => "honua.geoservices.imageserver";

    /// <inheritdoc />
    public RasterDataCapabilities RasterCapabilities => CapabilitiesValue;

    /// <inheritdoc />
    public async Task<RasterDatasetMetadata> GetRasterMetadataAsync(
        RasterMetadataRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var serviceId = RequireServiceId(request.Source);

        var metadata = await _imageServer.GetServiceMetadataAsync(serviceId, cancellationToken).ConfigureAwait(false);

        var bands = request.IncludeBands ? BuildBands(metadata) : [];

        return new RasterDatasetMetadata
        {
            Source = request.Source,
            DatasetId = serviceId,
            Name = metadata.Name ?? metadata.ServiceId,
            Description = metadata.ServiceDescription,
            SpatialReference = metadata.Extent?.SpatialReferenceWkid?.ToString(CultureInfo.InvariantCulture),
            Extent = ToBoundingBox(metadata.Extent),
            PixelType = ParsePixelType(metadata.PixelType),
            Bands = bands,
            Capabilities = BuildCapabilityNames(metadata),
            RawMetadata = metadata.RawResponse,
        };
    }

    /// <inheritdoc />
    public async Task<RasterCoverageStatisticsResponse> GetCoverageStatisticsAsync(
        RasterCoverageStatisticsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var serviceId = RequireServiceId(request.Source);

        var form = new List<(string Key, string? Value)>
        {
            ("f", "json"),
            ("mosaicRule", SelectorToString(request.Selector)),
        };

        if (request.Extent is { } extent)
        {
            form.Add(("geometryType", "esriGeometryEnvelope"));
            form.Add(("geometry", BuildEnvelopeGeometry(extent)));
        }

        if (request.CellSize is { } cellSize)
        {
            var size = Num(cellSize);
            form.Add(("pixelSize", $"{size},{size}"));
        }

        AddAdditional(form, request.AdditionalParameters);

        using var document = await _imageServer
            .ComputeStatisticsHistogramsAsync(serviceId, form, cancellationToken)
            .ConfigureAwait(false);
        var root = document.RootElement;
        var bands = ParseBandStatistics(root, request.BandIndexes);

        return new RasterCoverageStatisticsResponse
        {
            Source = request.Source,
            Bands = bands,
            Extent = request.Extent,
            CellSize = request.CellSize,
            RawResponse = root.Clone(),
        };
    }

    /// <inheritdoc />
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The ExportImageResult's content stream is transferred to and owned by the returned RasterWindowReadResult; disposing the wrapper here would dispose the caller's stream.")]
    public async Task<RasterWindowReadResult> ReadWindowAsync(
        RasterWindowReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Width <= 0 || request.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Raster window size must be positive on both axes.");
        }

        var serviceId = RequireServiceId(request.Source);
        var exportRequest = new ExportImageRequest
        {
            ServiceId = serviceId,
            BoundingBox = ToImageServerExtent(request.Extent),
            BoundingBoxSpatialReference = ParseWkid(request.Extent.Crs),
            ImageSpatialReference = ParseWkid(request.OutputSpatialReference),
            Width = request.Width,
            Height = request.Height,
            Format = MapFormat(request.Format),
            NoData = request.NoData,
            Interpolation = MapInterpolation(request.ResamplingMethod),
            MosaicRule = SelectorToString(request.Selector),
            AdditionalParameters = BuildWindowAdditional(request),
        };

        var export = await _imageServer.ExportImageAsync(exportRequest, cancellationToken).ConfigureAwait(false);

        // Transfer stream ownership from the export result to the window result.
        return new RasterWindowReadResult(
            export.Content,
            request.Source,
            request.Extent,
            request.Width,
            request.Height,
            export.ContentType,
            export.ContentLength);
    }

    private static Dictionary<string, string?>? BuildWindowAdditional(RasterWindowReadRequest request)
    {
        Dictionary<string, string?>? additional = null;
        if (request.BandIndexes is { Count: > 0 } bands)
        {
            additional = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["bandIds"] = string.Join(',', bands.Select(static b => b.ToString(CultureInfo.InvariantCulture))),
            };
        }

        if (request.AdditionalParameters is { Count: > 0 } extra)
        {
            additional ??= new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var pair in extra)
            {
                additional[pair.Key] = pair.Value;
            }
        }

        return additional;
    }

    private static List<RasterBandStatistics> ParseBandStatistics(
        JsonElement root, IReadOnlyList<int>? bandIndexes)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("statistics", out var statistics) ||
            statistics.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var bands = new List<RasterBandStatistics>();
        var index = 0;
        foreach (var band in statistics.EnumerateArray())
        {
            var bandIndex = bandIndexes is { Count: > 0 } && index < bandIndexes.Count
                ? bandIndexes[index]
                : index;

            bands.Add(new RasterBandStatistics
            {
                BandIndex = bandIndex,
                Minimum = ReadDouble(band, "min"),
                Maximum = ReadDouble(band, "max"),
                Mean = ReadDouble(band, "mean"),
                StandardDeviation = ReadDouble(band, "standardDeviation"),
                Count = ReadLong(band, "count"),
            });
            index++;
        }

        return bands;
    }

    private static List<RasterBandMetadata> BuildBands(ImageServerMetadata metadata)
    {
        var bandCount = metadata.BandCount ?? 0;
        if (bandCount <= 0)
        {
            return [];
        }

        var pixelType = ParsePixelType(metadata.PixelType);
        var bands = new List<RasterBandMetadata>(bandCount);
        for (var band = 0; band < bandCount; band++)
        {
            bands.Add(new RasterBandMetadata
            {
                BandIndex = band,
                PixelType = pixelType,
                DataType = metadata.PixelType,
                Minimum = ValueAt(metadata.MinValues, band),
                Maximum = ValueAt(metadata.MaxValues, band),
            });
        }

        return bands;
    }

    private static List<string> BuildCapabilityNames(ImageServerMetadata metadata)
    {
        var names = new List<string> { "Image", "Metadata" };
        if (metadata.HasHistograms == true)
        {
            names.Add("Histograms");
        }

        if (metadata.HasRasterAttributeTable == true)
        {
            names.Add("RasterAttributeTable");
        }

        return names;
    }

    private static double? ValueAt(IReadOnlyList<double>? values, int index)
        => values is not null && index < values.Count ? values[index] : null;

    private static FeatureBoundingBox? ToBoundingBox(ImageServerExtent? extent)
        => extent is null
            ? null
            : new FeatureBoundingBox
            {
                MinX = extent.XMin,
                MinY = extent.YMin,
                MaxX = extent.XMax,
                MaxY = extent.YMax,
                Crs = extent.SpatialReferenceWkid?.ToString(CultureInfo.InvariantCulture),
            };

    private static ImageServerExtent ToImageServerExtent(FeatureBoundingBox extent)
        => new()
        {
            XMin = extent.MinX,
            YMin = extent.MinY,
            XMax = extent.MaxX,
            YMax = extent.MaxY,
            SpatialReferenceWkid = ParseWkid(extent.Crs),
        };

    private static string BuildEnvelopeGeometry(FeatureBoundingBox extent)
    {
        var wkid = ParseWkid(extent.Crs);
        var sr = wkid is { } w
            ? $",\"spatialReference\":{{\"wkid\":{w.ToString(CultureInfo.InvariantCulture)}}}"
            : string.Empty;
        return $"{{\"xmin\":{Num(extent.MinX)},\"ymin\":{Num(extent.MinY)},\"xmax\":{Num(extent.MaxX)},\"ymax\":{Num(extent.MaxY)}{sr}}}";
    }

    private static string MapFormat(RasterWindowFormat format) => format switch
    {
        RasterWindowFormat.GeoTiff => "tiff",
        RasterWindowFormat.Png => "png",
        RasterWindowFormat.Jpeg => "jpg",
        _ => "tiff",
    };

    private static string? MapInterpolation(RasterResamplingMethod method) => method switch
    {
        RasterResamplingMethod.Nearest => "RSP_NearestNeighbor",
        RasterResamplingMethod.Bilinear => "RSP_BilinearInterpolation",
        RasterResamplingMethod.Cubic => "RSP_CubicConvolution",
        RasterResamplingMethod.Majority => "RSP_Majority",
        _ => null,
    };

    private static RasterPixelType ParsePixelType(string? pixelType) => pixelType?.ToUpperInvariant() switch
    {
        "U1" or "U2" or "U4" or "U8" => RasterPixelType.UnsignedByte,
        "S8" => RasterPixelType.SignedByte,
        "U16" => RasterPixelType.UnsignedShort,
        "S16" => RasterPixelType.SignedShort,
        "U32" => RasterPixelType.UnsignedInteger,
        "S32" => RasterPixelType.SignedInteger,
        "F32" => RasterPixelType.SinglePrecision,
        "F64" => RasterPixelType.DoublePrecision,
        "C64" => RasterPixelType.ComplexSinglePrecision,
        "C128" => RasterPixelType.ComplexDoublePrecision,
        _ => RasterPixelType.Unknown,
    };

    private static int? ParseWkid(string? crs)
    {
        if (string.IsNullOrWhiteSpace(crs))
        {
            return null;
        }

        // Accept a bare wkid ("4326") or an EPSG-prefixed form ("EPSG:4326").
        var token = crs.Contains(':', StringComparison.Ordinal)
            ? crs[(crs.LastIndexOf(':') + 1)..]
            : crs;
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wkid) ? wkid : null;
    }

    private static string? SelectorToString(JsonElement? selector)
        => selector is { } element && element.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? element.GetRawText()
            : null;

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

    private static string RequireServiceId(SpatialDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var serviceId = source.ServiceId ?? source.DatasetId ?? source.RasterId;
        return string.IsNullOrWhiteSpace(serviceId)
            ? throw new InvalidOperationException(
                "Raster source must provide a ServiceId (or DatasetId / RasterId) to address the ImageServer.")
            : serviceId;
    }

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static double? ReadDouble(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number &&
           value.TryGetDouble(out var number)
            ? number
            : null;

    private static long? ReadLong(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Number &&
           value.TryGetInt64(out var number)
            ? number
            : null;
}
