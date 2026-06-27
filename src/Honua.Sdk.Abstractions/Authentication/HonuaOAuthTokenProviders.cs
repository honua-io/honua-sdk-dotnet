// Copyright (c) Honua. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Honua.Sdk.Abstractions.Authentication;

/// <summary>
/// Options for the built-in OAuth/OIDC client-credentials token provider.
/// </summary>
public sealed class HonuaOAuthClientCredentialsTokenProviderOptions
{
    /// <summary>
    /// OAuth/OIDC token endpoint.
    /// </summary>
    public Uri? TokenEndpoint { get; init; }

    /// <summary>
    /// OAuth/OIDC client identifier.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Optional OAuth/OIDC client secret. Do not use this in browser or mobile public-client builds.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Default scopes requested from the token endpoint.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Default audience or resource requested from the token endpoint.
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Token refresh skew applied before expiration. Defaults to five minutes.
    /// </summary>
    public TimeSpan RefreshSkew { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Extra token endpoint parameters for identity-provider-specific requirements.
    /// </summary>
    public IReadOnlyDictionary<string, string> AdditionalParameters { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// Options for the built-in OAuth/OIDC authorization-code token provider.
/// </summary>
public sealed class HonuaOAuthAuthorizationCodeTokenProviderOptions
{
    /// <summary>
    /// OAuth/OIDC token endpoint.
    /// </summary>
    public Uri? TokenEndpoint { get; init; }

    /// <summary>
    /// OAuth/OIDC client identifier.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>
    /// Optional OAuth/OIDC client secret. Do not use this in browser or mobile public-client builds.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Initial authorization code to exchange when no refresh token is available.
    /// </summary>
    public string? AuthorizationCode { get; init; }

    /// <summary>
    /// Redirect URI used for the authorization-code exchange.
    /// </summary>
    public Uri? RedirectUri { get; init; }

    /// <summary>
    /// Optional PKCE code verifier used for public-client authorization-code flows.
    /// </summary>
    public string? CodeVerifier { get; init; }

    /// <summary>
    /// Optional initial refresh token used instead of an authorization-code exchange.
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// Default scopes requested from the token endpoint.
    /// </summary>
    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Default audience or resource requested from the token endpoint.
    /// </summary>
    public string? Audience { get; init; }

    /// <summary>
    /// Token refresh skew applied before expiration. Defaults to five minutes.
    /// </summary>
    public TimeSpan RefreshSkew { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Extra token endpoint parameters for identity-provider-specific requirements.
    /// </summary>
    public IReadOnlyDictionary<string, string> AdditionalParameters { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>
/// OAuth/OIDC client-credentials provider with in-memory access-token refresh.
/// </summary>
public sealed class HonuaOAuthClientCredentialsTokenProvider : IHonuaAccessTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly HonuaOAuthClientCredentialsTokenProviderOptions _options;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, HonuaAccessToken> _cachedTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<HonuaAccessToken?>> _refreshTasks = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOAuthClientCredentialsTokenProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client used to call the token endpoint.</param>
    /// <param name="options">Provider options.</param>
    public HonuaOAuthClientCredentialsTokenProvider(
        HttpClient httpClient,
        HonuaOAuthClientCredentialsTokenProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        HonuaOAuthTokenEndpoint.ValidateTokenEndpoint(options.TokenEndpoint);
        HonuaOAuthTokenEndpoint.ValidateRefreshSkew(options.RefreshSkew);
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new ArgumentException("OAuth client id must be configured.", nameof(options));
        }

        _httpClient = httpClient;
        _options = options;
    }

    /// <inheritdoc />
    public ValueTask<HonuaAccessToken?> GetAccessTokenAsync(
        HonuaAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cacheKey = HonuaOAuthTokenEndpoint.CreateCacheKey(
            _options.Scopes,
            request.Scopes,
            request.Audience ?? _options.Audience);
        lock (_syncRoot)
        {
            if (_cachedTokens.TryGetValue(cacheKey, out var cachedToken) &&
                cachedToken.IsUsable(DateTimeOffset.UtcNow, _options.RefreshSkew))
            {
                return ValueTask.FromResult<HonuaAccessToken?>(cachedToken);
            }
        }

        return new ValueTask<HonuaAccessToken?>(GetOrRefreshAsync(request, cacheKey, cancellationToken));
    }

    private async Task<HonuaAccessToken?> GetOrRefreshAsync(
        HonuaAuthenticationRequest request,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        Task<HonuaAccessToken?> refreshTask;
        lock (_syncRoot)
        {
            if (_cachedTokens.TryGetValue(cacheKey, out var cachedToken) &&
                cachedToken.IsUsable(DateTimeOffset.UtcNow, _options.RefreshSkew))
            {
                return cachedToken;
            }

            if (!_refreshTasks.TryGetValue(cacheKey, out var existing) || existing.IsCompleted)
            {
                // Coalesce concurrent refreshes, but run the shared request under a
                // non-cancellable token. Binding it to the first caller's token would let
                // that caller's cancellation/timeout fault the refresh for every other
                // waiter (the exact thundering-herd case this de-duplication exists for).
                existing = RequestTokenAsync(request, cacheKey, CancellationToken.None);
                _refreshTasks[cacheKey] = existing;
            }

            refreshTask = existing;
        }

        // Each caller observes the shared refresh but honors its own cancellation token,
        // so an abandoned caller never propagates failure to unrelated concurrent callers.
        return await refreshTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HonuaAccessToken?> RequestTokenAsync(
        HonuaAuthenticationRequest request,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", _options.ClientId)
        };
        HonuaOAuthTokenEndpoint.AddIfNotEmpty(parameters, "client_secret", _options.ClientSecret);
        HonuaOAuthTokenEndpoint.AddScope(parameters, _options.Scopes, request.Scopes);
        HonuaOAuthTokenEndpoint.AddIfNotEmpty(parameters, "audience", request.Audience ?? _options.Audience);
        HonuaOAuthTokenEndpoint.AddAdditionalParameters(parameters, _options.AdditionalParameters);

        var response = await HonuaOAuthTokenEndpoint.RequestAsync(
            _httpClient,
            _options.TokenEndpoint!,
            parameters,
            cancellationToken).ConfigureAwait(false);

        lock (_syncRoot)
        {
            _cachedTokens[cacheKey] = response.AccessToken;
        }

        return response.AccessToken;
    }
}

/// <summary>
/// OAuth/OIDC authorization-code provider with in-memory access-token refresh.
/// </summary>
public sealed class HonuaOAuthAuthorizationCodeTokenProvider : IHonuaAccessTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly HonuaOAuthAuthorizationCodeTokenProviderOptions _options;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, HonuaAccessToken> _cachedTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<HonuaAccessToken?>> _refreshTasks = new(StringComparer.Ordinal);
    private string? _refreshToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="HonuaOAuthAuthorizationCodeTokenProvider"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client used to call the token endpoint.</param>
    /// <param name="options">Provider options.</param>
    public HonuaOAuthAuthorizationCodeTokenProvider(
        HttpClient httpClient,
        HonuaOAuthAuthorizationCodeTokenProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        HonuaOAuthTokenEndpoint.ValidateTokenEndpoint(options.TokenEndpoint);
        HonuaOAuthTokenEndpoint.ValidateRefreshSkew(options.RefreshSkew);
        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            throw new ArgumentException("OAuth client id must be configured.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.AuthorizationCode) &&
            string.IsNullOrWhiteSpace(options.RefreshToken))
        {
            throw new ArgumentException(
                "OAuth authorization-code provider requires an authorization code or refresh token.",
                nameof(options));
        }

        _httpClient = httpClient;
        _options = options;
        _refreshToken = options.RefreshToken;
    }

    /// <inheritdoc />
    public ValueTask<HonuaAccessToken?> GetAccessTokenAsync(
        HonuaAuthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cacheKey = HonuaOAuthTokenEndpoint.CreateCacheKey(
            _options.Scopes,
            request.Scopes,
            request.Audience ?? _options.Audience);
        lock (_syncRoot)
        {
            if (_cachedTokens.TryGetValue(cacheKey, out var cachedToken) &&
                cachedToken.IsUsable(DateTimeOffset.UtcNow, _options.RefreshSkew))
            {
                return ValueTask.FromResult<HonuaAccessToken?>(cachedToken);
            }
        }

        return new ValueTask<HonuaAccessToken?>(GetOrRefreshAsync(request, cacheKey, cancellationToken));
    }

    private async Task<HonuaAccessToken?> GetOrRefreshAsync(
        HonuaAuthenticationRequest request,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        Task<HonuaAccessToken?> refreshTask;
        lock (_syncRoot)
        {
            if (_cachedTokens.TryGetValue(cacheKey, out var cachedToken) &&
                cachedToken.IsUsable(DateTimeOffset.UtcNow, _options.RefreshSkew))
            {
                return cachedToken;
            }

            if (!_refreshTasks.TryGetValue(cacheKey, out var existing) || existing.IsCompleted)
            {
                // Coalesce concurrent refreshes, but run the shared request under a
                // non-cancellable token. Binding it to the first caller's token would let
                // that caller's cancellation/timeout fault the refresh for every other
                // waiter (the exact thundering-herd case this de-duplication exists for).
                existing = RequestTokenAsync(request, cacheKey, CancellationToken.None);
                _refreshTasks[cacheKey] = existing;
            }

            refreshTask = existing;
        }

        // Each caller observes the shared refresh but honors its own cancellation token,
        // so an abandoned caller never propagates failure to unrelated concurrent callers.
        return await refreshTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<HonuaAccessToken?> RequestTokenAsync(
        HonuaAuthenticationRequest request,
        string cacheKey,
        CancellationToken cancellationToken)
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("client_id", _options.ClientId)
        };

        if (!string.IsNullOrWhiteSpace(_refreshToken))
        {
            parameters.Add(new KeyValuePair<string, string>("grant_type", "refresh_token"));
            HonuaOAuthTokenEndpoint.AddIfNotEmpty(parameters, "refresh_token", _refreshToken);
        }
        else
        {
            parameters.Add(new KeyValuePair<string, string>("grant_type", "authorization_code"));
            HonuaOAuthTokenEndpoint.AddIfNotEmpty(parameters, "code", _options.AuthorizationCode);
            HonuaOAuthTokenEndpoint.AddIfNotEmpty(parameters, "redirect_uri", _options.RedirectUri?.ToString());
            HonuaOAuthTokenEndpoint.AddIfNotEmpty(parameters, "code_verifier", _options.CodeVerifier);
        }

        HonuaOAuthTokenEndpoint.AddIfNotEmpty(parameters, "client_secret", _options.ClientSecret);
        HonuaOAuthTokenEndpoint.AddScope(parameters, _options.Scopes, request.Scopes);
        HonuaOAuthTokenEndpoint.AddIfNotEmpty(parameters, "audience", request.Audience ?? _options.Audience);
        HonuaOAuthTokenEndpoint.AddAdditionalParameters(parameters, _options.AdditionalParameters);

        var response = await HonuaOAuthTokenEndpoint.RequestAsync(
            _httpClient,
            _options.TokenEndpoint!,
            parameters,
            cancellationToken).ConfigureAwait(false);

        lock (_syncRoot)
        {
            _cachedTokens[cacheKey] = response.AccessToken;
            if (!string.IsNullOrWhiteSpace(response.RefreshToken))
            {
                _refreshToken = response.RefreshToken;
            }
        }

        return response.AccessToken;
    }
}

internal static class HonuaOAuthTokenEndpoint
{
    public static void AddAdditionalParameters(
        ICollection<KeyValuePair<string, string>> parameters,
        IEnumerable<KeyValuePair<string, string>> additionalParameters)
    {
        foreach (var parameter in additionalParameters)
        {
            AddIfNotEmpty(parameters, parameter.Key, parameter.Value);
        }
    }

    public static void AddIfNotEmpty(
        ICollection<KeyValuePair<string, string>> parameters,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
        {
            parameters.Add(new KeyValuePair<string, string>(name, value));
        }
    }

    public static void AddScope(
        ICollection<KeyValuePair<string, string>> parameters,
        IEnumerable<string> defaultScopes,
        IEnumerable<string> requestScopes)
    {
        var scopes = HonuaAuthenticationSupport.NormalizeScopes(defaultScopes.Concat(requestScopes));
        if (scopes.Count > 0)
        {
            parameters.Add(new KeyValuePair<string, string>("scope", string.Join(' ', scopes)));
        }
    }

    public static string CreateCacheKey(
        IEnumerable<string> defaultScopes,
        IEnumerable<string> requestScopes,
        string? audience)
    {
        var scopes = HonuaAuthenticationSupport.NormalizeScopes(defaultScopes.Concat(requestScopes));
        return string.Concat(string.Join(' ', scopes), "|", audience ?? string.Empty);
    }

    public static void ValidateTokenEndpoint(Uri? tokenEndpoint)
    {
        if (tokenEndpoint is null)
        {
            throw new ArgumentException("OAuth token endpoint must be configured.", nameof(tokenEndpoint));
        }

        if (!tokenEndpoint.IsAbsoluteUri)
        {
            throw new ArgumentException("OAuth token endpoint must be an absolute URI.", nameof(tokenEndpoint));
        }

        if (!string.Equals(tokenEndpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
            !IsLocalDevelopmentHttp(tokenEndpoint))
        {
            throw new ArgumentException(
                "OAuth token endpoint must use HTTPS, except loopback HTTP for local development.",
                nameof(tokenEndpoint));
        }
    }

    public static void ValidateRefreshSkew(TimeSpan refreshSkew)
    {
        if (refreshSkew < TimeSpan.Zero || refreshSkew > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshSkew),
                refreshSkew,
                "OAuth refresh skew must be between zero and one hour.");
        }
    }

    public static async Task<HonuaOAuthTokenEndpointResponse> RequestAsync(
        HttpClient httpClient,
        Uri tokenEndpoint,
        IEnumerable<KeyValuePair<string, string>> parameters,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(parameters)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(responseStream, default, cancellationToken).ConfigureAwait(false);
        return HonuaOAuthTokenEndpointResponse.Parse(document.RootElement, DateTimeOffset.UtcNow);
    }

    private static bool IsLocalDevelopmentHttp(Uri uri)
        => string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
           (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase));
}

internal sealed class HonuaOAuthTokenEndpointResponse
{
    private HonuaOAuthTokenEndpointResponse(HonuaAccessToken accessToken, string? refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
    }

    public HonuaAccessToken AccessToken { get; }

    public string? RefreshToken { get; }

    public static HonuaOAuthTokenEndpointResponse Parse(JsonElement root, DateTimeOffset utcNow)
    {
        var accessToken = ReadString(root, "access_token");
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("OAuth token endpoint response did not include an access token.");
        }

        var tokenType = ReadString(root, "token_type");
        var scope = ReadString(root, "scope");
        var audience = ReadString(root, "audience");
        var expiresIn = ReadExpiresIn(root);

        return new HonuaOAuthTokenEndpointResponse(
            new HonuaAccessToken
            {
                Token = accessToken,
                TokenType = string.IsNullOrWhiteSpace(tokenType) ? "Bearer" : tokenType,
                ExpiresAtUtc = expiresIn.HasValue ? utcNow.AddSeconds(expiresIn.Value) : null,
                Scopes = SplitScope(scope),
                Audience = audience
            },
            ReadString(root, "refresh_token"));
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    private static double? ReadExpiresIn(JsonElement root)
    {
        if (!root.TryGetProperty("expires_in", out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var seconds))
        {
            return seconds;
        }

        if (property.ValueKind == JsonValueKind.String &&
            double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
        {
            return seconds;
        }

        return null;
    }

    private static string[] SplitScope(string? scope)
        => string.IsNullOrWhiteSpace(scope)
            ? Array.Empty<string>()
            : scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
