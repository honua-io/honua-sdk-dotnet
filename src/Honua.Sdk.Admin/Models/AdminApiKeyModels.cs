// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Admin.Models;

/// <summary>Parameters for creating an administrative API key.</summary>
public sealed class CreateAdminApiKeyRequest
{
    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Permission grants.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Optional expiration time.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Optional allowed referrer patterns.</summary>
    public IReadOnlyList<string>? AllowedReferrers { get; init; }

    /// <summary>Optional allowed IP ranges.</summary>
    public IReadOnlyList<string>? AllowedIpRanges { get; init; }
}

/// <summary>API-key metadata without the secret.</summary>
public sealed class AdminApiKeyResponse
{
    /// <summary>Key identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Non-secret key prefix.</summary>
    public required string KeyPrefix { get; init; }

    /// <summary>Permission grants.</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>Key status.</summary>
    public required string Status { get; init; }

    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last update time.</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Expiration time.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Last authentication time.</summary>
    public DateTimeOffset? LastUsedAt { get; init; }

    /// <summary>Last rotation time.</summary>
    public DateTimeOffset? RotatedAt { get; init; }

    /// <summary>Revocation time.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Creating operator.</summary>
    public string? CreatedBy { get; init; }

    /// <summary>Allowed referrer patterns.</summary>
    public IReadOnlyList<string>? AllowedReferrers { get; init; }

    /// <summary>Allowed IP ranges.</summary>
    public IReadOnlyList<string>? AllowedIpRanges { get; init; }
}

/// <summary>Creation result containing the secret returned once by the server.</summary>
public sealed class AdminApiKeySecretResponse
{
    /// <summary>Created key metadata.</summary>
    public required AdminApiKeyResponse ApiKey { get; init; }

    /// <summary>Secret key; keep it out of logs and diagnostics.</summary>
    public required string Key { get; init; }
}

