// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Features.OgcFeatures.Models;

namespace Honua.Sdk.Features.OgcFeatures;

/// <summary>
/// Client interface for the Honua OGC API Features read/query API.
/// </summary>
public interface IHonuaOgcFeaturesClient
{
    /// <summary>
    /// Gets the OGC API landing page with service metadata and navigation links.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The landing page response.</returns>
    Task<OgcLandingPage> GetLandingPageAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the conformance declaration listing supported OGC standards.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The conformance response with URIs of supported conformance classes.</returns>
    Task<OgcConformance> GetConformanceAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists all available feature collections.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of collections with metadata.</returns>
    Task<IReadOnlyList<OgcCollection>> ListCollectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets metadata for a specific collection.
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection metadata including extent, CRS, and links.</returns>
    Task<OgcCollection> GetCollectionAsync(string collectionId, CancellationToken ct = default);

    /// <summary>
    /// Gets the queryable properties for a collection (JSON Schema).
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Queryable properties as a JSON Schema object.</returns>
    Task<OgcQueryables> GetQueryablesAsync(string collectionId, CancellationToken ct = default);

    /// <summary>
    /// Gets a single page of features from a collection.
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="query">Optional query parameters for filtering, paging, and projection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A GeoJSON FeatureCollection with features and paging links.</returns>
    Task<OgcFeatureCollection> GetItemsAsync(string collectionId, OgcItemsParams? query = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a single feature by ID from a collection.
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="featureId">The feature identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The requested GeoJSON feature.</returns>
    Task<OgcFeature> GetItemAsync(string collectionId, string featureId, CancellationToken ct = default);

    /// <summary>
    /// Gets features from a collection with automatic paging via <see cref="IAsyncEnumerable{T}"/>.
    /// Follows HATEOAS "next" links for subsequent pages.
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="query">Optional query parameters for filtering and projection.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of feature collection pages.</returns>
    IAsyncEnumerable<OgcFeatureCollection> GetItemsPagesAsync(string collectionId, OgcItemsParams? query = null, CancellationToken ct = default);

    /// <summary>
    /// Gets features in a raw format, returning the <see cref="HttpResponseMessage"/> for
    /// non-JSON formats (GML, CSV, HTML, etc.). The caller is responsible for disposing the response.
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="query">Optional query parameters including the desired format.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw HTTP response message.</returns>
    Task<HttpResponseMessage> GetItemsRawAsync(string collectionId, OgcItemsParams? query = null, CancellationToken ct = default);
}
