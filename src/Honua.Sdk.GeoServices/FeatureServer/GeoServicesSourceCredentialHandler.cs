// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net.Http.Headers;
using System.Text;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Abstractions.Authentication;
using Honua.Sdk.Abstractions.Http;

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>Authentication mode applied by <see cref="GeoServicesSourceCredentialHandler"/>.</summary>
public enum GeoServicesCredentialMode
{
    /// <summary>No credential is applied.</summary>
    None,

    /// <summary>Appends <c>token=</c> to the query string; sent only over HTTPS or to loopback, and visible wherever URLs are logged.</summary>
    Token,

    /// <summary>HTTP Basic authentication via the <c>Authorization: Basic</c> header.</summary>
    Basic,

    /// <summary>OAuth/federated bearer token via the <c>Authorization: Bearer</c> header.</summary>
    Bearer
}

/// <summary>Credential for a third-party GeoServices REST source, applied by <see cref="GeoServicesSourceCredentialHandler"/>.</summary>
public sealed record GeoServicesSourceCredential
{
    /// <summary>The authentication mode to apply.</summary>
    public required GeoServicesCredentialMode Mode { get; init; }

    /// <summary>The token value for <see cref="GeoServicesCredentialMode.Token"/>.</summary>
    public string? Token { get; init; }

    /// <summary>Optional per-request token provider; overrides <see cref="Token"/>, and null or empty omits the token.</summary>
    public Func<CancellationToken, Task<string?>>? TokenProvider { get; init; }

    /// <summary>The username for <see cref="GeoServicesCredentialMode.Basic"/>.</summary>
    public string? Username { get; init; }

    /// <summary>The password for <see cref="GeoServicesCredentialMode.Basic"/>.</summary>
    public string? Password { get; init; }

    /// <summary>The bearer token for <see cref="GeoServicesCredentialMode.Bearer"/>.</summary>
    public string? BearerToken { get; init; }

    /// <summary>Optional per-request bearer token provider; overrides <see cref="BearerToken"/>, and null or empty omits the header.</summary>
    public Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; init; }

    /// <summary>Origin the credential is bound to; when unset, the first authenticated request's origin is used.</summary>
    public Uri? SourceBaseAddress { get; init; }
}

/// <summary>Applies a <see cref="GeoServicesSourceCredential"/> only to same-origin HTTPS (or loopback) requests; a supplied inner handler keeps its own redirect behaviour.</summary>
public sealed class GeoServicesSourceCredentialHandler : DelegatingHandler
{
    private readonly GeoServicesSourceCredential _credential;

    // The default transport this handler created itself (null when the caller supplied one). This
    // handler owns it and disposes it in Dispose(bool).
    private readonly HttpMessageHandler? _ownedInnerHandler;

    // The origin the credential is bound to: the configured source base address, or (when none is
    // configured) the origin of the first authenticated request.
    private Uri? _boundOrigin;


    /// <summary>Creates the handler; only the default inner handler disables redirects, so a supplied handler that follows them can forward the credential.</summary>
    public GeoServicesSourceCredentialHandler(GeoServicesSourceCredential credential, HttpMessageHandler? innerHandler = null)
    {
        ArgumentNullException.ThrowIfNull(credential);
        _credential = credential;

        if (credential.SourceBaseAddress is { } sourceBaseAddress)
        {
            _boundOrigin = ValidateSourceBaseAddress(sourceBaseAddress, credential.Mode);
        }

        if (innerHandler is null)
        {
            _ownedInnerHandler = HonuaHttpHandlerDefaults.CreateNoRedirectPrimaryHandler();
            innerHandler = _ownedInnerHandler;
        }

        InnerHandler = innerHandler;
    }

    /// <summary>Attaches the credential to a same-origin request and sends it.</summary>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_credential.Mode != GeoServicesCredentialMode.None && IsBoundOrigin(request.RequestUri))
        {
            await ApplyCredentialAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Releases the inner handler when disposing.</summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ownedInnerHandler?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Uri ValidateSourceBaseAddress(Uri sourceBaseAddress, GeoServicesCredentialMode mode)
    {
        if (!sourceBaseAddress.IsAbsoluteUri ||
            (!string.Equals(sourceBaseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(sourceBaseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            throw new HonuaConfigurationException("GeoServices REST source base address must be an absolute HTTP or HTTPS URI.");
        }

        if (mode != GeoServicesCredentialMode.None &&
            HonuaAuthenticationSupport.RequiresHttpsForAuthentication(sourceBaseAddress))
        {
            throw new HonuaConfigurationException(HonuaAuthenticationSupport.InsecureTransportRejectedMessage);
        }

        return new Uri(sourceBaseAddress.GetLeftPart(UriPartial.Authority));
    }

    private bool IsBoundOrigin(Uri? requestUri)
    {
        if (requestUri is null || !requestUri.IsAbsoluteUri)
        {
            return false;
        }

        var origin = Volatile.Read(ref _boundOrigin);
        if (origin is null)
        {
            // No configured source: bind to the first request this handler authenticates. Refuse an
            // insecure transport before binding, so a rejected plain-HTTP request cannot pin the
            // credential to an origin that a corrected HTTPS retry would then fail to match.
            EnsureSecureTransport(requestUri);
            var candidate = new Uri(requestUri.GetLeftPart(UriPartial.Authority));
            origin = Interlocked.CompareExchange(ref _boundOrigin, candidate, null) ?? candidate;
        }

        return NextLinkOriginValidator.IsSameOrigin(requestUri.AbsoluteUri, origin);
    }

    private async Task ApplyCredentialAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        switch (_credential.Mode)
        {
            case GeoServicesCredentialMode.Token:
                var token = _credential.TokenProvider is { } tokenProvider
                    ? await tokenProvider(cancellationToken).ConfigureAwait(false)
                    : _credential.Token;
                if (!string.IsNullOrEmpty(token))
                {
                    EnsureSecureTransport(request.RequestUri);
                    request.RequestUri = AppendQueryParameter(request.RequestUri!, "token", token);
                }

                break;

            case GeoServicesCredentialMode.Basic:
                if (!string.IsNullOrEmpty(_credential.Username))
                {
                    EnsureSecureTransport(request.RequestUri);
                    var raw = $"{_credential.Username}:{_credential.Password}";
                    var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
                }

                break;

            case GeoServicesCredentialMode.Bearer:
                var bearerToken = _credential.BearerTokenProvider is { } bearerProvider
                    ? await bearerProvider(cancellationToken).ConfigureAwait(false)
                    : _credential.BearerToken;
                if (!string.IsNullOrEmpty(bearerToken))
                {
                    EnsureSecureTransport(request.RequestUri);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                }

                break;

            case GeoServicesCredentialMode.None:
            default:
                break;
        }
    }

    private static void EnsureSecureTransport(Uri? requestUri)
    {
        // Same type and message as HonuaAuthHandler<TOptions>'s send-time refusal, so callers handle the
        // insecure-transport refusal identically for Honua-hosted and third-party GeoServices REST sources.
        if (HonuaAuthenticationSupport.RequiresHttpsForAuthentication(requestUri))
        {
            throw new InvalidOperationException(HonuaAuthenticationSupport.InsecureTransportRejectedMessage);
        }
    }

    private static Uri AppendQueryParameter(Uri uri, string name, string value)
    {
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return new Uri($"{uri.GetLeftPart(UriPartial.Path)}{uri.Query}{separator}{name}={Uri.EscapeDataString(value)}");
    }
}
