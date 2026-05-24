// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Streaming subscriber list response.
/// </summary>
public sealed class SubscriberListResponse
{
    /// <summary>
    /// Number of connected subscribers.
    /// </summary>
    [JsonPropertyName("subscriberCount")]
    public int SubscriberCount { get; init; }

    /// <summary>
    /// Connected subscribers.
    /// </summary>
    [JsonPropertyName("subscribers")]
    public IReadOnlyList<SubscriberInfoResponse> Subscribers { get; init; } = [];

    /// <summary>
    /// Response generation timestamp.
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; init; }
}

/// <summary>
/// Connected streaming subscriber summary.
/// </summary>
public sealed class SubscriberInfoResponse
{
    /// <summary>
    /// Subscriber identifier.
    /// </summary>
    [JsonPropertyName("subscriberId")]
    public Guid SubscriberId { get; init; }

    /// <summary>
    /// Connection timestamp.
    /// </summary>
    [JsonPropertyName("connectedAt")]
    public DateTimeOffset ConnectedAt { get; init; }

    /// <summary>
    /// Optional client label.
    /// </summary>
    [JsonPropertyName("clientLabel")]
    public string? ClientLabel { get; init; }

    /// <summary>
    /// Connection duration in seconds.
    /// </summary>
    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }
}
