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
    /// Indicates whether additional pages of results are likely available.
    /// </summary>
    /// <remarks>
    /// True when this page contains features and either <see cref="NumberMatched"/> is unknown
    /// or exceeds <see cref="NumberReturned"/>. For manual paging, callers should track
    /// cumulative results against <see cref="NumberMatched"/>.
    /// </remarks>
    public bool HasMoreResults => NumberReturned > 0 &&
        (!NumberMatched.HasValue || NumberMatched.Value > NumberReturned);
}
