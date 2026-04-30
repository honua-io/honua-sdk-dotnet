// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Admin.Catalog;

/// <summary>
/// Client interface for portal and catalog discovery over Honua Server metadata.
/// </summary>
public interface IHonuaCatalogClient
{
    /// <summary>
    /// Searches across services, layers, groups, and saved source descriptors.
    /// </summary>
    /// <param name="options">Search, filter, sort, and paging options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Filtered and paged catalog items.</returns>
    Task<CatalogSearchResult> SearchAsync(CatalogQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Lists published services.
    /// </summary>
    /// <param name="options">Filter, sort, and paging options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Published services.</returns>
    Task<IReadOnlyList<CatalogService>> ListServicesAsync(CatalogQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Gets one published service by name.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The service detail, or <c>null</c> when it is not found.</returns>
    Task<CatalogService?> GetServiceAsync(string serviceName, CancellationToken ct = default);

    /// <summary>
    /// Lists layers discovered from service metadata.
    /// </summary>
    /// <param name="options">Filter, sort, and paging options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Catalog layer details.</returns>
    Task<IReadOnlyList<CatalogLayer>> ListLayersAsync(CatalogQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Gets one layer by service name and layer ID.
    /// </summary>
    /// <param name="serviceName">Service name.</param>
    /// <param name="layerId">Layer ID within the service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The layer detail, or <c>null</c> when it is not found.</returns>
    Task<CatalogLayer?> GetLayerAsync(string serviceName, int layerId, CancellationToken ct = default);

    /// <summary>
    /// Lists metadata-backed catalog groups.
    /// </summary>
    /// <param name="options">Filter, sort, and paging options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Catalog groups.</returns>
    Task<IReadOnlyList<CatalogGroup>> ListGroupsAsync(CatalogQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Gets one metadata-backed catalog group.
    /// </summary>
    /// <param name="ns">Metadata namespace.</param>
    /// <param name="name">Group name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The group, or <c>null</c> when it is not found.</returns>
    Task<CatalogGroup?> GetGroupAsync(string ns, string name, CancellationToken ct = default);

    /// <summary>
    /// Lists metadata-backed saved SDK source descriptors.
    /// </summary>
    /// <param name="options">Filter, sort, and paging options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Saved SDK source descriptors.</returns>
    Task<IReadOnlyList<CatalogSourceDescriptor>> ListSourceDescriptorsAsync(CatalogQueryOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Gets one metadata-backed saved SDK source descriptor.
    /// </summary>
    /// <param name="ns">Metadata namespace.</param>
    /// <param name="name">Descriptor name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The source descriptor, or <c>null</c> when it is not found.</returns>
    Task<CatalogSourceDescriptor?> GetSourceDescriptorAsync(string ns, string name, CancellationToken ct = default);
}
