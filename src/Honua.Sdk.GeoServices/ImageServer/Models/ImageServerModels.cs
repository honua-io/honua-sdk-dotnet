// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.GeoServices.ImageServer;

/// <summary>
/// Spatial extent of an ImageServer service, with an optional spatial reference well-known id.
/// </summary>
public sealed class ImageServerExtent
{
    /// <summary>Minimum x coordinate.</summary>
    public double XMin { get; init; }

    /// <summary>Minimum y coordinate.</summary>
    public double YMin { get; init; }

    /// <summary>Maximum x coordinate.</summary>
    public double XMax { get; init; }

    /// <summary>Maximum y coordinate.</summary>
    public double YMax { get; init; }

    /// <summary>Spatial reference well-known id, when present.</summary>
    public int? SpatialReferenceWkid { get; init; }
}

/// <summary>
/// Parsed ImageServer service description (from <c>GET .../ImageServer?f=json</c>).
/// </summary>
public sealed class ImageServerMetadata
{
    /// <summary>The resolved service id used to address the ImageServer.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Human-readable service description.</summary>
    public string? ServiceDescription { get; init; }

    /// <summary>Service name.</summary>
    public string? Name { get; init; }

    /// <summary>Pixel type (e.g. <c>U8</c>, <c>F32</c>).</summary>
    public string? PixelType { get; init; }

    /// <summary>Full extent of the image service.</summary>
    public ImageServerExtent? Extent { get; init; }

    /// <summary>Number of raster bands.</summary>
    public int? BandCount { get; init; }

    /// <summary>Per-band minimum pixel values.</summary>
    public IReadOnlyList<double>? MinValues { get; init; }

    /// <summary>Per-band maximum pixel values.</summary>
    public IReadOnlyList<double>? MaxValues { get; init; }

    /// <summary>Supported query formats (e.g. <c>JSON, geoJSON</c>).</summary>
    public IReadOnlyList<string>? SupportedQueryFormats { get; init; }

    /// <summary>Supported image output formats (e.g. <c>PNG, JPG, ...</c>).</summary>
    public IReadOnlyList<string>? SupportedImageFormatTypes { get; init; }

    /// <summary>Allowed mosaic methods.</summary>
    public IReadOnlyList<string>? AllowedMosaicMethods { get; init; }

    /// <summary>Default mosaic method.</summary>
    public string? DefaultMosaicMethod { get; init; }

    /// <summary>Whether a raster attribute table is available.</summary>
    public bool? HasRasterAttributeTable { get; init; }

    /// <summary>Whether histograms are available.</summary>
    public bool? HasHistograms { get; init; }

    /// <summary>Raw service description JSON.</summary>
    public JsonElement RawResponse { get; init; }
}

/// <summary>
/// Parameters for an ImageServer <c>exportImage</c> request.
/// </summary>
public sealed class ExportImageRequest
{
    /// <summary>Optional service id override; falls back to the metadata service id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Export bounding box <c>xmin,ymin,xmax,ymax</c>. Required.</summary>
    public required ImageServerExtent BoundingBox { get; init; }

    /// <summary>Spatial reference wkid of the bounding box.</summary>
    public int? BoundingBoxSpatialReference { get; init; }

    /// <summary>Output image spatial reference wkid.</summary>
    public int? ImageSpatialReference { get; init; }

    /// <summary>Output image width in pixels. Required.</summary>
    public required int Width { get; init; }

    /// <summary>Output image height in pixels. Required.</summary>
    public required int Height { get; init; }

    /// <summary>Output format (e.g. <c>png</c>, <c>png24</c>, <c>jpg</c>, <c>tiff</c>).</summary>
    public string Format { get; init; } = "png";

    /// <summary>Optional pixel type override.</summary>
    public string? PixelType { get; init; }

    /// <summary>Optional no-data value(s).</summary>
    public string? NoData { get; init; }

    /// <summary>Optional no-data interpretation (e.g. <c>esriNoDataMatchAny</c>).</summary>
    public string? NoDataInterpretation { get; init; }

    /// <summary>Optional resampling interpolation (e.g. <c>RSP_BilinearInterpolation</c>).</summary>
    public string? Interpolation { get; init; }

    /// <summary>Optional mosaic rule JSON.</summary>
    public string? MosaicRule { get; init; }

    /// <summary>Optional rendering rule JSON.</summary>
    public string? RenderingRule { get; init; }

    /// <summary>Additional GeoServices parameters appended verbatim.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Result of an <c>exportImage</c> request issued with <c>f=image</c>. The caller owns the stream.
/// </summary>
public sealed class ExportImageResult : IDisposable, IAsyncDisposable
{
    private readonly Stream _content;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportImageResult"/> class.
    /// </summary>
    /// <param name="content">The raster content stream (owned by this result).</param>
    /// <param name="contentType">The response content type.</param>
    /// <param name="contentLength">The response content length, if known.</param>
    public ExportImageResult(Stream content, string? contentType, long? contentLength)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        ContentType = contentType;
        ContentLength = contentLength;
    }

    /// <summary>The raster image stream. Disposing this result disposes the stream.</summary>
    public Stream Content => _content;

    /// <summary>The response content type (e.g. <c>image/png</c>).</summary>
    public string? ContentType { get; }

    /// <summary>The response content length, if reported by the server.</summary>
    public long? ContentLength { get; }

    /// <inheritdoc />
    public void Dispose() => _content.Dispose();

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _content.DisposeAsync();
}

/// <summary>
/// Result of an <c>exportImage</c> request issued with <c>f=json</c> (href + extent metadata).
/// </summary>
public sealed class ExportImageMetadata
{
    /// <summary>URL of the exported image.</summary>
    public string? Href { get; init; }

    /// <summary>Image width in pixels.</summary>
    public int? Width { get; init; }

    /// <summary>Image height in pixels.</summary>
    public int? Height { get; init; }

    /// <summary>Extent covered by the exported image.</summary>
    public ImageServerExtent? Extent { get; init; }

    /// <summary>Map scale of the exported image.</summary>
    public double? Scale { get; init; }

    /// <summary>Raw JSON response.</summary>
    public JsonElement RawResponse { get; init; }
}

/// <summary>
/// Parameters for an ImageServer <c>identify</c> request at a point location.
/// </summary>
public sealed class IdentifyImageRequest
{
    /// <summary>Optional service id override; falls back to the metadata service id.</summary>
    public string? ServiceId { get; init; }

    /// <summary>X coordinate of the identify point. Required.</summary>
    public required double X { get; init; }

    /// <summary>Y coordinate of the identify point. Required.</summary>
    public required double Y { get; init; }

    /// <summary>Spatial reference wkid of the identify point.</summary>
    public int? SpatialReferenceWkid { get; init; }

    /// <summary>Optional mosaic rule JSON.</summary>
    public string? MosaicRule { get; init; }

    /// <summary>Optional rendering rule JSON.</summary>
    public string? RenderingRule { get; init; }

    /// <summary>Optional pixel size <c>x,y</c>.</summary>
    public string? PixelSize { get; init; }

    /// <summary>Additional GeoServices parameters appended verbatim.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Parsed ImageServer <c>identify</c> response.
/// </summary>
public sealed class IdentifyImageResult
{
    /// <summary>Object id of the identified catalog item, when present.</summary>
    public long? ObjectId { get; init; }

    /// <summary>Name of the identified item.</summary>
    public string? Name { get; init; }

    /// <summary>Pixel value(s) at the identified location, as returned by the service.</summary>
    public string? Value { get; init; }

    /// <summary>Location text, when present.</summary>
    public string? Location { get; init; }

    /// <summary>Raw JSON response, including any <c>catalogItems</c>.</summary>
    public JsonElement RawResponse { get; init; }
}
