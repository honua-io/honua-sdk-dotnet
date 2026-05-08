// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.OgcRecords.Models;

/// <summary>
/// OGC API Records conformance response.
/// </summary>
public sealed class OgcRecordsConformance
{
    /// <summary>Supported conformance class URIs.</summary>
    [JsonPropertyName("conformsTo")]
    public IReadOnlyList<string> ConformsTo { get; init; } = [];
}
