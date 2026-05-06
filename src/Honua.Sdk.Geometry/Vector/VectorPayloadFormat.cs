// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Geometry.Vector;

/// <summary>
/// Vector payload formats understood by the shared payload readers.
/// </summary>
public enum VectorPayloadFormat
{
    /// <summary>No vector payload format has been specified.</summary>
    Unspecified = 0,

    /// <summary>GeoServices/Esri JSON feature payloads.</summary>
    EsriJson = 1,

    /// <summary>GeoJSON Feature or FeatureCollection payloads.</summary>
    GeoJson = 2,

    /// <summary>GML feature or feature collection payloads.</summary>
    Gml = 3
}
