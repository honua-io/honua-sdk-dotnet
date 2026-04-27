// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.OgcFeatures.Models;

/// <summary>
/// OGC API landing page response.
/// </summary>
public sealed class OgcLandingPage
{
    /// <summary>The title of the API.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>A description of the API.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Navigation links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcLink>? Links { get; init; }
}
