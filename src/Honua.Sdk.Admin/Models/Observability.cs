// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Represents a recent error captured by the server.
/// </summary>
public sealed class RecentError
{
    /// <summary>
    /// Unique identifier for the error.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Source of the error.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; init; }

    /// <summary>
    /// Stack trace associated with the error.
    /// </summary>
    [JsonPropertyName("stackTrace")]
    public string? StackTrace { get; init; }

    /// <summary>
    /// Severity level of the error.
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; init; } = string.Empty;

    /// <summary>
    /// When the error occurred.
    /// </summary>
    [JsonPropertyName("occurredAt")]
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Number of times this error has occurred.
    /// </summary>
    [JsonPropertyName("count")]
    public int Count { get; init; } = 1;
}

/// <summary>
/// Status of the telemetry subsystem.
/// </summary>
public sealed class TelemetryStatus
{
    /// <summary>
    /// Whether telemetry is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    /// <summary>
    /// Telemetry provider name.
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Telemetry export endpoint.
    /// </summary>
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; init; }

    /// <summary>
    /// Whether metrics collection is enabled.
    /// </summary>
    [JsonPropertyName("metricsEnabled")]
    public bool MetricsEnabled { get; init; }

    /// <summary>
    /// Whether trace collection is enabled.
    /// </summary>
    [JsonPropertyName("tracesEnabled")]
    public bool TracesEnabled { get; init; }

    /// <summary>
    /// Whether log export is enabled.
    /// </summary>
    [JsonPropertyName("logsEnabled")]
    public bool LogsEnabled { get; init; }

    /// <summary>
    /// When telemetry was last exported.
    /// </summary>
    [JsonPropertyName("lastExportAt")]
    public DateTimeOffset? LastExportAt { get; init; }
}

/// <summary>
/// Status of database migrations.
/// </summary>
public sealed class MigrationStatus
{
    /// <summary>
    /// Current schema version.
    /// </summary>
    [JsonPropertyName("currentVersion")]
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>
    /// Target schema version, if an upgrade is available.
    /// </summary>
    [JsonPropertyName("targetVersion")]
    public string? TargetVersion { get; init; }

    /// <summary>
    /// List of pending migration identifiers.
    /// </summary>
    [JsonPropertyName("pendingMigrations")]
    public IReadOnlyList<string> PendingMigrations { get; init; } = [];

    /// <summary>
    /// List of migrations that have been applied.
    /// </summary>
    [JsonPropertyName("appliedMigrations")]
    public IReadOnlyList<AppliedMigration> AppliedMigrations { get; init; } = [];

    /// <summary>
    /// Whether the schema is up to date.
    /// </summary>
    [JsonPropertyName("isUpToDate")]
    public bool IsUpToDate { get; init; }
}

/// <summary>
/// A migration that has been applied to the database.
/// </summary>
public sealed class AppliedMigration
{
    /// <summary>
    /// Migration version identifier.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable migration name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// When the migration was applied.
    /// </summary>
    [JsonPropertyName("appliedAt")]
    public DateTimeOffset AppliedAt { get; init; }
}
