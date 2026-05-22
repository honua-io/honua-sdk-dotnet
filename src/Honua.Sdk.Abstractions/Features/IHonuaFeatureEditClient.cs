// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Shared feature write contract implemented by edit-capable Honua clients.
/// </summary>
public interface IHonuaFeatureEditClient
{
    /// <summary>
    /// Provider name for diagnostics and provider selection.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Provider edit capabilities exposed by this client.
    /// </summary>
    FeatureEditCapabilities EditCapabilities { get; }

    /// <summary>
    /// Applies add, update, and delete feature edits.
    /// </summary>
    /// <param name="request">Provider-neutral edit request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Provider-neutral edit response.</returns>
    Task<FeatureEditResponse> ApplyEditsAsync(FeatureEditRequest request, CancellationToken cancellationToken = default);
}
