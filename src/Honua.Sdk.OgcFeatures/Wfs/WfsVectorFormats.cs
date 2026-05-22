// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry.Vector;

namespace Honua.Sdk.OgcFeatures.Wfs;

/// <summary>
/// Maps shared vector payload formats to WFS OUTPUTFORMAT values.
/// </summary>
public static class WfsVectorFormats
{
    /// <summary>Default WFS GeoJSON output format value.</summary>
    public const string GeoJsonOutputFormat = "application/geo+json";

    /// <summary>Default WFS GML 3.2 output format value.</summary>
    public const string GmlOutputFormat = "application/gml+xml; version=3.2";

    /// <summary>Vector payload formats supported by WFS typed vector queries.</summary>
    public static IReadOnlyList<VectorPayloadFormat> SupportedFormats { get; } =
        [VectorPayloadFormat.GeoJson, VectorPayloadFormat.Gml];

    /// <summary>
    /// Converts a shared vector format to a WFS OUTPUTFORMAT value.
    /// </summary>
    /// <param name="format">Shared vector payload format.</param>
    /// <returns>The WFS OUTPUTFORMAT value.</returns>
    public static string ToOutputFormat(VectorPayloadFormat format) => format switch
    {
        VectorPayloadFormat.GeoJson => GeoJsonOutputFormat,
        VectorPayloadFormat.Gml => GmlOutputFormat,
        VectorPayloadFormat.EsriJson => throw new NotSupportedException("WFS does not expose Esri JSON GetFeature output."),
        _ => throw new NotSupportedException($"Vector payload format '{format}' is not supported by WFS.")
    };
}
