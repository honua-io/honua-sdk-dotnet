// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Metadata-resource administration: list, read, create, update and delete
/// metadata resources (kind/namespace/name addressed) with optimistic concurrency.
/// </summary>
public interface IHonuaAdminMetadataClient
{
    /// <summary>
    /// Lists metadata resources, optionally filtered by kind and namespace.
    /// </summary>
    /// <param name="kind">Optional resource kind filter.</param>
    /// <param name="ns">Optional namespace filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of metadata resources.</returns>
    Task<IReadOnlyList<MetadataResource>> ListMetadataResourcesAsync(string? kind = null, string? ns = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific metadata resource by its identifier.
    /// </summary>
    /// <param name="kind">Resource kind.</param>
    /// <param name="ns">Resource namespace.</param>
    /// <param name="name">Resource name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the resource and its ETag (if present).</returns>
    Task<(MetadataResource Resource, string? ETag)> GetMetadataResourceAsync(string kind, string ns, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new metadata resource.
    /// </summary>
    /// <param name="resource">The resource to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created resource.</returns>
    Task<MetadataResource> CreateMetadataResourceAsync(MetadataResource resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new metadata resource and returns transport metadata.
    /// </summary>
    /// <param name="resource">The resource to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created resource and its ETag, if present.</returns>
    Task<MetadataResourceResponse> CreateMetadataResourceWithResponseAsync(MetadataResource resource, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing metadata resource.
    /// </summary>
    /// <param name="kind">Resource kind.</param>
    /// <param name="ns">Resource namespace.</param>
    /// <param name="name">Resource name.</param>
    /// <param name="resource">The updated resource.</param>
    /// <param name="ifMatch">Optional ETag for optimistic concurrency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated resource.</returns>
    Task<MetadataResource> UpdateMetadataResourceAsync(string kind, string ns, string name, MetadataResource resource, string? ifMatch = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing metadata resource and returns transport metadata.
    /// </summary>
    /// <param name="kind">Resource kind.</param>
    /// <param name="ns">Resource namespace.</param>
    /// <param name="name">Resource name.</param>
    /// <param name="resource">The updated resource.</param>
    /// <param name="ifMatch">Optional ETag for optimistic concurrency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated resource and its ETag, if present.</returns>
    Task<MetadataResourceResponse> UpdateMetadataResourceWithResponseAsync(string kind, string ns, string name, MetadataResource resource, string? ifMatch = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a metadata resource.
    /// </summary>
    /// <param name="kind">Resource kind.</param>
    /// <param name="ns">Resource namespace.</param>
    /// <param name="name">Resource name.</param>
    /// <param name="ifMatch">Optional ETag for optimistic concurrency.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteMetadataResourceAsync(string kind, string ns, string name, string? ifMatch = null, CancellationToken cancellationToken = default);
}
