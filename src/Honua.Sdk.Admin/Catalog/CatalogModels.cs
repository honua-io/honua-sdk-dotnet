// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Features;
using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin.Catalog;

/// <summary>
/// Catalog item categories returned by the portal and catalog discovery client.
/// </summary>
public enum CatalogItemKind
{
    /// <summary>A published service.</summary>
    Service = 0,

    /// <summary>A layer within a published service.</summary>
    Layer = 1,

    /// <summary>A metadata-backed catalog group.</summary>
    Group = 2,

    /// <summary>A saved SDK source descriptor.</summary>
    SourceDescriptor = 3
}

/// <summary>
/// Catalog sort fields supported by the SDK client.
/// </summary>
public enum CatalogSortBy
{
    /// <summary>Sort by display name.</summary>
    Name = 0,

    /// <summary>Sort by catalog item kind.</summary>
    Kind = 1,

    /// <summary>Sort by owning service name.</summary>
    ServiceName = 2,

    /// <summary>Sort by metadata creation timestamp when available.</summary>
    CreatedAt = 3,

    /// <summary>Sort by metadata update timestamp when available.</summary>
    UpdatedAt = 4
}

/// <summary>
/// Catalog sort direction.
/// </summary>
public enum CatalogSortDirection
{
    /// <summary>Ascending sort order.</summary>
    Ascending = 0,

    /// <summary>Descending sort order.</summary>
    Descending = 1
}

/// <summary>
/// Search, filter, paging, and sorting options for catalog operations.
/// </summary>
public sealed record CatalogQueryOptions
{
    /// <summary>Free-text search applied to names, descriptions, service names, tags, and owners.</summary>
    public string? Query { get; init; }

    /// <summary>Catalog item kinds to include when using the unified search API.</summary>
    public IReadOnlyList<CatalogItemKind>? Kinds { get; init; }

    /// <summary>Service protocol or service type filters, such as FeatureServer, MapServer, OgcFeatures, OData, or Grpc.</summary>
    public IReadOnlyList<string>? ServiceTypes { get; init; }

    /// <summary>Tags that must be present on the catalog item.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Owner filter from metadata labels or annotations.</summary>
    public string? Owner { get; init; }

    /// <summary>Namespace filter for metadata-backed catalog resources.</summary>
    public string? Namespace { get; init; }

    /// <summary>Geometry type filters, such as Point, Polyline, Polygon, or esriGeometryPoint.</summary>
    public IReadOnlyList<string>? GeometryTypes { get; init; }

    /// <summary>Capability filters, such as Query, Create, Update, Delete, Attachments, Statistics, or FeatureServer.</summary>
    public IReadOnlyList<string>? Capabilities { get; init; }

    /// <summary>Zero-based result offset. Defaults to 0.</summary>
    public int? Offset { get; init; }

    /// <summary>Maximum number of results to return. A null value returns all filtered results.</summary>
    public int? Limit { get; init; }

    /// <summary>Field used to sort results.</summary>
    public CatalogSortBy SortBy { get; init; } = CatalogSortBy.Name;

    /// <summary>Sort direction.</summary>
    public CatalogSortDirection SortDirection { get; init; } = CatalogSortDirection.Ascending;
}

/// <summary>
/// Unified catalog search result.
/// </summary>
public sealed record CatalogSearchResult
{
    /// <summary>Filtered and paged catalog items.</summary>
    public IReadOnlyList<CatalogItem> Items { get; init; } = [];

    /// <summary>Total item count after filters and before paging.</summary>
    public int TotalCount { get; init; }

    /// <summary>Offset applied to this page.</summary>
    public int Offset { get; init; }

    /// <summary>Next offset when more results are available.</summary>
    public int? NextOffset { get; init; }
}

/// <summary>
/// Unified catalog item envelope.
/// </summary>
public sealed record CatalogItem
{
    /// <summary>Stable item identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Catalog item kind.</summary>
    public required CatalogItemKind Kind { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Optional human-readable description.</summary>
    public string? Description { get; init; }

    /// <summary>Owning service name when the item belongs to a service.</summary>
    public string? ServiceName { get; init; }

    /// <summary>Layer ID when the item represents a layer.</summary>
    public int? LayerId { get; init; }

    /// <summary>Metadata namespace when available.</summary>
    public string? Namespace { get; init; }

    /// <summary>Owner from metadata labels or annotations.</summary>
    public string? Owner { get; init; }

    /// <summary>Tags from metadata labels or annotations.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Service protocol or service type values associated with the item.</summary>
    public IReadOnlyList<string> ServiceTypes { get; init; } = [];

    /// <summary>Capability values associated with the item.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Geometry type when known.</summary>
    public string? GeometryType { get; init; }

    /// <summary>Spatial extent when known.</summary>
    public FeatureBoundingBox? Extent { get; init; }

    /// <summary>Created timestamp from metadata when available.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Updated timestamp from metadata when available.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Service detail when <see cref="Kind"/> is <see cref="CatalogItemKind.Service"/>.</summary>
    public CatalogService? Service { get; init; }

    /// <summary>Layer detail when <see cref="Kind"/> is <see cref="CatalogItemKind.Layer"/>.</summary>
    public CatalogLayer? Layer { get; init; }

    /// <summary>Group detail when <see cref="Kind"/> is <see cref="CatalogItemKind.Group"/>.</summary>
    public CatalogGroup? Group { get; init; }

    /// <summary>Saved source descriptor detail when <see cref="Kind"/> is <see cref="CatalogItemKind.SourceDescriptor"/>.</summary>
    public CatalogSourceDescriptor? SourceDescriptor { get; init; }
}

/// <summary>
/// Catalog service detail.
/// </summary>
public sealed record CatalogService
{
    /// <summary>Stable service identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Service name.</summary>
    public required string Name { get; init; }

    /// <summary>Service description.</summary>
    public string? Description { get; init; }

    /// <summary>Number of layers advertised by the service list endpoint.</summary>
    public int LayerCount { get; init; }

    /// <summary>Enabled service protocols.</summary>
    public IReadOnlyList<string> ServiceTypes { get; init; } = [];

    /// <summary>Service-level capabilities.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Geometry types discovered from layer details when available.</summary>
    public IReadOnlyList<string> GeometryTypes { get; init; } = [];

    /// <summary>Service extent when available.</summary>
    public FeatureBoundingBox? Extent { get; init; }

    /// <summary>Metadata namespace when available.</summary>
    public string? Namespace { get; init; }

    /// <summary>Owner from metadata labels or annotations.</summary>
    public string? Owner { get; init; }

    /// <summary>Tags from metadata labels or annotations.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Backing metadata resource when one was discovered.</summary>
    public MetadataResource? MetadataResource { get; init; }
}

/// <summary>
/// Catalog layer detail.
/// </summary>
public sealed record CatalogLayer
{
    /// <summary>Stable layer identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Owning service name.</summary>
    public required string ServiceName { get; init; }

    /// <summary>Layer ID within the service.</summary>
    public int LayerId { get; init; }

    /// <summary>Layer name.</summary>
    public required string Name { get; init; }

    /// <summary>Layer description.</summary>
    public string? Description { get; init; }

    /// <summary>Enabled service protocols on the owning service.</summary>
    public IReadOnlyList<string> ServiceTypes { get; init; } = [];

    /// <summary>Layer capabilities.</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Layer geometry type when known.</summary>
    public string? GeometryType { get; init; }

    /// <summary>Layer extent when known.</summary>
    public FeatureBoundingBox? Extent { get; init; }

    /// <summary>SDK source descriptor for the FeatureServer-backed layer.</summary>
    public SourceDescriptor SourceDescriptor { get; init; } = new()
    {
        Id = string.Empty,
        Protocol = FeatureProtocolIds.GeoServicesFeatureService
    };

    /// <summary>Metadata namespace when available.</summary>
    public string? Namespace { get; init; }

    /// <summary>Owner from metadata labels or annotations.</summary>
    public string? Owner { get; init; }

    /// <summary>Tags from metadata labels or annotations.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Backing metadata resource when one was discovered.</summary>
    public MetadataResource? MetadataResource { get; init; }
}

/// <summary>
/// Metadata-backed catalog group detail.
/// </summary>
public sealed record CatalogGroup
{
    /// <summary>Stable group identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Group name.</summary>
    public required string Name { get; init; }

    /// <summary>Group namespace.</summary>
    public string? Namespace { get; init; }

    /// <summary>Group description.</summary>
    public string? Description { get; init; }

    /// <summary>Owner from metadata labels or annotations.</summary>
    public string? Owner { get; init; }

    /// <summary>Tags from metadata labels or annotations.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Backing metadata resource.</summary>
    public required MetadataResource Resource { get; init; }
}

/// <summary>
/// Metadata-backed saved SDK source descriptor.
/// </summary>
public sealed record CatalogSourceDescriptor
{
    /// <summary>Stable descriptor identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Descriptor name.</summary>
    public required string Name { get; init; }

    /// <summary>Descriptor namespace.</summary>
    public string? Namespace { get; init; }

    /// <summary>Owner from metadata labels or annotations.</summary>
    public string? Owner { get; init; }

    /// <summary>Tags from metadata labels or annotations.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Parsed SDK source descriptor.</summary>
    public required SourceDescriptor Descriptor { get; init; }

    /// <summary>Backing metadata resource.</summary>
    public required MetadataResource Resource { get; init; }
}

/// <summary>
/// Known metadata resource kinds used by the catalog client.
/// </summary>
public static class CatalogMetadataKinds
{
    /// <summary>Service metadata resource kind.</summary>
    public const string Service = "Service";

    /// <summary>Layer metadata resource kind.</summary>
    public const string Layer = "Layer";

    /// <summary>Catalog group metadata resource kind.</summary>
    public const string Group = "Group";

    /// <summary>Saved SDK source descriptor metadata resource kind.</summary>
    public const string SourceDescriptor = "SourceDescriptor";
}
