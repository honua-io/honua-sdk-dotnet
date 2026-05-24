// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Feature-change event replay client.
/// </summary>
public interface IHonuaAdminFeatureEventsClient
{
    /// <summary>
    /// Replays feature-change events by cursor and optional time window.
    /// </summary>
    Task<FeatureEventReplayResponse> ReplayFeatureEventsAsync(FeatureEventReplayQuery? query = null, CancellationToken cancellationToken = default);
}
