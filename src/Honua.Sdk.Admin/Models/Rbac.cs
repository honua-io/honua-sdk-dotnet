// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// Role definition returned by the Admin RBAC API.
/// </summary>
public sealed class RoleResponse
{
    /// <summary>
    /// Role identifier.
    /// </summary>
    [JsonPropertyName("roleId")]
    public Guid RoleId { get; init; }

    /// <summary>
    /// Role name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Optional role description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    /// <summary>
    /// Whether the role is built in and cannot be deleted.
    /// </summary>
    [JsonPropertyName("isBuiltIn")]
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// Permission grants assigned to the role.
    /// </summary>
    [JsonPropertyName("permissions")]
    public IReadOnlyList<PermissionGrantResponse> Permissions { get; init; } = [];

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Request payload for creating a role.
/// </summary>
public sealed record CreateRoleRequest
{
    /// <summary>
    /// Role name.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Optional role description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Request payload for updating a role.
/// </summary>
public sealed record UpdateRoleRequest
{
    /// <summary>
    /// Updated role name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Updated role description.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Request payload for replacing role permissions.
/// </summary>
public sealed record SetPermissionsRequest
{
    /// <summary>
    /// Complete permission list for the role.
    /// </summary>
    [JsonPropertyName("permissions")]
    public required IReadOnlyList<PermissionGrantRequest> Permissions { get; init; }
}

/// <summary>
/// Permission grant request tuple.
/// </summary>
public sealed record PermissionGrantRequest
{
    /// <summary>
    /// Service identifier or wildcard.
    /// </summary>
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    /// <summary>
    /// Layer identifier or wildcard.
    /// </summary>
    [JsonPropertyName("layer")]
    public required string Layer { get; init; }

    /// <summary>
    /// Operation identifier or wildcard.
    /// </summary>
    [JsonPropertyName("operation")]
    public required string Operation { get; init; }
}

/// <summary>
/// Permission grant response tuple.
/// </summary>
public sealed class PermissionGrantResponse
{
    /// <summary>
    /// Service identifier or wildcard.
    /// </summary>
    [JsonPropertyName("service")]
    public string Service { get; init; } = string.Empty;

    /// <summary>
    /// Layer identifier or wildcard.
    /// </summary>
    [JsonPropertyName("layer")]
    public string Layer { get; init; } = string.Empty;

    /// <summary>
    /// Operation identifier or wildcard.
    /// </summary>
    [JsonPropertyName("operation")]
    public string Operation { get; init; } = string.Empty;
}

/// <summary>
/// Managed user returned by the Admin user API.
/// </summary>
public sealed class UserResponse
{
    /// <summary>
    /// Stable user identifier.
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Operator-facing display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional email address.
    /// </summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>
    /// Provisioning source, such as oidc, scim, or manual.
    /// </summary>
    [JsonPropertyName("provisioningSource")]
    public string ProvisioningSource { get; init; } = string.Empty;

    /// <summary>
    /// OIDC provider identifier, when available.
    /// </summary>
    [JsonPropertyName("providerId")]
    public Guid? ProviderId { get; init; }

    /// <summary>
    /// Whether the user is active.
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }

    /// <summary>
    /// Assigned role names.
    /// </summary>
    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Paginated user list response.
/// </summary>
public sealed class UserListResponse
{
    /// <summary>
    /// Users in the returned page.
    /// </summary>
    [JsonPropertyName("users")]
    public IReadOnlyList<UserResponse> Users { get; init; } = [];

    /// <summary>
    /// Total users matching the filter.
    /// </summary>
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }
}

/// <summary>
/// User list query filter.
/// </summary>
public sealed record UserListQuery
{
    /// <summary>
    /// Optional provisioning source filter.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Optional role filter.
    /// </summary>
    public string? Role { get; init; }

    /// <summary>
    /// Optional active-state filter.
    /// </summary>
    public bool? Active { get; init; }

    /// <summary>
    /// Maximum records to return.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Records to skip.
    /// </summary>
    public int? Offset { get; init; }
}

/// <summary>
/// Request payload for replacing a user's role assignments.
/// </summary>
public sealed record UpdateUserRolesRequest
{
    /// <summary>
    /// Complete list of role names to assign.
    /// </summary>
    [JsonPropertyName("roles")]
    public required IReadOnlyList<string> Roles { get; init; }
}

/// <summary>
/// Effective permissions resolved across all user roles.
/// </summary>
public sealed class EffectivePermissionsResponse
{
    /// <summary>
    /// User identifier.
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// Role memberships used for resolution.
    /// </summary>
    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Resolved permission grants.
    /// </summary>
    [JsonPropertyName("permissions")]
    public IReadOnlyList<PermissionGrantResponse> Permissions { get; init; } = [];

    /// <summary>
    /// Resolution timestamp.
    /// </summary>
    [JsonPropertyName("resolvedAt")]
    public DateTimeOffset ResolvedAt { get; init; }
}
