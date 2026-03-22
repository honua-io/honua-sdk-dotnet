// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Grpc.Models;

namespace Honua.Sdk.Grpc;

/// <summary>
/// Client interface for the Honua gRPC FeatureService.
/// </summary>
public interface IHonuaGrpcClient
{
    /// <summary>
    /// Executes a feature query and returns all results in a single response.
    /// </summary>
    /// <param name="request">The query request parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The query response containing features, counts, or extent.</returns>
    Task<QueryFeaturesResponse> QueryFeaturesAsync(QueryFeaturesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Executes a feature query and streams results as pages.
    /// </summary>
    /// <param name="request">The query request parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An async enumerable of feature pages.</returns>
    IAsyncEnumerable<FeaturePage> QueryFeaturesStreamAsync(QueryFeaturesRequest request, CancellationToken ct = default);

    /// <summary>
    /// Applies add, update, and delete edits to a feature layer.
    /// </summary>
    /// <param name="request">The edit request containing adds, updates, and deletes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The edit response containing per-operation results.</returns>
    Task<ApplyEditsResponse> ApplyEditsAsync(ApplyEditsRequest request, CancellationToken ct = default);
}
