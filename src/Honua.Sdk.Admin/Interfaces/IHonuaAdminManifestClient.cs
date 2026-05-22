// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Metadata-manifest export and apply.
/// </summary>
public interface IHonuaAdminManifestClient
{
    /// <summary>
    /// Exports the metadata manifest.
    /// </summary>
    /// <param name="ns">Optional namespace filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The metadata manifest.</returns>
    Task<MetadataManifest> GetManifestAsync(string? ns = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a metadata manifest.
    /// </summary>
    /// <param name="request">The manifest apply request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The manifest apply result.</returns>
    Task<ManifestApplyResult> ApplyManifestAsync(ManifestApplyRequest request, CancellationToken cancellationToken = default);
}
