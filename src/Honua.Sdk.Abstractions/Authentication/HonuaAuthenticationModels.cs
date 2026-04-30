// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

namespace Honua.Sdk.Abstractions.Authentication;

/// <summary>
/// Transport kind for SDK authentication decisions.
/// </summary>
public enum HonuaAuthenticationTransport
{
    /// <summary>
    /// Authentication for an HTTP request.
    /// </summary>
    Http,

    /// <summary>
    /// Authentication for a gRPC call.
    /// </summary>
    Grpc
}

/// <summary>
/// Request context passed to production token providers.
/// </summary>
public sealed class HonuaAuthenticationRequest
{
    /// <summary>
    /// SDK transport sending the credential.
    /// </summary>
    public HonuaAuthenticationTransport Transport { get; init; }

    /// <summary>
    /// Logical Honua service name, such as admin, wfs, geoservices, ogc-features, scenes, spec, or grpc.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Optional operation name for request-aware token decisions.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    /// HTTP method or gRPC method name associated with the request.
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Request URI when the transport exposes one.
    /// </summary>
    public Uri? RequestUri { get; init; }

    /// <summary>
    /// Requested OAuth/OIDC scopes for this SDK request.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Requested OAuth/OIDC audience or resource value for this SDK request.
    /// </summary>
    public string? Audience { get; init; }
}

/// <summary>
/// Access token returned by a request-aware token provider.
/// </summary>
public sealed class HonuaAccessToken
{
    /// <summary>
    /// Raw access token value. The SDK never emits this value through diagnostics.
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Authorization scheme for the token. Defaults to Bearer.
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// Optional UTC expiration instant for cache and refresh decisions.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; init; }

    /// <summary>
    /// Scopes granted by the identity provider, when returned.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Audience or resource granted by the identity provider, when returned.
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Determines whether the token is usable after applying a refresh skew.
    /// </summary>
    /// <param name="utcNow">Current UTC time.</param>
    /// <param name="refreshSkew">Refresh skew to subtract from the expiration.</param>
    /// <returns><c>true</c> when the token is non-empty and not close to expiration.</returns>
    public bool IsUsable(DateTimeOffset utcNow, TimeSpan refreshSkew)
        => !string.IsNullOrWhiteSpace(Token) &&
           (!ExpiresAtUtc.HasValue || ExpiresAtUtc.Value - refreshSkew > utcNow);
}

/// <summary>
/// Provides a request-aware access token.
/// </summary>
public interface IHonuaAccessTokenProvider
{
    /// <summary>
    /// Gets the current access token for the supplied SDK request context.
    /// </summary>
    /// <param name="request">Authentication request context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An access token, or null to omit the authorization credential.</returns>
    ValueTask<HonuaAccessToken?> GetAccessTokenAsync(
        HonuaAuthenticationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared authentication options implemented by Honua SDK client options.
/// </summary>
public interface IHonuaAuthenticationOptions
{
    /// <summary>
    /// Static API key value.
    /// </summary>
    string? ApiKey { get; }

    /// <summary>
    /// Request-time API key provider.
    /// </summary>
    Func<CancellationToken, Task<string?>>? ApiKeyProvider { get; }

    /// <summary>
    /// Static bearer token value.
    /// </summary>
    string? BearerToken { get; }

    /// <summary>
    /// Request-time bearer token provider.
    /// </summary>
    Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; }

    /// <summary>
    /// Request-aware access token provider. When configured, this takes precedence over bearer token delegates.
    /// </summary>
    IHonuaAccessTokenProvider? AccessTokenProvider { get; }

    /// <summary>
    /// Default OAuth/OIDC scopes requested by this client.
    /// </summary>
    IReadOnlyList<string> AuthenticationScopes { get; }

    /// <summary>
    /// Default OAuth/OIDC audience or resource requested by this client.
    /// </summary>
    string? AuthenticationAudience { get; }

    /// <summary>
    /// Optional diagnostics callback. SDK diagnostics never include raw credential values.
    /// </summary>
    HonuaAuthenticationDiagnosticHandler? AuthenticationDiagnostics { get; }
}

/// <summary>
/// Diagnostic event emitted by SDK authentication plumbing.
/// </summary>
public sealed class HonuaAuthenticationDiagnostic
{
    /// <summary>
    /// Event name.
    /// </summary>
    public string EventName { get; init; } = string.Empty;

    /// <summary>
    /// Transport associated with the event.
    /// </summary>
    public HonuaAuthenticationTransport Transport { get; init; }

    /// <summary>
    /// Logical Honua service associated with the event.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Optional operation name.
    /// </summary>
    public string? Operation { get; init; }

    /// <summary>
    /// HTTP method or gRPC method name.
    /// </summary>
    public string? Method { get; init; }

    /// <summary>
    /// Request URI associated with the event.
    /// </summary>
    public Uri? RequestUri { get; init; }

    /// <summary>
    /// Requested scopes associated with the event.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Requested audience associated with the event.
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Sanitized diagnostic attributes. Values must not contain raw credentials.
    /// </summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Handles sanitized SDK authentication diagnostics.
/// </summary>
/// <param name="diagnostic">Sanitized authentication diagnostic.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>A value task that completes when the diagnostic is handled.</returns>
public delegate ValueTask HonuaAuthenticationDiagnosticHandler(
    HonuaAuthenticationDiagnostic diagnostic,
    CancellationToken cancellationToken);

/// <summary>
/// HTTP request option keys used to override auth context for a single SDK request.
/// </summary>
public static class HonuaAuthenticationRequestOptions
{
    /// <summary>
    /// Request-scoped OAuth/OIDC scope override.
    /// </summary>
    public static readonly HttpRequestOptionsKey<IReadOnlyList<string>> Scopes =
        new("Honua.Authentication.Scopes");

    /// <summary>
    /// Request-scoped OAuth/OIDC audience override.
    /// </summary>
    public static readonly HttpRequestOptionsKey<string> Audience =
        new("Honua.Authentication.Audience");

    /// <summary>
    /// Request-scoped operation name for diagnostics and token provider decisions.
    /// </summary>
    public static readonly HttpRequestOptionsKey<string> Operation =
        new("Honua.Authentication.Operation");
}
