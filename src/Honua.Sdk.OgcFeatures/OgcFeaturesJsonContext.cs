// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures;

/// <summary>
/// Source-generated JSON serializer context for OGC Features types (AOT-compatible).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OgcLandingPage))]
[JsonSerializable(typeof(OgcConformance))]
[JsonSerializable(typeof(OgcCollection))]
[JsonSerializable(typeof(OgcCollectionsResponse))]
[JsonSerializable(typeof(OgcQueryables))]
[JsonSerializable(typeof(OgcFeatureCollection))]
[JsonSerializable(typeof(OgcFeature))]
[JsonSerializable(typeof(OgcProblemDetails))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class OgcFeaturesJsonContext : JsonSerializerContext
{
}
