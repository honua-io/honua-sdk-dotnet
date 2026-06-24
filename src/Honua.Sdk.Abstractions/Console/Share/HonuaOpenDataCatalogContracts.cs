// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Console.Share;

/// <summary>
/// DCAT-US 3.0 / Project Open Data <c>data.json</c> catalog document. Property
/// names follow the data.json schema so a published catalog round-trips through
/// standard open-data harvesters. Maps to the server <c>dcatCatalog</c> contract.
/// </summary>
public sealed record HonuaDcatCatalog
{
    /// <summary>JSON-LD context URI for the data.json schema.</summary>
    [JsonPropertyName("@context")]
    public string? Context { get; init; }

    /// <summary>Catalog RDF type (<c>dcat:Catalog</c>).</summary>
    [JsonPropertyName("@type")]
    public string? Type { get; init; }

    /// <summary>Conforms-to schema version URI.</summary>
    [JsonPropertyName("conformsTo")]
    public string? ConformsTo { get; init; }

    /// <summary>Catalog datasets.</summary>
    [JsonPropertyName("dataset")]
    public IReadOnlyList<HonuaDcatDataset> Dataset { get; init; } = [];
}

/// <summary>
/// A single DCAT-US dataset entry. Maps to the server <c>dcatDataset</c> contract.
/// </summary>
public sealed record HonuaDcatDataset
{
    /// <summary>RDF type (<c>dcat:Dataset</c>).</summary>
    [JsonPropertyName("@type")]
    public string? Type { get; init; }

    /// <summary>Stable dataset identifier.</summary>
    [JsonPropertyName("identifier")]
    public required string Identifier { get; init; }

    /// <summary>Dataset title.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>Dataset description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Discovery keywords.</summary>
    [JsonPropertyName("keyword")]
    public IReadOnlyList<string>? Keyword { get; init; }

    /// <summary>Last-modified timestamp (ISO-8601).</summary>
    [JsonPropertyName("modified")]
    public string? Modified { get; init; }

    /// <summary>Publishing organization.</summary>
    [JsonPropertyName("publisher")]
    public HonuaDcatPublisher? Publisher { get; init; }

    /// <summary>Point of contact.</summary>
    [JsonPropertyName("contactPoint")]
    public HonuaDcatContactPoint? ContactPoint { get; init; }

    /// <summary>License URL or SPDX identifier.</summary>
    [JsonPropertyName("license")]
    public string? License { get; init; }

    /// <summary>Public landing page URL.</summary>
    [JsonPropertyName("landingPage")]
    public string? LandingPage { get; init; }

    /// <summary>Spatial coverage as a DCAT bbox string (W,S,E,N).</summary>
    [JsonPropertyName("spatial")]
    public string? Spatial { get; init; }

    /// <summary>Temporal coverage as an ISO-8601 interval.</summary>
    [JsonPropertyName("temporal")]
    public string? Temporal { get; init; }

    /// <summary>Access level (open-data datasets are always <c>public</c>).</summary>
    [JsonPropertyName("accessLevel")]
    public string? AccessLevel { get; init; }

    /// <summary>Distributions / access links.</summary>
    [JsonPropertyName("distribution")]
    public IReadOnlyList<HonuaDcatDistribution>? Distribution { get; init; }
}

/// <summary>DCAT publishing organization. Maps to the server <c>dcatPublisher</c> contract.</summary>
public sealed record HonuaDcatPublisher
{
    /// <summary>RDF type (<c>org:Organization</c>).</summary>
    [JsonPropertyName("@type")]
    public string? Type { get; init; }

    /// <summary>Organization name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

/// <summary>DCAT point-of-contact (vCard). Maps to the server <c>dcatContactPoint</c> contract.</summary>
public sealed record HonuaDcatContactPoint
{
    /// <summary>RDF type (<c>vcard:Contact</c>).</summary>
    [JsonPropertyName("@type")]
    public string? Type { get; init; }

    /// <summary>Contact full name.</summary>
    [JsonPropertyName("fn")]
    public required string Fn { get; init; }

    /// <summary>Mailto-prefixed contact email, when an email was supplied.</summary>
    [JsonPropertyName("hasEmail")]
    public string? HasEmail { get; init; }
}

/// <summary>DCAT distribution (access link). Maps to the server <c>dcatDistribution</c> contract.</summary>
public sealed record HonuaDcatDistribution
{
    /// <summary>RDF type (<c>dcat:Distribution</c>).</summary>
    [JsonPropertyName("@type")]
    public string? Type { get; init; }

    /// <summary>Distribution title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Access URL.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Mirrors the DCAT data.json contract, which carries accessURL as a verbatim string.")]
    [JsonPropertyName("accessURL")]
    public required string AccessUrl { get; init; }

    /// <summary>IANA media type.</summary>
    [JsonPropertyName("mediaType")]
    public string? MediaType { get; init; }

    /// <summary>Short format label.</summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }
}

/// <summary>
/// Schema.org <c>Dataset</c> JSON-LD projection for an open-data item. Maps to
/// the server <c>schemaOrgDataset</c> contract.
/// </summary>
public sealed record HonuaSchemaOrgDataset
{
    /// <summary>JSON-LD context (<c>https://schema.org</c>).</summary>
    [JsonPropertyName("@context")]
    public string? Context { get; init; }

    /// <summary>Schema.org type (<c>Dataset</c>).</summary>
    [JsonPropertyName("@type")]
    public string? Type { get; init; }

    /// <summary>Dataset name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Dataset description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Keywords.</summary>
    [JsonPropertyName("keywords")]
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>License URL/identifier.</summary>
    [JsonPropertyName("license")]
    public string? License { get; init; }

    /// <summary>Landing page URL.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Mirrors the Schema.org Dataset contract, which carries the landing-page URL as a verbatim string.")]
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>Spatial coverage as a bounding-box place.</summary>
    [JsonPropertyName("spatialCoverage")]
    public HonuaSchemaOrgPlace? SpatialCoverage { get; init; }

    /// <summary>Temporal coverage as an ISO-8601 interval string.</summary>
    [JsonPropertyName("temporalCoverage")]
    public string? TemporalCoverage { get; init; }

    /// <summary>Publisher organization.</summary>
    [JsonPropertyName("publisher")]
    public HonuaSchemaOrgOrganization? Publisher { get; init; }

    /// <summary>Distributions.</summary>
    [JsonPropertyName("distribution")]
    public IReadOnlyList<HonuaSchemaOrgDataDownload>? Distribution { get; init; }
}

/// <summary>Schema.org <c>Place</c> with a bounding-box geo shape.</summary>
public sealed record HonuaSchemaOrgPlace
{
    /// <summary>Schema.org type (<c>Place</c>).</summary>
    [JsonPropertyName("@type")]
    public string? Type { get; init; }

    /// <summary>Bounding-box geo shape.</summary>
    [JsonPropertyName("geo")]
    public required HonuaSchemaOrgGeoShape Geo { get; init; }
}

/// <summary>Schema.org <c>GeoShape</c> box.</summary>
public sealed record HonuaSchemaOrgGeoShape
{
    /// <summary>Schema.org type (<c>GeoShape</c>).</summary>
    [JsonPropertyName("@type")]
    public string? Type { get; init; }

    /// <summary>Box as "minLat minLon maxLat maxLon".</summary>
    [JsonPropertyName("box")]
    public required string Box { get; init; }
}

/// <summary>Schema.org <c>Organization</c>.</summary>
public sealed record HonuaSchemaOrgOrganization
{
    /// <summary>Schema.org type (<c>Organization</c>).</summary>
    [JsonPropertyName("@type")]
    public string? Type { get; init; }

    /// <summary>Organization name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

/// <summary>Schema.org <c>DataDownload</c>.</summary>
public sealed record HonuaSchemaOrgDataDownload
{
    /// <summary>Schema.org type (<c>DataDownload</c>).</summary>
    [JsonPropertyName("@type")]
    public string? Type { get; init; }

    /// <summary>Download/access URL.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Mirrors the Schema.org DataDownload contract, which carries contentUrl as a verbatim string.")]
    [JsonPropertyName("contentUrl")]
    public required string ContentUrl { get; init; }

    /// <summary>Encoding/media type.</summary>
    [JsonPropertyName("encodingFormat")]
    public string? EncodingFormat { get; init; }

    /// <summary>Distribution name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

/// <summary>Minimal STAC link object. Maps to the server <c>stacProjectionLink</c> contract.</summary>
public sealed record HonuaStacLink
{
    /// <summary>Link relation type (for example <c>self</c>, <c>root</c>, <c>child</c>, <c>item</c>).</summary>
    [JsonPropertyName("rel")]
    public required string Rel { get; init; }

    /// <summary>Link target.</summary>
    [JsonPropertyName("href")]
    public required string Href { get; init; }

    /// <summary>Optional media type of the target.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Optional human-readable link title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }
}

/// <summary>STAC asset object (distribution link). Maps to the server <c>stacProjectionAsset</c> contract.</summary>
public sealed record HonuaStacAsset
{
    /// <summary>Asset href.</summary>
    [JsonPropertyName("href")]
    public required string Href { get; init; }

    /// <summary>Asset title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Asset media type.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Asset roles (for example <c>data</c>).</summary>
    [JsonPropertyName("roles")]
    public IReadOnlyList<string>? Roles { get; init; }
}

/// <summary>
/// STAC Catalog projection over the published open-data items. Maps to the
/// server <c>stacProjectionCatalog</c> contract.
/// </summary>
public sealed record HonuaStacCatalog
{
    /// <summary>STAC spec version.</summary>
    [JsonPropertyName("stac_version")]
    public string? StacVersion { get; init; }

    /// <summary>STAC entity type (<c>Catalog</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Catalog id.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Catalog title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Catalog description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Navigation links (self/root plus a child link per published collection).</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<HonuaStacLink> Links { get; init; } = [];
}

/// <summary>STAC spatial extent: an array of bbox arrays (W,S,E,N).</summary>
public sealed record HonuaStacSpatialExtent
{
    /// <summary>Bounding boxes; the first is the overall extent.</summary>
    [JsonPropertyName("bbox")]
    public IReadOnlyList<IReadOnlyList<double>> Bbox { get; init; } = [];
}

/// <summary>STAC temporal extent: an array of [start, end] RFC-3339 interval arrays.</summary>
public sealed record HonuaStacTemporalExtent
{
    /// <summary>Intervals; null bounds denote open-ended.</summary>
    [JsonPropertyName("interval")]
    public IReadOnlyList<IReadOnlyList<string?>> Interval { get; init; } = [];
}

/// <summary>STAC collection extent (spatial bbox plus temporal interval).</summary>
public sealed record HonuaStacExtent
{
    /// <summary>Spatial extent.</summary>
    [JsonPropertyName("spatial")]
    public required HonuaStacSpatialExtent Spatial { get; init; }

    /// <summary>Temporal extent.</summary>
    [JsonPropertyName("temporal")]
    public required HonuaStacTemporalExtent Temporal { get; init; }
}

/// <summary>
/// STAC Collection projection for a single published open-data item. Maps to the
/// server <c>stacProjectionCollection</c> contract.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "STAC specification type name (Collection).")]
public sealed record HonuaStacCollection
{
    /// <summary>STAC spec version.</summary>
    [JsonPropertyName("stac_version")]
    public string? StacVersion { get; init; }

    /// <summary>STAC entity type (<c>Collection</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Collection id.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Collection title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Collection description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>License URL/SPDX identifier (STAC requires a value; defaults to <c>other</c>).</summary>
    [JsonPropertyName("license")]
    public string? License { get; init; }

    /// <summary>Discovery keywords.</summary>
    [JsonPropertyName("keywords")]
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>Spatial plus temporal extents.</summary>
    [JsonPropertyName("extent")]
    public required HonuaStacExtent Extent { get; init; }

    /// <summary>Navigation links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<HonuaStacLink> Links { get; init; } = [];

    /// <summary>Distribution assets keyed by asset key.</summary>
    [JsonPropertyName("assets")]
    public IReadOnlyDictionary<string, HonuaStacAsset>? Assets { get; init; }
}

/// <summary>GeoJSON Polygon geometry derived from the dataset bbox.</summary>
public sealed record HonuaStacGeometry
{
    /// <summary>GeoJSON geometry type (Honua emits a bbox-derived Polygon).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Polygon coordinate rings.</summary>
    [JsonPropertyName("coordinates")]
    public IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> Coordinates { get; init; } = [];
}

/// <summary>
/// STAC Item projection for a published open-data item. Maps to the server
/// <c>stacProjectionItem</c> contract.
/// </summary>
public sealed record HonuaStacItem
{
    /// <summary>STAC spec version.</summary>
    [JsonPropertyName("stac_version")]
    public string? StacVersion { get; init; }

    /// <summary>STAC entity type (GeoJSON <c>Feature</c>).</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary>Item id.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>Owning collection id.</summary>
    [JsonPropertyName("collection")]
    public required string Collection { get; init; }

    /// <summary>Item bounding box (W,S,E,N).</summary>
    [JsonPropertyName("bbox")]
    public IReadOnlyList<double>? Bbox { get; init; }

    /// <summary>Item geometry (GeoJSON), or <c>null</c> when no spatial extent is known.</summary>
    [JsonPropertyName("geometry")]
    public HonuaStacGeometry? Geometry { get; init; }

    /// <summary>Common metadata properties (datetime, title, etc.).</summary>
    [JsonPropertyName("properties")]
    public IReadOnlyDictionary<string, string?> Properties { get; init; }
        = new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>Navigation links.</summary>
    [JsonPropertyName("links")]
    public IReadOnlyList<HonuaStacLink> Links { get; init; } = [];

    /// <summary>Distribution assets keyed by asset key.</summary>
    [JsonPropertyName("assets")]
    public IReadOnlyDictionary<string, HonuaStacAsset>? Assets { get; init; }
}
