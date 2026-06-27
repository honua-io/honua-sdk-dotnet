// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;
using Honua.Sdk.Catalogs.Stac.Models;

namespace Honua.Sdk.Catalogs.Stac;

/// <summary>
/// Source-generated JSON serializer context for STAC types.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(StacLandingPage))]
[JsonSerializable(typeof(StacCollection))]
[JsonSerializable(typeof(StacCollectionsResponse))]
[JsonSerializable(typeof(StacItemCollection))]
[JsonSerializable(typeof(StacItem))]
[JsonSerializable(typeof(StacAsset))]
[JsonSerializable(typeof(StacSearchRequest))]
[JsonSerializable(typeof(StacFields))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class StacJsonContext : JsonSerializerContext
{
}
