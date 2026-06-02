// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures.Styles.Models;

// TODO(#184): dedup these envelopes against Geospatial.Grpc StyleRef once the
// 0.1.0-alpha.2 package is consumable in this repo's restore. The repo currently
// pins Geospatial.Grpc 0.1.0-alpha.1 (Directory.Build.props) and the OgcFeatures
// package does not reference the proto package, so the dedup is deferred.

/// <summary>
/// OGC API - Styles styles list response (<c>GET /ogc/styles</c>).
/// </summary>
public sealed class OgcStylesList
{
    /// <summary>The styles available on the server.</summary>
    [JsonPropertyName("styles")]
    public IReadOnlyList<OgcStyleEntry> Styles { get; init; } = [];

    /// <summary>Optional default style identifier.</summary>
    [JsonPropertyName("default")]
    public string? Default { get; init; }

    /// <summary>Links to related resources (self, alternate, etc.).</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcLink>? Links { get; init; }
}

/// <summary>
/// A single entry in the OGC API - Styles styles list.
/// </summary>
public sealed class OgcStyleEntry
{
    /// <summary>Stable style identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable title for the style.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Links to the stylesheet encodings and style metadata.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcLink> Links { get; init; } = [];
}

/// <summary>
/// OGC API - Styles style metadata response (<c>GET /ogc/styles/{styleId}/metadata</c>).
/// </summary>
public sealed class OgcStyleMetadata
{
    /// <summary>Stable style identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable title for the style.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Description of the style.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Keywords describing the style.</summary>
    [JsonPropertyName("keywords")]
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>License identifier for the style, when known.</summary>
    [JsonPropertyName("license")]
    public string? License { get; init; }

    /// <summary>Style revision number, expressed as a string, when known.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    /// Links incl. the stylesheet encodings, the schema (describedby), and a preview.
    /// </summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<OgcLink> Links { get; init; } = [];
}

/// <summary>
/// The encoding (media type) used to read or write a stylesheet from the
/// OGC API - Styles surface.
/// </summary>
public enum OgcStyleEncoding
{
    /// <summary>MapLibre/Mapbox style JSON (<c>application/vnd.mapbox.style+json</c>); the canonical default.</summary>
    MapboxStyle,

    /// <summary>OGC SLD 1.0 (<c>application/vnd.ogc.sld+xml;version=1.0</c>), derived from the canonical style.</summary>
    Sld10,

    /// <summary>OGC SLD 1.1 (<c>application/vnd.ogc.sld+xml;version=1.1</c>), derived from the canonical style.</summary>
    Sld11
}

/// <summary>
/// A stylesheet returned by <c>GET /ogc/styles/{styleId}</c>, including the raw
/// document content and the negotiated media type.
/// </summary>
public sealed class OgcStylesheet
{
    /// <summary>The stable style identifier the stylesheet was requested for.</summary>
    public string StyleId { get; init; } = string.Empty;

    /// <summary>The encoding the server returned the stylesheet in.</summary>
    public OgcStyleEncoding Encoding { get; init; }

    /// <summary>The media type reported by the server (the response <c>Content-Type</c>).</summary>
    public string? MediaType { get; init; }

    /// <summary>The raw stylesheet document (MapLibre JSON or SLD XML).</summary>
    public string Content { get; init; } = string.Empty;
}
