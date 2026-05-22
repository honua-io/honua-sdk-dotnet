// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.Catalogs.Stac.Models;

namespace Honua.Sdk.Catalogs.Stac;

/// <summary>
/// Raw / JSON escape-hatch surface for the Honua STAC client. Provides
/// caller-owned <see cref="JsonDocument"/> and <see cref="HttpResponseMessage"/>
/// returns for STAC extension fields that have not yet been promoted to typed
/// SDK properties on <see cref="IHonuaStacClient"/>. The caller is responsible
/// for disposing every <see cref="HttpResponseMessage"/> returned by this surface.
/// </summary>
public interface IHonuaStacRawClient
{
    /// <summary>
    /// Gets one page of STAC items as raw JSON for extension fields not yet
    /// promoted to typed SDK properties.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="query">Optional query parameters for paging and filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A caller-owned JSON document.</returns>
    Task<JsonDocument> GetItemsJsonAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one STAC item as raw JSON for extension fields not yet promoted to
    /// typed SDK properties.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="itemId">STAC item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A caller-owned JSON document.</returns>
    Task<JsonDocument> GetItemJsonAsync(string collectionId, string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches STAC items with GET and returns raw JSON for extension fields
    /// not yet promoted to typed SDK properties.
    /// </summary>
    /// <param name="query">Optional STAC search query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A caller-owned JSON document.</returns>
    Task<JsonDocument> SearchJsonAsync(StacSearchQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches STAC items with POST and returns raw JSON for extension fields
    /// not yet promoted to typed SDK properties.
    /// </summary>
    /// <param name="request">Optional STAC search request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A caller-owned JSON document.</returns>
    Task<JsonDocument> PostSearchJsonAsync(StacSearchRequest? request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raw HTTP response for a STAC items query. The caller is
    /// responsible for disposing the response.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="query">Optional query parameters for paging and filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw HTTP response.</returns>
    Task<HttpResponseMessage> GetItemsRawAsync(
        string collectionId,
        StacItemsQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the raw HTTP response for a STAC GET search. The caller is
    /// responsible for disposing the response.
    /// </summary>
    /// <param name="query">Optional STAC search query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw HTTP response.</returns>
    Task<HttpResponseMessage> SearchRawAsync(StacSearchQuery? query = null, CancellationToken cancellationToken = default);
}
