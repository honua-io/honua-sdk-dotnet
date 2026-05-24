// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// User management and effective permission client for Console route guards.
/// </summary>
public interface IHonuaAdminUsersClient
{
    /// <summary>
    /// Lists users with optional filters.
    /// </summary>
    Task<UserListResponse> ListUsersAsync(UserListQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user by identifier.
    /// </summary>
    Task<UserResponse?> GetUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a user's role assignments.
    /// </summary>
    Task<UserResponse> UpdateUserRolesAsync(string userId, UpdateUserRolesRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deprovisions a user.
    /// </summary>
    Task DeprovisionUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets effective permissions for route guards and capability checks.
    /// </summary>
    Task<EffectivePermissionsResponse> GetEffectivePermissionsAsync(string userId, CancellationToken cancellationToken = default);
}
