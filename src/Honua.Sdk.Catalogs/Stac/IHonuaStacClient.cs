// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Catalogs.Stac.Models;

namespace Honua.Sdk.Catalogs.Stac;

/// <summary>
/// Client interface for the Honua STAC catalog and item search API. Provides the
/// typed surface (landing page, collections, items, GET/POST search, paging).
/// For raw <see cref="System.Text.Json.JsonDocument"/> / <see cref="HttpResponseMessage"/>
/// escape hatches covering STAC extension fields that have not yet been promoted to
/// typed SDK properties, inherit the segregated <see cref="IHonuaStacRawClient"/>
/// surface (the concrete client implements both).
/// </summary>
public interface IHonuaStacClient : IHonuaStacRawClient
{
    /// <summary>
    /// Gets the STAC landing page with service metadata and navigation links.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The landing page response.</returns>
    Task<StacLandingPage> GetLandingPageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the STAC catalog document. This is an alias for
    /// <see cref="GetLandingPageAsync"/> matching the Python SDK naming.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The STAC catalog response.</returns>
    Task<StacLandingPage> GetCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available STAC collections.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>STAC collections exposed by the server.</returns>
    Task<IReadOnlyList<StacCollection>> ListCollectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one STAC collection by identifier.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The collection metadata.</returns>
    Task<StacCollection> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one page of STAC items from a collection.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="query">Optional query parameters for paging and filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A GeoJSON-compatible STAC item collection.</returns>
    Task<StacItemCollection> GetItemsAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one STAC item by collection and item identifier.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="itemId">STAC item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The STAC item.</returns>
    Task<StacItem> GetItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches STAC items with GET query parameters.
    /// </summary>
    /// <param name="query">Optional STAC search query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A GeoJSON-compatible STAC item collection.</returns>
    Task<StacItemCollection> SearchAsync(StacSearchQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches STAC items by POSTing a JSON request body.
    /// </summary>
    /// <param name="request">Optional STAC search request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A GeoJSON-compatible STAC item collection.</returns>
    Task<StacItemCollection> PostSearchAsync(StacSearchRequest? request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets STAC item pages from a collection with automatic next-link
    /// pagination.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="query">Optional query parameters for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>STAC item collection pages.</returns>
    IAsyncEnumerable<StacItemCollection> GetItemsPagesAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches STAC items with GET and automatic next-link pagination.
    /// </summary>
    /// <param name="query">Optional STAC search query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>STAC item collection pages.</returns>
    IAsyncEnumerable<StacItemCollection> SearchPagesAsync(
        StacSearchQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches STAC items with POST and automatic next-link pagination.
    /// </summary>
    /// <param name="request">Optional STAC search request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>STAC item collection pages.</returns>
    IAsyncEnumerable<StacItemCollection> PostSearchPagesAsync(
        StacSearchRequest? request,
        CancellationToken cancellationToken = default);
}
