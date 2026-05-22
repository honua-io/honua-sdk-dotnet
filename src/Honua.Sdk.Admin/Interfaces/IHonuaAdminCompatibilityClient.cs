// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Server version, capabilities, and SDK/server compatibility checks.
/// </summary>
public interface IHonuaAdminCompatibilityClient
{
    /// <summary>
    /// Gets the admin API version information.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The version information.</returns>
    Task<AdminVersionResponse> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the admin API capabilities.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The capabilities response.</returns>
    Task<AdminCapabilitiesResponse> GetCapabilitiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the connected server is supported by this SDK baseline.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A compatibility result containing support status and coarse feature metadata.</returns>
    Task<ServerCompatibilityResult> CheckCompatibilityAsync(CancellationToken cancellationToken = default);
}
