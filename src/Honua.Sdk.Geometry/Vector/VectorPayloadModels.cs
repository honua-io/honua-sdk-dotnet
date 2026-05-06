// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using NetTopologySuite.Geometries;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace Honua.Sdk.Geometry.Vector;

/// <summary>
/// A link advertised by a vector payload, normally copied from GeoJSON or OGC responses.
/// </summary>
public sealed record VectorPayloadLink
{
    /// <summary>The link target.</summary>
    public required string Href { get; init; }

    /// <summary>The link relation type.</summary>
    public string? Rel { get; init; }

    /// <summary>The linked representation media type.</summary>
    public string? Type { get; init; }

    /// <summary>A human-readable link title.</summary>
    public string? Title { get; init; }
}

/// <summary>
/// A typed vector feature parsed from a protocol payload.
/// </summary>
public sealed record VectorPayloadFeature
{
    /// <summary>Provider feature identifier, when present.</summary>
    public string? Id { get; init; }

    /// <summary>Feature attributes or properties as JSON values.</summary>
    public IReadOnlyDictionary<string, JsonElement> Attributes { get; init; } =
        new Dictionary<string, JsonElement>();

    /// <summary>Feature geometry parsed into NetTopologySuite, when present and supported.</summary>
    public NtsGeometry? Geometry { get; init; }

    /// <summary>Raw geometry JSON for JSON-backed formats, when present.</summary>
    public JsonElement? GeometryJson { get; init; }

    /// <summary>Native feature type name for XML/GML payloads, when available.</summary>
    public string? NativeTypeName { get; init; }

    /// <summary>Native CRS identifier read from the feature geometry, when available.</summary>
    public string? Crs { get; init; }
}

/// <summary>
/// A typed vector feature collection parsed from a protocol payload.
/// </summary>
public sealed record VectorPayloadFeatureSet
{
    /// <summary>The payload format used to parse the response.</summary>
    public required VectorPayloadFormat Format { get; init; }

    /// <summary>Features returned in this payload.</summary>
    public IReadOnlyList<VectorPayloadFeature> Features { get; init; } = [];

    /// <summary>Total matching features when the provider reports it.</summary>
    public long? NumberMatched { get; init; }

    /// <summary>Number of features returned by this payload.</summary>
    public int NumberReturned { get; init; }

    /// <summary>Whether the provider indicates more results may be available.</summary>
    public bool HasMoreResults { get; init; }

    /// <summary>FeatureServer object ID field name, when present.</summary>
    public string? ObjectIdFieldName { get; init; }

    /// <summary>Matching object IDs for ID-only responses, when present.</summary>
    public IReadOnlyList<long> ObjectIds { get; init; } = [];

    /// <summary>Provider-reported extent, when present.</summary>
    public Envelope? Extent { get; init; }

    /// <summary>Collection CRS identifier, when present.</summary>
    public string? Crs { get; init; }

    /// <summary>Payload links such as GeoJSON or OGC next-page links.</summary>
    public IReadOnlyList<VectorPayloadLink> Links { get; init; } = [];
}
