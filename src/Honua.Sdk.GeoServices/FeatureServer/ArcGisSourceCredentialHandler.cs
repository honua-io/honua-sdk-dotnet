// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net.Http.Headers;
using System.Text;
using Honua.Sdk.Abstractions;
using Honua.Sdk.Abstractions.Authentication;
using Honua.Sdk.Abstractions.Http;

namespace Honua.Sdk.GeoServices.FeatureServer;

/// <summary>
/// Authentication mode applied by <see cref="ArcGisSourceCredentialHandler"/>.
/// </summary>
public enum ArcGisCredentialMode
{
    /// <summary>No credential is applied.</summary>
    None,

    /// <summary>
    /// Classic ArcGIS token authentication: appends <c>token=&lt;value&gt;</c> to the request's
    /// query string. This is the widely-supported ArcGIS Server / Portal token flow (the result of
    /// <c>generateToken</c>) and works against sources that do not federate OAuth bearer tokens.
    /// </summary>
    /// <remarks>
    /// The ArcGIS REST protocol requires the token to travel in the request URL, so in this mode the
    /// secret is visible to anything that records URLs (proxy and server access logs, diagnostics,
    /// browser history on the source side). Use HTTPS for every Token-mode source; the handler refuses
    /// to send the token over plain HTTP to anything other than a loopback/<c>localhost</c> host. Prefer
    /// <see cref="Bearer"/> where the source supports it, because it keeps the secret out of the URL.
    /// </remarks>
    Token,

    /// <summary>HTTP Basic authentication via the <c>Authorization: Basic</c> header.</summary>
    Basic,

    /// <summary>OAuth/federated bearer token via the <c>Authorization: Bearer</c> header.</summary>
    Bearer
}

/// <summary>
/// Credential for authenticating directly against a third-party ArcGIS source service (as opposed to
/// <see cref="HonuaGeoServicesClientOptions"/>'s <c>ApiKey</c>/<c>BearerToken</c>, which authenticate
/// to Honua's own server). Construct with <see cref="ArcGisSourceCredentialHandler"/> and register it
/// via <c>HonuaGeoServicesClientOptions.PrimaryHttpMessageHandlerFactory</c> when building a client
/// that queries an external ArcGIS Server/Portal service for import.
/// </summary>
public sealed record ArcGisSourceCredential
{
    /// <summary>The authentication mode to apply.</summary>
    public required ArcGisCredentialMode Mode { get; init; }

    /// <summary>The token value for <see cref="ArcGisCredentialMode.Token"/>.</summary>
    public string? Token { get; init; }

    /// <summary>
    /// Optional token provider for <see cref="ArcGisCredentialMode.Token"/>, invoked before each
    /// request. Takes precedence over <see cref="Token"/> when set; returning null or empty omits the
    /// token parameter for that request.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? TokenProvider { get; init; }

    /// <summary>The username for <see cref="ArcGisCredentialMode.Basic"/>.</summary>
    public string? Username { get; init; }

    /// <summary>The password for <see cref="ArcGisCredentialMode.Basic"/>.</summary>
    public string? Password { get; init; }

    /// <summary>The bearer token for <see cref="ArcGisCredentialMode.Bearer"/>.</summary>
    public string? BearerToken { get; init; }

    /// <summary>
    /// Optional bearer token provider for <see cref="ArcGisCredentialMode.Bearer"/>, invoked before
    /// each request. Takes precedence over <see cref="BearerToken"/> when set; returning null or empty
    /// omits the authorization header for that request.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? BearerTokenProvider { get; init; }

    /// <summary>
    /// The base address of the ArcGIS source this credential belongs to (for example
    /// <c>https://gis.example.com/arcgis/rest/services/</c>). Only its scheme, host and port are used:
    /// the credential is attached only to requests whose origin matches exactly, so a stray absolute URL
    /// or a server-supplied link to another host never receives it. Strongly recommended. When unset, the
    /// handler binds the credential to the origin of the first request it authenticates and refuses to
    /// attach it to any other origin for the handler's lifetime.
    /// </summary>
    public Uri? SourceBaseAddress { get; init; }
}

/// <summary>
/// Delegating handler that applies an <see cref="ArcGisSourceCredential"/> to outgoing requests
/// against a third-party ArcGIS source. Compose with an inner transport handler (or leave the default)
/// and register the result via <c>HonuaGeoServicesClientOptions.PrimaryHttpMessageHandlerFactory</c>.
/// </summary>
/// <remarks>
/// <para>
/// The handler applies the same transport guards as the SDK's Honua-hosted clients:
/// </para>
/// <list type="bullet">
/// <item><description>A credential is never sent over plain HTTP except to a loopback/<c>localhost</c>
/// development host. As with the Honua-hosted clients' <c>HonuaAuthHandler</c>, such a request fails
/// with <see cref="InvalidOperationException"/> carrying
/// <see cref="HonuaAuthenticationSupport.InsecureTransportRejectedMessage"/>, before the credential is
/// attached or the request is sent. A plain-HTTP non-loopback
/// <see cref="ArcGisSourceCredential.SourceBaseAddress"/> is instead rejected at construction with
/// <see cref="HonuaConfigurationException"/> and the same message, matching the SDK's other
/// construction-time options validation.</description></item>
/// <item><description>A credential is attached only to requests whose scheme, host and port match the
/// source origin (see <see cref="ArcGisSourceCredential.SourceBaseAddress"/>); requests to any other
/// origin are sent without it.</description></item>
/// <item><description>The default inner transport does not follow redirects, so a <c>30x</c> response
/// surfaces to the caller instead of being replayed (with the token in the URL) against the redirect
/// target. A caller-supplied inner handler is used exactly as configured: this handler never enables
/// redirect following on it, and callers that enable it themselves accept the credential-forwarding
/// semantics that entails.</description></item>
/// </list>
/// </remarks>
public sealed class ArcGisSourceCredentialHandler : DelegatingHandler
{
    private readonly ArcGisSourceCredential _credential;

    // The default transport this handler created itself (null when the caller supplied one). This
    // handler owns it and disposes it in Dispose(bool).
    private readonly HttpMessageHandler? _ownedInnerHandler;

    // The origin the credential is bound to: the configured source base address, or (when none is
    // configured) the origin of the first authenticated request.
    private Uri? _boundOrigin;


    /// <summary>
    /// Initializes a new instance of the <see cref="ArcGisSourceCredentialHandler"/> class.
    /// </summary>
    /// <param name="credential">The credential to apply to every outgoing request.</param>
    /// <param name="innerHandler">
    /// Optional inner handler. Defaults to a new handler owned by this handler that does not follow
    /// redirects (see <see cref="HonuaHttpHandlerDefaults.CreateNoRedirectPrimaryHandler"/>). A supplied
    /// handler is used as configured and is not modified. As with any <see cref="DelegatingHandler"/>,
    /// the inner handler is disposed with this handler.
    /// </param>
    /// <exception cref="HonuaConfigurationException">
    /// <see cref="ArcGisSourceCredential.SourceBaseAddress"/> is set but is not an absolute HTTP(S) URI,
    /// or it is a plain-HTTP non-loopback address and <see cref="ArcGisSourceCredential.Mode"/> carries a
    /// credential.
    /// </exception>
    public ArcGisSourceCredentialHandler(ArcGisSourceCredential credential, HttpMessageHandler? innerHandler = null)
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

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_credential.Mode != ArcGisCredentialMode.None && IsBoundOrigin(request.RequestUri))
        {
            await ApplyCredentialAsync(request, cancellationToken).ConfigureAwait(false);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ownedInnerHandler?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Uri ValidateSourceBaseAddress(Uri sourceBaseAddress, ArcGisCredentialMode mode)
    {
        if (!sourceBaseAddress.IsAbsoluteUri ||
            (!string.Equals(sourceBaseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(sourceBaseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            throw new HonuaConfigurationException("ArcGIS source base address must be an absolute HTTP or HTTPS URI.");
        }

        if (mode != ArcGisCredentialMode.None &&
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
            // No configured source: bind to the first request this handler authenticates.
            var candidate = new Uri(requestUri.GetLeftPart(UriPartial.Authority));
            origin = Interlocked.CompareExchange(ref _boundOrigin, candidate, null) ?? candidate;
        }

        return NextLinkOriginValidator.IsSameOrigin(requestUri.AbsoluteUri, origin);
    }

    private async Task ApplyCredentialAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        switch (_credential.Mode)
        {
            case ArcGisCredentialMode.Token:
                var token = _credential.TokenProvider is { } tokenProvider
                    ? await tokenProvider(cancellationToken).ConfigureAwait(false)
                    : _credential.Token;
                if (!string.IsNullOrEmpty(token))
                {
                    EnsureSecureTransport(request.RequestUri);
                    request.RequestUri = AppendQueryParameter(request.RequestUri!, "token", token);
                }

                break;

            case ArcGisCredentialMode.Basic:
                if (!string.IsNullOrEmpty(_credential.Username))
                {
                    EnsureSecureTransport(request.RequestUri);
                    var raw = $"{_credential.Username}:{_credential.Password}";
                    var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
                }

                break;

            case ArcGisCredentialMode.Bearer:
                var bearerToken = _credential.BearerTokenProvider is { } bearerProvider
                    ? await bearerProvider(cancellationToken).ConfigureAwait(false)
                    : _credential.BearerToken;
                if (!string.IsNullOrEmpty(bearerToken))
                {
                    EnsureSecureTransport(request.RequestUri);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                }

                break;

            case ArcGisCredentialMode.None:
            default:
                break;
        }
    }

    private static void EnsureSecureTransport(Uri? requestUri)
    {
        // Same type and message as HonuaAuthHandler<TOptions>'s send-time refusal, so callers handle the
        // insecure-transport refusal identically for Honua-hosted and third-party ArcGIS sources.
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
