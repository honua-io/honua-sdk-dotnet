// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Query filter for feature-event replay.
/// </summary>
public sealed record FeatureEventReplayQuery
{
    /// <summary>
    /// Optional cursor after which events should be replayed.
    /// </summary>
    public long? Cursor { get; init; }

    /// <summary>
    /// Optional start timestamp.
    /// </summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>
    /// Optional end timestamp.
    /// </summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>
    /// Maximum number of events to return.
    /// </summary>
    public int? Limit { get; init; }
}

/// <summary>
/// Feature-event replay page.
/// </summary>
public sealed class FeatureEventReplayResponse
{
    /// <summary>
    /// Events in the page.
    /// </summary>
    [JsonPropertyName("events")]
    public IReadOnlyList<FeatureChangeEvent> Events { get; init; } = [];

    /// <summary>
    /// Cursor for the next replay request.
    /// </summary>
    [JsonPropertyName("nextCursor")]
    public long? NextCursor { get; init; }

    /// <summary>
    /// Whether more events are available.
    /// </summary>
    [JsonPropertyName("hasMore")]
    public bool HasMore { get; init; }
}

/// <summary>
/// Stored feature-change event used for replay and recovery.
/// </summary>
public sealed class FeatureChangeEvent
{
    /// <summary>
    /// Event identifier.
    /// </summary>
    [JsonPropertyName("eventId")]
    public string EventId { get; init; } = string.Empty;

    /// <summary>
    /// Monotonic replay cursor.
    /// </summary>
    [JsonPropertyName("cursor")]
    public long Cursor { get; init; }

    /// <summary>
    /// Event timestamp.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Optional source identifier.
    /// </summary>
    [JsonPropertyName("sourceId")]
    public string? SourceId { get; init; }

    /// <summary>
    /// Service identifier.
    /// </summary>
    [JsonPropertyName("serviceId")]
    public string ServiceId { get; init; } = string.Empty;

    /// <summary>
    /// Layer identifier.
    /// </summary>
    [JsonPropertyName("layerId")]
    public int LayerId { get; init; }

    /// <summary>
    /// Object id for the changed feature.
    /// </summary>
    [JsonPropertyName("objectId")]
    public long ObjectId { get; init; }

    /// <summary>
    /// Mutation operation.
    /// </summary>
    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;

    /// <summary>
    /// Originating protocol.
    /// </summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = string.Empty;

    /// <summary>
    /// Request identifier correlated with the mutation.
    /// </summary>
    [JsonPropertyName("requestId")]
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Canonical governed operation instance supplied by the server.</summary>
    [JsonPropertyName("operationInstanceId")]
    public string? OperationInstanceId { get; init; }

    /// <summary>Canonical request correlation identity supplied by the server.</summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    /// <summary>Durable acceptance-audit identity supplied by the server.</summary>
    [JsonPropertyName("auditId")]
    public string? AuditId { get; init; }

    /// <summary>Approved proposal identity supplied by the server, when applicable.</summary>
    [JsonPropertyName("proposalId")]
    public string? ProposalId { get; init; }

    /// <summary>
    /// Changed attributes when available.
    /// </summary>
    [JsonPropertyName("changedAttributes")]
    public Dictionary<string, JsonElement>? ChangedAttributes { get; init; }

    /// <summary>
    /// Whether geometry changed.
    /// </summary>
    [JsonPropertyName("geometryChanged")]
    public bool GeometryChanged { get; init; }

    /// <summary>
    /// Geometry envelope in min x, min y, max x, max y order.
    /// </summary>
    [JsonPropertyName("geometryEnvelope")]
    public IReadOnlyList<double>? GeometryEnvelope { get; init; }

    /// <summary>
    /// Feature properties serialized as JSON.
    /// </summary>
    [JsonPropertyName("propertiesJson")]
    public string? PropertiesJson { get; init; }

    /// <summary>
    /// GeoJSON geometry serialized as JSON.
    /// </summary>
    [JsonPropertyName("geometryJson")]
    public string? GeometryJson { get; init; }

    /// <summary>
    /// Geometry SRID when known.
    /// </summary>
    [JsonPropertyName("geometrySrid")]
    public int? GeometrySrid { get; init; }
}
