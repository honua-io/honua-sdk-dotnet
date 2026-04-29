// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Admin.Models;

/// <summary>
/// OIDC provider configuration returned by the admin identity API.
/// </summary>
public sealed class OidcProviderResponse
{
    /// <summary>
    /// Stable provider identifier.
    /// </summary>
    [JsonPropertyName("providerId")]
    public Guid ProviderId { get; init; }

    /// <summary>
    /// Operator-facing provider name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Provider type, such as generic, google, or azuread.
    /// </summary>
    [JsonPropertyName("providerType")]
    public string ProviderType { get; init; } = string.Empty;

    /// <summary>
    /// OIDC authority URL.
    /// </summary>
    [JsonPropertyName("authority")]
    public string Authority { get; init; } = string.Empty;

    /// <summary>
    /// Client identifier registered with the identity provider.
    /// </summary>
    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Whether the provider is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    /// <summary>
    /// Whether the server considers the provider healthy.
    /// </summary>
    [JsonPropertyName("isHealthy")]
    public bool IsHealthy { get; init; }

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

    /// <summary>
    /// Last health-check timestamp, if one has run.
    /// </summary>
    [JsonPropertyName("lastHealthCheck")]
    public DateTimeOffset? LastHealthCheck { get; init; }
}

/// <summary>
/// Request payload for creating an OIDC provider.
/// </summary>
public sealed class CreateOidcProviderRequest
{
    /// <summary>
    /// Operator-facing provider name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Provider type, such as generic, google, or azuread.
    /// </summary>
    [JsonPropertyName("providerType")]
    public string ProviderType { get; init; } = string.Empty;

    /// <summary>
    /// OIDC authority URL.
    /// </summary>
    [JsonPropertyName("authority")]
    public string Authority { get; init; } = string.Empty;

    /// <summary>
    /// Client identifier registered with the identity provider.
    /// </summary>
    [JsonPropertyName("clientId")]
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Plaintext client secret sent only to create or rotate credentials.
    /// </summary>
    [JsonPropertyName("clientSecret")]
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Whether the provider should be enabled after creation.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;
}

/// <summary>
/// Request payload for updating an OIDC provider.
/// </summary>
public sealed class UpdateOidcProviderRequest
{
    /// <summary>
    /// Operator-facing provider name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// OIDC authority URL.
    /// </summary>
    [JsonPropertyName("authority")]
    public string? Authority { get; init; }

    /// <summary>
    /// Client identifier registered with the identity provider.
    /// </summary>
    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    /// <summary>
    /// Optional replacement client secret.
    /// </summary>
    [JsonPropertyName("clientSecret")]
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Optional enabled-state update.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }
}

/// <summary>
/// Result of testing a configured OIDC provider.
/// </summary>
public sealed class OidcProviderTestResponse
{
    /// <summary>
    /// Tested provider identifier.
    /// </summary>
    [JsonPropertyName("providerId")]
    public Guid ProviderId { get; init; }

    /// <summary>
    /// Whether the provider discovery endpoint was reachable.
    /// </summary>
    [JsonPropertyName("isReachable")]
    public bool IsReachable { get; init; }

    /// <summary>
    /// Server-provided test message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when the test ran.
    /// </summary>
    [JsonPropertyName("testedAt")]
    public DateTimeOffset TestedAt { get; init; }
}

/// <summary>
/// Identity provider catalog status response.
/// </summary>
public sealed class IdentityProvidersResponse
{
    /// <summary>
    /// Whether identity-provider authentication is enabled server-side.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    /// <summary>
    /// Configured provider statuses.
    /// </summary>
    [JsonPropertyName("providers")]
    public IReadOnlyList<IdentityProviderStatus> Providers { get; init; } = [];
}

/// <summary>
/// Status for one catalog identity provider.
/// </summary>
public sealed class IdentityProviderStatus
{
    /// <summary>
    /// Provider type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// Whether the provider is enabled.
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    /// <summary>
    /// Operator-facing provider name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Configured authority URL.
    /// </summary>
    [JsonPropertyName("authority")]
    public string? Authority { get; init; }

    /// <summary>
    /// Callback path registered by the server.
    /// </summary>
    [JsonPropertyName("callbackPath")]
    public string? CallbackPath { get; init; }

    /// <summary>
    /// Requested scopes, if exposed by the server.
    /// </summary>
    [JsonPropertyName("scopes")]
    public IReadOnlyList<string>? Scopes { get; init; }

    /// <summary>
    /// Whether the current provider configuration is valid.
    /// </summary>
    [JsonPropertyName("isConfigurationValid")]
    public bool IsConfigurationValid { get; init; }
}

/// <summary>
/// Result of testing an identity provider catalog entry.
/// </summary>
public sealed class IdentityProviderTestResult
{
    /// <summary>
    /// Provider type tested by the server.
    /// </summary>
    [JsonPropertyName("providerType")]
    public string ProviderType { get; init; } = string.Empty;

    /// <summary>
    /// Whether the provider discovery endpoint was reachable.
    /// </summary>
    [JsonPropertyName("isReachable")]
    public bool IsReachable { get; init; }

    /// <summary>
    /// Response time reported by the server.
    /// </summary>
    [JsonPropertyName("responseTimeMs")]
    public double? ResponseTimeMs { get; init; }

    /// <summary>
    /// Discovery URL tested by the server.
    /// </summary>
    [JsonPropertyName("discoveryUrl")]
    public Uri? DiscoveryUrl { get; init; }

    /// <summary>
    /// Error message reported by the server when the test failed.
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Issuer returned by provider discovery.
    /// </summary>
    [JsonPropertyName("issuer")]
    public string? Issuer { get; init; }
}
