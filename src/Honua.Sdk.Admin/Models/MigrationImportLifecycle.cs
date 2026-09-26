// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// ArcGIS credential descriptor for GeoServices discovery and import requests. Prefer the
/// <c>*SecretReference</c> fields for queued/durable operations; the inline <see cref="AccessToken"/>
/// and <see cref="Password"/> fields are accepted by the server for discovery-only calls but are
/// rejected for queued imports.
/// </summary>
public sealed record GeoservicesCredentialDescriptor
{
    /// <summary>Authentication mode label (for example <c>token</c>, <c>basic</c>, <c>bearer</c>, or <c>oauth</c>).</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    /// <summary>Secret reference for an ArcGIS token or OAuth access token.</summary>
    [JsonPropertyName("accessTokenSecretReference")]
    public string? AccessTokenSecretReference { get; init; }

    /// <summary>Inline ArcGIS token or OAuth access token. Discovery-only; rejected for queued imports.</summary>
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; init; }

    /// <summary>Username for HTTP Basic authentication.</summary>
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    /// <summary>Secret reference for the HTTP Basic password.</summary>
    [JsonPropertyName("passwordSecretReference")]
    public string? PasswordSecretReference { get; init; }

    /// <summary>Inline HTTP Basic password. Discovery-only; rejected for queued imports.</summary>
    [JsonPropertyName("password")]
    public string? Password { get; init; }

    /// <summary>Optional OAuth client identifier associated with the token reference.</summary>
    [JsonPropertyName("oAuthClientId")]
    public string? OAuthClientId { get; init; }

    /// <summary>Optional OAuth token endpoint used to identify the token issuer.</summary>
    [JsonPropertyName("oAuthTokenEndpoint")]
    public string? OAuthTokenEndpoint { get; init; }
}

/// <summary>
/// Request to discover the layers and metadata exposed by an ArcGIS service
/// (<c>POST /api/v1/admin/import/geoservices/discover</c>).
/// </summary>
public sealed record GeoservicesDiscoverRequest
{
    /// <summary>URL of the ArcGIS service to discover.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The GeoServices import JSON contract represents source URLs as strings.")]
    [JsonPropertyName("serviceUrl")]
    public required string ServiceUrl { get; init; }

    /// <summary>Optional discovery timeout in seconds.</summary>
    [JsonPropertyName("timeoutSeconds")]
    public int? TimeoutSeconds { get; init; }

    /// <summary>Optional ArcGIS credential descriptor for discovery.</summary>
    [JsonPropertyName("credentials")]
    public GeoservicesCredentialDescriptor? Credentials { get; init; }
}

/// <summary>Layer summary returned by ArcGIS service discovery.</summary>
public sealed record GeoservicesLayerSummary
{
    /// <summary>Source layer id.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; }

    /// <summary>Layer name.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Layer description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Esri geometry type (for example <c>esriGeometryPoint</c>).</summary>
    [JsonPropertyName("geometryType")]
    public string? GeometryType { get; init; }

    /// <summary>Estimated feature count reported at discovery time.</summary>
    [JsonPropertyName("featureCount")]
    public int? FeatureCount { get; init; }

    /// <summary>Whether the layer supports attachments.</summary>
    [JsonPropertyName("hasAttachments")]
    public bool HasAttachments { get; init; }
}

/// <summary>Response returned by <c>POST /api/v1/admin/import/geoservices/discover</c>.</summary>
public sealed record GeoservicesDiscoverResponse
{
    /// <summary>The discovered service URL.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The GeoServices import JSON contract represents source URLs as strings.")]
    [JsonPropertyName("serviceUrl")]
    public required string ServiceUrl { get; init; }

    /// <summary>Service name.</summary>
    [JsonPropertyName("serviceName")]
    public required string ServiceName { get; init; }

    /// <summary>Service description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>Spatial reference WKID advertised by the service.</summary>
    [JsonPropertyName("spatialReferenceWkid")]
    public int? SpatialReferenceWkid { get; init; }

    /// <summary>Maximum records per query page advertised by the service.</summary>
    [JsonPropertyName("maxRecordCount")]
    public int? MaxRecordCount { get; init; }

    /// <summary>Available layers.</summary>
    [JsonPropertyName("layers")]
    public IReadOnlyList<GeoservicesLayerSummary> Layers { get; init; } = [];
}

/// <summary>
/// Request to start a single-layer GeoServices import job
/// (<c>POST /api/v1/admin/import/geoservices/start</c>).
/// </summary>
public sealed record GeoservicesStartImportRequest
{
    /// <summary>URL of the ArcGIS service.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The GeoServices import JSON contract represents source URLs as strings.")]
    [JsonPropertyName("serviceUrl")]
    public required string ServiceUrl { get; init; }

    /// <summary>Source layer id to import.</summary>
    [JsonPropertyName("layerId")]
    public int LayerId { get; init; }

    /// <summary>Target PostGIS table name.</summary>
    [JsonPropertyName("tableName")]
    public required string TableName { get; init; }

    /// <summary>Optional target schema for the imported table.</summary>
    [JsonPropertyName("targetSchema")]
    public string? TargetSchema { get; init; }

    /// <summary>Target SRID. Defaults to 4326 when omitted.</summary>
    [JsonPropertyName("targetSrid")]
    public int? TargetSrid { get; init; }

    /// <summary>Whether to overwrite an existing table of the same name.</summary>
    [JsonPropertyName("overwriteExisting")]
    public bool? OverwriteExisting { get; init; }

    /// <summary>Optional Esri WHERE clause used to filter source features.</summary>
    [JsonPropertyName("whereClause")]
    public string? WhereClause { get; init; }

    /// <summary>Optional explicit field selection. Imports every field when omitted.</summary>
    [JsonPropertyName("outputFields")]
    public IReadOnlyList<string>? OutputFields { get; init; }

    /// <summary>Optional page size used for bounded feature paging.</summary>
    [JsonPropertyName("batchSize")]
    public int? BatchSize { get; init; }

    /// <summary>Optional per-request timeout in seconds.</summary>
    [JsonPropertyName("requestTimeoutSeconds")]
    public int? RequestTimeoutSeconds { get; init; }

    /// <summary>Optional maximum retry attempts for transient source failures.</summary>
    [JsonPropertyName("maxRetries")]
    public int? MaxRetries { get; init; }

    /// <summary>Whether to auto-publish the imported layer. Defaults to true.</summary>
    [JsonPropertyName("autoPublish")]
    public bool? AutoPublish { get; init; }

    /// <summary>Optional target Honua service name for auto-publishing.</summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    /// <summary>
    /// Optional ArcGIS credential descriptor. Queued imports must use the
    /// <c>*SecretReference</c> fields; inline secrets are rejected.
    /// </summary>
    [JsonPropertyName("credentials")]
    public GeoservicesCredentialDescriptor? Credentials { get; init; }
}

/// <summary>Response returned when a GeoServices import job is queued (HTTP 202).</summary>
public sealed record GeoservicesImportJobResponse
{
    /// <summary>Queued job id.</summary>
    [JsonPropertyName("jobId")]
    public required string JobId { get; init; }

    /// <summary>Status message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>Relative URL to poll job status.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The GeoServices import JSON contract represents URLs as strings.")]
    [JsonPropertyName("statusUrl")]
    public required string StatusUrl { get; init; }

    /// <summary>Relative URL to cancel the job.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The GeoServices import JSON contract represents URLs as strings.")]
    [JsonPropertyName("cancelUrl")]
    public required string CancelUrl { get; init; }
}

/// <summary>Response returned by <c>GET /api/v1/admin/import/geoservices/jobs</c>.</summary>
public sealed record GeoservicesImportJobsResponse
{
    /// <summary>Active (non-terminal) import jobs.</summary>
    [JsonPropertyName("jobs")]
    public IReadOnlyList<GeoservicesImportProgress> Jobs { get; init; } = [];
}

/// <summary>Response returned by <c>POST /api/v1/admin/import/geoservices/jobs/{jobId}/cancel</c>.</summary>
public sealed record GeoservicesImportCancelResponse
{
    /// <summary>The cancelled job id.</summary>
    [JsonPropertyName("jobId")]
    public required string JobId { get; init; }

    /// <summary>Confirmation message.</summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

/// <summary>
/// Status of a GeoServices import job. Every terminal state the server can report is represented:
/// <see cref="Completed"/> (clean success), <see cref="NeedsReview"/> (published but a hard
/// reconciliation finding blocked completion — data and layer are left in place for operator
/// review), <see cref="Failed"/>, and <see cref="Cancelled"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GeoservicesImportStatus>))]
public enum GeoservicesImportStatus
{
    /// <summary>Import is queued for processing.</summary>
    Queued,

    /// <summary>Discovering service metadata.</summary>
    Discovering,

    /// <summary>Retrieving features from the remote service.</summary>
    RetrievingFeatures,

    /// <summary>Creating the target PostGIS table.</summary>
    CreatingTable,

    /// <summary>Inserting features into PostGIS.</summary>
    InsertingFeatures,

    /// <summary>Publishing the imported layer.</summary>
    Publishing,

    /// <summary>Copying source feature attachments into the Honua attachment store.</summary>
    CopyingAttachments,

    /// <summary>Post-publish reconciliation is comparing the published layer against the source snapshot.</summary>
    Validating,

    /// <summary>Import completed successfully.</summary>
    Completed,

    /// <summary>
    /// The import published data but a hard reconciliation finding routed the run to operator
    /// review before it could be declared complete.
    /// </summary>
    NeedsReview,

    /// <summary>Import failed with errors.</summary>
    Failed,

    /// <summary>Import was cancelled.</summary>
    Cancelled
}

/// <summary>
/// Progress and terminal-state information for one GeoServices import job. Returned by discover job
/// listing, job status polling, and the bounded-wait helper.
/// </summary>
public sealed record GeoservicesImportProgress
{
    /// <summary>Unique job identifier.</summary>
    [JsonPropertyName("jobId")]
    public required string JobId { get; init; }

    /// <summary>Current job status.</summary>
    [JsonPropertyName("status")]
    public required GeoservicesImportStatus Status { get; init; }

    /// <summary>Number of features processed so far.</summary>
    [JsonPropertyName("featuresProcessed")]
    public int FeaturesProcessed { get; init; }

    /// <summary>Estimated total feature count, when known.</summary>
    [JsonPropertyName("estimatedTotalFeatures")]
    public int? EstimatedTotalFeatures { get; init; }

    /// <summary>Number of paged batches completed.</summary>
    [JsonPropertyName("batchesCompleted")]
    public int BatchesCompleted { get; init; }

    /// <summary>Total number of paged batches, when known.</summary>
    [JsonPropertyName("totalBatches")]
    public int? TotalBatches { get; init; }

    /// <summary>Number of features that failed to import.</summary>
    [JsonPropertyName("failedFeatures")]
    public int FailedFeatures { get; init; }

    /// <summary>Number of feature attachments copied so far.</summary>
    [JsonPropertyName("attachmentsProcessed")]
    public int AttachmentsProcessed { get; init; }

    /// <summary>Number of source attachments that failed to copy.</summary>
    [JsonPropertyName("failedAttachments")]
    public int FailedAttachments { get; init; }

    /// <summary>Progress percentage (0-100), null when the total is unknown.</summary>
    [JsonPropertyName("percentComplete")]
    public double? PercentComplete { get; init; }

    /// <summary>Source ArcGIS service URL.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The GeoServices import JSON contract represents source URLs as strings.")]
    [JsonPropertyName("sourceServiceUrl")]
    public required string SourceServiceUrl { get; init; }

    /// <summary>Stable source kind identifier.</summary>
    [JsonPropertyName("sourceKind")]
    public string SourceKind { get; init; } = "arcgis-geoservices-rest";

    /// <summary>Source layer id.</summary>
    [JsonPropertyName("sourceLayerId")]
    public required int SourceLayerId { get; init; }

    /// <summary>Source layer name, when discovered.</summary>
    [JsonPropertyName("sourceLayerName")]
    public string? SourceLayerName { get; init; }

    /// <summary>Target table name.</summary>
    [JsonPropertyName("tableName")]
    public required string TableName { get; init; }

    /// <summary>Target Honua service name, when auto-publish was requested.</summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    /// <summary>Published Honua layer id once auto-publish completed successfully.</summary>
    [JsonPropertyName("publishedLayerId")]
    public int? PublishedLayerId { get; init; }

    /// <summary>UTC instant the import started.</summary>
    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>UTC instant the import completed, null while running.</summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Error message when <see cref="Status"/> is <see cref="GeoservicesImportStatus.Failed"/>.</summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    /// <summary>Non-fatal warnings encountered during the import.</summary>
    [JsonPropertyName("warnings")]
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Human-readable description of the current processing phase.</summary>
    [JsonPropertyName("currentPhase")]
    public string? CurrentPhase { get; init; }

    /// <summary>
    /// Per-layer data-reconciliation findings produced by the <see cref="GeoservicesImportStatus.Validating"/>
    /// phase. Null until that phase completes, or when the import did not publish a queryable layer.
    /// </summary>
    [JsonPropertyName("reconciliationArtifact")]
    public MigrationReconciliationArtifact? ReconciliationArtifact { get; init; }

    /// <summary>
    /// Raw catalog-parity findings (schema/domain/identifier/relationship/attachment/subtype), preserved
    /// losslessly as the server emits them. Left untyped deliberately: this dimension has its own
    /// evolving schema (<c>MigrationCatalogReconciliationReport</c> server-side) and typed re-modeling
    /// here would drop unknown/new server classifications the moment the server adds one — the exact
    /// failure mode the parent acceptance criteria calls out. Consumers who need typed access to this
    /// dimension should read <see cref="JsonElement"/> members directly or track honua-server#4600.
    /// </summary>
    [JsonPropertyName("catalogReconciliationReport")]
    public JsonElement? CatalogReconciliationReport { get; init; }
}

/// <summary>
/// Deterministic per-run data-reconciliation artifact (count/geometry/content/extent probes),
/// produced by the <see cref="GeoservicesImportStatus.Validating"/> phase.
/// </summary>
public sealed record MigrationReconciliationArtifact
{
    /// <summary>Stable artifact kind identifier.</summary>
    [JsonPropertyName("artifactKind")]
    public string ArtifactKind { get; init; } = "honua.migration.reconciliation";

    /// <summary>Artifact schema version.</summary>
    [JsonPropertyName("artifactVersion")]
    public string ArtifactVersion { get; init; } = "1.0";

    /// <summary>Originating migration run identifier.</summary>
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    /// <summary>Source kind that produced the layer.</summary>
    [JsonPropertyName("sourceKind")]
    public required string SourceKind { get; init; }

    /// <summary>Aggregate classification across every probe: <c>pass</c>, <c>warn</c>, or <c>fail</c>.</summary>
    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    /// <summary>UTC instant reconciliation began.</summary>
    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>UTC instant reconciliation completed.</summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>Aggregate counts across every per-layer report.</summary>
    [JsonPropertyName("summary")]
    public required MigrationReconciliationSummary Summary { get; init; }

    /// <summary>Per-layer reconciliation reports, ordered by source layer id.</summary>
    [JsonPropertyName("layers")]
    public IReadOnlyList<MigrationReconciliationLayerReport> Layers { get; init; } = [];

    /// <summary>Sorted, deduplicated, secret-safe reason strings rolled up across every layer.</summary>
    [JsonPropertyName("reasons")]
    public IReadOnlyList<string> Reasons { get; init; } = [];

    /// <summary>Tolerances applied to this reconciliation run.</summary>
    [JsonPropertyName("options")]
    public required LayerReconciliationOptions Options { get; init; }
}

/// <summary>Aggregate counts for a <see cref="MigrationReconciliationArtifact"/>.</summary>
public sealed record MigrationReconciliationSummary
{
    /// <summary>Total per-layer reports.</summary>
    [JsonPropertyName("layerCount")]
    public int LayerCount { get; init; }

    /// <summary>Number of layers classified <c>pass</c>.</summary>
    [JsonPropertyName("passCount")]
    public int PassCount { get; init; }

    /// <summary>Number of layers classified <c>warn</c>.</summary>
    [JsonPropertyName("warnCount")]
    public int WarnCount { get; init; }

    /// <summary>Number of layers classified <c>fail</c>.</summary>
    [JsonPropertyName("failCount")]
    public int FailCount { get; init; }

    /// <summary>Number of layers classified <c>skipped</c>.</summary>
    [JsonPropertyName("skippedCount")]
    public int SkippedCount { get; init; }
}

/// <summary>Per-layer reconciliation report.</summary>
public sealed record MigrationReconciliationLayerReport
{
    /// <summary>Source-side layer identifier.</summary>
    [JsonPropertyName("sourceLayerId")]
    public required string SourceLayerId { get; init; }

    /// <summary>Optional source-side layer display name.</summary>
    [JsonPropertyName("sourceLayerName")]
    public string? SourceLayerName { get; init; }

    /// <summary>Honua catalog layer id, when the apply published one.</summary>
    [JsonPropertyName("targetHonuaLayerId")]
    public int? TargetHonuaLayerId { get; init; }

    /// <summary>Aggregate classification across the four probes for this layer.</summary>
    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    /// <summary>Feature-count probe.</summary>
    [JsonPropertyName("count")]
    public required MigrationReconciliationCountProbe Count { get; init; }

    /// <summary>Geometry-validity probe.</summary>
    [JsonPropertyName("geometry")]
    public required MigrationReconciliationGeometryProbe Geometry { get; init; }

    /// <summary>Content / attribute-keys probe.</summary>
    [JsonPropertyName("content")]
    public required MigrationReconciliationContentProbe Content { get; init; }

    /// <summary>Spatial-extent probe.</summary>
    [JsonPropertyName("extent")]
    public required MigrationReconciliationExtentProbe Extent { get; init; }
}

/// <summary>Feature-count reconciliation probe result.</summary>
public sealed record MigrationReconciliationCountProbe
{
    /// <summary>Source-side count snapshot at apply time.</summary>
    [JsonPropertyName("sourceCount")]
    public long? SourceCount { get; init; }

    /// <summary>Target-side count returned by Honua post-apply.</summary>
    [JsonPropertyName("targetCount")]
    public long? TargetCount { get; init; }

    /// <summary>Target minus source. Null when either side is unavailable.</summary>
    [JsonPropertyName("delta")]
    public long? Delta { get; init; }

    /// <summary>Absolute value of delta divided by source. Null when source is zero or unavailable.</summary>
    [JsonPropertyName("deltaRatio")]
    public double? DeltaRatio { get; init; }

    /// <summary>Secret-safe filter mirror used on the count query, when one was supplied.</summary>
    [JsonPropertyName("filterMirror")]
    public string? FilterMirror { get; init; }

    /// <summary>Pass / warn / fail / skipped.</summary>
    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    /// <summary>Operator-visible explanation. Null when the probe passed.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>Geometry-validity reconciliation probe result.</summary>
public sealed record MigrationReconciliationGeometryProbe
{
    /// <summary>Number of features sampled.</summary>
    [JsonPropertyName("sampled")]
    public int Sampled { get; init; }

    /// <summary>Number of sampled features whose geometry was present and well-formed.</summary>
    [JsonPropertyName("valid")]
    public int Valid { get; init; }

    /// <summary>Valid divided by sampled, or 1 when nothing was sampled.</summary>
    [JsonPropertyName("ratio")]
    public double Ratio { get; init; }

    /// <summary>Pass / warn / fail / skipped.</summary>
    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    /// <summary>Operator-visible explanation. Null when the probe passed.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>Content / attribute-keys reconciliation probe result.</summary>
public sealed record MigrationReconciliationContentProbe
{
    /// <summary>Sorted source-side field names.</summary>
    [JsonPropertyName("sourceFieldNames")]
    public IReadOnlyList<string> SourceFieldNames { get; init; } = [];

    /// <summary>Sorted target-side field names sampled from the published layer.</summary>
    [JsonPropertyName("targetFieldNames")]
    public IReadOnlyList<string> TargetFieldNames { get; init; } = [];

    /// <summary>Source field names that were not present on the target. Hard-failure trigger.</summary>
    [JsonPropertyName("missingOnTarget")]
    public IReadOnlyList<string> MissingOnTarget { get; init; } = [];

    /// <summary>Target field names that were not present in the source (for example an ObjectID remap).</summary>
    [JsonPropertyName("extraOnTarget")]
    public IReadOnlyList<string> ExtraOnTarget { get; init; } = [];

    /// <summary>Pass / warn / fail / skipped.</summary>
    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    /// <summary>Operator-visible explanation. Null when the probe passed.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>Spatial-extent reconciliation probe result.</summary>
public sealed record MigrationReconciliationExtentProbe
{
    /// <summary>Source-side extent snapshot at apply time.</summary>
    [JsonPropertyName("source")]
    public MigrationExtentBox? Source { get; init; }

    /// <summary>Target-side extent returned by Honua post-apply.</summary>
    [JsonPropertyName("target")]
    public MigrationExtentBox? Target { get; init; }

    /// <summary>
    /// Absolute source/target extent differences normalized by source dimension. Null when either
    /// side is unavailable or the source has zero width/height.
    /// </summary>
    [JsonPropertyName("maxDimensionDelta")]
    public double? MaxDimensionDelta { get; init; }

    /// <summary>Pass / warn / fail / skipped.</summary>
    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    /// <summary>Operator-visible explanation. Null when the probe passed.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>Flat extent representation used by reconciliation probes.</summary>
public sealed record MigrationExtentBox
{
    /// <summary>Minimum X coordinate.</summary>
    [JsonPropertyName("minX")]
    public required double MinX { get; init; }

    /// <summary>Minimum Y coordinate.</summary>
    [JsonPropertyName("minY")]
    public required double MinY { get; init; }

    /// <summary>Maximum X coordinate.</summary>
    [JsonPropertyName("maxX")]
    public required double MaxX { get; init; }

    /// <summary>Maximum Y coordinate.</summary>
    [JsonPropertyName("maxY")]
    public required double MaxY { get; init; }

    /// <summary>Spatial reference SRID (defaults to 4326 when unknown).</summary>
    [JsonPropertyName("srid")]
    public required int Srid { get; init; }
}

/// <summary>Reconciliation tolerances applied to a run, persisted so an audit can replay the gate.</summary>
public sealed record LayerReconciliationOptions
{
    /// <summary>Sample size used for the geometry-validity and content probes. Clamped to [1, 10000].</summary>
    [JsonPropertyName("sampleSize")]
    public int SampleSize { get; init; } = 100;

    /// <summary>Count delta-ratio at or below which the count probe records <c>pass</c>.</summary>
    [JsonPropertyName("countWarnRatio")]
    public double CountWarnRatio { get; init; } = 0.05;

    /// <summary>Count delta-ratio above which the count probe records <c>fail</c>.</summary>
    [JsonPropertyName("countFailRatio")]
    public double CountFailRatio { get; init; } = 0.20;

    /// <summary>Geometry validity ratio at or above which the geometry probe records <c>pass</c>.</summary>
    [JsonPropertyName("geometryPassRatio")]
    public double GeometryPassRatio { get; init; } = 0.99;

    /// <summary>Geometry validity ratio at or above which the geometry probe records <c>warn</c>.</summary>
    [JsonPropertyName("geometryWarnRatio")]
    public double GeometryWarnRatio { get; init; } = 0.95;

    /// <summary>Extent dimension tolerance, expressed as a ratio of the source dimension.</summary>
    [JsonPropertyName("extentTolerance")]
    public double ExtentTolerance { get; init; } = 0.001;
}

// ── Batch (multi-layer) import lifecycle ────────────────────────────────

/// <summary>
/// One layer-import specification in a batch (footprint) import request.
/// </summary>
public sealed record MigrationBatchLayerSpec
{
    /// <summary>Stable source resource id (must match manifest ids for relationship-apply).</summary>
    [JsonPropertyName("sourceResourceId")]
    public required string SourceResourceId { get; init; }

    /// <summary>Source ArcGIS service URL.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The GeoServices import JSON contract represents source URLs as strings.")]
    [JsonPropertyName("serviceUrl")]
    public required string ServiceUrl { get; init; }

    /// <summary>Source layer id within the service.</summary>
    [JsonPropertyName("layerId")]
    public int LayerId { get; init; }

    /// <summary>Target PostGIS table name.</summary>
    [JsonPropertyName("tableName")]
    public required string TableName { get; init; }

    /// <summary>Optional target schema for the imported table.</summary>
    [JsonPropertyName("targetSchema")]
    public string? TargetSchema { get; init; }

    /// <summary>Optional target Honua service name for auto-publishing.</summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    /// <summary>Source resource ids this layer depends on (imported first).</summary>
    [JsonPropertyName("dependsOn")]
    public IReadOnlyList<string>? DependsOn { get; init; }
}

/// <summary>
/// Request to start an ordered, resumable batch (multi-layer) migration run from a footprint
/// selection (<c>POST /api/v1/admin/import/migrations</c>).
/// </summary>
public sealed record MigrationBatchStartRequest
{
    /// <summary>Source kind identifier, such as <c>arcgis-geoservices-rest</c>.</summary>
    [JsonPropertyName("sourceKind")]
    public required string SourceKind { get; init; }

    /// <summary>Redacted source URL describing the footprint origin (display only).</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The migration batch JSON contract represents source URLs as strings.")]
    [JsonPropertyName("sourceUrl")]
    public string? SourceUrl { get; init; }

    /// <summary>Optional operator-visible source display name.</summary>
    [JsonPropertyName("sourceDisplayName")]
    public string? SourceDisplayName { get; init; }

    /// <summary>Footprint layer-import specifications. At least one is required.</summary>
    [JsonPropertyName("layers")]
    public required IReadOnlyList<MigrationBatchLayerSpec> Layers { get; init; }

    /// <summary>Optional manifest JSON body used for post-publish relationship application.</summary>
    [JsonPropertyName("manifestBody")]
    public string? ManifestBody { get; init; }

    /// <summary>Whether to apply manifest relationship classes after all layers publish.</summary>
    [JsonPropertyName("applyRelationships")]
    public bool? ApplyRelationships { get; init; }
}

/// <summary>Per-child progress within a <see cref="MigrationBatchResponse"/>.</summary>
public sealed record MigrationBatchChildResponse
{
    /// <summary>Zero-based execution ordinal.</summary>
    [JsonPropertyName("ordinal")]
    public int Ordinal { get; init; }

    /// <summary>Stable source resource id.</summary>
    [JsonPropertyName("sourceResourceId")]
    public required string SourceResourceId { get; init; }

    /// <summary>Source ArcGIS service URL.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The migration batch JSON contract represents source URLs as strings.")]
    [JsonPropertyName("serviceUrl")]
    public required string ServiceUrl { get; init; }

    /// <summary>Source layer id.</summary>
    [JsonPropertyName("sourceLayerId")]
    public int SourceLayerId { get; init; }

    /// <summary>Target PostGIS table name.</summary>
    [JsonPropertyName("tableName")]
    public required string TableName { get; init; }

    /// <summary>Optional target Honua service name.</summary>
    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; init; }

    /// <summary>Source resource ids this child depends on.</summary>
    [JsonPropertyName("dependsOn")]
    public IReadOnlyList<string> DependsOn { get; init; } = [];

    /// <summary>Child status: <c>pending</c>, <c>running</c>, <c>succeeded</c>, <c>failed</c>, <c>needs-review</c>, <c>cancelled</c>.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>Per-layer import job id once queued. Pass to the single-layer job status/cancel/wait APIs.</summary>
    [JsonPropertyName("jobId")]
    public string? JobId { get; init; }

    /// <summary>Resolved Honua layer id once the child publishes.</summary>
    [JsonPropertyName("publishedLayerId")]
    public int? PublishedLayerId { get; init; }

    /// <summary>Operator-visible note recorded on failure or review.</summary>
    [JsonPropertyName("statusNote")]
    public string? StatusNote { get; init; }
}

/// <summary>
/// Response for batch start and batch status polling
/// (<c>POST /api/v1/admin/import/migrations</c>, <c>GET .../{batchId}</c>).
/// </summary>
public sealed record MigrationBatchResponse
{
    /// <summary>Stable batch id.</summary>
    [JsonPropertyName("batchId")]
    public required string BatchId { get; init; }

    /// <summary>Source kind.</summary>
    [JsonPropertyName("sourceKind")]
    public required string SourceKind { get; init; }

    /// <summary>Redacted source URL.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The migration run JSON contract represents source URLs as strings.")]
    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; init; } = string.Empty;

    /// <summary>Operator-visible source display name.</summary>
    [JsonPropertyName("sourceDisplayName")]
    public string? SourceDisplayName { get; init; }

    /// <summary>Rolled-up batch status: <c>running</c>, <c>succeeded</c>, <c>failed</c>, <c>cancelled</c>, <c>needs-review</c>.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>UTC start instant.</summary>
    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>UTC completion instant, null while running.</summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Total child layer imports in the footprint.</summary>
    [JsonPropertyName("totalChildren")]
    public int TotalChildren { get; init; }

    /// <summary>Number of succeeded children.</summary>
    [JsonPropertyName("succeededChildren")]
    public int SucceededChildren { get; init; }

    /// <summary>Number of failed children.</summary>
    [JsonPropertyName("failedChildren")]
    public int FailedChildren { get; init; }

    /// <summary>Number of cancelled children.</summary>
    [JsonPropertyName("cancelledChildren")]
    public int CancelledChildren { get; init; }

    /// <summary>Whether relationship-apply was requested.</summary>
    [JsonPropertyName("applyRelationships")]
    public bool ApplyRelationships { get; init; }

    /// <summary>Whether relationship-apply has run.</summary>
    [JsonPropertyName("relationshipsApplied")]
    public bool RelationshipsApplied { get; init; }

    /// <summary>Operator-visible note.</summary>
    [JsonPropertyName("statusNote")]
    public string? StatusNote { get; init; }

    /// <summary>Per-child progress, ordered by execution ordinal.</summary>
    [JsonPropertyName("children")]
    public IReadOnlyList<MigrationBatchChildResponse> Children { get; init; } = [];
}

// ── Migration run reconciliation report retrieval ───────────────────────

/// <summary>
/// Wire representation of a migration run returned by the run admin/reconciliation-report APIs.
/// </summary>
public sealed record MigrationRunDto
{
    /// <summary>Stable run id.</summary>
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    /// <summary>Source kind (for example <c>arcgis-geoservices-rest</c>).</summary>
    [JsonPropertyName("sourceKind")]
    public required string SourceKind { get; init; }

    /// <summary>Redacted source URL.</summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The migration run JSON contract represents source URLs as strings.")]
    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; init; } = string.Empty;

    /// <summary>Operator-visible source display name.</summary>
    [JsonPropertyName("sourceDisplayName")]
    public string? SourceDisplayName { get; init; }

    /// <summary>Lowercase status: <c>running</c>, <c>succeeded</c>, <c>failed</c>, <c>cancelled</c>.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    /// <summary>UTC start instant.</summary>
    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>UTC completion instant, null while running.</summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Storage reference for the evidence pack.</summary>
    [JsonPropertyName("evidencePackRef")]
    public string? EvidencePackRef { get; init; }

    /// <summary>Fingerprint of the evidence pack.</summary>
    [JsonPropertyName("evidencePackFingerprint")]
    public string? EvidencePackFingerprint { get; init; }

    /// <summary>Operator-visible note recorded on cancel or failure.</summary>
    [JsonPropertyName("statusNote")]
    public string? StatusNote { get; init; }

    /// <summary>True when the evidence pack is available for download.</summary>
    [JsonPropertyName("hasEvidencePack")]
    public bool HasEvidencePack { get; init; }

    /// <summary>Fingerprint of the signed reconciliation scorecard, when recorded.</summary>
    [JsonPropertyName("reconciliationScorecardFingerprint")]
    public string? ReconciliationScorecardFingerprint { get; init; }

    /// <summary>True when the reconciliation scorecard is available for download.</summary>
    [JsonPropertyName("hasReconciliationScorecard")]
    public bool HasReconciliationScorecard { get; init; }
}

/// <summary>Response envelope for <c>GET /api/v1/admin/migration/runs</c>.</summary>
public sealed record MigrationRunListResponse
{
    /// <summary>Records in this page.</summary>
    [JsonPropertyName("items")]
    public required IReadOnlyList<MigrationRunDto> Items { get; init; }

    /// <summary>Total matching records (pre-paging).</summary>
    [JsonPropertyName("totalCount")]
    public required long TotalCount { get; init; }

    /// <summary>Echo of the applied page limit.</summary>
    [JsonPropertyName("limit")]
    public required int Limit { get; init; }

    /// <summary>Echo of the applied page offset.</summary>
    [JsonPropertyName("offset")]
    public required int Offset { get; init; }
}

/// <summary>Request body for <c>POST /api/v1/admin/migration/runs/{runId}/cancel</c>.</summary>
public sealed record MigrationRunCancelApiRequest
{
    /// <summary>Optional operator-supplied note explaining why the run was cancelled.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// A raw JSON artifact downloaded from a migration run (for example the evidence pack), together
/// with its ETag when the server supplied one.
/// </summary>
public sealed record MigrationRunArtifactDownload
{
    /// <summary>The raw JSON body, verbatim.</summary>
    public required string Body { get; init; }

    /// <summary>The response ETag, when the server supplied one (mirrors the artifact's fingerprint).</summary>
    public string? ETag { get; init; }
}

/// <summary>Optional query filters for <see cref="MigrationRunListResponse"/> retrieval.</summary>
public sealed record MigrationRunListQuery
{
    /// <summary>Maximum records to return, clamped server-side to 100.</summary>
    public int? Limit { get; init; }

    /// <summary>Zero-based page offset.</summary>
    public int? Offset { get; init; }

    /// <summary>Optional source kind filter.</summary>
    public string? SourceKind { get; init; }

    /// <summary>Optional status filter: <c>running</c>, <c>succeeded</c>, <c>failed</c>, or <c>cancelled</c>.</summary>
    public string? Status { get; init; }
}

/// <summary>Signed, deterministic roll-up of a migration run's reconciliation outcome.</summary>
public sealed record MigrationReconciliationScorecard
{
    /// <summary>Stable artifact kind identifier.</summary>
    [JsonPropertyName("artifactKind")]
    public string ArtifactKind { get; init; } = "honua.migration.reconciliation-scorecard";

    /// <summary>Artifact schema version.</summary>
    [JsonPropertyName("artifactVersion")]
    public string ArtifactVersion { get; init; } = "1.0";

    /// <summary>Originating migration run identifier.</summary>
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    /// <summary>Source kind that produced the layers.</summary>
    [JsonPropertyName("sourceKind")]
    public required string SourceKind { get; init; }

    /// <summary>UTC instant the scorecard was generated.</summary>
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Overall gate verdict for the run: <c>pass</c>, <c>warn</c>, or <c>fail</c>.</summary>
    [JsonPropertyName("verdict")]
    public required string Verdict { get; init; }

    /// <summary>Data-reconciliation dimension (count/geometry/content/extent + catalog parity).</summary>
    [JsonPropertyName("dataReconciliation")]
    public required MigrationScorecardDataReconciliation DataReconciliation { get; init; }

    /// <summary>Capability-parity dimension (per-construct automation/fidelity), kept distinct.</summary>
    [JsonPropertyName("capabilityParity")]
    public required MigrationScorecardCapabilityParity CapabilityParity { get; init; }

    /// <summary>SHA-256 fingerprint of the scorecard body, formatted <c>sha256:&lt;hex&gt;</c>.</summary>
    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; init; }
}

/// <summary>Data-reconciliation dimension of a <see cref="MigrationReconciliationScorecard"/>.</summary>
public sealed record MigrationScorecardDataReconciliation
{
    /// <summary>Aggregate data-reconciliation classification: <c>pass</c>, <c>warn</c>, or <c>fail</c>.</summary>
    [JsonPropertyName("classification")]
    public required string Classification { get; init; }

    /// <summary>Total layers covered by data reconciliation.</summary>
    [JsonPropertyName("layerCount")]
    public int LayerCount { get; init; }

    /// <summary>Layers whose data reconciled cleanly.</summary>
    [JsonPropertyName("passCount")]
    public int PassCount { get; init; }

    /// <summary>Layers with warn-level data-reconciliation findings.</summary>
    [JsonPropertyName("warnCount")]
    public int WarnCount { get; init; }

    /// <summary>Layers with at least one hard data-reconciliation finding.</summary>
    [JsonPropertyName("failCount")]
    public int FailCount { get; init; }

    /// <summary>Layers skipped (for example, no published target to reconcile against).</summary>
    [JsonPropertyName("skippedCount")]
    public int SkippedCount { get; init; }

    /// <summary>Number of catalog-parity findings folded into this dimension.</summary>
    [JsonPropertyName("catalogFindingCount")]
    public int CatalogFindingCount { get; init; }

    /// <summary>Per-layer scorecard rows, ordered deterministically by source layer id.</summary>
    [JsonPropertyName("layers")]
    public IReadOnlyList<MigrationScorecardLayer> Layers { get; init; } = [];

    /// <summary>Sorted, deduplicated, secret-safe blocking/advisory reasons rolled up across all layers.</summary>
    [JsonPropertyName("reasons")]
    public IReadOnlyList<string> Reasons { get; init; } = [];
}

/// <summary>One per-layer row in the data-reconciliation dimension of the scorecard.</summary>
public sealed record MigrationScorecardLayer
{
    /// <summary>Source-side layer identifier.</summary>
    [JsonPropertyName("sourceLayerId")]
    public required string SourceLayerId { get; init; }

    /// <summary>Optional source-side layer display name.</summary>
    [JsonPropertyName("sourceLayerName")]
    public string? SourceLayerName { get; init; }

    /// <summary>Honua catalog layer id, when a queryable layer was published.</summary>
    [JsonPropertyName("targetHonuaLayerId")]
    public int? TargetHonuaLayerId { get; init; }

    /// <summary>Aggregate per-layer data-reconciliation classification.</summary>
    [JsonPropertyName("classification")]
    public required string Classification { get; init; }
}

/// <summary>Capability-parity dimension of a <see cref="MigrationReconciliationScorecard"/>.</summary>
public sealed record MigrationScorecardCapabilityParity
{
    /// <summary>Total source constructs assessed for capability parity.</summary>
    [JsonPropertyName("constructCount")]
    public int ConstructCount { get; init; }

    /// <summary>Constructs Honua can express without operator involvement.</summary>
    [JsonPropertyName("automatedCount")]
    public int AutomatedCount { get; init; }

    /// <summary>Constructs Honua can express with operator assistance.</summary>
    [JsonPropertyName("assistedCount")]
    public int AssistedCount { get; init; }

    /// <summary>Constructs requiring manual review to express.</summary>
    [JsonPropertyName("manualReviewCount")]
    public int ManualReviewCount { get; init; }

    /// <summary>Constructs Honua cannot express.</summary>
    [JsonPropertyName("unsupportedCount")]
    public int UnsupportedCount { get; init; }

    /// <summary>Capability-parity ratio in [0, 1]. Advisory only; never gates the run.</summary>
    [JsonPropertyName("parityRatio")]
    public double ParityRatio { get; init; }
}
