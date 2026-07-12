// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Geometry;

[JsonSerializable(typeof(NetTopologySuite.Geometries.Geometry))]
internal sealed partial class GeoJsonGeometryJsonContext : JsonSerializerContext
{
}
