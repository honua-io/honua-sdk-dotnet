// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Wfs.Formats;
using Honua.Sdk.Wfs.Models;

namespace Honua.Sdk.Wfs;

/// <summary>
/// Client for WFS 2.0 read and query operations.
/// </summary>
public interface IHonuaWfsClient
{
    /// <summary>
    /// Retrieves the service capabilities document.
    /// </summary>
    Task<WfsCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves the schema definition for a feature type.
    /// </summary>
    /// <param name="typeName">The qualified feature type name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WfsFeatureTypeSchema> DescribeFeatureTypeAsync(string typeName, CancellationToken ct = default);

    /// <summary>
    /// Retrieves features using the default GeoJSON output format.
    /// </summary>
    /// <param name="request">The feature request parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WfsFeatureCollection> GetFeaturesAsync(GetFeaturesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Retrieves features using a custom output format handler.
    /// </summary>
    /// <typeparam name="TResult">The result type produced by the handler.</typeparam>
    /// <param name="request">The feature request parameters.</param>
    /// <param name="handler">The output format handler to process the response.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TResult> GetFeaturesAsync<TResult>(
        GetFeaturesRequest request,
        IWfsOutputFormatHandler<TResult> handler,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the count of features matching the query (RESULTTYPE=hits).
    /// Returns <c>null</c> if the server reports "unknown".
    /// </summary>
    /// <param name="typeName">The qualified feature type name.</param>
    /// <param name="filter">Optional FES 2.0 XML filter expression.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<long?> GetFeatureCountAsync(string typeName, string? filter = null, CancellationToken ct = default);

    /// <summary>
    /// Auto-pages through all matching features, yielding each feature as it becomes available.
    /// </summary>
    /// <param name="request">The feature request parameters. <see cref="GetFeaturesRequest.Count"/>
    /// controls the page size; <see cref="GetFeaturesRequest.StartIndex"/> controls the starting offset.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the internal safety limit of 100 pages is
    /// reached. Use manual paging with <see cref="GetFeaturesAsync(GetFeaturesRequest, CancellationToken)"/>
    /// for result sets exceeding this limit.</exception>
    IAsyncEnumerable<WfsFeature> GetFeaturesAsyncEnumerable(
        GetFeaturesRequest request,
        CancellationToken ct = default);
}
