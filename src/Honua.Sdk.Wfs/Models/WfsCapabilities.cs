// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Wfs.Models;

/// <summary>
/// Parsed WFS GetCapabilities response.
/// </summary>
public sealed class WfsCapabilities
{
    /// <summary>WFS service version (e.g. "2.0.0").</summary>
    public string Version { get; init; } = "";

    /// <summary>Service title.</summary>
    public string? Title { get; init; }

    /// <summary>Service abstract/description.</summary>
    public string? Abstract { get; init; }

    /// <summary>OGC service type identifier.</summary>
    public string? ServiceType { get; init; }

    /// <summary>OGC service type version.</summary>
    public string? ServiceTypeVersion { get; init; }

    /// <summary>Feature types advertised by the service.</summary>
    public IReadOnlyList<WfsFeatureType> FeatureTypes { get; init; } = [];

    /// <summary>Output formats supported globally by the service.</summary>
    public IReadOnlyList<string> OutputFormats { get; init; } = [];
}
