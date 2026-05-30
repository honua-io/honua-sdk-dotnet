// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Console.Share;

/// <summary>
/// Visibility level of a Console Share item. Mirrors the server
/// <c>shareVisibility</c> contract for access, public-link, and embed surfaces.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaShareVisibility>))]
public enum HonuaShareVisibility
{
    /// <summary>Visible only to explicitly granted principals.</summary>
    [JsonStringEnumMemberName("private")]
    Private,

    /// <summary>Visible to any authenticated member of the owning organization.</summary>
    [JsonStringEnumMemberName("organization")]
    Organization,

    /// <summary>Reachable anonymously through an active public link.</summary>
    [JsonStringEnumMemberName("public")]
    Public,
}

/// <summary>
/// A single access grant on a Console Share item.
/// </summary>
public sealed record HonuaShareGrant
{
    /// <summary>Stable identifier of the granted principal (user, group, or service).</summary>
    [JsonPropertyName("principalId")]
    public required string PrincipalId { get; init; }

    /// <summary>Principal kind, for example <c>user</c>, <c>group</c>, or <c>service</c>.</summary>
    [JsonPropertyName("principalKind")]
    public string? PrincipalKind { get; init; }

    /// <summary>Operator-facing display name for the principal.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>Granted role on the share, for example <c>viewer</c> or <c>editor</c>.</summary>
    [JsonPropertyName("role")]
    public required string Role { get; init; }
}

/// <summary>
/// Summary projection of a Console Share item as returned by list/read surfaces.
/// </summary>
public sealed record HonuaShareItem
{
    /// <summary>Stable share identifier.</summary>
    [JsonPropertyName("shareId")]
    public required string ShareId { get; init; }

    /// <summary>Identifier of the underlying shared resource (map, layer, dataset).</summary>
    [JsonPropertyName("resourceId")]
    public required string ResourceId { get; init; }

    /// <summary>Resource kind, for example <c>map</c>, <c>layer</c>, or <c>dataset</c>.</summary>
    [JsonPropertyName("resourceKind")]
    public string? ResourceKind { get; init; }

    /// <summary>Operator-facing title.</summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>Current visibility of the share.</summary>
    [JsonPropertyName("visibility")]
    public HonuaShareVisibility Visibility { get; init; }

    /// <summary>Identifier of the owning principal.</summary>
    [JsonPropertyName("ownerId")]
    public string? OwnerId { get; init; }

    /// <summary>Timestamp the share was last updated, in UTC.</summary>
    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>
/// Detailed projection of a Console Share item including grants and active
/// public-link / embed-token state. Maps to the server share-detail contract.
/// </summary>
public sealed record HonuaShareItemDetail
{
    /// <summary>Summary fields for the share.</summary>
    [JsonPropertyName("item")]
    public required HonuaShareItem Item { get; init; }

    /// <summary>Explicit access grants on the share.</summary>
    [JsonPropertyName("grants")]
    public IReadOnlyList<HonuaShareGrant> Grants { get; init; } = [];

    /// <summary>Active public link, when one exists.</summary>
    [JsonPropertyName("publicLink")]
    public HonuaPublicLink? PublicLink { get; init; }

    /// <summary>Active embed token, when one exists.</summary>
    [JsonPropertyName("embedToken")]
    public HonuaEmbedToken? EmbedToken { get; init; }
}

/// <summary>
/// Request body to update Console Share access (visibility and explicit grants).
/// Maps to the server access-update contract.
/// </summary>
public sealed record HonuaShareAccessUpdate
{
    /// <summary>Target visibility for the share.</summary>
    [JsonPropertyName("visibility")]
    public HonuaShareVisibility Visibility { get; init; }

    /// <summary>Replacement set of explicit access grants.</summary>
    [JsonPropertyName("grants")]
    public IReadOnlyList<HonuaShareGrant> Grants { get; init; } = [];
}

/// <summary>
/// Result of validating a Console Share dependency closure before a visibility
/// change. Maps to the server dependency-closure validation contract.
/// </summary>
public sealed record HonuaShareDependencyClosure
{
    /// <summary>Whether the requested visibility change is permitted.</summary>
    [JsonPropertyName("valid")]
    public bool Valid { get; init; }

    /// <summary>Identifiers of dependencies that block the requested change.</summary>
    [JsonPropertyName("blockingDependencies")]
    public IReadOnlyList<HonuaShareDependency> BlockingDependencies { get; init; } = [];
}

/// <summary>
/// A single dependency reported by share dependency-closure validation.
/// </summary>
public sealed record HonuaShareDependency
{
    /// <summary>Identifier of the dependent resource.</summary>
    [JsonPropertyName("resourceId")]
    public required string ResourceId { get; init; }

    /// <summary>Resource kind of the dependency.</summary>
    [JsonPropertyName("resourceKind")]
    public string? ResourceKind { get; init; }

    /// <summary>Current visibility of the dependency.</summary>
    [JsonPropertyName("visibility")]
    public HonuaShareVisibility Visibility { get; init; }

    /// <summary>Operator-facing reason the dependency blocks the change.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

/// <summary>
/// An active public link for a Console Share item.
/// </summary>
public sealed record HonuaPublicLink
{
    /// <summary>Stable public-link identifier.</summary>
    [JsonPropertyName("linkId")]
    public required string LinkId { get; init; }

    /// <summary>Absolute URL clients use to reach the shared resource anonymously.</summary>
    [JsonPropertyName("url")]
    public required Uri Url { get; init; }

    /// <summary>Whether the link is currently enabled.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    /// <summary>Optional expiry timestamp, in UTC.</summary>
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Timestamp the link was created, in UTC.</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>
/// Request body to create or update a public link for a Console Share item.
/// </summary>
public sealed record HonuaPublicLinkRequest
{
    /// <summary>Whether the link should be enabled.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    /// <summary>Optional expiry timestamp, in UTC.</summary>
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>
/// An active embed token for a Console Share item.
/// </summary>
public sealed record HonuaEmbedToken
{
    /// <summary>Stable embed-token identifier.</summary>
    [JsonPropertyName("tokenId")]
    public required string TokenId { get; init; }

    /// <summary>Opaque token value used by the embed host.</summary>
    [JsonPropertyName("token")]
    public required string Token { get; init; }

    /// <summary>Allowed embedding origins (CORS / frame-ancestors).</summary>
    [JsonPropertyName("allowedOrigins")]
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];

    /// <summary>Optional expiry timestamp, in UTC.</summary>
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Timestamp the token was created, in UTC.</summary>
    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>
/// Request body to create or rotate an embed token for a Console Share item.
/// </summary>
public sealed record HonuaEmbedTokenRequest
{
    /// <summary>Allowed embedding origins for the token.</summary>
    [JsonPropertyName("allowedOrigins")]
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];

    /// <summary>Optional expiry timestamp, in UTC.</summary>
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }
}
