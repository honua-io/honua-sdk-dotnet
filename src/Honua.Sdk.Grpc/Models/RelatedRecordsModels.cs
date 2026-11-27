// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Grpc.Models;

/// <summary>Request parameters for querying related records along a declared layer relationship.</summary>
public sealed record QueryRelatedRecordsRequest
{
    /// <summary>Service identifier.</summary>
    public required string ServiceId { get; init; }

    /// <summary>Origin layer identifier.</summary>
    public required int LayerId { get; init; }

    /// <summary>Declared relationship identifier on the origin layer.</summary>
    public required int RelationshipId { get; init; }

    /// <summary>Distinct origin feature object IDs to follow.</summary>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>Destination layer fields to include.</summary>
    public IReadOnlyList<string>? OutFields { get; init; }

    /// <summary>Optional SQL-like destination filter.</summary>
    public string? Where { get; init; }

    /// <summary>Whether to return destination feature geometries.</summary>
    public bool ReturnGeometry { get; init; }

    /// <summary>Optional output spatial reference for destination geometries.</summary>
    public SpatialReference? OutSr { get; init; }

    /// <summary>Maximum total destination rows in this page; zero uses the server default.</summary>
    public int PageSize { get; init; }

    /// <summary>Opaque continuation token from a preceding response.</summary>
    public string PageToken { get; init; } = string.Empty;
}

/// <summary>Group of destination features attributed to one origin object ID.</summary>
public sealed class RelatedRecordGroup
{
    /// <summary>Origin feature object ID.</summary>
    public long ObjectId { get; init; }

    /// <summary>Related destination features for this origin.</summary>
    public IReadOnlyList<Feature> Features { get; init; } = [];
}

/// <summary>Response page from a related-records query.</summary>
public sealed class QueryRelatedRecordsResponse
{
    /// <summary>Destination related layer identifier.</summary>
    public int RelatedLayerId { get; init; }

    /// <summary>Destination object ID field name.</summary>
    public string ObjectIdFieldName { get; init; } = string.Empty;

    /// <summary>Destination geometry type.</summary>
    public GeometryType GeometryType { get; init; }

    /// <summary>Destination spatial reference.</summary>
    public SpatialReference? SpatialReference { get; init; }

    /// <summary>Destination field definitions.</summary>
    public IReadOnlyList<FieldDefinition> Fields { get; init; } = [];

    /// <summary>Related record groups keyed by origin object ID.</summary>
    public IReadOnlyList<RelatedRecordGroup> Groups { get; init; } = [];

    /// <summary>Opaque continuation token, empty when all requested origins are exhausted.</summary>
    public string NextPageToken { get; init; } = string.Empty;

    /// <summary>Destination global ID field name when available.</summary>
    public string GlobalIdFieldName { get; init; } = string.Empty;
}
