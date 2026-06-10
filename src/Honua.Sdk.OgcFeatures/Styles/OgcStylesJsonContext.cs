// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;
using Honua.Sdk.OgcFeatures.Styles.Models;

namespace Honua.Sdk.OgcFeatures.Styles;

/// <summary>
/// Source-generated JSON serializer context for OGC API - Styles types (AOT-compatible).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OgcStylesList))]
[JsonSerializable(typeof(OgcStyleEntry))]
[JsonSerializable(typeof(OgcStyleMetadata))]
internal sealed partial class OgcStylesJsonContext : JsonSerializerContext
{
}
