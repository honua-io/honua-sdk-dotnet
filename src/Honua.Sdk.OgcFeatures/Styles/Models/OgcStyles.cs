// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures.Styles.Models;

// Resolves TODO(#184) — relationship to Geospatial.Grpc StyleRef / StyleEncoding
// (geospatial.v1, available from Geospatial.Grpc 0.1.0-alpha.2):
//
// These types intentionally stay separate from the generated proto messages and
// the OgcFeatures package does NOT take a dependency on Geospatial.Grpc:
//
//   * Layering. OgcFeatures is the REST-over-HttpClient surface and ships to
//     browser/WASM consumers that deliberately exclude the gRPC/proto stack.
//     Referencing Geospatial.Grpc here would force Google.Protobuf onto that
//     subset for no shared serialization benefit.
//   * Different wire shapes. The OGC API - Styles responses below are
//     link-driven (HATEOAS) projections: a styles list / metadata document plus
//     `links` that enumerate the available stylesheet encodings. StyleRef is a
//     self-contained, encoding-list-centric message (an `encodings` collection
//     of inline/stored bodies, no links). They model the same logical style at
//     two different layers, so neither is a drop-in for the other.
//   * No gRPC style surface exists in this SDK yet. There is currently no
//     gRPC client that exposes StyleRef, so there is nothing in this repo for a
//     REST DTO to be deduped against; StyleRef is consumed only inside
//     Honua.Sdk.Grpc (the one package that references Geospatial.Grpc).
//
// The one concept the two layers genuinely share is the *encoding identifier*
// vocabulary. To keep that aligned without coupling packages, the
// <see cref="OgcStyleEncoding"/> members below map 1:1 onto the canonical
// StyleEncoding.encoding values defined by geospatial.v1.StyleEncoding
// (`mapbox-style`, `sld-1.0.0`, `sld-1.1.0`); see
// <see cref="OgcStyleEncodingExtensions.ToCanonicalEncodingId"/>.

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
/// Helpers for <see cref="OgcStyleEncoding"/> that align the REST encoding enum
/// with the canonical encoding identifiers used by the gRPC style contract
/// (<c>geospatial.v1.StyleEncoding.encoding</c> in <c>Geospatial.Grpc</c>).
/// </summary>
public static class OgcStyleEncodingExtensions
{
    /// <summary>
    /// Canonical encoding identifier for MapLibre/Mapbox style JSON, matching
    /// <c>geospatial.v1.StyleEncoding.encoding == "mapbox-style"</c>.
    /// </summary>
    public const string MapboxStyleEncodingId = "mapbox-style";

    /// <summary>
    /// Canonical encoding identifier for OGC SLD 1.0, matching
    /// <c>geospatial.v1.StyleEncoding.encoding == "sld-1.0.0"</c>.
    /// </summary>
    public const string Sld10EncodingId = "sld-1.0.0";

    /// <summary>
    /// Canonical encoding identifier for OGC SLD 1.1, matching
    /// <c>geospatial.v1.StyleEncoding.encoding == "sld-1.1.0"</c>.
    /// </summary>
    public const string Sld11EncodingId = "sld-1.1.0";

    /// <summary>
    /// Maps an <see cref="OgcStyleEncoding"/> to the canonical encoding
    /// identifier shared with the gRPC style contract
    /// (<c>geospatial.v1.StyleEncoding.encoding</c>). This keeps the REST and
    /// gRPC layers using one encoding vocabulary without coupling the packages.
    /// </summary>
    /// <param name="encoding">The REST stylesheet encoding.</param>
    /// <returns>The canonical <c>StyleEncoding.encoding</c> identifier.</returns>
    public static string ToCanonicalEncodingId(this OgcStyleEncoding encoding) => encoding switch
    {
        OgcStyleEncoding.MapboxStyle => MapboxStyleEncodingId,
        OgcStyleEncoding.Sld10 => Sld10EncodingId,
        OgcStyleEncoding.Sld11 => Sld11EncodingId,
        _ => MapboxStyleEncodingId,
    };
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
