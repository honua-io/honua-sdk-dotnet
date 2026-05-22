// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// OIDC provider configuration and identity-provider catalog management.
/// </summary>
public interface IHonuaAdminIdentityClient
{
    /// <summary>
    /// Lists configured OIDC providers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of OIDC providers.</returns>
    Task<IReadOnlyList<OidcProviderResponse>> ListOidcProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a configured OIDC provider by identifier.
    /// </summary>
    /// <param name="providerId">Provider identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider, or null when the server returns 404.</returns>
    Task<OidcProviderResponse?> GetOidcProviderAsync(Guid providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a configured OIDC provider.
    /// </summary>
    /// <param name="request">Provider create request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created provider.</returns>
    Task<OidcProviderResponse> CreateOidcProviderAsync(CreateOidcProviderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a configured OIDC provider.
    /// </summary>
    /// <param name="providerId">Provider identifier.</param>
    /// <param name="request">Provider update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated provider.</returns>
    Task<OidcProviderResponse> UpdateOidcProviderAsync(Guid providerId, UpdateOidcProviderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a configured OIDC provider.
    /// </summary>
    /// <param name="providerId">Provider identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteOidcProviderAsync(Guid providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests a configured OIDC provider.
    /// </summary>
    /// <param name="providerId">Provider identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider test result.</returns>
    Task<OidcProviderTestResponse> TestOidcProviderAsync(Guid providerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets identity provider catalog status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identity provider catalog response.</returns>
    Task<IdentityProvidersResponse> GetIdentityProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests a provider type from the identity provider catalog.
    /// </summary>
    /// <param name="providerType">Provider type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The provider test result.</returns>
    Task<IdentityProviderTestResult> TestIdentityProviderAsync(string providerType, CancellationToken cancellationToken = default);
}
