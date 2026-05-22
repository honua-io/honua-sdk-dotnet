// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json;
using Honua.Sdk.OgcFeatures.Models;

namespace Honua.Sdk.OgcFeatures;

/// <summary>
/// Client interface for OGC API Features JSON Merge Patch operations.
/// </summary>
public interface IHonuaOgcFeaturesPatchClient
{
    /// <summary>
    /// Applies an RFC 7396 JSON Merge Patch payload to a feature in a collection.
    /// </summary>
    /// <param name="collectionId">The collection identifier.</param>
    /// <param name="featureId">The feature identifier.</param>
    /// <param name="patch">JSON Merge Patch payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The patched feature representation returned by the server.</returns>
    Task<OgcFeature> PatchItemAsync(string collectionId, string featureId, JsonElement patch, CancellationToken cancellationToken = default);
}
