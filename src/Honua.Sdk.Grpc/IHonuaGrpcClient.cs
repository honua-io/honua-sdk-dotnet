// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Grpc.Models;

namespace Honua.Sdk.Grpc;

/// <summary>
/// Client interface for the Honua gRPC FeatureService.
/// <para>
/// The concrete <see cref="HonuaGrpcClient"/> implementation owns an underlying
/// <c>GrpcChannel</c> and is <see cref="IDisposable"/>, so the channel is
/// released when the DI container shuts down. The interface itself intentionally
/// does <b>not</b> extend <see cref="IDisposable"/>: surfacing disposal on the
/// public contract leaks transport ownership into a type that downstream callers
/// may resolve and would invite consumers to dispose a DI-managed singleton,
/// tearing down the channel for every other dependent in the container.
/// </para>
/// <para>
/// Consumers depending on this interface should let DI manage the lifetime and
/// must <b>not</b> manually dispose the resolved instance. Code that genuinely
/// needs to own disposal (for example, manual construction in tests) should
/// depend on the concrete <see cref="HonuaGrpcClient"/> type, which remains
/// <see cref="IDisposable"/>.
/// </para>
/// </summary>
public interface IHonuaGrpcClient
{
    /// <summary>
    /// Executes a feature query and returns all results in a single response.
    /// </summary>
    /// <param name="request">The query request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The query response containing features, counts, or extent.</returns>
    Task<QueryFeaturesResponse> QueryFeaturesAsync(QueryFeaturesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a feature query and streams results as pages.
    /// </summary>
    /// <param name="request">The query request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of feature pages.</returns>
    IAsyncEnumerable<FeaturePage> QueryFeaturesStreamAsync(QueryFeaturesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies add, update, and delete edits to a feature layer.
    /// </summary>
    /// <param name="request">The edit request containing adds, updates, and deletes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The edit response containing per-operation results.</returns>
    Task<ApplyEditsResponse> ApplyEditsAsync(ApplyEditsRequest request, CancellationToken cancellationToken = default);
}
