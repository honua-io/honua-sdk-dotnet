// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Runtime.CompilerServices;
using Honua.Sdk.GeoServices.FeatureServer.Models;
namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>
/// Client interface for the Honua FeatureServer (GeoServices) read/query API.
/// </summary>
public interface IHonuaFeatureServerClient
{
    /// <summary>
    /// Gets service-level metadata for a FeatureServer service.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Service metadata including layers, extent, and capabilities.</returns>
    Task<FeatureServerServiceInfo> GetServiceInfoAsync(string serviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed metadata for a specific layer in a FeatureServer service.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Layer metadata including fields, extent, and capabilities.</returns>
    Task<FeatureServerLayerInfo> GetLayerInfoAsync(string serviceId, int layerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and returns a single page of results.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="query">Query parameters including filters, paging, and projection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query response containing features and metadata.</returns>
    Task<FeatureServerQueryResponse> QueryAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single feature by object ID.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="objectId">The object ID to read.</param>
    /// <param name="query">Optional query parameters for projection, geometry, and output spatial reference.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching feature, or <c>null</c> when no feature is returned.</returns>
    Task<FeatureServerFeature?> GetFeatureAsync(
        string serviceId,
        int layerId,
        long objectId,
        FeatureServerQueryParams? query = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This implementation does not support FeatureServer read-by-id.");

    /// <summary>
    /// Executes a count-only query, returning the number of matching features.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="query">Query parameters for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of matching features.</returns>
    Task<long> QueryCountAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an IDs-only query, returning object IDs of matching features.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="query">Query parameters for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching object IDs.</returns>
    Task<IReadOnlyList<long>> QueryIdsAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an extent-only query, returning the bounding extent of matching features.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="query">Query parameters for filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The bounding extent of matching features.</returns>
    Task<FeatureServerExtent> QueryExtentAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query with automatic paging via <see cref="IAsyncEnumerable{T}"/>.
    /// Advances <c>ResultOffset</c> automatically and stops when the server indicates no more records.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="query">Query parameters. <c>ResultOffset</c> will be advanced automatically.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of query response pages.</returns>
    IAsyncEnumerable<FeatureServerQueryResponse> QueryPagesAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a statistics query on a layer.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="query">Statistics query parameters including outStatistics, groupBy, and having.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query response containing statistics results as features.</returns>
    Task<FeatureServerQueryResponse> QueryStatisticsAsync(string serviceId, int layerId, FeatureServerStatisticsParams query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a SQL WHERE clause against a layer.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="where">The SQL WHERE clause to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result indicating whether the SQL is valid.</returns>
    Task<FeatureServerValidateSqlResponse> ValidateSqlAsync(string serviceId, int layerId, string where, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a query and returns the raw <see cref="HttpResponseMessage"/> for binary formats
    /// (PBF, FlatGeobuf, Parquet, etc.). The caller is responsible for disposing the response.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="query">Query parameters including the desired output format.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw HTTP response message.</returns>
    Task<HttpResponseMessage> QueryRawAsync(string serviceId, int layerId, FeatureServerQueryParams query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a complete, duplicate-free feature retrieval using stable object-ID batching rather
    /// than <c>resultOffset</c>-based paging. First collects every matching object ID via
    /// <see cref="QueryIdsAsync"/> (unbounded by <c>maxRecordCount</c> per the ArcGIS REST contract),
    /// then queries features in stable batches drawn from that id set. Termination and completeness
    /// therefore do not depend on the source advertising <c>supportsPagination</c> or honoring
    /// <c>resultOffset</c>: a source that ignores <c>resultOffset</c> (and would otherwise re-serve
    /// page one forever, or silently truncate a <see cref="QueryPagesAsync(string, int, FeatureServerQueryParams, CancellationToken)"/>
    /// walk to a single page) cannot produce a duplicate or a missed record here, because every batch
    /// is addressed by explicit object IDs rather than an offset the server is free to disregard.
    /// Implemented entirely in terms of <see cref="QueryIdsAsync"/> and <see cref="QueryAsync"/>, so
    /// every implementation of this interface gets a correct default without extra work.
    /// </summary>
    /// <param name="serviceId">The service identifier.</param>
    /// <param name="layerId">The layer ID within the service.</param>
    /// <param name="query">
    /// Query parameters (filters, output fields, geometry/SR options). <c>ObjectIds</c>,
    /// <c>ResultOffset</c>, <c>ResultRecordCount</c>, <c>ReturnIdsOnly</c>, <c>ReturnCountOnly</c>, and
    /// <c>ReturnExtentOnly</c> are overwritten per batch and should be left unset.
    /// </param>
    /// <param name="batchSize">
    /// Object IDs per batch. Defaults to 1000; pass the layer's advertised <c>maxRecordCount</c> (or
    /// lower) when known, since a batch larger than the source's transfer limit is not guaranteed to
    /// return every requested id in one response.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An async enumerable of query response batches. Every object id returned by
    /// <see cref="QueryIdsAsync"/> is covered by exactly one batch (including a smaller final batch),
    /// so the total feature count across all batches equals the id count with no gaps or repeats.
    /// </returns>
    async IAsyncEnumerable<FeatureServerQueryResponse> QueryAllFeaturesByObjectIdBatchesAsync(
        string serviceId,
        int layerId,
        FeatureServerQueryParams query,
        int batchSize = 1000,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

        var objectIds = await QueryIdsAsync(serviceId, layerId, query, cancellationToken).ConfigureAwait(false);
        for (var offset = 0; offset < objectIds.Count; offset += batchSize)
        {
            var batch = objectIds.Skip(offset).Take(batchSize).ToArray();
            var batchQuery = query with
            {
                ObjectIds = batch,
                ResultOffset = null,
                ResultRecordCount = null,
                ReturnIdsOnly = null,
                ReturnCountOnly = null,
                ReturnExtentOnly = null
            };
            yield return await QueryAsync(serviceId, layerId, batchQuery, cancellationToken).ConfigureAwait(false);
        }
    }
}
