// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Abstractions.Console.Share;

namespace Honua.Sdk.ConsoleShare;

/// <summary>
/// Browser-safe client for the Console Share access, public-link, and embed-token
/// lifecycle surface. Wraps the server share APIs under
/// <c>/api/v1/console/shares</c>.
/// </summary>
public interface IHonuaConsoleShareClient
{
    /// <summary>
    /// Retrieves the detailed projection of a Console Share item, including
    /// grants and any active public link or embed token.
    /// Maps to <c>GET /api/v1/console/shares/{shareId}</c>.
    /// </summary>
    /// <param name="shareId">Share identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The share detail envelope.</returns>
    Task<HonuaShareItemDetail> GetShareAsync(string shareId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the access (visibility and explicit grants) of a Console Share item.
    /// Maps to <c>PUT /api/v1/console/shares/{shareId}/access</c>.
    /// </summary>
    /// <param name="shareId">Share identifier.</param>
    /// <param name="update">Replacement visibility and grant set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated share detail envelope.</returns>
    Task<HonuaShareItemDetail> UpdateAccessAsync(string shareId, HonuaShareAccessUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the dependency closure for a prospective visibility change.
    /// Maps to <c>POST /api/v1/console/shares/{shareId}/access/validate</c>.
    /// </summary>
    /// <param name="shareId">Share identifier.</param>
    /// <param name="update">Prospective visibility and grant set to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The dependency-closure validation result.</returns>
    Task<HonuaShareDependencyClosure> ValidateDependencyClosureAsync(string shareId, HonuaShareAccessUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the public link for a Console Share item.
    /// Maps to <c>PUT /api/v1/console/shares/{shareId}/public-link</c>.
    /// </summary>
    /// <param name="shareId">Share identifier.</param>
    /// <param name="request">Public-link lifecycle request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active public link.</returns>
    Task<HonuaPublicLink> CreatePublicLinkAsync(string shareId, HonuaPublicLinkRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the public link for a Console Share item.
    /// Maps to <c>DELETE /api/v1/console/shares/{shareId}/public-link</c>.
    /// </summary>
    /// <param name="shareId">Share identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokePublicLinkAsync(string shareId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or rotates the embed token for a Console Share item.
    /// Maps to <c>PUT /api/v1/console/shares/{shareId}/embed-token</c>.
    /// </summary>
    /// <param name="shareId">Share identifier.</param>
    /// <param name="request">Embed-token lifecycle request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active embed token.</returns>
    Task<HonuaEmbedToken> CreateEmbedTokenAsync(string shareId, HonuaEmbedTokenRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the embed token for a Console Share item.
    /// Maps to <c>DELETE /api/v1/console/shares/{shareId}/embed-token</c>.
    /// </summary>
    /// <param name="shareId">Share identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeEmbedTokenAsync(string shareId, CancellationToken cancellationToken = default);
}
