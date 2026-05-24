// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using Honua.Sdk.Admin.Models;

namespace Honua.Sdk.Admin;

/// <summary>
/// Role and permission management client for Console route guards.
/// </summary>
public interface IHonuaAdminRolesClient
{
    /// <summary>
    /// Lists roles.
    /// </summary>
    Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a role by identifier.
    /// </summary>
    Task<RoleResponse?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a role.
    /// </summary>
    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a role.
    /// </summary>
    Task<RoleResponse> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a role.
    /// </summary>
    Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets role permissions.
    /// </summary>
    Task<IReadOnlyList<PermissionGrantResponse>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces role permissions.
    /// </summary>
    Task<IReadOnlyList<PermissionGrantResponse>> SetRolePermissionsAsync(Guid roleId, SetPermissionsRequest request, CancellationToken cancellationToken = default);
}
