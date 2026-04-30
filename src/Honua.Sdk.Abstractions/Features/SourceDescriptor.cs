// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Protocol-specific endpoint information for a source-oriented feature client.
/// </summary>
public sealed record SourceLocator
{
    /// <summary>Fully qualified URL to the protocol endpoint, when known.</summary>
    public string? Url { get; init; }

    /// <summary>Honua service identifier for gRPC and GeoServices FeatureServer providers.</summary>
    public string? ServiceId { get; init; }

    /// <summary>Layer identifier within a service.</summary>
    public int? LayerId { get; init; }

    /// <summary>OGC API Features collection identifier.</summary>
    public string? CollectionId { get; init; }

    /// <summary>WFS qualified feature type name.</summary>
    public string? TypeName { get; init; }

    /// <summary>WFS feature namespace URI used by transaction-capable providers.</summary>
    public string? FeatureNamespace { get; init; }

    /// <summary>OGC tile-matrix-set identifier for tile-backed sources.</summary>
    public string? TileMatrixSetId { get; init; }

    /// <summary>OGC style identifier for styled-output endpoints.</summary>
    public string? StyleId { get; init; }

    /// <summary>OData entity set identifier.</summary>
    public string? EntitySet { get; init; }

    /// <summary>GeoServices GP task identifier.</summary>
    public string? TaskName { get; init; }
}

/// <summary>
/// Optional schema description for a source.
/// </summary>
public sealed record SourceSchema
{
    /// <summary>Field descriptors advertised for the source.</summary>
    public IReadOnlyList<SourceField> Fields { get; init; } = [];

    /// <summary>Primary key or object ID field name.</summary>
    public string? PrimaryKey { get; init; }

    /// <summary>Provider object ID field name, when the source has one.</summary>
    public string? ObjectIdField { get; init; }

    /// <summary>Provider global ID field name, when the source has one.</summary>
    public string? GlobalIdField { get; init; }

    /// <summary>Feature geometry type advertised by the source.</summary>
    public FeatureSpatialGeometryType GeometryType { get; init; } = FeatureSpatialGeometryType.Unspecified;

    /// <summary>Full source extent, when advertised by the provider.</summary>
    public FeatureBoundingBox? Extent { get; init; }

    /// <summary>Coordinate reference system identifier for the source geometry, when advertised.</summary>
    public string? SpatialReference { get; init; }

    /// <summary>Temporal validity field name, when applicable.</summary>
    public string? TimeField { get; init; }

    /// <summary>Temporal metadata advertised by the source.</summary>
    public SourceTimeInfo? TimeInfo { get; init; }

    /// <summary>Edit capabilities advertised by the source.</summary>
    public FeatureEditCapabilities? EditCapabilities { get; init; }

    /// <summary>Attachment capabilities advertised by the source.</summary>
    public FeatureAttachmentCapabilities? AttachmentCapabilities { get; init; }

    /// <summary>Structured editing-rule metadata advertised by the source.</summary>
    public FeatureEditingRulesMetadata? EditingRules { get; init; }
}

/// <summary>
/// Field descriptor used by <see cref="SourceSchema"/>.
/// </summary>
public sealed record SourceField
{
    /// <summary>Field name.</summary>
    public required string Name { get; init; }

    /// <summary>Human-readable field alias or title.</summary>
    public string? Alias { get; init; }

    /// <summary>Provider or canonical field type.</summary>
    public string? Type { get; init; }

    /// <summary>Whether the field accepts null values.</summary>
    public bool? Nullable { get; init; }

    /// <summary>Maximum length for string fields.</summary>
    public int? Length { get; init; }

    /// <summary>Whether the field can be edited by clients.</summary>
    public bool? Editable { get; init; }

    /// <summary>Whether the field is required for writes.</summary>
    public bool? Required { get; init; }

    /// <summary>Provider-native field domain metadata, when advertised.</summary>
    public JsonElement? Domain { get; init; }

    /// <summary>Structured field domain metadata, when advertised.</summary>
    public FeatureFieldDomain? DomainInfo { get; init; }

    /// <summary>Provider-native default value, when advertised.</summary>
    public JsonElement? DefaultValue { get; init; }
}

/// <summary>
/// Temporal metadata for a source.
/// </summary>
public sealed record SourceTimeInfo
{
    /// <summary>Start time field name.</summary>
    public string? StartTimeField { get; init; }

    /// <summary>End time field name for interval-aware sources.</summary>
    public string? EndTimeField { get; init; }

    /// <summary>Track ID field name for moving-object or track-aware sources.</summary>
    public string? TrackIdField { get; init; }

    /// <summary>Provider time reference identifier or JSON summary.</summary>
    public string? TimeReference { get; init; }
}

/// <summary>
/// Serializable descriptor for one protocol-backed feature source.
/// </summary>
public sealed record SourceDescriptor
{
    /// <summary>Application-level source identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Canonical protocol identifier or supported alias.</summary>
    public required string Protocol { get; init; }

    /// <summary>Protocol-specific source locator.</summary>
    public SourceLocator Locator { get; init; } = new();

    /// <summary>
    /// Declared source capabilities. When empty, the source facade uses this SDK's protocol defaults.
    /// </summary>
    public IReadOnlyList<string> Capabilities { get; init; } = [];

    /// <summary>Optional field schema.</summary>
    public SourceSchema? Schema { get; init; }

    /// <summary>Optional attribution text.</summary>
    public string? Attribution { get; init; }

    /// <summary>Gets the canonical protocol identifier for <see cref="Protocol"/>.</summary>
    public string CanonicalProtocol => FeatureProtocolIds.Normalize(Protocol);

    /// <summary>
    /// Converts this descriptor's locator into the existing provider-neutral feature source shape.
    /// </summary>
    /// <returns>A <see cref="FeatureSource"/> for query and edit requests.</returns>
    public FeatureSource ToFeatureSource()
        => new()
        {
            ServiceId = Locator.ServiceId,
            LayerId = Locator.LayerId,
            CollectionId = Locator.CollectionId,
            TypeName = Locator.TypeName
        };
}
