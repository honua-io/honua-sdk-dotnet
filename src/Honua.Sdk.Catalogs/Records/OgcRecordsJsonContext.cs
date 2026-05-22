// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Catalogs.Records.Models;

namespace Honua.Sdk.Catalogs.Records;

/// <summary>
/// Source-generated JSON serializer context for OGC Records types.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OgcRecordsLandingPage))]
[JsonSerializable(typeof(OgcRecordsConformance))]
[JsonSerializable(typeof(OgcRecordsCollection))]
[JsonSerializable(typeof(OgcRecordsCollectionsResponse))]
[JsonSerializable(typeof(OgcRecordCollection))]
[JsonSerializable(typeof(OgcRecord))]
[JsonSerializable(typeof(OgcRecordsProblemDetails))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class OgcRecordsJsonContext : JsonSerializerContext
{
}
