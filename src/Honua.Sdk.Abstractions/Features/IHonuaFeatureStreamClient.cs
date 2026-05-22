// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Shared real-time feature stream contract implemented by stream-capable Honua clients.
/// </summary>
public interface IHonuaFeatureStreamClient
{
    /// <summary>
    /// Provider name for diagnostics and provider selection.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Provider stream capabilities exposed by this client.
    /// </summary>
    FeatureStreamCapabilities StreamCapabilities { get; }

    /// <summary>
    /// Opens a stream connection and returns provider-neutral connection state.
    /// </summary>
    /// <param name="request">Connection request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-neutral connection state.</returns>
    Task<FeatureStreamConnection> ConnectAsync(FeatureStreamConnectRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens a stream connection using the last known resume and sequence tokens.
    /// </summary>
    /// <param name="request">Reconnect request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-neutral connection state.</returns>
    Task<FeatureStreamConnection> ReconnectAsync(FeatureStreamReconnectRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to feature events and returns a normalized event stream.
    /// </summary>
    /// <param name="request">Subscription request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Normalized feature stream events.</returns>
    IAsyncEnumerable<FeatureStreamEvent> SubscribeAsync(FeatureStreamSubscribeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unsubscribes from a feature event subscription.
    /// </summary>
    /// <param name="request">Unsubscribe request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnsubscribeAsync(FeatureStreamUnsubscribeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends or observes a provider heartbeat for an active stream connection.
    /// </summary>
    /// <param name="request">Heartbeat request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-neutral heartbeat state.</returns>
    Task<FeatureStreamHeartbeat> HeartbeatAsync(FeatureStreamHeartbeatRequest request, CancellationToken cancellationToken = default);
}
