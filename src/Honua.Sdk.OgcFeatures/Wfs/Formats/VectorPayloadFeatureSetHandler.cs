// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Geometry.Vector;

namespace Honua.Sdk.OgcFeatures.Wfs.Formats;

/// <summary>
/// Deserializes WFS GetFeature responses into the shared typed vector payload model.
/// </summary>
public sealed class VectorPayloadFeatureSetHandler : IWfsOutputFormatHandler<VectorPayloadFeatureSet>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VectorPayloadFeatureSetHandler"/> class.
    /// </summary>
    /// <param name="format">Shared vector payload format to request and parse.</param>
    public VectorPayloadFeatureSetHandler(VectorPayloadFormat format = VectorPayloadFormat.GeoJson)
    {
        Format = format;
        MediaType = WfsVectorFormats.ToOutputFormat(format);
    }

    /// <summary>The shared vector payload format parsed by this handler.</summary>
    public VectorPayloadFormat Format { get; }

    /// <inheritdoc />
    public string MediaType { get; }

    /// <inheritdoc />
    public Task<VectorPayloadFeatureSet> ReadAsync(Stream responseStream, CancellationToken cancellationToken = default)
        => VectorPayloadReaders.ReadAsync(responseStream, Format, cancellationToken: cancellationToken);
}
