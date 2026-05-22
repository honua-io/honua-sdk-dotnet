// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// License status, entitlements, and replacement-license upload.
/// </summary>
public interface IHonuaAdminLicenseClient
{
    /// <summary>
    /// Gets the active license status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active license status.</returns>
    Task<LicenseStatusResponse> GetLicenseStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active license entitlements.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active license entitlements.</returns>
    Task<IReadOnlyList<LicenseEntitlement>> GetLicenseEntitlementsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a replacement license file.
    /// </summary>
    /// <param name="bytes">License file bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refreshed license status.</returns>
    Task<LicenseStatusResponse> UploadLicenseAsync(byte[] bytes, CancellationToken cancellationToken = default);
}
