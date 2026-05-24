// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Text.Json.Serialization;

namespace Honua.Sdk.Abstractions.Environments;

/// <summary>
/// Host-neutral connection profile for one Honua environment.
/// </summary>
public sealed record HonuaEnvironmentProfile
{
    /// <summary>
    /// Stable environment identifier used by hosts for selection and persistence.
    /// </summary>
    public required string EnvironmentId { get; init; }

    /// <summary>
    /// Operator-facing display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Base Honua server address for HTTP and gRPC clients.
    /// </summary>
    public required Uri BaseAddress { get; init; }

    /// <summary>
    /// Optional tenant or workspace scope associated with this environment.
    /// </summary>
    public HonuaTenantScope? TenantScope { get; init; }

    /// <summary>
    /// Default authentication mode selected by the host for this profile.
    /// </summary>
    public HonuaEnvironmentAuthMode DefaultAuthMode { get; init; } = HonuaEnvironmentAuthMode.BrowserSession;

    /// <summary>
    /// Transport capabilities advertised or configured for this environment.
    /// </summary>
    public HonuaTransportCapabilities TransportCapabilities { get; init; } = new();

    /// <summary>
    /// Expected server trust policy and optional client certificate selector.
    /// </summary>
    public HonuaTrustProfile? TrustProfile { get; init; }

    /// <summary>
    /// Last host-supplied trust validation state.
    /// </summary>
    public HonuaEnvironmentTrustState TrustState { get; init; } = new();

    /// <summary>
    /// Additional non-secret metadata for host selection and diagnostics.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// Collection of profiles available to a host.
/// </summary>
public sealed record HonuaEnvironmentProfileSet
{
    /// <summary>
    /// Schema version for persisted or fixture profile documents.
    /// </summary>
    public string SchemaVersion { get; init; } = "honua.environment-profiles.v1";

    /// <summary>
    /// Default environment id, when a host should preselect one profile.
    /// </summary>
    public string? DefaultEnvironmentId { get; init; }

    /// <summary>
    /// Available environment profiles.
    /// </summary>
    public IReadOnlyList<HonuaEnvironmentProfile> Environments { get; init; } = [];
}

/// <summary>
/// Optional tenant, workspace, or audience hints used by authentication providers.
/// </summary>
public sealed record HonuaTenantScope
{
    /// <summary>
    /// Stable tenant identifier.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Operator-facing tenant name.
    /// </summary>
    public string? TenantName { get; init; }

    /// <summary>
    /// Optional workspace identifier or slug.
    /// </summary>
    public string? WorkspaceId { get; init; }

    /// <summary>
    /// Optional OAuth audience or resource hint.
    /// </summary>
    public string? Audience { get; init; }
}

/// <summary>
/// Authentication mode a host can use for an environment.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaEnvironmentAuthMode>))]
public enum HonuaEnvironmentAuthMode
{
    /// <summary>
    /// Browser session or backend-for-frontend authentication.
    /// </summary>
    BrowserSession,

    /// <summary>
    /// Bearer token supplied by the host.
    /// </summary>
    BearerToken,

    /// <summary>
    /// API key supplied by the host.
    /// </summary>
    ApiKey,

    /// <summary>
    /// OAuth device code flow managed by the host.
    /// </summary>
    DeviceCode,

    /// <summary>
    /// External token provider managed by the host.
    /// </summary>
    ExternalTokenProvider,

    /// <summary>
    /// Native mutual TLS-capable authentication.
    /// </summary>
    NativeMutualTls
}

/// <summary>
/// Transport capabilities available for one environment.
/// </summary>
public sealed record HonuaTransportCapabilities
{
    /// <summary>
    /// REST or OpenAPI clients are available.
    /// </summary>
    public bool SupportsRestOpenApi { get; init; }

    /// <summary>
    /// Server-sent events are available to browser-safe hosts.
    /// </summary>
    public bool SupportsServerSentEvents { get; init; }

    /// <summary>
    /// WebSocket transport is available.
    /// </summary>
    public bool SupportsWebSockets { get; init; }

    /// <summary>
    /// SignalR-compatible realtime transport is available.
    /// </summary>
    public bool SupportsSignalR { get; init; }

    /// <summary>
    /// Browser-safe realtime transport is available.
    /// </summary>
    public bool SupportsBrowserRealtime { get; init; }

    /// <summary>
    /// Native gRPC transport is available.
    /// </summary>
    public bool SupportsNativeGrpc { get; init; }

    /// <summary>
    /// Telemetry streaming is available.
    /// </summary>
    public bool SupportsTelemetryStream { get; init; }

    /// <summary>
    /// Job lifecycle streaming is available.
    /// </summary>
    public bool SupportsJobStream { get; init; }

    /// <summary>
    /// Native mTLS can be configured by the host for this environment.
    /// </summary>
    public bool SupportsMutualTls { get; init; }
}

/// <summary>
/// Expected server trust profile and optional client certificate selector.
/// </summary>
public sealed record HonuaTrustProfile
{
    /// <summary>
    /// Expected server-side environment identifier.
    /// </summary>
    public string? ExpectedEnvironmentId { get; init; }

    /// <summary>
    /// Expected DNS name, issuer, or operator-facing server identity.
    /// </summary>
    public string? ExpectedServerIdentity { get; init; }

    /// <summary>
    /// Optional trust bundle reference understood by the host.
    /// </summary>
    public string? TrustBundleReference { get; init; }

    /// <summary>
    /// Whether this environment policy requires a client certificate.
    /// </summary>
    public bool RequiresClientCertificate { get; init; }

    /// <summary>
    /// Optional client certificate selector. Certificate bytes and private keys are not stored here.
    /// </summary>
    public HonuaClientCertificateReference? ClientCertificate { get; init; }

    /// <summary>
    /// Last validation status captured by the host.
    /// </summary>
    public HonuaCertificateValidationStatus LastValidationStatus { get; init; } =
        HonuaCertificateValidationStatus.NotConfigured;
}

/// <summary>
/// Host-owned selector for a client certificate.
/// </summary>
public sealed record HonuaClientCertificateReference
{
    /// <summary>
    /// Selector kind.
    /// </summary>
    public HonuaClientCertificateReferenceKind Kind { get; init; } =
        HonuaClientCertificateReferenceKind.None;

    /// <summary>
    /// Host-specific reference, such as a file alias, keychain id, or external selector id.
    /// </summary>
    public string? Reference { get; init; }

    /// <summary>
    /// Optional certificate thumbprint when safe to expose.
    /// </summary>
    public string? Thumbprint { get; init; }

    /// <summary>
    /// Operator-facing certificate label.
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Optional provider name for external certificate selection.
    /// </summary>
    public string? ProviderName { get; init; }
}

/// <summary>
/// Host-owned client certificate selector kind.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaClientCertificateReferenceKind>))]
public enum HonuaClientCertificateReferenceKind
{
    /// <summary>
    /// No certificate selector is configured.
    /// </summary>
    None,

    /// <summary>
    /// File reference managed by the host.
    /// </summary>
    FileReference,

    /// <summary>
    /// Certificate thumbprint lookup managed by the host.
    /// </summary>
    Thumbprint,

    /// <summary>
    /// Platform keychain lookup managed by the host.
    /// </summary>
    KeychainReference,

    /// <summary>
    /// Platform keystore lookup managed by the host.
    /// </summary>
    KeystoreReference,

    /// <summary>
    /// External selector managed by the host or enterprise agent.
    /// </summary>
    ExternalSelector
}

/// <summary>
/// Result of host-supplied certificate and trust validation.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<HonuaCertificateValidationStatus>))]
public enum HonuaCertificateValidationStatus
{
    /// <summary>
    /// Validation has not been configured.
    /// </summary>
    NotConfigured,

    /// <summary>
    /// Required certificate reference could not be resolved.
    /// </summary>
    Missing,

    /// <summary>
    /// Certificate is expired.
    /// </summary>
    Expired,

    /// <summary>
    /// Certificate is valid but close to expiration.
    /// </summary>
    ExpiringSoon,

    /// <summary>
    /// Certificate or server trust chain is untrusted.
    /// </summary>
    Untrusted,

    /// <summary>
    /// Server rejected the client certificate.
    /// </summary>
    Rejected,

    /// <summary>
    /// Certificate is valid but bound to a different environment.
    /// </summary>
    WrongEnvironment,

    /// <summary>
    /// Trust validation succeeded and the profile is ready.
    /// </summary>
    Ready
}

/// <summary>
/// Last host-supplied trust validation state for an environment.
/// </summary>
public sealed record HonuaEnvironmentTrustState
{
    /// <summary>
    /// Current validation status.
    /// </summary>
    public HonuaCertificateValidationStatus Status { get; init; } =
        HonuaCertificateValidationStatus.NotConfigured;

    /// <summary>
    /// Timestamp when the state was checked.
    /// </summary>
    public DateTimeOffset? CheckedAt { get; init; }

    /// <summary>
    /// Certificate expiration timestamp, when known.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Safe server certificate fingerprint summary, when exposed by the host.
    /// </summary>
    public string? ServerFingerprint { get; init; }

    /// <summary>
    /// Safe issuer summary, when exposed by the host.
    /// </summary>
    public string? IssuerSummary { get; init; }

    /// <summary>
    /// Policy identifier used for validation.
    /// </summary>
    public string? PolicyId { get; init; }

    /// <summary>
    /// Sanitized machine-readable reason code.
    /// </summary>
    public string? ReasonCode { get; init; }

    /// <summary>
    /// Sanitized operator-facing message. Secret paths, certificate bytes, and private-key ids must not be stored here.
    /// </summary>
    public string? SanitizedMessage { get; init; }
}
