// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Console.Share;

namespace Honua.Sdk.ConsoleShare;

/// <summary>
/// Client for the Console Share open-data / DCAT / STAC publication surface.
/// Wraps both the authenticated admin endpoints under
/// <c>/api/v1/console/content/{id}/open-data</c> and the anonymous public
/// open-data read endpoints under <c>/api/v1/open-data</c>.
/// </summary>
/// <remarks>
/// The authenticated open-data endpoints and the anonymous dataset read return
/// data inside the server's <c>{ "success", "data", ... }</c> envelope; this
/// client unwraps the <c>data</c> payload. The anonymous data.json, Schema.org,
/// and STAC reads return standards-shaped documents directly.
/// </remarks>
public interface IHonuaConsoleShareOpenDataClient
{
    /// <summary>
    /// Reads the server-owned open-data page, eligibility, STAC publication
    /// state, and DCAT validation status for a content item.
    /// Maps to <c>GET /api/v1/console/content/{id}/open-data</c>.
    /// </summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The combined open-data page projection.</returns>
    Task<HonuaOpenDataPageResponse> GetPageAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the editable open-data page metadata for an item.
    /// Maps to <c>PUT /api/v1/console/content/{id}/open-data</c>.
    /// </summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="request">Page metadata to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The combined open-data page projection after the update.</returns>
    Task<HonuaOpenDataPageResponse> UpdatePageAsync(string itemId, HonuaUpdateOpenDataPageRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates whether an item may be published as open data and the stable
    /// reason why or why not.
    /// Maps to <c>GET /api/v1/console/content/{id}/open-data/eligibility</c>.
    /// </summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The eligibility decision.</returns>
    Task<HonuaOpenDataEligibility> GetEligibilityAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews the DCAT-US 3.0 / data.json export for an item's open-data page
    /// and reports its validation status.
    /// Maps to <c>GET /api/v1/console/content/{id}/open-data/dcat</c>.
    /// </summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated catalog document plus validation status.</returns>
    Task<HonuaDcatExportResponse> PreviewDcatAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an item's current STAC publication status and collection readback.
    /// Maps to <c>GET /api/v1/console/content/{id}/open-data/stac</c>.
    /// </summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The STAC publication state.</returns>
    Task<HonuaConsoleStacPublicationState> GetStacPublicationAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes (or re-publishes/updates) an item to the STAC catalog. Requires
    /// open-data eligibility and a DCAT-valid page; the server returns a 409 with
    /// validation errors otherwise (surfaced as a
    /// <see cref="Exceptions.HonuaConsoleShareApiException"/>).
    /// Maps to <c>POST /api/v1/console/content/{id}/open-data/stac/publish</c>.
    /// </summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The STAC publication state after publishing.</returns>
    Task<HonuaConsoleStacPublicationState> PublishStacAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unpublishes an item from the STAC catalog. Anonymous STAC reads return 404
    /// afterward.
    /// Maps to <c>DELETE /api/v1/console/content/{id}/open-data/stac</c>.
    /// </summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The STAC publication state after unpublishing.</returns>
    Task<HonuaConsoleStacPublicationState> UnpublishStacAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the anonymous open-data page projection for a public-indexed item.
    /// Private/ineligible/missing items return 404 (surfaced as a
    /// <see cref="Exceptions.HonuaConsoleShareApiException"/>) without leaking titles.
    /// Maps to <c>GET /api/v1/open-data/datasets/{id}</c>.
    /// </summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The anonymous open-data page projection.</returns>
    Task<HonuaOpenDataPage> GetPublicDatasetAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the DCAT-US 3.0 / data.json catalog for a public-indexed item.
    /// Maps to <c>GET /api/v1/open-data/datasets/{id}/data.json</c>.
    /// </summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The data.json catalog document.</returns>
    Task<HonuaDcatCatalog> GetPublicDataJsonAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the Schema.org Dataset JSON-LD projection for a public-indexed item.
    /// Maps to <c>GET /api/v1/open-data/datasets/{id}/schema.org</c>.
    /// </summary>
    /// <param name="itemId">Content item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Schema.org Dataset document.</returns>
    Task<HonuaSchemaOrgDataset> GetPublicSchemaOrgAsync(string itemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the STAC catalog root over all currently-published open-data items.
    /// Maps to <c>GET /api/v1/open-data/stac</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The STAC catalog root.</returns>
    Task<HonuaStacCatalog> GetPublicStacCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the STAC collection for a published item.
    /// Maps to <c>GET /api/v1/open-data/stac/collections/{collectionId}</c>.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The STAC collection.</returns>
    Task<HonuaStacCollection> GetPublicStacCollectionAsync(string collectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the representative STAC item for a published collection.
    /// Maps to <c>GET /api/v1/open-data/stac/collections/{collectionId}/items/{itemId}</c>.
    /// </summary>
    /// <param name="collectionId">STAC collection identifier.</param>
    /// <param name="itemId">STAC item identifier (equals the collection id for the representative item).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The STAC item.</returns>
    Task<HonuaStacItem> GetPublicStacItemAsync(string collectionId, string itemId, CancellationToken cancellationToken = default);
}
