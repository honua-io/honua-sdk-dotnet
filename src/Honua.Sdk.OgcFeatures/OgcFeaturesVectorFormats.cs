// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry.Vector;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures;

/// <summary>
/// Maps shared vector payload formats to OGC API Features item formats.
/// </summary>
public static class OgcFeaturesVectorFormats
{
    /// <summary>Vector payload formats supported by OGC API Features typed vector queries.</summary>
    public static IReadOnlyList<VectorPayloadFormat> SupportedFormats { get; } =
        [VectorPayloadFormat.GeoJson, VectorPayloadFormat.Gml];

    /// <summary>
    /// Converts a shared vector format to the OGC API Features format value.
    /// </summary>
    /// <param name="format">Shared vector payload format.</param>
    /// <returns>The OGC API Features format value.</returns>
    public static OgcFeaturesFormat ToOgcFeaturesFormat(VectorPayloadFormat format) => format switch
    {
        VectorPayloadFormat.GeoJson => OgcFeaturesFormat.GeoJson,
        VectorPayloadFormat.Gml => OgcFeaturesFormat.Gml,
        VectorPayloadFormat.EsriJson => throw new NotSupportedException("OGC API Features does not expose Esri JSON item output."),
        _ => throw new NotSupportedException($"Vector payload format '{format}' is not supported by OGC API Features.")
    };

    /// <summary>
    /// Resolves an OGC API Features item format to the shared vector payload reader format.
    /// </summary>
    /// <param name="format">OGC API Features format value.</param>
    /// <returns>The shared vector payload format.</returns>
    public static VectorPayloadFormat FromOgcFeaturesFormat(OgcFeaturesFormat? format) => format switch
    {
        null or OgcFeaturesFormat.GeoJson or OgcFeaturesFormat.Json => VectorPayloadFormat.GeoJson,
        OgcFeaturesFormat.Gml => VectorPayloadFormat.Gml,
        OgcFeaturesFormat.Html => throw new NotSupportedException("HTML item output is not a typed vector payload."),
        OgcFeaturesFormat.Csv => throw new NotSupportedException("CSV vector payload parsing is not implemented by this SDK."),
        OgcFeaturesFormat.FlatGeobuf => throw new NotSupportedException("FlatGeobuf vector payload parsing is not implemented by this SDK."),
        OgcFeaturesFormat.Parquet => throw new NotSupportedException("Parquet vector payload parsing is not implemented by this SDK."),
        _ => throw new NotSupportedException($"OGC API Features format '{format}' is not supported by typed vector queries.")
    };
}
