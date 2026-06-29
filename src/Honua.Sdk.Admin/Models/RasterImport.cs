// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Request to import (upload) a raster file into PostGIS via the admin raster import endpoint
/// (<c>POST /api/v1/admin/import/raster</c>). The raster bytes are sent as a multipart file part;
/// optional world-file and projection sidecars are sent as additional file parts.
/// </summary>
/// <remarks>
/// This is the write/output half of the raster geoprocessing round-trip. The read half lives on the
/// read-only <c>IHonuaRasterDataClient</c> (ImageServer-backed metadata, statistics, and windowed reads).
/// </remarks>
public sealed class RasterImportRequest
{
    /// <summary>
    /// The raster content stream (GeoTIFF / PNG / JPEG bytes). The caller retains ownership of the
    /// stream; the client reads but does not dispose it.
    /// </summary>
    public required Stream Content { get; init; }

    /// <summary>
    /// The raster file name including extension (for example <c>elevation.tif</c>). The server detects
    /// the format from the extension (<c>.tif</c>, <c>.tiff</c>, <c>.png</c>, <c>.jpg</c>, <c>.jpeg</c>).
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The target layer identifier to associate the imported raster with.
    /// </summary>
    public required int LayerId { get; init; }

    /// <summary>
    /// A human-readable name for the imported raster.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description of the imported raster.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Optional spatial reference (EPSG SRID). Required when the source raster lacks embedded
    /// georeferencing and no projection sidecar is supplied.
    /// </summary>
    public int? Srid { get; init; }

    /// <summary>
    /// Optional acquisition timestamp for the raster.
    /// </summary>
    public DateTimeOffset? AcquisitionDate { get; init; }

    /// <summary>
    /// Optional world-file sidecar content (for PNG/JPEG sources that carry georeferencing in a
    /// <c>.pgw</c>/<c>.jgw</c>/<c>.wld</c> sidecar rather than embedded in the raster).
    /// </summary>
    public string? WorldFileContent { get; init; }

    /// <summary>
    /// Optional projection (<c>.prj</c>) sidecar content describing the coordinate reference system.
    /// </summary>
    public string? ProjectionContent { get; init; }

    /// <summary>
    /// Optional tile pyramid zoom levels to generate (0-24). Defaults to the server's pyramid policy
    /// when omitted.
    /// </summary>
    public IReadOnlyList<int>? TileZoomLevels { get; init; }

    /// <summary>
    /// Optional MIME content type for the raster part. Defaults to <c>application/octet-stream</c>.
    /// </summary>
    public string? ContentType { get; init; }
}

/// <summary>
/// Result of an admin raster import operation.
/// </summary>
public sealed record RasterImportResult
{
    /// <summary>
    /// Whether the import succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The identifier of the imported raster, when available.
    /// </summary>
    public long? RasterId { get; init; }

    /// <summary>
    /// The target layer identifier.
    /// </summary>
    public int LayerId { get; init; }

    /// <summary>
    /// The name of the imported raster.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The detected source format (for example <c>GeoTiff</c>, <c>PngWorldFile</c>, <c>JpegWorldFile</c>).
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// The spatial reference (EPSG SRID) of the imported raster, when resolved.
    /// </summary>
    public int? Srid { get; init; }

    /// <summary>
    /// Raster width in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Raster height in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Number of bands in the imported raster.
    /// </summary>
    public int BandCount { get; init; }

    /// <summary>
    /// Number of bands for which statistics were computed.
    /// </summary>
    public int StatisticsBands { get; init; }

    /// <summary>
    /// Number of tiles generated during import.
    /// </summary>
    public int TilesGenerated { get; init; }

    /// <summary>
    /// Error message when <see cref="Success"/> is <c>false</c>.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Non-fatal warnings emitted during import.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Total import duration.
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Supported raster import formats and their human-readable descriptions.
/// </summary>
public sealed record RasterFormatsResponse
{
    /// <summary>
    /// Supported file extensions (for example <c>.tif</c>, <c>.png</c>, <c>.jpg</c>).
    /// </summary>
    public IReadOnlyList<string> SupportedExtensions { get; init; } = [];

    /// <summary>
    /// Format descriptions keyed by file extension.
    /// </summary>
    public IReadOnlyDictionary<string, string> FormatDescriptions { get; init; }
        = new Dictionary<string, string>();
}
