// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Provider stream capabilities exposed through the shared feature stream abstraction.
/// </summary>
public sealed record FeatureStreamCapabilities
{
    /// <summary>Whether the provider supports opening real-time feature streams.</summary>
    public bool SupportsConnect { get; init; }

    /// <summary>Whether the provider supports reconnecting with resume or sequence tokens.</summary>
    public bool SupportsReconnect { get; init; }

    /// <summary>Whether the provider supports heartbeat messages.</summary>
    public bool SupportsHeartbeat { get; init; }

    /// <summary>Whether the provider supports explicit subscribe messages after connection.</summary>
    public bool SupportsSubscribe { get; init; }

    /// <summary>Whether the provider supports explicit unsubscribe messages.</summary>
    public bool SupportsUnsubscribe { get; init; }

    /// <summary>Whether the provider supplies monotonic sequence numbers.</summary>
    public bool SupportsSequenceNumbers { get; init; }

    /// <summary>Whether the provider supplies opaque resume tokens.</summary>
    public bool SupportsResumeTokens { get; init; }

    /// <summary>Whether the provider can honor bounded-buffer backpressure hints.</summary>
    public bool SupportsBackpressure { get; init; }

    /// <summary>Native protocol surface used by the provider, when useful for diagnostics.</summary>
    public string? NativeSurface { get; init; }

    /// <summary>Reason streams are unsupported when no stream operation is available.</summary>
    public string? UnsupportedReason { get; init; }
}

/// <summary>
/// Stream connection lifecycle state.
/// </summary>
public enum FeatureStreamConnectionState
{
    /// <summary>Connection state is unknown.</summary>
    Unknown = 0,

    /// <summary>Connection is open.</summary>
    Connected = 1,

    /// <summary>Connection is reconnecting.</summary>
    Reconnecting = 2,

    /// <summary>Connection is closed normally.</summary>
    Closed = 3,

    /// <summary>Connection failed.</summary>
    Failed = 4
}

/// <summary>
/// Feature stream event kind.
/// </summary>
public enum FeatureStreamEventKind
{
    /// <summary>Event kind is unknown.</summary>
    Unknown = 0,

    /// <summary>A feature was inserted.</summary>
    Insert = 1,

    /// <summary>A feature was updated.</summary>
    Update = 2,

    /// <summary>A feature was deleted.</summary>
    Delete = 3,

    /// <summary>A provider heartbeat was observed.</summary>
    Heartbeat = 4,

    /// <summary>A subscription was acknowledged.</summary>
    Subscribed = 5,

    /// <summary>A subscription was removed.</summary>
    Unsubscribed = 6,

    /// <summary>The provider reported a stream error as an event.</summary>
    Error = 7
}

/// <summary>
/// Bounded-buffer behavior used when a feature stream producer is faster than its consumer.
/// </summary>
public enum FeatureStreamBackpressureMode
{
    /// <summary>Wait for buffer capacity before accepting another event.</summary>
    Wait = 0,

    /// <summary>Reject the incoming event when the buffer is full.</summary>
    Reject = 1,

    /// <summary>Drop the oldest buffered event and enqueue the incoming event.</summary>
    DropOldest = 2,

    /// <summary>Drop the incoming event when the buffer is full.</summary>
    DropNewest = 3
}

/// <summary>
/// Options for bounded real-time feature stream buffers.
/// </summary>
public sealed record FeatureStreamBackpressureOptions
{
    /// <summary>Default bounded buffer capacity.</summary>
    public const int DefaultCapacity = 256;

    /// <summary>Maximum number of events to buffer before backpressure applies.</summary>
    public int Capacity { get; init; } = DefaultCapacity;

    /// <summary>Behavior to apply when the buffer is full.</summary>
    public FeatureStreamBackpressureMode Mode { get; init; } = FeatureStreamBackpressureMode.Wait;
}

/// <summary>
/// Request to open a feature stream connection.
/// </summary>
public sealed record FeatureStreamConnectRequest
{
    /// <summary>Optional client-provided connection identifier for diagnostics.</summary>
    public string? ClientId { get; init; }

    /// <summary>Initial subscriptions to attach when the provider supports subscribe-on-connect.</summary>
    public IReadOnlyList<FeatureStreamSubscribeRequest> Subscriptions { get; init; } = [];

    /// <summary>Requested heartbeat interval.</summary>
    public TimeSpan? HeartbeatInterval { get; init; }

    /// <summary>Requested bounded-buffer behavior.</summary>
    public FeatureStreamBackpressureOptions Backpressure { get; init; } = new();
}

/// <summary>
/// Request to reconnect a feature stream connection.
/// </summary>
public sealed record FeatureStreamReconnectRequest
{
    /// <summary>Provider connection identifier to resume.</summary>
    public required string ConnectionId { get; init; }

    /// <summary>Provider resume token from the last accepted event or heartbeat.</summary>
    public string? ResumeToken { get; init; }

    /// <summary>Last accepted monotonic sequence number.</summary>
    public long? LastSequenceNumber { get; init; }

    /// <summary>Subscriptions that should be active after reconnect.</summary>
    public IReadOnlyList<FeatureStreamSubscribeRequest> Subscriptions { get; init; } = [];

    /// <summary>Requested bounded-buffer behavior after reconnect.</summary>
    public FeatureStreamBackpressureOptions Backpressure { get; init; } = new();
}

/// <summary>
/// Provider-neutral feature stream connection state.
/// </summary>
public sealed record FeatureStreamConnection
{
    /// <summary>Provider connection identifier.</summary>
    public required string ConnectionId { get; init; }

    /// <summary>Current connection state.</summary>
    public FeatureStreamConnectionState State { get; init; } = FeatureStreamConnectionState.Connected;

    /// <summary>Time when the provider established the connection.</summary>
    public DateTimeOffset ConnectedAt { get; init; }

    /// <summary>Time when the connection or resume token expires, when known.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Provider resume token for reconnect workflows.</summary>
    public string? ResumeToken { get; init; }

    /// <summary>Last accepted monotonic sequence number.</summary>
    public long? LastSequenceNumber { get; init; }

    /// <summary>Provider heartbeat interval for this connection.</summary>
    public TimeSpan? HeartbeatInterval { get; init; }
}

/// <summary>
/// Request to subscribe to feature events.
/// </summary>
public sealed record FeatureStreamSubscribeRequest
{
    /// <summary>Client or provider subscription identifier.</summary>
    public required string SubscriptionId { get; init; }

    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Filter expression in the language specified by <see cref="FilterLanguage"/>.</summary>
    public string? Filter { get; init; }

    /// <summary>Filter language. <see cref="FeatureFilterLanguage.ProviderDefault"/> uses the provider's native default.</summary>
    public FeatureFilterLanguage FilterLanguage { get; init; } = FeatureFilterLanguage.ProviderDefault;

    /// <summary>Fields/properties to include in streamed feature payloads.</summary>
    public IReadOnlyList<string>? OutFields { get; init; }

    /// <summary>Whether streamed insert/update events should include geometry.</summary>
    public bool? ReturnGeometry { get; init; }

    /// <summary>Optional bounding box spatial filter.</summary>
    public FeatureBoundingBox? Bbox { get; init; }

    /// <summary>Optional time instant or interval filter.</summary>
    public FeatureTimeFilter? TimeFilter { get; init; }

    /// <summary>Resume after this provider sequence number when supported.</summary>
    public long? StartAfterSequenceNumber { get; init; }

    /// <summary>Resume after this provider sequence token when supported.</summary>
    public string? StartAfterSequenceToken { get; init; }
}

/// <summary>
/// Request to remove a feature stream subscription.
/// </summary>
public sealed record FeatureStreamUnsubscribeRequest
{
    /// <summary>Provider connection identifier, when the protocol requires it.</summary>
    public string? ConnectionId { get; init; }

    /// <summary>Subscription identifier to remove.</summary>
    public required string SubscriptionId { get; init; }
}

/// <summary>
/// Request to send or observe a stream heartbeat.
/// </summary>
public sealed record FeatureStreamHeartbeatRequest
{
    /// <summary>Provider connection identifier.</summary>
    public required string ConnectionId { get; init; }

    /// <summary>Last accepted monotonic sequence number.</summary>
    public long? LastSequenceNumber { get; init; }

    /// <summary>Last provider sequence token.</summary>
    public string? LastSequenceToken { get; init; }

    /// <summary>Last provider resume token.</summary>
    public string? ResumeToken { get; init; }
}

/// <summary>
/// Provider-neutral stream heartbeat state.
/// </summary>
public sealed record FeatureStreamHeartbeat
{
    /// <summary>Provider connection identifier.</summary>
    public required string ConnectionId { get; init; }

    /// <summary>Time when the heartbeat was sent or observed.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Provider resume token for reconnect workflows.</summary>
    public string? ResumeToken { get; init; }

    /// <summary>Last accepted monotonic sequence number.</summary>
    public long? LastSequenceNumber { get; init; }

    /// <summary>Last provider sequence token.</summary>
    public string? LastSequenceToken { get; init; }
}

/// <summary>
/// Provider-neutral real-time feature event envelope.
/// </summary>
public sealed record FeatureStreamEvent
{
    /// <summary>Provider name that produced the event.</summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>Provider connection identifier, when available.</summary>
    public string? ConnectionId { get; init; }

    /// <summary>Subscription identifier that produced the event.</summary>
    public required string SubscriptionId { get; init; }

    /// <summary>Provider-specific source identifiers.</summary>
    public FeatureSource Source { get; init; } = new();

    /// <summary>Normalized event kind.</summary>
    public required FeatureStreamEventKind Kind { get; init; }

    /// <summary>Provider feature identifier, when available.</summary>
    public string? FeatureId { get; init; }

    /// <summary>Provider object identifier, when available.</summary>
    public long? ObjectId { get; init; }

    /// <summary>Event timestamp from the provider or adapter.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Monotonic provider sequence number, when available.</summary>
    public long? SequenceNumber { get; init; }

    /// <summary>Opaque provider sequence token, when available.</summary>
    public string? SequenceToken { get; init; }

    /// <summary>Provider resume token for reconnect workflows.</summary>
    public string? ResumeToken { get; init; }

    /// <summary>Feature attributes/properties as JSON values.</summary>
    public IReadOnlyDictionary<string, JsonElement> Attributes { get; init; } =
        new Dictionary<string, JsonElement>();

    /// <summary>Feature geometry as a provider-native JSON object, when supplied.</summary>
    public JsonElement? Geometry { get; init; }

    /// <summary>Stream error details when <see cref="Kind"/> is <see cref="FeatureStreamEventKind.Error"/>.</summary>
    public FeatureEditError? Error { get; init; }
}
