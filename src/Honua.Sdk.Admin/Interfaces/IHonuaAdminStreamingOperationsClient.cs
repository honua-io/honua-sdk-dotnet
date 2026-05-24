// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Streaming subscriber operations client.
/// </summary>
public interface IHonuaAdminStreamingOperationsClient
{
    /// <summary>
    /// Lists connected streaming subscribers.
    /// </summary>
    Task<SubscriberListResponse> ListStreamingSubscribersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects a streaming subscriber.
    /// </summary>
    Task DisconnectStreamingSubscriberAsync(Guid subscriberId, CancellationToken cancellationToken = default);
}
