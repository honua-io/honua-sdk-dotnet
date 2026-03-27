// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Wfs.Models;

/// <summary>
/// A single WFS feature with geometry and properties.
/// </summary>
public sealed class WfsFeature
{
    /// <summary>The feature identifier (string representation).</summary>
    public string? Id { get; init; }

    /// <summary>The feature geometry, if present.</summary>
    public GeoJsonGeometry? Geometry { get; init; }

    /// <summary>Feature attribute properties.</summary>
    public IReadOnlyDictionary<string, JsonElement> Properties { get; init; } =
        new Dictionary<string, JsonElement>();
}
