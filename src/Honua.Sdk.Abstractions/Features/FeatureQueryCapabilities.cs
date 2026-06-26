// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Features;

/// <summary>
/// Provider query capabilities exposed through the shared feature query abstraction.
/// A geoprocessing tool inspects these flags (or relies on
/// <see cref="IHonuaFeatureGateway"/> routing) to pick a provider that supports a
/// temporal or grouped-statistics query instead of catching a runtime
/// <see cref="NotSupportedException"/>.
/// </summary>
public sealed record FeatureQueryCapabilities
{
    /// <summary>Whether the provider can evaluate a provider-neutral time instant or interval filter.</summary>
    public bool SupportsTimeFilter { get; init; }

    /// <summary>Whether the provider can evaluate aggregate statistics (<c>OutStatistics</c>).</summary>
    public bool SupportsStatistics { get; init; }

    /// <summary>Whether the provider can group aggregate statistics by one or more fields (<c>GroupBy</c>).</summary>
    public bool SupportsGroupBy { get; init; }

    /// <summary>Whether the provider can apply a provider-native <c>Having</c> clause to grouped statistics.</summary>
    public bool SupportsHaving { get; init; }

    /// <summary>Native protocol surface used by the provider, when useful for diagnostics.</summary>
    public string? NativeSurface { get; init; }

    /// <summary>
    /// Reason a query facet is unsupported, when one or more facets are unavailable.
    /// </summary>
    public string? UnsupportedReason { get; init; }
}
