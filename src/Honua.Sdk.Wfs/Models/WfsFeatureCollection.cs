// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Wfs.Models;

/// <summary>
/// A collection of WFS features with paging metadata.
/// </summary>
public sealed class WfsFeatureCollection
{
    /// <summary>The features returned in this page.</summary>
    public IReadOnlyList<WfsFeature> Features { get; init; } = [];

    /// <summary>
    /// Total number of features matching the query, or <c>null</c> if the server returned "unknown".
    /// </summary>
    public long? NumberMatched { get; init; }

    /// <summary>Number of features returned in this response page.</summary>
    public int NumberReturned { get; init; }

    /// <summary>
    /// Indicates whether this single page suggests more results may exist.
    /// </summary>
    /// <remarks>
    /// This is a per-page heuristic: <c>true</c> when features were returned and either
    /// <see cref="NumberMatched"/> is unknown or exceeds this page's <see cref="NumberReturned"/>.
    /// It does not track cumulative offset across pages, so manual paging loops should compare
    /// the running total of fetched features against <see cref="NumberMatched"/> instead of
    /// relying solely on this property.
    /// For automatic paging, use <see cref="IHonuaWfsClient.GetFeaturesAsyncEnumerable"/> which
    /// handles cumulative offset tracking internally.
    /// </remarks>
    public bool HasMoreResults => NumberReturned > 0 &&
        (!NumberMatched.HasValue || NumberMatched.Value > NumberReturned);
}
