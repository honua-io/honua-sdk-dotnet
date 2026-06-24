// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Console.Share;

/// <summary>
/// Category of a Console content item, as evaluated for open-data publication.
/// Mirrors the server <c>consoleContentItemType</c> contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaConsoleContentItemType>))]
public enum HonuaConsoleContentItemType
{
    /// <summary>A published service (Map/Feature/Tile/etc.).</summary>
    [JsonStringEnumMemberName("service")]
    Service,

    /// <summary>A layer exposed via a service or open-data endpoint.</summary>
    [JsonStringEnumMemberName("layer")]
    Layer,

    /// <summary>A saved map composition.</summary>
    [JsonStringEnumMemberName("saved-map")]
    SavedMap,

    /// <summary>A dashboard composed of widgets and data sources.</summary>
    [JsonStringEnumMemberName("dashboard")]
    Dashboard,

    /// <summary>A report template or generated report instance.</summary>
    [JsonStringEnumMemberName("report")]
    Report,

    /// <summary>A generated application surface produced by Studio.</summary>
    [JsonStringEnumMemberName("generated-app")]
    GeneratedApp,

    /// <summary>An open-data catalog item with public distribution links.</summary>
    [JsonStringEnumMemberName("open-data")]
    OpenData,
}

/// <summary>
/// Effective share access tier evaluated for an open-data eligibility decision.
/// Mirrors the server <c>consoleShareAccessTier</c> contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaConsoleShareAccessTier>))]
public enum HonuaConsoleShareAccessTier
{
    /// <summary>Visible only to explicitly granted principals.</summary>
    [JsonStringEnumMemberName("private")]
    Private,

    /// <summary>Visible to any authenticated member of the owning organization.</summary>
    [JsonStringEnumMemberName("organization")]
    Organization,

    /// <summary>Reachable anonymously through an active public link.</summary>
    [JsonStringEnumMemberName("public-link")]
    PublicLink,

    /// <summary>Publicly discoverable / indexable (required for open-data publication).</summary>
    [JsonStringEnumMemberName("public-indexed")]
    PublicIndexed,
}

/// <summary>
/// STAC publication lifecycle status of a Console content item. Mirrors the
/// server <c>consoleStacPublicationStatus</c> contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaConsoleStacPublicationStatus>))]
public enum HonuaConsoleStacPublicationStatus
{
    /// <summary>The item is not published to the STAC catalog.</summary>
    [JsonStringEnumMemberName("unpublished")]
    Unpublished,

    /// <summary>The item is published to the STAC catalog.</summary>
    [JsonStringEnumMemberName("published")]
    Published,
}

/// <summary>
/// Severity of an open-data / DCAT validation issue. Mirrors the server
/// <c>consoleOpenDataValidationSeverity</c> contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaConsoleOpenDataValidationSeverity>))]
public enum HonuaConsoleOpenDataValidationSeverity
{
    /// <summary>A blocking error: publication is refused while present.</summary>
    [JsonStringEnumMemberName("error")]
    Error,

    /// <summary>A non-blocking warning: publication is allowed but flagged.</summary>
    [JsonStringEnumMemberName("warning")]
    Warning,
}

/// <summary>
/// A distribution / access link advertised by an open-data page. Maps to the
/// server <c>consoleOpenDataDistribution</c> contract.
/// </summary>
public sealed record HonuaOpenDataDistribution
{
    /// <summary>Distribution title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Access URL. Required by the server on write.</summary>
    // CA1056: the open-data contract carries access URLs verbatim as strings (they may be
    // relative or non-HTTP service references); the SDK mirrors the server contract shape.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Mirrors the server open-data distribution contract, which carries the access URL as a verbatim string.")]
    [JsonPropertyName("accessUrl")]
    public required string AccessUrl { get; init; }

    /// <summary>IANA media type.</summary>
    [JsonPropertyName("mediaType")]
    public string? MediaType { get; init; }

    /// <summary>Short format label (for example <c>GeoJSON</c>).</summary>
    [JsonPropertyName("format")]
    public string? Format { get; init; }
}

/// <summary>
/// Spatial coverage as a WGS84 bounding box. Maps to the server
/// <c>consoleSpatialExtent</c> contract.
/// </summary>
public sealed record HonuaOpenDataSpatialExtent
{
    /// <summary>Western longitude bound.</summary>
    [JsonPropertyName("west")]
    public required double West { get; init; }

    /// <summary>Southern latitude bound.</summary>
    [JsonPropertyName("south")]
    public required double South { get; init; }

    /// <summary>Eastern longitude bound.</summary>
    [JsonPropertyName("east")]
    public required double East { get; init; }

    /// <summary>Northern latitude bound.</summary>
    [JsonPropertyName("north")]
    public required double North { get; init; }
}

/// <summary>
/// Temporal coverage as an optional start/end interval. Maps to the server
/// <c>consoleTemporalExtent</c> contract.
/// </summary>
public sealed record HonuaOpenDataTemporalExtent
{
    /// <summary>Inclusive interval start, in UTC; <c>null</c> for open-ended.</summary>
    [JsonPropertyName("start")]
    public DateTimeOffset? Start { get; init; }

    /// <summary>Inclusive interval end, in UTC; <c>null</c> for open-ended.</summary>
    [JsonPropertyName("end")]
    public DateTimeOffset? End { get; init; }
}

/// <summary>
/// The server-owned open-data page projection for a content item. Maps to the
/// server <c>consoleOpenDataPage</c> contract; also returned (anonymous-safe)
/// from the public open-data dataset read.
/// </summary>
public sealed record HonuaOpenDataPage
{
    /// <summary>Identifier of the content item the page describes.</summary>
    [JsonPropertyName("itemId")]
    public required string ItemId { get; init; }

    /// <summary>Dataset title; falls back to the item title server-side when unset.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Dataset description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Publishing organization name.</summary>
    [JsonPropertyName("publisherName")]
    public string? PublisherName { get; init; }

    /// <summary>Point-of-contact full name.</summary>
    [JsonPropertyName("contactName")]
    public string? ContactName { get; init; }

    /// <summary>Point-of-contact email.</summary>
    [JsonPropertyName("contactEmail")]
    public string? ContactEmail { get; init; }

    /// <summary>License URL or SPDX identifier.</summary>
    [JsonPropertyName("license")]
    public string? License { get; init; }

    /// <summary>Public landing page URL.</summary>
    [JsonPropertyName("landingPage")]
    public string? LandingPage { get; init; }

    /// <summary>Discovery keywords.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Distribution / access links.</summary>
    [JsonPropertyName("distributions")]
    public IReadOnlyList<HonuaOpenDataDistribution> Distributions { get; init; } = [];

    /// <summary>Spatial coverage.</summary>
    [JsonPropertyName("spatialExtent")]
    public HonuaOpenDataSpatialExtent? SpatialExtent { get; init; }

    /// <summary>Temporal coverage.</summary>
    [JsonPropertyName("temporalExtent")]
    public HonuaOpenDataTemporalExtent? TemporalExtent { get; init; }

    /// <summary>Free-form provenance references.</summary>
    [JsonPropertyName("provenanceRefs")]
    public IReadOnlyList<string> ProvenanceRefs { get; init; } = [];

    /// <summary>Timestamp the page was last updated, in UTC.</summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Identifier of the principal that last updated the page.</summary>
    [JsonPropertyName("updatedById")]
    public string? UpdatedById { get; init; }
}

/// <summary>
/// Request body to create or replace an item's editable open-data page. All
/// fields are optional editable metadata; the route binds the item id. Maps to
/// the server <c>updateOpenDataPageRequest</c> contract.
/// </summary>
public sealed record HonuaUpdateOpenDataPageRequest
{
    /// <summary>Dataset title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Dataset description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Publishing organization name.</summary>
    [JsonPropertyName("publisherName")]
    public string? PublisherName { get; init; }

    /// <summary>Point-of-contact full name.</summary>
    [JsonPropertyName("contactName")]
    public string? ContactName { get; init; }

    /// <summary>Point-of-contact email.</summary>
    [JsonPropertyName("contactEmail")]
    public string? ContactEmail { get; init; }

    /// <summary>License URL or SPDX identifier.</summary>
    [JsonPropertyName("license")]
    public string? License { get; init; }

    /// <summary>Public landing page URL.</summary>
    [JsonPropertyName("landingPage")]
    public string? LandingPage { get; init; }

    /// <summary>Discovery keywords.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Distribution / access links. Each must carry a non-empty access URL.</summary>
    [JsonPropertyName("distributions")]
    public IReadOnlyList<HonuaOpenDataDistribution>? Distributions { get; init; }

    /// <summary>Spatial coverage.</summary>
    [JsonPropertyName("spatialExtent")]
    public HonuaOpenDataSpatialExtent? SpatialExtent { get; init; }

    /// <summary>Temporal coverage.</summary>
    [JsonPropertyName("temporalExtent")]
    public HonuaOpenDataTemporalExtent? TemporalExtent { get; init; }

    /// <summary>Free-form provenance references.</summary>
    [JsonPropertyName("provenanceRefs")]
    public IReadOnlyList<string>? ProvenanceRefs { get; init; }
}

/// <summary>
/// Why an item is or is not eligible for open-data publication. Maps to the
/// server <c>consoleOpenDataEligibilityResponse</c> contract.
/// </summary>
public sealed record HonuaOpenDataEligibility
{
    /// <summary>Content item id.</summary>
    [JsonPropertyName("itemId")]
    public required string ItemId { get; init; }

    /// <summary>Item category.</summary>
    [JsonPropertyName("itemType")]
    public HonuaConsoleContentItemType ItemType { get; init; }

    /// <summary>True when the item may be published as open data.</summary>
    [JsonPropertyName("eligible")]
    public bool Eligible { get; init; }

    /// <summary>
    /// Stable machine reason code (for example <c>not-distributable-type</c>,
    /// <c>not-public-indexed</c>, or <c>eligible</c>).
    /// </summary>
    [JsonPropertyName("reasonCode")]
    public required string ReasonCode { get; init; }

    /// <summary>Human-readable explanation of the eligibility decision.</summary>
    [JsonPropertyName("reason")]
    public required string Reason { get; init; }

    /// <summary>Effective share access tier evaluated for the decision.</summary>
    [JsonPropertyName("accessTier")]
    public HonuaConsoleShareAccessTier AccessTier { get; init; }

    /// <summary>True when an open-data page has been authored for the item.</summary>
    [JsonPropertyName("hasPage")]
    public bool HasPage { get; init; }
}

/// <summary>
/// A single open-data / DCAT validation issue. Maps to the server
/// <c>consoleOpenDataValidationIssue</c> contract.
/// </summary>
public sealed record HonuaOpenDataValidationIssue
{
    /// <summary>Field the issue applies to.</summary>
    [JsonPropertyName("field")]
    public required string Field { get; init; }

    /// <summary>Severity of the issue.</summary>
    [JsonPropertyName("severity")]
    public HonuaConsoleOpenDataValidationSeverity Severity { get; init; }

    /// <summary>Human-readable description of the issue.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

/// <summary>
/// Result of validating an open-data page for DCAT/data.json export. Maps to the
/// server <c>consoleOpenDataValidationResult</c> contract.
/// </summary>
public sealed record HonuaOpenDataValidationResult
{
    /// <summary>True when the page has no blocking validation errors.</summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }

    /// <summary>Validation issues (errors and warnings).</summary>
    [JsonPropertyName("issues")]
    public IReadOnlyList<HonuaOpenDataValidationIssue> Issues { get; init; } = [];
}

/// <summary>
/// STAC publication lifecycle state of a Console content item. Maps to the
/// server <c>consoleStacPublicationState</c> contract.
/// </summary>
public sealed record HonuaConsoleStacPublicationState
{
    /// <summary>Content item id.</summary>
    [JsonPropertyName("itemId")]
    public required string ItemId { get; init; }

    /// <summary>Current publication status.</summary>
    [JsonPropertyName("status")]
    public HonuaConsoleStacPublicationStatus Status { get; init; }

    /// <summary>Stable STAC collection id assigned at first publish, when published.</summary>
    [JsonPropertyName("collectionId")]
    public string? CollectionId { get; init; }

    /// <summary>Monotonic publication revision.</summary>
    [JsonPropertyName("revision")]
    public long Revision { get; init; }

    /// <summary>Timestamp the item was first published, in UTC.</summary>
    [JsonPropertyName("firstPublishedAt")]
    public DateTimeOffset? FirstPublishedAt { get; init; }

    /// <summary>Timestamp the publication state was last updated, in UTC.</summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Identifier of the principal that last updated the publication state.</summary>
    [JsonPropertyName("updatedById")]
    public string? UpdatedById { get; init; }
}

/// <summary>
/// Combined open-data page read projection: the editable page, the current
/// eligibility decision, the STAC publication state, and DCAT validation status.
/// Maps to the server <c>consoleOpenDataPageResponse</c> contract.
/// </summary>
public sealed record HonuaOpenDataPageResponse
{
    /// <summary>Editable open-data page fields (page-default filled from the item).</summary>
    [JsonPropertyName("page")]
    public required HonuaOpenDataPage Page { get; init; }

    /// <summary>Current eligibility decision.</summary>
    [JsonPropertyName("eligibility")]
    public required HonuaOpenDataEligibility Eligibility { get; init; }

    /// <summary>STAC publication lifecycle state.</summary>
    [JsonPropertyName("stacPublication")]
    public required HonuaConsoleStacPublicationState StacPublication { get; init; }

    /// <summary>DCAT/data.json validation status for the current page.</summary>
    [JsonPropertyName("dcatValidation")]
    public required HonuaOpenDataValidationResult DcatValidation { get; init; }
}

/// <summary>
/// DCAT/data.json export preview: the generated catalog document plus its
/// validation status. Maps to the server <c>consoleDcatExportResponse</c> contract.
/// </summary>
public sealed record HonuaDcatExportResponse
{
    /// <summary>DCAT-US 3.0 / data.json catalog document.</summary>
    [JsonPropertyName("catalog")]
    public required HonuaDcatCatalog Catalog { get; init; }

    /// <summary>Validation status of the dataset the catalog was generated from.</summary>
    [JsonPropertyName("validation")]
    public required HonuaOpenDataValidationResult Validation { get; init; }
}
