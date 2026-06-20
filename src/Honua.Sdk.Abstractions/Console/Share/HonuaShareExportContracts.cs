// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Console.Share;

/// <summary>
/// Destination family a scheduled Share export can target. Mirrors the server
/// <c>shareExportDestinationType</c> contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaShareExportDestinationType>))]
public enum HonuaShareExportDestinationType
{
    /// <summary>Amazon S3 (or S3-compatible) object storage destination.</summary>
    [JsonStringEnumMemberName("S3")]
    S3,

    /// <summary>SFTP file-transfer destination.</summary>
    [JsonStringEnumMemberName("Sftp")]
    Sftp,

    /// <summary>HTTP webhook delivery destination.</summary>
    [JsonStringEnumMemberName("Webhook")]
    Webhook,

    /// <summary>Point-in-time Share access audit snapshot destination.</summary>
    [JsonStringEnumMemberName("AuditSnapshot")]
    AuditSnapshot,
}

/// <summary>
/// Resolved availability of a destination family in the server build and
/// environment. Mirrors the server <c>shareExportDestinationStatus</c> contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaShareExportDestinationStatus>))]
public enum HonuaShareExportDestinationStatus
{
    /// <summary>A worker is registered and execution can be backed by the job runner.</summary>
    [JsonStringEnumMemberName("Supported")]
    Supported,

    /// <summary>No worker is registered for this destination family in the current build.</summary>
    [JsonStringEnumMemberName("Unsupported")]
    Unsupported,

    /// <summary>The destination family is known but lacks credentials/configuration to run.</summary>
    [JsonStringEnumMemberName("NotConfigured")]
    NotConfigured,
}

/// <summary>
/// Whether a scheduled export's automatic firing is active or paused. Mirrors the
/// server <c>shareExportScheduleState</c> contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaShareExportScheduleState>))]
public enum HonuaShareExportScheduleState
{
    /// <summary>The schedule is active and fires on its cron expression.</summary>
    [JsonStringEnumMemberName("Active")]
    Active,

    /// <summary>The schedule is paused and will not fire automatically.</summary>
    [JsonStringEnumMemberName("Paused")]
    Paused,
}

/// <summary>
/// What initiated an export run. Mirrors the server <c>shareExportTriggerKind</c>
/// contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaShareExportTriggerKind>))]
public enum HonuaShareExportTriggerKind
{
    /// <summary>The run was initiated by the schedule.</summary>
    [JsonStringEnumMemberName("Scheduled")]
    Scheduled,

    /// <summary>The run was initiated by an explicit manual trigger.</summary>
    [JsonStringEnumMemberName("Manual")]
    Manual,
}

/// <summary>
/// Lifecycle state of an individual export run. Mirrors the server
/// <c>shareExportRunStatus</c> contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaShareExportRunStatus>))]
public enum HonuaShareExportRunStatus
{
    /// <summary>The run is queued and not yet started.</summary>
    [JsonStringEnumMemberName("Queued")]
    Queued,

    /// <summary>The run is actively executing.</summary>
    [JsonStringEnumMemberName("Running")]
    Running,

    /// <summary>The run completed successfully.</summary>
    [JsonStringEnumMemberName("Succeeded")]
    Succeeded,

    /// <summary>The run failed.</summary>
    [JsonStringEnumMemberName("Failed")]
    Failed,

    /// <summary>The run was cancelled.</summary>
    [JsonStringEnumMemberName("Cancelled")]
    Cancelled,
}

/// <summary>
/// Request body to create or replace a scheduled Share export definition. Maps to
/// the server share-export definition contract.
/// </summary>
public sealed record HonuaShareExportDefinitionRequest
{
    /// <summary>Optional share/resource identifier the export targets.</summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    /// <summary>Service the exported layer belongs to.</summary>
    [JsonPropertyName("serviceName")]
    public required string ServiceName { get; init; }

    /// <summary>Layer identifier within the service.</summary>
    [JsonPropertyName("layerId")]
    public required int LayerId { get; init; }

    /// <summary>Operator-facing display name.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>Destination family for the export.</summary>
    [JsonPropertyName("destinationType")]
    public required HonuaShareExportDestinationType DestinationType { get; init; }

    /// <summary>
    /// Destination configuration key/value pairs. Secret material must be passed as
    /// a <c>*Ref</c>/<c>*Reference</c> key; the server rejects raw secrets and
    /// redacts secret-looking values on read.
    /// </summary>
    [JsonPropertyName("destinationConfig")]
    public IReadOnlyDictionary<string, string> DestinationConfig { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Export format (for example <c>geojson</c> or <c>csv</c>).</summary>
    [JsonPropertyName("format")]
    public required string Format { get; init; }

    /// <summary>Schedule expression (for example a cron string).</summary>
    [JsonPropertyName("schedule")]
    public required string Schedule { get; init; }

    /// <summary>Optional initial schedule state; defaults to active server-side.</summary>
    [JsonPropertyName("scheduleState")]
    public HonuaShareExportScheduleState? ScheduleState { get; init; }
}

/// <summary>
/// A scheduled Share export definition as returned by the server. Secret-looking
/// destination configuration values are redacted to <c>redacted</c> on read.
/// </summary>
public sealed record HonuaShareExportDefinition
{
    /// <summary>Stable export-definition identifier.</summary>
    [JsonPropertyName("exportId")]
    public required string ExportId { get; init; }

    /// <summary>Optional share/resource identifier the export targets.</summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    /// <summary>Service the exported layer belongs to.</summary>
    [JsonPropertyName("serviceName")]
    public required string ServiceName { get; init; }

    /// <summary>Layer identifier within the service.</summary>
    [JsonPropertyName("layerId")]
    public required int LayerId { get; init; }

    /// <summary>Operator-facing display name.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>Destination family for the export.</summary>
    [JsonPropertyName("destinationType")]
    public HonuaShareExportDestinationType DestinationType { get; init; }

    /// <summary>Resolved destination availability in the server build/environment.</summary>
    [JsonPropertyName("destinationStatus")]
    public HonuaShareExportDestinationStatus DestinationStatus { get; init; }

    /// <summary>Destination configuration key/value pairs (secrets redacted on read).</summary>
    [JsonPropertyName("destinationConfig")]
    public IReadOnlyDictionary<string, string> DestinationConfig { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Export format.</summary>
    [JsonPropertyName("format")]
    public required string Format { get; init; }

    /// <summary>Schedule expression.</summary>
    [JsonPropertyName("schedule")]
    public required string Schedule { get; init; }

    /// <summary>Current schedule state.</summary>
    [JsonPropertyName("scheduleState")]
    public HonuaShareExportScheduleState ScheduleState { get; init; }

    /// <summary>Timestamp the definition was created, in UTC.</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Timestamp the definition was last updated, in UTC.</summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Timestamp of the last run, when one has occurred, in UTC.</summary>
    [JsonPropertyName("lastRunAt")]
    public DateTimeOffset? LastRunAt { get; init; }

    /// <summary>Timestamp of the next scheduled run, when planned, in UTC.</summary>
    [JsonPropertyName("nextRunAt")]
    public DateTimeOffset? NextRunAt { get; init; }
}

/// <summary>
/// A cursor-paged list of Share export definitions.
/// </summary>
public sealed record HonuaShareExportDefinitionPage
{
    /// <summary>The definitions in this page.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<HonuaShareExportDefinition> Items { get; init; } = [];

    /// <summary>Opaque cursor for the next page, or <c>null</c> when exhausted.</summary>
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; init; }
}

/// <summary>
/// An individual Share export run record.
/// </summary>
public sealed record HonuaShareExportRun
{
    /// <summary>Stable run identifier.</summary>
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    /// <summary>Identifier of the owning export definition.</summary>
    [JsonPropertyName("exportId")]
    public required string ExportId { get; init; }

    /// <summary>What initiated the run.</summary>
    [JsonPropertyName("triggerKind")]
    public HonuaShareExportTriggerKind TriggerKind { get; init; }

    /// <summary>Lifecycle status of the run.</summary>
    [JsonPropertyName("status")]
    public HonuaShareExportRunStatus Status { get; init; }

    /// <summary>Identifier of the backing job run, when dispatched.</summary>
    [JsonPropertyName("jobRunId")]
    public string? JobRunId { get; init; }

    /// <summary>Timestamp the run was triggered, in UTC.</summary>
    [JsonPropertyName("triggeredAt")]
    public DateTimeOffset TriggeredAt { get; init; }

    /// <summary>Timestamp the run started executing, in UTC.</summary>
    [JsonPropertyName("startedAt")]
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Timestamp the run reached a terminal state, in UTC.</summary>
    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>Human-readable summary of the export target.</summary>
    [JsonPropertyName("targetSummary")]
    public string? TargetSummary { get; init; }

    /// <summary>Identifiers/URIs of artifacts the run produced.</summary>
    [JsonPropertyName("resultArtifacts")]
    public IReadOnlyList<string> ResultArtifacts { get; init; } = [];

    /// <summary>Stable error code/message when the run failed.</summary>
    [JsonPropertyName("lastError")]
    public string? LastError { get; init; }
}

/// <summary>
/// A cursor-paged list of Share export runs.
/// </summary>
public sealed record HonuaShareExportRunPage
{
    /// <summary>The runs in this page.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<HonuaShareExportRun> Items { get; init; } = [];

    /// <summary>Opaque cursor for the next page, or <c>null</c> when exhausted.</summary>
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; init; }
}

/// <summary>
/// Identifies the item a Share traffic projection is scoped to.
/// </summary>
public sealed record HonuaShareItemRef
{
    /// <summary>Optional share/resource identifier.</summary>
    [JsonPropertyName("resourceId")]
    public string? ResourceId { get; init; }

    /// <summary>Service name of the item.</summary>
    [JsonPropertyName("serviceName")]
    public required string ServiceName { get; init; }

    /// <summary>Layer identifier of the item.</summary>
    [JsonPropertyName("layerId")]
    public required int LayerId { get; init; }
}

/// <summary>
/// Share request counts broken out by interaction type.
/// </summary>
public sealed record HonuaShareTrafficCounts
{
    /// <summary>Anonymous reads against a public-indexed item.</summary>
    [JsonPropertyName("public")]
    public long Public { get; init; }

    /// <summary>Reads redeemed through a public link.</summary>
    [JsonPropertyName("publicLink")]
    public long PublicLink { get; init; }

    /// <summary>Reads served to an embed host.</summary>
    [JsonPropertyName("embed")]
    public long Embed { get; init; }

    /// <summary>Open-data landing/projection reads.</summary>
    [JsonPropertyName("openData")]
    public long OpenData { get; init; }

    /// <summary>DCAT / data.json reads.</summary>
    [JsonPropertyName("dcat")]
    public long Dcat { get; init; }

    /// <summary>STAC catalog reads.</summary>
    [JsonPropertyName("stac")]
    public long Stac { get; init; }

    /// <summary>Export deliveries.</summary>
    [JsonPropertyName("export")]
    public long Export { get; init; }
}

/// <summary>
/// Aggregate Share traffic summary over a period.
/// </summary>
public sealed record HonuaShareTrafficSummary
{
    /// <summary>Item the summary is scoped to, or <c>null</c> for the aggregate.</summary>
    [JsonPropertyName("itemRef")]
    public HonuaShareItemRef? ItemRef { get; init; }

    /// <summary>Inclusive start of the period, in UTC.</summary>
    [JsonPropertyName("periodStart")]
    public DateTimeOffset PeriodStart { get; init; }

    /// <summary>Exclusive end of the period, in UTC.</summary>
    [JsonPropertyName("periodEnd")]
    public DateTimeOffset PeriodEnd { get; init; }

    /// <summary>Request counts by interaction type over the period.</summary>
    [JsonPropertyName("byInteractionType")]
    public required HonuaShareTrafficCounts ByInteractionType { get; init; }

    /// <summary>Total requests over the period.</summary>
    [JsonPropertyName("totalRequests")]
    public long TotalRequests { get; init; }
}

/// <summary>
/// A single time-series bucket of Share traffic.
/// </summary>
public sealed record HonuaShareTrafficBucket
{
    /// <summary>Inclusive start of the bucket, in UTC.</summary>
    [JsonPropertyName("bucketStart")]
    public DateTimeOffset BucketStart { get; init; }

    /// <summary>Request counts by interaction type within the bucket.</summary>
    [JsonPropertyName("byInteractionType")]
    public required HonuaShareTrafficCounts ByInteractionType { get; init; }

    /// <summary>Total requests within the bucket.</summary>
    [JsonPropertyName("total")]
    public long Total { get; init; }
}

/// <summary>
/// A bucketed Share traffic time series over a period.
/// </summary>
public sealed record HonuaShareTrafficSeries
{
    /// <summary>Item the series is scoped to, or <c>null</c> for the aggregate.</summary>
    [JsonPropertyName("itemRef")]
    public HonuaShareItemRef? ItemRef { get; init; }

    /// <summary>Inclusive start of the period, in UTC.</summary>
    [JsonPropertyName("periodStart")]
    public DateTimeOffset PeriodStart { get; init; }

    /// <summary>Exclusive end of the period, in UTC.</summary>
    [JsonPropertyName("periodEnd")]
    public DateTimeOffset PeriodEnd { get; init; }

    /// <summary>Width of each time-series bucket.</summary>
    [JsonPropertyName("bucketDuration")]
    public TimeSpan BucketDuration { get; init; }

    /// <summary>The traffic buckets, oldest first.</summary>
    [JsonPropertyName("buckets")]
    public IReadOnlyList<HonuaShareTrafficBucket> Buckets { get; init; } = [];
}

/// <summary>
/// Query parameters for listing scheduled Share export definitions.
/// </summary>
public sealed record HonuaShareExportDefinitionQuery
{
    /// <summary>Filter by service name.</summary>
    public string? ServiceName { get; init; }

    /// <summary>Filter by share/resource identifier.</summary>
    public string? ResourceId { get; init; }

    /// <summary>Filter by layer identifier.</summary>
    public int? LayerId { get; init; }

    /// <summary>Filter by destination family.</summary>
    public HonuaShareExportDestinationType? DestinationType { get; init; }

    /// <summary>Filter by schedule state.</summary>
    public HonuaShareExportScheduleState? ScheduleState { get; init; }

    /// <summary>Opaque page cursor from a prior response.</summary>
    public string? Cursor { get; init; }

    /// <summary>Page size; the server clamps to its supported range.</summary>
    public int? Limit { get; init; }
}

/// <summary>
/// Query parameters for a Share traffic summary or time series.
/// </summary>
public sealed record HonuaShareTrafficQuery
{
    /// <summary>Inclusive start of the period, in UTC; server defaults when omitted.</summary>
    public DateTimeOffset? PeriodStart { get; init; }

    /// <summary>Exclusive end of the period, in UTC; server defaults to now when omitted.</summary>
    public DateTimeOffset? PeriodEnd { get; init; }

    /// <summary>
    /// Bucket width in minutes for a time-series request; ignored for summaries.
    /// The server defaults and bounds the bucket count.
    /// </summary>
    public int? BucketMinutes { get; init; }
}
