// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;
using Honua.Sdk.OgcFeatures.Tiles.Models;

namespace Honua.Sdk.OgcFeatures.Tiles;

/// <summary>
/// Source-generated JSON serializer context for OGC API Tiles types (AOT-compatible).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OgcTileSetsList))]
[JsonSerializable(typeof(OgcTileSetItem))]
[JsonSerializable(typeof(OgcTileMatrixSetLimit))]
internal sealed partial class OgcTilesJsonContext : JsonSerializerContext
{
}
