// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Net.Http.Headers;
using System.Text;

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
}

/// <summary>
/// Delegating handler that applies an <see cref="ArcGisSourceCredential"/> to outgoing requests
/// against a third-party ArcGIS source. Compose with an inner transport handler (or leave the default)
/// and register the result via <c>HonuaGeoServicesClientOptions.PrimaryHttpMessageHandlerFactory</c>.
/// </summary>
public sealed class ArcGisSourceCredentialHandler : DelegatingHandler
{
    private readonly ArcGisSourceCredential _credential;

    // The default transport this handler created itself (null when the caller supplied one). This
    // handler owns it and disposes it in Dispose(bool).
    private readonly HttpClientHandler? _ownedInnerHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="ArcGisSourceCredentialHandler"/> class.
    /// </summary>
    /// <param name="credential">The credential to apply to every outgoing request.</param>
    /// <param name="innerHandler">
    /// Optional inner handler. Defaults to a new <see cref="HttpClientHandler"/> owned by this handler.
    /// As with any <see cref="DelegatingHandler"/>, the inner handler is disposed with this handler.
    /// </param>
    public ArcGisSourceCredentialHandler(ArcGisSourceCredential credential, HttpMessageHandler? innerHandler = null)
    {
        ArgumentNullException.ThrowIfNull(credential);
        _credential = credential;

        if (innerHandler is null)
        {
            _ownedInnerHandler = new HttpClientHandler();
            innerHandler = _ownedInnerHandler;
        }

        InnerHandler = innerHandler;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        switch (_credential.Mode)
        {
            case ArcGisCredentialMode.Token:
                var token = _credential.TokenProvider is { } tokenProvider
                    ? await tokenProvider(cancellationToken).ConfigureAwait(false)
                    : _credential.Token;
                if (!string.IsNullOrEmpty(token) && request.RequestUri is not null)
                {
                    request.RequestUri = AppendQueryParameter(request.RequestUri, "token", token);
                }

                break;

            case ArcGisCredentialMode.Basic:
                if (!string.IsNullOrEmpty(_credential.Username))
                {
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
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
                }

                break;

            case ArcGisCredentialMode.None:
            default:
                break;
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

    private static Uri AppendQueryParameter(Uri uri, string name, string value)
    {
        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        return new Uri($"{uri.GetLeftPart(UriPartial.Path)}{uri.Query}{separator}{name}={Uri.EscapeDataString(value)}");
    }
}
