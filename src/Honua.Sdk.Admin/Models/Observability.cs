// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Response payload for recent server errors.
/// </summary>
public sealed class RecentErrorsResponse
{
    /// <summary>
    /// Maximum number of errors retained by the server buffer.
    /// </summary>
    [JsonPropertyName("capacity")]
    public int Capacity { get; init; }

    /// <summary>
    /// Identifier for the server instance that generated the response.
    /// </summary>
    [JsonPropertyName("instanceId")]
    public string InstanceId { get; init; } = string.Empty;

    /// <summary>
    /// Recent errors ordered newest-first.
    /// </summary>
    [JsonPropertyName("errors")]
    public IReadOnlyList<RecentError> Errors { get; init; } = [];
}

/// <summary>
/// Represents a recent error captured by the server.
/// </summary>
public sealed class RecentError
{
    /// <summary>
    /// Timestamp when the error was captured.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Correlation ID for the request that produced the error.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// Request path that produced the error.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// HTTP status code returned for the request.
    /// </summary>
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; }

    /// <summary>
    /// Sanitized error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Status of the telemetry subsystem.
/// </summary>
public sealed class TelemetryStatus
{
    /// <summary>
    /// Whether tracing is enabled.
    /// </summary>
    [JsonPropertyName("tracingEnabled")]
    public bool TracingEnabled { get; init; }

    /// <summary>
    /// Whether an OTLP endpoint is configured.
    /// </summary>
    [JsonPropertyName("otlpConfigured")]
    public bool OtlpConfigured { get; init; }

    /// <summary>
    /// Configured OTLP endpoint, if any.
    /// </summary>
    [JsonPropertyName("otlpEndpoint")]
    public string? OtlpEndpoint { get; init; }
}

/// <summary>
/// Status of database migrations.
/// </summary>
public sealed class MigrationStatus
{
    /// <summary>
    /// Current migration lifecycle status.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Whether the instance is ready for traffic.
    /// </summary>
    [JsonPropertyName("isReady")]
    public bool IsReady { get; init; }

    /// <summary>
    /// Whether migrations failed.
    /// </summary>
    [JsonPropertyName("isFailed")]
    public bool IsFailed { get; init; }

    /// <summary>
    /// Optional operator-facing detail.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Whether a migration plan could be generated.
    /// </summary>
    [JsonPropertyName("planAvailable")]
    public bool PlanAvailable { get; init; }

    /// <summary>
    /// Whether pending migrations were detected.
    /// </summary>
    [JsonPropertyName("upgradeRequired")]
    public bool UpgradeRequired { get; init; }

    /// <summary>
    /// Pending migration scripts.
    /// </summary>
    [JsonPropertyName("pendingScripts")]
    public IReadOnlyList<string> PendingScripts { get; init; } = [];

    /// <summary>
    /// Executed scripts no longer discovered by the current binary.
    /// </summary>
    [JsonPropertyName("executedButNotDiscoveredScripts")]
    public IReadOnlyList<string> ExecutedButNotDiscoveredScripts { get; init; } = [];

    /// <summary>
    /// Error detail when migration planning could not complete.
    /// </summary>
    [JsonPropertyName("planError")]
    public string? PlanError { get; init; }

    /// <summary>
    /// Timestamp when the status was generated.
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; init; }
}
