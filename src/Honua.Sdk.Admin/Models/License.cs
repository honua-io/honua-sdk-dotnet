// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// License status metadata returned by the admin license API.
/// </summary>
public sealed class LicenseStatusResponse
{
    /// <summary>
    /// Default issuance-source label for servers that omit the field.
    /// </summary>
    public const string DefaultIssuanceSource = "BYOL portal";

    /// <summary>
    /// Active edition name.
    /// </summary>
    [JsonPropertyName("edition")]
    public string Edition { get; init; } = string.Empty;

    /// <summary>
    /// Expiration timestamp, or null for a perpetual license.
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Issuance timestamp, if exposed by the server.
    /// </summary>
    [JsonPropertyName("issuedAt")]
    public DateTimeOffset? IssuedAt { get; init; }

    /// <summary>
    /// Licensed organization or account.
    /// </summary>
    [JsonPropertyName("licensedTo")]
    public string? LicensedTo { get; init; }

    /// <summary>
    /// Whether the server considers the license valid.
    /// </summary>
    [JsonPropertyName("isValid")]
    public bool IsValid { get; init; }

    /// <summary>
    /// Source that issued the license.
    /// </summary>
    [JsonPropertyName("issuanceSource")]
    public string? IssuanceSource { get; init; }

    /// <summary>
    /// Server validation state.
    /// </summary>
    [JsonPropertyName("validationState")]
    public string ValidationState { get; init; } = string.Empty;

    /// <summary>
    /// Server-computed days until expiry.
    /// </summary>
    [JsonPropertyName("daysUntilExpiry")]
    public int? DaysUntilExpiry { get; init; }

    /// <summary>
    /// Whether the server reports an expiry warning.
    /// </summary>
    [JsonPropertyName("expiryWarning")]
    public bool ExpiryWarning { get; init; }

    /// <summary>
    /// Entitlements included in the active license.
    /// </summary>
    [JsonPropertyName("entitlements")]
    public IReadOnlyList<LicenseEntitlement> Entitlements { get; init; } = [];
}

/// <summary>
/// License entitlement row returned by the admin license API.
/// </summary>
public sealed class LicenseEntitlement
{
    /// <summary>
    /// Stable entitlement key.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Operator-facing entitlement name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Whether the entitlement is active.
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; init; }
}
