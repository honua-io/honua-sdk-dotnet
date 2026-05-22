// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures;

/// <summary>
/// Client interface for OGC API Features create, update, and delete operations.
/// </summary>
public interface IHonuaOgcFeaturesEditClient
{
    /// <summary>
    /// Creates a feature in a collection with the OGC API Features items endpoint.
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="feature">GeoJSON feature to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created feature representation returned by the server.</returns>
    Task<OgcFeature> CreateItemAsync(string collectionId, OgcFeature feature, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates or replaces a feature in a collection with the OGC API Features item endpoint.
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="featureId">The feature identifier.</param>
    /// <param name="feature">GeoJSON feature payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated feature representation returned by the server.</returns>
    Task<OgcFeature> UpdateItemAsync(string collectionId, string featureId, OgcFeature feature, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a feature from a collection with the OGC API Features item endpoint.
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="featureId">The feature identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteItemAsync(string collectionId, string featureId, CancellationToken cancellationToken = default);
}
