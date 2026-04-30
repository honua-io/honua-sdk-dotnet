// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Abstractions.Features;

namespace Honua.Sdk.Abstractions.Data;

/// <summary>
/// Capabilities advertised by an enrichment data provider.
/// </summary>
public sealed record EnrichmentDataCapabilities
{
    /// <summary>Whether enrichment variable metadata can be discovered.</summary>
    public bool SupportsMetadata { get; init; }

    /// <summary>Whether enrichment can run against caller-provided geometry.</summary>
    public bool SupportsGeometryEnrichment { get; init; }

    /// <summary>Whether enrichment can run against feature identifiers.</summary>
    public bool SupportsFeatureEnrichment { get; init; }

    /// <summary>Whether batch enrichment is supported.</summary>
    public bool SupportsBatchEnrichment { get; init; }

    /// <summary>Whether demographic or business-analysis variables are available.</summary>
    public bool SupportsDemographicVariables { get; init; }

    /// <summary>Whether custom or tenant-defined variables are available.</summary>
    public bool SupportsCustomVariables { get; init; }

    /// <summary>Native provider surface backing the implementation.</summary>
    public string? NativeSurface { get; init; }

    /// <summary>Reason the capability set is unavailable, when applicable.</summary>
    public string? UnsupportedReason { get; init; }
}

/// <summary>
/// Enrichment attribute or variable definition.
/// </summary>
public sealed record EnrichmentAttributeDefinition
{
    /// <summary>Stable attribute or variable id.</summary>
    public required string AttributeId { get; init; }

    /// <summary>Display or lookup name.</summary>
    public string? Name { get; init; }

    /// <summary>Attribute category, such as demographics or parcel context.</summary>
    public string? Category { get; init; }

    /// <summary>Portable value type.</summary>
    public SpatialDataValueType ValueType { get; init; } = SpatialDataValueType.Unknown;

    /// <summary>Measurement unit for the attribute value.</summary>
    public string? Unit { get; init; }

    /// <summary>Provider description.</summary>
    public string? Description { get; init; }

    /// <summary>Provider aggregation or apportionment method.</summary>
    public string? AggregationMethod { get; init; }

    /// <summary>Raw provider metadata for this attribute.</summary>
    public JsonElement? Raw { get; init; }
}

/// <summary>
/// Request for enrichment metadata.
/// </summary>
public sealed record EnrichmentMetadataRequest
{
    /// <summary>Provider-specific enrichment source identifiers.</summary>
    public SpatialDataSource Source { get; init; } = new();

    /// <summary>Optional variable set or enrichment catalog identifier.</summary>
    public string? VariableSetId { get; init; }

    /// <summary>Optional categories to include.</summary>
    public IReadOnlyList<string>? Categories { get; init; }

    /// <summary>Additional provider parameters that do not affect SDK display behavior.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Enrichment metadata returned by a provider.
/// </summary>
public sealed record EnrichmentMetadata
{
    /// <summary>Source that produced the metadata.</summary>
    public SpatialDataSource Source { get; init; } = new();

    /// <summary>Attributes or variables available for enrichment.</summary>
    public IReadOnlyList<EnrichmentAttributeDefinition> Attributes { get; init; } = [];

    /// <summary>Advertised attribute categories.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>Raw provider metadata payload.</summary>
    public JsonElement? RawMetadata { get; init; }
}

/// <summary>
/// Enrichment request for feature identifiers, geometry, or an area of interest.
/// </summary>
public sealed record EnrichmentRequest
{
    /// <summary>Provider-specific enrichment source identifiers.</summary>
    public SpatialDataSource Source { get; init; } = new();

    /// <summary>Attributes or variables requested from the provider.</summary>
    public IReadOnlyList<string> AttributeIds { get; init; } = [];

    /// <summary>Feature source to enrich when feature identifiers are supplied.</summary>
    public FeatureSource? FeatureSource { get; init; }

    /// <summary>String feature identifiers to enrich.</summary>
    public IReadOnlyList<string>? FeatureIds { get; init; }

    /// <summary>Numeric object identifiers to enrich.</summary>
    public IReadOnlyList<long>? ObjectIds { get; init; }

    /// <summary>Provider JSON geometry to enrich.</summary>
    public JsonElement? Geometry { get; init; }

    /// <summary>Geometry shape type for <see cref="Geometry"/>.</summary>
    public FeatureSpatialGeometryType GeometryType { get; init; } = FeatureSpatialGeometryType.Unspecified;

    /// <summary>Coordinate reference system for <see cref="Geometry"/> when not embedded in the geometry.</summary>
    public string? GeometryCrs { get; init; }

    /// <summary>Bounding extent to enrich or use as an area hint.</summary>
    public FeatureBoundingBox? Extent { get; init; }

    /// <summary>Optional area-of-interest geometry filter.</summary>
    public FeatureSpatialFilter? AreaOfInterest { get; init; }

    /// <summary>Provider or CQL filter applied before enrichment.</summary>
    public string? Filter { get; init; }

    /// <summary>Filter language for <see cref="Filter"/>.</summary>
    public FeatureFilterLanguage FilterLanguage { get; init; } = FeatureFilterLanguage.ProviderDefault;

    /// <summary>Whether geometry should be returned with enriched records.</summary>
    public bool ReturnGeometry { get; init; }

    /// <summary>Additional provider parameters that do not affect SDK display behavior.</summary>
    public IReadOnlyDictionary<string, string?>? AdditionalParameters { get; init; }
}

/// <summary>
/// Enriched record returned by a provider.
/// </summary>
public sealed record EnrichmentRecord
{
    /// <summary>Record identifier, when returned by the provider.</summary>
    public string? RecordId { get; init; }

    /// <summary>Feature source associated with this record, when known.</summary>
    public FeatureSource? FeatureSource { get; init; }

    /// <summary>Returned geometry, when requested and supported.</summary>
    public JsonElement? Geometry { get; init; }

    /// <summary>Enriched attribute values keyed by attribute id.</summary>
    public IReadOnlyDictionary<string, JsonElement> Attributes { get; init; } = new Dictionary<string, JsonElement>();

    /// <summary>Provider messages specific to this record.</summary>
    public IReadOnlyList<SpatialDataMessage> Messages { get; init; } = [];
}

/// <summary>
/// Enrichment response returned by a provider.
/// </summary>
public sealed record EnrichmentResponse
{
    /// <summary>Source that produced the response.</summary>
    public SpatialDataSource Source { get; init; } = new();

    /// <summary>Returned enrichment records.</summary>
    public IReadOnlyList<EnrichmentRecord> Records { get; init; } = [];

    /// <summary>Attribute definitions associated with returned values, when available.</summary>
    public IReadOnlyList<EnrichmentAttributeDefinition> Attributes { get; init; } = [];

    /// <summary>Provider messages.</summary>
    public IReadOnlyList<SpatialDataMessage> Messages { get; init; } = [];

    /// <summary>Raw provider response payload.</summary>
    public JsonElement? RawResponse { get; init; }

    /// <summary>Whether the response and all returned records do not contain provider errors.</summary>
    public bool Succeeded =>
        !Messages.Any(static message => message.Severity == SpatialDataMessageSeverity.Error) &&
        !Records.Any(static record => record.Messages.Any(static message => message.Severity == SpatialDataMessageSeverity.Error));
}
