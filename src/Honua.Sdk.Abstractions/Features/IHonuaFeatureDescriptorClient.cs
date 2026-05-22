// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Shared source descriptor discovery contract implemented by protocol-specific Honua clients.
/// </summary>
public interface IHonuaFeatureDescriptorClient
{
    /// <summary>
    /// Provider name for diagnostics and provider selection.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Retrieves provider-neutral source schema and capability metadata for the supplied source descriptor.
    /// </summary>
    /// <param name="descriptor">Source descriptor containing the provider-specific locator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source descriptor enriched with discovered schema and capabilities.</returns>
    Task<SourceDescriptor> GetDescriptorAsync(SourceDescriptor descriptor, CancellationToken cancellationToken = default);
}
