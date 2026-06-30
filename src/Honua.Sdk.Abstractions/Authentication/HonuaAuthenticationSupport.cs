// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net.Http.Headers;

namespace Honua.Sdk.Abstractions.Authentication;

/// <summary>
/// Shared helpers for applying SDK authentication without exposing raw secrets to diagnostics.
/// </summary>
public static class HonuaAuthenticationSupport
{
    /// <summary>
    /// Diagnostic event emitted when credentials are applied or omitted for a request.
    /// </summary>
    public const string CredentialAppliedEvent = "honua.auth.credential_applied";

    /// <summary>
    /// Diagnostic event emitted before the SDK rejects credentials over remote plain HTTP.
    /// </summary>
    public const string InsecureTransportRejectedEvent = "honua.auth.insecure_transport_rejected";

    /// <summary>
    /// Canonical exception message thrown when the SDK refuses to send credentials over a
    /// remote plain-HTTP connection. Centralized here so every HTTP auth handler raises the
    /// same message (the per-service name is captured separately in auth diagnostics).
    /// </summary>
    public const string InsecureTransportRejectedMessage =
        "Refusing to send credentials over an insecure connection. Use HTTPS, " +
        "or use loopback HTTP only for local development.";

    /// <summary>
    /// Determines whether any credential source is configured.
    /// </summary>
    /// <param name="options">Authentication options.</param>
    /// <returns><c>true</c> when at least one credential source is configured.</returns>
    public static bool HasCredentialSource(IHonuaAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return !string.IsNullOrWhiteSpace(options.ApiKey) ||
               !string.IsNullOrWhiteSpace(options.BearerToken) ||
               options.ApiKeyProvider is not null ||
               options.BearerTokenProvider is not null ||
               options.AccessTokenProvider is not null;
    }

    /// <summary>
    /// Creates a request context for an HTTP request.
    /// </summary>
    /// <param name="request">HTTP request.</param>
    /// <param name="options">Authentication options.</param>
    /// <param name="serviceName">Logical Honua service name.</param>
    /// <returns>Request-aware authentication context.</returns>
    public static HonuaAuthenticationRequest CreateHttpRequest(
        HttpRequestMessage request,
        IHonuaAuthenticationOptions options,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        request.Options.TryGetValue(HonuaAuthenticationRequestOptions.Scopes, out var requestScopes);
        request.Options.TryGetValue(HonuaAuthenticationRequestOptions.Audience, out var requestAudience);
        request.Options.TryGetValue(HonuaAuthenticationRequestOptions.Operation, out var requestOperation);

        return new HonuaAuthenticationRequest
        {
            Transport = HonuaAuthenticationTransport.Http,
            ServiceName = serviceName,
            Operation = requestOperation,
            Method = request.Method.Method,
            RequestUri = request.RequestUri,
            Scopes = NormalizeScopes(requestScopes ?? options.AuthenticationScopes),
            Audience = string.IsNullOrWhiteSpace(requestAudience) ? options.AuthenticationAudience : requestAudience
        };
    }

    /// <summary>
    /// Creates a request context for a gRPC request.
    /// </summary>
    /// <param name="options">Authentication options.</param>
    /// <param name="serviceName">Logical Honua service name.</param>
    /// <param name="methodName">gRPC method name.</param>
    /// <returns>Request-aware authentication context.</returns>
    public static HonuaAuthenticationRequest CreateGrpcRequest(
        IHonuaAuthenticationOptions options,
        string serviceName,
        string? methodName)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new HonuaAuthenticationRequest
        {
            Transport = HonuaAuthenticationTransport.Grpc,
            ServiceName = serviceName,
            Operation = methodName,
            Method = methodName,
            Scopes = NormalizeScopes(options.AuthenticationScopes),
            Audience = options.AuthenticationAudience
        };
    }

    /// <summary>
    /// Applies API key and authorization credentials to an HTTP request.
    /// </summary>
    /// <param name="request">HTTP request.</param>
    /// <param name="options">Authentication options.</param>
    /// <param name="serviceName">Logical Honua service name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes after credentials are applied.</returns>
    public static async Task ApplyHttpCredentialsAsync(
        HttpRequestMessage request,
        IHonuaAuthenticationOptions options,
        string serviceName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        var context = CreateHttpRequest(request, options, serviceName);

        var apiKey = await ResolveApiKeyAsync(options, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.TryAddWithoutValidation("X-API-Key", apiKey);
        }

        var accessToken = await ResolveAccessTokenAsync(options, context, cancellationToken).ConfigureAwait(false);
        if (accessToken is not null && !string.IsNullOrWhiteSpace(accessToken.Token))
        {
            var tokenType = string.IsNullOrWhiteSpace(accessToken.TokenType) ? "Bearer" : accessToken.TokenType;
            request.Headers.Authorization = new AuthenticationHeaderValue(tokenType, accessToken.Token);
        }

        await EmitCredentialAppliedDiagnosticAsync(
            options,
            context,
            hasApiKey: !string.IsNullOrWhiteSpace(apiKey),
            authorizationScheme: accessToken?.TokenType,
            hasAuthorization: accessToken is not null && !string.IsNullOrWhiteSpace(accessToken.Token),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves an API key from configured static or provider-based credentials.
    /// </summary>
    /// <param name="options">Authentication options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>API key value, or null when omitted.</returns>
    public static Task<string?> ResolveApiKeyAsync(
        IHonuaAuthenticationOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.ApiKeyProvider is { } provider
            ? provider(cancellationToken)
            : Task.FromResult(options.ApiKey);
    }

    /// <summary>
    /// Resolves an access token from request-aware or legacy bearer-token providers.
    /// </summary>
    /// <param name="options">Authentication options.</param>
    /// <param name="request">Authentication request context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An access token, or null when omitted.</returns>
    public static async ValueTask<HonuaAccessToken?> ResolveAccessTokenAsync(
        IHonuaAuthenticationOptions options,
        HonuaAuthenticationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(request);

        if (options.AccessTokenProvider is { } accessTokenProvider)
        {
            return await accessTokenProvider.GetAccessTokenAsync(request, cancellationToken).ConfigureAwait(false);
        }

        var bearerToken = options.BearerTokenProvider is { } bearerTokenProvider
            ? await bearerTokenProvider(cancellationToken).ConfigureAwait(false)
            : options.BearerToken;

        return string.IsNullOrWhiteSpace(bearerToken)
            ? null
            : new HonuaAccessToken { Token = bearerToken };
    }

    /// <summary>
    /// Emits a sanitized diagnostic for an insecure transport rejection.
    /// </summary>
    /// <param name="options">Authentication options.</param>
    /// <param name="context">Authentication request context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A value task that completes when the diagnostic is handled.</returns>
    public static ValueTask EmitInsecureTransportRejectedDiagnosticAsync(
        IHonuaAuthenticationOptions options,
        HonuaAuthenticationRequest context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);

        return EmitDiagnosticAsync(
            options,
            context,
            InsecureTransportRejectedEvent,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["reason"] = "remote-http"
            },
            cancellationToken);
    }

    /// <summary>
    /// Redacts a raw secret value for emergency caller-owned diagnostics.
    /// </summary>
    /// <param name="value">Potential secret value.</param>
    /// <returns>An empty string for empty values; otherwise a fixed redaction marker.</returns>
    public static string RedactSecret(string? value)
        => string.IsNullOrEmpty(value) ? string.Empty : "[redacted]";

    /// <summary>
    /// Determines whether credentials must only be sent over HTTPS for the given
    /// request URI. This is the single source of truth for the SDK's plain-HTTP
    /// credential guard: credentials are permitted over plain HTTP only for local
    /// development loopback targets (see <see cref="IsLocalDevelopmentHttp(Uri?)"/>),
    /// and a <c>null</c> URI is treated as requiring HTTPS (fail-closed).
    /// </summary>
    /// <param name="uri">The request (or token endpoint) URI.</param>
    /// <returns><c>true</c> when credentials must not be sent unless the transport is HTTPS.</returns>
    public static bool RequiresHttpsForAuthentication(Uri? uri)
    {
        if (uri is null)
        {
            return true;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsLocalDevelopmentHttp(uri);
    }

    /// <summary>
    /// Determines whether the URI is a local-development plain-HTTP endpoint
    /// (loopback or <c>localhost</c> over <c>http</c>) for which the SDK permits
    /// credentials to be sent without HTTPS.
    /// </summary>
    /// <param name="uri">The request (or token endpoint) URI.</param>
    /// <returns><c>true</c> for loopback/localhost HTTP endpoints; otherwise <c>false</c>.</returns>
    public static bool IsLocalDevelopmentHttp(Uri? uri)
    {
        if (uri is null || !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return uri.IsLoopback ||
               string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalizes a scope sequence by trimming empty values.
    /// </summary>
    /// <param name="scopes">Scope sequence.</param>
    /// <returns>Normalized scopes.</returns>
    public static IReadOnlyList<string> NormalizeScopes(IEnumerable<string>? scopes)
        => scopes?
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();

    /// <summary>
    /// Emits a sanitized credential-applied diagnostic.
    /// </summary>
    /// <param name="options">Authentication options.</param>
    /// <param name="context">Authentication request context.</param>
    /// <param name="hasApiKey">Whether an API key was applied.</param>
    /// <param name="authorizationScheme">Authorization scheme when applied.</param>
    /// <param name="hasAuthorization">Whether authorization was applied.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A value task that completes when the diagnostic is handled.</returns>
    public static ValueTask EmitCredentialAppliedDiagnosticAsync(
        IHonuaAuthenticationOptions options,
        HonuaAuthenticationRequest context,
        bool hasApiKey,
        string? authorizationScheme,
        bool hasAuthorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);

        return EmitDiagnosticAsync(
            options,
            context,
            CredentialAppliedEvent,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["apiKey"] = hasApiKey ? "present" : "omitted",
                ["authorization"] = hasAuthorization ? "present" : "omitted",
                ["authorizationScheme"] = hasAuthorization && !string.IsNullOrWhiteSpace(authorizationScheme)
                    ? authorizationScheme
                    : "none"
            },
            cancellationToken);
    }

    private static ValueTask EmitDiagnosticAsync(
        IHonuaAuthenticationOptions options,
        HonuaAuthenticationRequest context,
        string eventName,
        IReadOnlyDictionary<string, string> attributes,
        CancellationToken cancellationToken)
    {
        if (options.AuthenticationDiagnostics is not { } diagnostics)
        {
            return ValueTask.CompletedTask;
        }

        return diagnostics(
            new HonuaAuthenticationDiagnostic
            {
                EventName = eventName,
                Transport = context.Transport,
                ServiceName = context.ServiceName,
                Operation = context.Operation,
                Method = context.Method,
                RequestUri = SanitizeUriForDiagnostics(context.RequestUri),
                Scopes = context.Scopes,
                Audience = context.Audience,
                Attributes = attributes
            },
            cancellationToken);
    }

    private static Uri? SanitizeUriForDiagnostics(Uri? requestUri)
    {
        if (requestUri is null || string.IsNullOrEmpty(requestUri.Query))
        {
            return requestUri;
        }

        var builder = new UriBuilder(requestUri)
        {
            Query = string.Empty
        };
        return builder.Uri;
    }
}
