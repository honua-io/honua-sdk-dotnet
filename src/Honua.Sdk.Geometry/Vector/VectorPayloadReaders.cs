// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Geometry.Vector;

/// <summary>
/// Factory and convenience methods for shared vector payload readers.
/// </summary>
public static class VectorPayloadReaders
{
    /// <summary>
    /// Gets a reader for a supported vector payload format.
    /// </summary>
    /// <param name="format">The payload format.</param>
    /// <returns>A reader for the requested format.</returns>
    public static IVectorPayloadReader ForFormat(VectorPayloadFormat format) => format switch
    {
        VectorPayloadFormat.EsriJson => EsriJsonVectorPayloadReader.Instance,
        VectorPayloadFormat.GeoJson => GeoJsonVectorPayloadReader.Instance,
        VectorPayloadFormat.Gml => GmlVectorPayloadReader.Instance,
        _ => throw new NotSupportedException($"Vector payload format '{format}' is not supported.")
    };

    /// <summary>
    /// Reads a vector payload stream with the reader selected by format.
    /// </summary>
    /// <param name="stream">Payload stream.</param>
    /// <param name="format">The payload format.</param>
    /// <param name="options">Optional reader settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The typed feature collection.</returns>
    public static Task<VectorPayloadFeatureSet> ReadAsync(
        Stream stream,
        VectorPayloadFormat format,
        VectorPayloadReadOptions? options = null,
        CancellationToken ct = default)
    {
        return ForFormat(format).ReadAsync(stream, options, ct);
    }
}
