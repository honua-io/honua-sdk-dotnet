// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.OgcRecords.Models;

namespace Honua.Sdk.OgcRecords;

/// <summary>
/// Client interface for the Honua OGC API Records public catalog API.
/// </summary>
public interface IHonuaOgcRecordsClient
{
    /// <summary>
    /// Gets the Records landing page with service metadata and navigation links.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The landing page response.</returns>
    Task<OgcRecordsLandingPage> GetLandingPageAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the conformance declaration listing supported Records standards.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The conformance response.</returns>
    Task<OgcRecordsConformance> GetConformanceAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists available record collections.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Record collections exposed by the server.</returns>
    Task<IReadOnlyList<OgcRecordsCollection>> ListCollectionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets one record collection by identifier.
    /// </summary>
    /// <param name="collectionId">Record collection identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The collection metadata.</returns>
    Task<OgcRecordsCollection> GetCollectionAsync(string collectionId, CancellationToken ct = default);

    /// <summary>
    /// Gets one page of metadata records from a collection.
    /// </summary>
    /// <param name="collectionId">Record collection identifier.</param>
    /// <param name="query">Optional query parameters for paging and filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A GeoJSON-compatible record collection.</returns>
    Task<OgcRecordCollection> GetRecordsAsync(
        string collectionId,
        OgcRecordsQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Searches metadata records from a collection. This is an alias for
    /// <see cref="GetRecordsAsync"/> using Records search terminology.
    /// </summary>
    /// <param name="collectionId">Record collection identifier.</param>
    /// <param name="query">Optional query parameters for paging and filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A GeoJSON-compatible record collection.</returns>
    Task<OgcRecordCollection> SearchAsync(
        string collectionId,
        OgcRecordsQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets one metadata record by identifier.
    /// </summary>
    /// <param name="collectionId">Record collection identifier.</param>
    /// <param name="recordId">Record identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The metadata record.</returns>
    Task<OgcRecord> GetRecordAsync(string collectionId, string recordId, CancellationToken ct = default);

    /// <summary>
    /// Gets records with automatic next-link pagination.
    /// </summary>
    /// <param name="collectionId">Record collection identifier.</param>
    /// <param name="query">Optional query parameters for filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Record collection pages.</returns>
    IAsyncEnumerable<OgcRecordCollection> GetRecordsPagesAsync(
        string collectionId,
        OgcRecordsQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets one page of metadata records as raw JSON for contract fields not
    /// yet promoted to typed SDK properties.
    /// </summary>
    /// <param name="collectionId">Record collection identifier.</param>
    /// <param name="query">Optional query parameters for paging and filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A caller-owned JSON document.</returns>
    Task<JsonDocument> GetRecordsJsonAsync(
        string collectionId,
        OgcRecordsQuery? query = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets one metadata record as raw JSON for contract fields not yet promoted
    /// to typed SDK properties.
    /// </summary>
    /// <param name="collectionId">Record collection identifier.</param>
    /// <param name="recordId">Record identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A caller-owned JSON document.</returns>
    Task<JsonDocument> GetRecordJsonAsync(string collectionId, string recordId, CancellationToken ct = default);

    /// <summary>
    /// Gets the raw HTTP response for a records query. The caller is
    /// responsible for disposing the response.
    /// </summary>
    /// <param name="collectionId">Record collection identifier.</param>
    /// <param name="query">Optional query parameters for paging and filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The raw HTTP response.</returns>
    Task<HttpResponseMessage> GetRecordsRawAsync(
        string collectionId,
        OgcRecordsQuery? query = null,
        CancellationToken ct = default);
}
