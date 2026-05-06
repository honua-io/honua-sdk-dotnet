// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using NetTopologySuite.Geometries;

namespace Honua.Sdk.Geometry.Vector;

/// <summary>
/// Options used by vector payload readers.
/// </summary>
public sealed record VectorPayloadReadOptions
{
    /// <summary>Optional geometry factory used by JSON and GML readers.</summary>
    public GeometryFactory? GeometryFactory { get; init; }
}
