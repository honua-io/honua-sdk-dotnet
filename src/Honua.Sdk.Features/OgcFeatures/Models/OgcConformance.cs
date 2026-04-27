// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Features.OgcFeatures.Models;

/// <summary>
/// OGC API conformance declaration listing supported standards.
/// </summary>
public sealed class OgcConformance
{
    /// <summary>URIs of conformance classes implemented by the server.</summary>
    [JsonPropertyName("conformsTo")]
    public IReadOnlyList<string>? ConformsTo { get; init; }
}
