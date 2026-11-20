// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>Request parameters for describing a feature service and paging its accessible layers.</summary>
public sealed record DescribeServiceRequest
{
    /// <summary>Service identifier.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Maximum number of layers to return; zero selects the server default.</summary>
    public int PageSize { get; init; }

    /// <summary>Opaque continuation token from a preceding response.</summary>
    public string PageToken { get; init; } = string.Empty;
}

/// <summary>Response from describing a feature service.</summary>
public sealed class DescribeServiceResponse
{
    /// <summary>Authorized service descriptor.</summary>
    public GrpcServiceDescriptor? Service { get; init; }

    /// <summary>Page of accessible layer summaries.</summary>
    public IReadOnlyList<LayerSummary> Layers { get; init; } = [];

    /// <summary>Opaque continuation token, empty when no more accessible layers remain.</summary>
    public string NextPageToken { get; init; } = string.Empty;
}

/// <summary>Metadata describing an authorized feature service.</summary>
public sealed class GrpcServiceDescriptor
{
    /// <summary>Service identifier.</summary>
    public string ServiceId { get; init; } = string.Empty;

    /// <summary>Human-readable service name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Service description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Default spatial reference of the service.</summary>
    public SpatialReference? SpatialReference { get; init; }

    /// <summary>Full extent of the service.</summary>
    public Extent? Extent { get; init; }

    /// <summary>Upper-bound effective capabilities for the caller.</summary>
    public FeatureServiceCapabilities? Capabilities { get; init; }

    /// <summary>Maximum record count returned per query.</summary>
    public long MaxRecordCount { get; init; }
}

/// <summary>Summary of a discoverable layer or non-spatial table in a service.</summary>
public sealed class LayerSummary
{
    /// <summary>Layer identifier within the service.</summary>
    public int LayerId { get; init; }

    /// <summary>Layer name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Layer description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Geometry type of the layer.</summary>
    public GeometryType GeometryType { get; init; }
}

/// <summary>Request parameters for describing one layer or table.</summary>
public sealed record DescribeLayerRequest
{
    /// <summary>Service identifier.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Layer identifier within the service.</summary>
    public required int LayerId { get; init; }
}

/// <summary>Response from describing a single layer or table.</summary>
public sealed class DescribeLayerResponse
{
    /// <summary>Authoritative schema and effective capabilities for the layer.</summary>
    public LayerDescriptor? Layer { get; init; }
}

/// <summary>Authoritative metadata and effective capabilities for one layer or table.</summary>
public sealed class LayerDescriptor
{
    /// <summary>Service identifier.</summary>
    public string ServiceId { get; init; } = string.Empty;

    /// <summary>Layer identifier within the service.</summary>
    public int LayerId { get; init; }

    /// <summary>Layer name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Layer description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Geometry type of the layer.</summary>
    public GeometryType GeometryType { get; init; }

    /// <summary>Spatial reference of the layer.</summary>
    public SpatialReference? SpatialReference { get; init; }

    /// <summary>Full extent of the layer.</summary>
    public Extent? Extent { get; init; }

    /// <summary>Object ID field name.</summary>
    public string ObjectIdField { get; init; } = string.Empty;

    /// <summary>Global ID field name.</summary>
    public string GlobalIdField { get; init; } = string.Empty;

    /// <summary>Display field name.</summary>
    public string DisplayField { get; init; } = string.Empty;

    /// <summary>Detailed field descriptors including editing constraints.</summary>
    public IReadOnlyList<LayerFieldDescriptor> Fields { get; init; } = [];

    /// <summary>Optional subtype definition for the layer.</summary>
    public SubtypeDefinition? Subtypes { get; init; }

    /// <summary>Feature creation templates for the layer.</summary>
    public IReadOnlyList<FeatureTemplate> Templates { get; init; } = [];

    /// <summary>Declared relationships originating from or targeting this layer.</summary>
    public IReadOnlyList<RelationshipDescriptor> Relationships { get; init; } = [];

    /// <summary>Effective capabilities for the current caller.</summary>
    public FeatureServiceCapabilities? Capabilities { get; init; }

    /// <summary>Maximum record count per query.</summary>
    public long MaxRecordCount { get; init; }

    /// <summary>Whether geometries carry Z elevation ordinates.</summary>
    public bool HasZ { get; init; }

    /// <summary>Whether geometries carry M measure ordinates.</summary>
    public bool HasM { get; init; }

    /// <summary>Optional temporal metadata for the layer.</summary>
    public LayerTimeInfo? TimeInfo { get; init; }

    /// <summary>Optional editor tracking and ownership metadata.</summary>
    public LayerEditingInfo? EditingInfo { get; init; }

    /// <summary>Optional effective attachment size and quota limits.</summary>
    public AttachmentLimits? AttachmentLimits { get; init; }
}

/// <summary>Effective gRPC FeatureService capabilities reported for the current caller.</summary>
public sealed class FeatureServiceCapabilities
{
    /// <summary>Whether unary feature queries are supported and authorized.</summary>
    public bool Query { get; init; }

    /// <summary>Whether streaming feature queries are supported and authorized.</summary>
    public bool QueryStream { get; init; }

    /// <summary>Whether feature creation is supported and authorized.</summary>
    public bool Create { get; init; }

    /// <summary>Whether feature updates are supported and authorized.</summary>
    public bool Update { get; init; }

    /// <summary>Whether feature deletes are supported and authorized.</summary>
    public bool Delete { get; init; }

    /// <summary>Whether related-record queries are supported and authorized.</summary>
    public bool QueryRelated { get; init; }

    /// <summary>Whether multi-layer service edits are supported and authorized.</summary>
    public bool ApplyServiceEdits { get; init; }

    /// <summary>Whether global-ID selectors and edits are supported.</summary>
    public bool UseGlobalIds { get; init; }

    /// <summary>Whether listing attachments is supported and authorized.</summary>
    public bool ListAttachments { get; init; }

    /// <summary>Whether uploading attachments is supported and authorized.</summary>
    public bool UploadAttachments { get; init; }

    /// <summary>Whether deleting attachments is supported and authorized.</summary>
    public bool DeleteAttachments { get; init; }

    /// <summary>Whether temporal queries are supported.</summary>
    public bool QueryTime { get; init; }

    /// <summary>Whether aggregate statistics queries are supported.</summary>
    public bool QueryStatistics { get; init; }

    /// <summary>Whether distinct queries are supported.</summary>
    public bool QueryDistinct { get; init; }

    /// <summary>Whether ordered queries are supported.</summary>
    public bool QueryOrderBy { get; init; }

    /// <summary>Whether paginated queries are supported.</summary>
    public bool QueryPagination { get; init; }
}
