// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.GeoServices.FeatureServer.Models;
using Honua.Sdk.Geometry.Vector;

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>
/// Maps shared vector payload formats to GeoServices FeatureServer query formats.
/// </summary>
public static class FeatureServerVectorFormats
{
    /// <summary>Vector payload formats supported by FeatureServer typed vector queries.</summary>
    public static IReadOnlyList<VectorPayloadFormat> SupportedFormats { get; } =
        [VectorPayloadFormat.EsriJson, VectorPayloadFormat.GeoJson];

    /// <summary>
    /// Converts a shared vector format to the FeatureServer query format value.
    /// </summary>
    /// <param name="format">Shared vector payload format.</param>
    /// <returns>The FeatureServer format value.</returns>
    public static FeatureServerFormat ToFeatureServerFormat(VectorPayloadFormat format) => format switch
    {
        VectorPayloadFormat.EsriJson => FeatureServerFormat.Json,
        VectorPayloadFormat.GeoJson => FeatureServerFormat.GeoJson,
        VectorPayloadFormat.Gml => throw new NotSupportedException("FeatureServer does not expose GML query output."),
        _ => throw new NotSupportedException($"Vector payload format '{format}' is not supported by FeatureServer.")
    };

    /// <summary>
    /// Resolves a FeatureServer query format to the shared vector payload reader format.
    /// </summary>
    /// <param name="format">FeatureServer query format.</param>
    /// <returns>The shared vector payload format.</returns>
    public static VectorPayloadFormat FromFeatureServerFormat(FeatureServerFormat? format) => format switch
    {
        null or FeatureServerFormat.Json => VectorPayloadFormat.EsriJson,
        FeatureServerFormat.GeoJson => VectorPayloadFormat.GeoJson,
        FeatureServerFormat.Pbf => throw new NotSupportedException("PBF vector payload parsing is not implemented by this SDK."),
        FeatureServerFormat.FlatGeobuf => throw new NotSupportedException("FlatGeobuf vector payload parsing is not implemented by this SDK."),
        FeatureServerFormat.Parquet => throw new NotSupportedException("Parquet vector payload parsing is not implemented by this SDK."),
        _ => throw new NotSupportedException($"FeatureServer format '{format}' is not supported by typed vector queries.")
    };
}
