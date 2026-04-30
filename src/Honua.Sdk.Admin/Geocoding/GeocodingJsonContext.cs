// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Geocoding;

/// <summary>
/// Source-generated JSON serializer context for GeoServices geocoding responses.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GeoServicesFindAddressCandidatesResponse))]
[JsonSerializable(typeof(GeoServicesReverseGeocodeResponse))]
[JsonSerializable(typeof(GeoServicesSuggestResponse))]
[JsonSerializable(typeof(GeoServicesRequestPoint))]
[JsonSerializable(typeof(GeoServicesRequestExtent))]
[JsonSerializable(typeof(GeoServicesBatchGeocodeRequest))]
[JsonSerializable(typeof(GeoServicesBatchGeocodeResponse))]
internal sealed partial class GeocodingJsonContext : JsonSerializerContext
{
}
