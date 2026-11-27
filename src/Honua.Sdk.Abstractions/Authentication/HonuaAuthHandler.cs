// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net;
using System.Net.Http.Headers;
using Honua.Sdk.Abstractions.Http;

namespace Honua.Sdk.Abstractions.Authentication;

/// <summary>Shared authentication handler for Honua REST clients; rejects credentials over plain HTTP to non-loopback hosts.</summary>
public abstract class HonuaAuthHandler<TOptions> : DelegatingHandler
    where TOptions : class, IHonuaClientOptions, IHonuaAuthenticationOptions
{
    private readonly TOptions _options;
    private readonly string _serviceName;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaAuthHandler{TOptions}"/> class.
    /// </summary>
    /// <param name="options">The resolved client options carrying credentials and base address.</param>
    /// <param name="serviceName">
    /// The logical Honua service name used in authentication diagnostics (for example
    /// <c>"stac"</c> or <c>"admin"</c>).
    /// </param>
    /// <param name="validateBaseAddress">
    /// The owning package's base-address validation callback, invoked once at construction so the
    /// failure carries the client-specific display name.
    /// </param>
    protected HonuaAuthHandler(TOptions options, string serviceName, Action<Uri?> validateBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(validateBaseAddress);

        _options = options;
        _serviceName = serviceName;
        validateBaseAddress(options.BaseAddress);
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsCredentialTargetAllowed(request.RequestUri, _options.BaseAddress))
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        await EnsureSecureTransportAsync(request, cancellationToken).ConfigureAwait(false);
        await HonuaAuthenticationSupport.ApplyHttpCredentialsAsync(
            request,
            _options,
            _serviceName,
            cancellationToken).ConfigureAwait(false);

        var canRefresh = HasRefreshableTokenProvider(_options);
        if (canRefresh && request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
        }

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!canRefresh || !await IsAuthChallengeResponseAsync(response, cancellationToken).ConfigureAwait(false))
        {
            return response;
        }

        return await RetryWithRefreshedTokenAsync(request, response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> RetryWithRefreshedTokenAsync(
        HttpRequestMessage request,
        HttpResponseMessage challengedResponse,
        CancellationToken cancellationToken)
    {
        bool refreshed;
        try
        {
            refreshed = await TryRefreshAuthorizationAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            challengedResponse.Dispose();
            throw;
        }

        if (!refreshed)
        {
            return challengedResponse;
        }

        challengedResponse.Dispose();
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureSecureTransportAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var effectiveUri = ResolveEffectiveUri(request.RequestUri, _options.BaseAddress);
        if (!HonuaAuthenticationSupport.HasCredentialSource(_options) ||
            !HonuaAuthenticationSupport.RequiresHttpsForAuthentication(effectiveUri))
        {
            return;
        }

        var context = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, _serviceName);
        await HonuaAuthenticationSupport.EmitInsecureTransportRejectedDiagnosticAsync(
            _options,
            context,
            cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException(HonuaAuthenticationSupport.InsecureTransportRejectedMessage);
    }

    private async Task<bool> TryRefreshAuthorizationAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var refreshContext = HonuaAuthenticationSupport.CreateHttpRequest(request, _options, _serviceName) with
        {
            ForceRefresh = true
        };
        var refreshedToken = await HonuaAuthenticationSupport.ResolveAccessTokenAsync(
            _options,
            refreshContext,
            cancellationToken).ConfigureAwait(false);
        if (refreshedToken is null || string.IsNullOrWhiteSpace(refreshedToken.Token))
        {
            return false;
        }

        var tokenType = string.IsNullOrWhiteSpace(refreshedToken.TokenType) ? "Bearer" : refreshedToken.TokenType;
        request.Headers.Authorization = new AuthenticationHeaderValue(tokenType, refreshedToken.Token);
        await HonuaAuthenticationSupport.EmitCredentialAppliedDiagnosticAsync(
            _options,
            refreshContext,
            hasApiKey: request.Headers.Contains("X-API-Key"),
            authorizationScheme: tokenType,
            hasAuthorization: true,
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool HasRefreshableTokenProvider(IHonuaAuthenticationOptions options)
        => options.AccessTokenProvider is not null || options.BearerTokenProvider is not null;

    private static async Task<bool> IsAuthChallengeResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return true;
        }

        if (response.StatusCode != HttpStatusCode.OK || !HonuaGeoServicesEnvelopeInspector.IsJsonResponse(response))
        {
            return false;
        }

        var envelope = await HonuaGeoServicesEnvelopeInspector.TryInspectAsync(response, cancellationToken)
            .ConfigureAwait(false);
        return envelope is { Code: 401 or 498 or 499 };
    }

    private static bool IsCredentialTargetAllowed(Uri? requestUri, Uri? baseAddress)
    {
        if (baseAddress is null || !baseAddress.IsAbsoluteUri)
        {
            return true;
        }

        if (requestUri is null)
        {
            return true;
        }

        if (!requestUri.IsAbsoluteUri)
        {
            return NextLinkOriginValidator.IsSameOrigin(requestUri.OriginalString, baseAddress);
        }

        return string.Equals(requestUri.Scheme, baseAddress.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(requestUri.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase) &&
               requestUri.Port == baseAddress.Port;
    }

    private static Uri? ResolveEffectiveUri(Uri? requestUri, Uri? baseAddress)
    {
        if (requestUri is not null && !requestUri.IsAbsoluteUri &&
            baseAddress is not null && baseAddress.IsAbsoluteUri &&
            Uri.TryCreate(baseAddress, requestUri, out var resolved))
        {
            return resolved;
        }

        return requestUri;
    }
}
