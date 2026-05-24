// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;
using Honua.Sdk.Abstractions.Environments;

namespace Honua.Sdk.Abstractions.Console;

/// <summary>
/// Browser-safe shell contract used by Console hosts.
/// </summary>
public sealed record HonuaConsoleShellDescriptor
{
    /// <summary>
    /// Available environment profiles.
    /// </summary>
    public HonuaEnvironmentProfileSet Environments { get; init; } = new();

    /// <summary>
    /// Current operator principal, when authenticated.
    /// </summary>
    public HonuaConsolePrincipal? Principal { get; init; }

    /// <summary>
    /// Navigation items available to the host.
    /// </summary>
    public IReadOnlyList<HonuaConsoleNavigationItem> Navigation { get; init; } = [];

    /// <summary>
    /// Route guard declarations used by a host to protect views before data loading.
    /// </summary>
    public IReadOnlyList<HonuaConsoleRouteGuard> RouteGuards { get; init; } = [];
}

/// <summary>
/// Operator principal and effective permission summary.
/// </summary>
public sealed record HonuaConsolePrincipal
{
    /// <summary>
    /// Stable user identifier.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// Operator-facing display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Optional email address.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Role names assigned to the operator.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Effective permission grants resolved by the server.
    /// </summary>
    public IReadOnlyList<HonuaConsolePermissionGrant> EffectivePermissions { get; init; } = [];
}

/// <summary>
/// Console navigation item.
/// </summary>
public sealed record HonuaConsoleNavigationItem
{
    /// <summary>
    /// Stable navigation item identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Operator-facing label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Route path for the item.
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Required permission grants for showing or opening this item.
    /// </summary>
    public IReadOnlyList<HonuaConsolePermissionGrant> RequiredPermissions { get; init; } = [];

    /// <summary>
    /// Required role names for showing or opening this item.
    /// </summary>
    public IReadOnlyList<string> RequiredRoles { get; init; } = [];
}

/// <summary>
/// Route guard declaration for Console route protection.
/// </summary>
public sealed record HonuaConsoleRouteGuard
{
    /// <summary>
    /// Route template or concrete route path.
    /// </summary>
    public required string RoutePattern { get; init; }

    /// <summary>
    /// Whether the route requires an authenticated principal.
    /// </summary>
    public bool RequiresAuthenticatedUser { get; init; } = true;

    /// <summary>
    /// Required permission grants.
    /// </summary>
    public IReadOnlyList<HonuaConsolePermissionGrant> RequiredPermissions { get; init; } = [];

    /// <summary>
    /// Required role names.
    /// </summary>
    public IReadOnlyList<string> RequiredRoles { get; init; } = [];

    /// <summary>
    /// Route to use when the user must authenticate.
    /// </summary>
    public string? ChallengeRoute { get; init; }

    /// <summary>
    /// Route to use when access is denied.
    /// </summary>
    public string? DeniedRoute { get; init; }
}

/// <summary>
/// Route guard evaluation result.
/// </summary>
public sealed record HonuaConsoleRouteGuardDecision
{
    /// <summary>
    /// Route evaluated by the host.
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Resulting access decision.
    /// </summary>
    public HonuaConsoleRouteAccess Access { get; init; }

    /// <summary>
    /// Machine-readable reason codes.
    /// </summary>
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];

    /// <summary>
    /// Permission grants required by the matched guard.
    /// </summary>
    public IReadOnlyList<HonuaConsolePermissionGrant> RequiredPermissions { get; init; } = [];
}

/// <summary>
/// Route guard access decision.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaConsoleRouteAccess>))]
public enum HonuaConsoleRouteAccess
{
    /// <summary>
    /// Access is allowed.
    /// </summary>
    Allowed,

    /// <summary>
    /// User must authenticate before access can be evaluated.
    /// </summary>
    Challenge,

    /// <summary>
    /// Access is denied.
    /// </summary>
    Denied
}

/// <summary>
/// Permission grant used by Console route guards.
/// </summary>
public sealed record HonuaConsolePermissionGrant
{
    /// <summary>
    /// Service identifier or wildcard.
    /// </summary>
    public required string Service { get; init; }

    /// <summary>
    /// Layer identifier or wildcard.
    /// </summary>
    public required string Layer { get; init; }

    /// <summary>
    /// Operation identifier or wildcard.
    /// </summary>
    public required string Operation { get; init; }
}
