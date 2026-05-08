// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Stac.Models;

namespace Honua.Sdk.Stac;

/// <summary>
/// Client interface for the Honua STAC catalog and item search API.
/// </summary>
public interface IHonuaStacClient
{
    /// <summary>
    /// Gets the STAC landing page with service metadata and navigation links.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The landing page response.</returns>
    Task<StacLandingPage> GetLandingPageAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the STAC catalog document. This is an alias for
    /// <see cref="GetLandingPageAsync"/> matching the Python SDK naming.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The STAC catalog response.</returns>
    Task<StacLandingPage> GetCatalogAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists available STAC collections.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>STAC collections exposed by the server.</returns>
    Task<IReadOnlyList<StacCollection>> ListCollectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets one STAC collection by identifier.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The collection metadata.</returns>
    Task<StacCollection> GetCollectionAsync(string collectionId, CancellationToken ct = default);

    /// <summary>
    /// Gets one page of STAC items from a collection.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="query">Optional query parameters for paging and filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A GeoJSON-compatible STAC item collection.</returns>
    Task<StacItemCollection> GetItemsAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets one STAC item by collection and item identifier.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="itemId">STAC item identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The STAC item.</returns>
    Task<StacItem> GetItemAsync(string collectionId, string itemId, CancellationToken ct = default);

    /// <summary>
    /// Searches STAC items with GET query parameters.
    /// </summary>
    /// <param name="query">Optional STAC search query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A GeoJSON-compatible STAC item collection.</returns>
    Task<StacItemCollection> SearchAsync(StacSearchQuery? query = null, CancellationToken ct = default);

    /// <summary>
    /// Searches STAC items by POSTing a JSON request body.
    /// </summary>
    /// <param name="request">Optional STAC search request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A GeoJSON-compatible STAC item collection.</returns>
    Task<StacItemCollection> SearchAsync(StacSearchRequest? request, CancellationToken ct = default);

    /// <summary>
    /// Gets STAC item pages from a collection with automatic next-link
    /// pagination.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="query">Optional query parameters for filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>STAC item collection pages.</returns>
    IAsyncEnumerable<StacItemCollection> GetItemsPagesAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Searches STAC items with GET and automatic next-link pagination.
    /// </summary>
    /// <param name="query">Optional STAC search query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>STAC item collection pages.</returns>
    IAsyncEnumerable<StacItemCollection> SearchPagesAsync(
        StacSearchQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Searches STAC items with POST and automatic next-link pagination.
    /// </summary>
    /// <param name="request">Optional STAC search request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>STAC item collection pages.</returns>
    IAsyncEnumerable<StacItemCollection> SearchPagesAsync(
        StacSearchRequest? request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets one page of STAC items as raw JSON for extension fields not yet
    /// promoted to typed SDK properties.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="query">Optional query parameters for paging and filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A caller-owned JSON document.</returns>
    Task<JsonDocument> GetItemsJsonAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets one STAC item as raw JSON for extension fields not yet promoted to
    /// typed SDK properties.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="itemId">STAC item identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A caller-owned JSON document.</returns>
    Task<JsonDocument> GetItemJsonAsync(string collectionId, string itemId, CancellationToken ct = default);

    /// <summary>
    /// Searches STAC items with GET and returns raw JSON for extension fields
    /// not yet promoted to typed SDK properties.
    /// </summary>
    /// <param name="query">Optional STAC search query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A caller-owned JSON document.</returns>
    Task<JsonDocument> SearchJsonAsync(StacSearchQuery? query = null, CancellationToken ct = default);

    /// <summary>
    /// Searches STAC items with POST and returns raw JSON for extension fields
    /// not yet promoted to typed SDK properties.
    /// </summary>
    /// <param name="request">Optional STAC search request body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A caller-owned JSON document.</returns>
    Task<JsonDocument> SearchJsonAsync(StacSearchRequest? request, CancellationToken ct = default);

    /// <summary>
    /// Gets the raw HTTP response for a STAC items query. The caller is
    /// responsible for disposing the response.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="query">Optional query parameters for paging and filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw HTTP response.</returns>
    Task<HttpResponseMessage> GetItemsRawAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the raw HTTP response for a STAC GET search. The caller is
    /// responsible for disposing the response.
    /// </summary>
    /// <param name="query">Optional STAC search query parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw HTTP response.</returns>
    Task<HttpResponseMessage> SearchRawAsync(StacSearchQuery? query = null, CancellationToken ct = default);
}
