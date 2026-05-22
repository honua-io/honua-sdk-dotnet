# Authentication

Honua SDK clients accept static credentials for simple deployments, provider
delegates for key rotation, and request-aware token providers for production
OAuth/OIDC refresh flows.

## Supported Clients

These option types support `ApiKey`, `BearerToken`, `ApiKeyProvider`, and
`BearerTokenProvider`. They also support `AccessTokenProvider`,
`AuthenticationScopes`, `AuthenticationAudience`,
`AuthenticationDiagnostics`, and `PrimaryHttpMessageHandlerFactory`:

- `HonuaAdminClientOptions` for Admin and Geocoding
- `HonuaGrpcClientOptions` for gRPC
- `HonuaWfsClientOptions` for WFS
- `HonuaGeoServicesClientOptions` for GeoServices FeatureServer
- `HonuaOgcFeaturesClientOptions` for OGC API Features
- `HonuaOgcRecordsClientOptions` for OGC API Records
- `HonuaStacClientOptions` for STAC
- `HonuaSceneClientOptions` for scene metadata and offline scene packages
- `HonuaSpecClientOptions` for spec workspace plan/apply APIs

Provider delegates are invoked before each SDK request or RPC. When a provider
is configured, its value takes precedence over the static property. Returning
null or an empty string omits that credential header, which lets applications
stop sending revoked credentials without rebuilding the client.

```csharp
builder.Services.AddHonuaAdmin(o =>
{
    o.BaseAddress = new Uri("https://honua.example.com");
    o.BearerTokenProvider = ct => tokenCache.GetAccessTokenAsync(ct);
});

builder.Services.AddHonuaGrpc(o =>
{
    o.BaseAddress = new Uri("https://honua.example.com");
    o.ApiKeyProvider = ct => apiKeyStore.GetCurrentApiKeyAsync(ct);
});
```

## Production Token Providers

Prefer `AccessTokenProvider` for production OAuth/OIDC integration. It receives
a `HonuaAuthenticationRequest` with transport, service name, method, request
URI when available, configured scopes, and configured audience. This lets one
provider choose different tokens for Admin, OGC Features, FeatureServer, WFS,
OGC Records, STAC, Scenes, Spec, or gRPC calls without parsing global SDK
state.

`AccessTokenProvider` takes precedence over `BearerTokenProvider` and
`BearerToken`. API key sources remain independent and can be sent alongside an
access token when a deployment requires both.

```csharp
var tokenProvider = new HonuaOAuthClientCredentialsTokenProvider(
    httpClient,
    new HonuaOAuthClientCredentialsTokenProviderOptions
    {
        TokenEndpoint = new Uri("https://identity.example.com/oauth/token"),
        ClientId = "honua-worker",
        ClientSecret = clientSecret,
        Scopes = ["honua.features.read"],
        Audience = "https://honua.example.com"
    });

builder.Services.AddHonuaOgcFeatures(o =>
{
    o.BaseAddress = new Uri("https://honua.example.com");
    o.AccessTokenProvider = tokenProvider;
    o.AuthenticationScopes = ["honua.features.read"];
    o.AuthenticationAudience = "https://honua.example.com";
});
```

The SDK includes two built-in provider hooks:

- `HonuaOAuthClientCredentialsTokenProvider` for service-to-service flows.
- `HonuaOAuthAuthorizationCodeTokenProvider` for authorization-code and refresh-token flows.

Both providers call a standard token endpoint, cache the current access token
in memory, and refresh before expiry using `RefreshSkew`. They do not persist
tokens or log token endpoint payloads.

## Scopes, Audience, And Diagnostics

Set `AuthenticationScopes` and `AuthenticationAudience` on client options when
the identity provider requires per-service values. HTTP requests can override
the default context with `HonuaAuthenticationRequestOptions.Scopes`,
`HonuaAuthenticationRequestOptions.Audience`, and
`HonuaAuthenticationRequestOptions.Operation` before the auth handler runs.

`AuthenticationDiagnostics` receives sanitized events only. The SDK reports
whether an API key or authorization header was present, the authorization
scheme, transport, service, method, URI, scopes, and audience. It never passes
raw API key, bearer token, client secret, refresh token, or access token values
to diagnostics. `HonuaAuthenticationSupport.RedactSecret` is available for
caller-owned diagnostic code that needs a fixed redaction marker.

## Certificates And mTLS

Use `PrimaryHttpMessageHandlerFactory` when a deployment requires client
certificates, custom trust roots, proxies, or other enterprise transport
settings. REST clients pass the factory to `HttpClientFactory`; the gRPC client
passes it to `GrpcChannelOptions.HttpHandler`.

```csharp
builder.Services.AddHonuaFeatureServer(o =>
{
    o.BaseAddress = new Uri("https://honua.example.com");
    o.PrimaryHttpMessageHandlerFactory = () =>
    {
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(clientCertificate);
        return handler;
    };
});
```

## Storage Guidance

Do not hard-code long-lived API keys, bearer tokens, or refresh tokens in
source, project files, or checked-in configuration. Prefer one of these
application-owned stores:

- Cloud secret managers or managed identity flows in hosted services
- OS-backed secure stores such as Windows Credential Manager, macOS Keychain,
  Linux Secret Service/libsecret, or platform-specific mobile secure storage
- ASP.NET Core user-secrets for local development only
- Environment variables injected by deployment tooling, not committed `.env`
  files

The SDK does not persist credentials. Provider delegates should read from your
secure store or token cache and return the current API key or bearer token.

Browser and Blazor WebAssembly apps need a stricter boundary: every value in
static browser configuration is visible to clients. Do not ship privileged
admin API keys, refresh tokens, client secrets, or server-side service tokens in
`wwwroot`, environment-generated static assets, or downloaded appsettings
files. Browser hosts should use delegated user bearer tokens, a same-origin
BFF that injects privileged credentials server-side, or another application-
owned auth flow that never exposes server-only secrets to JavaScript or WASM.

## Rotation and Revocation

Use providers when credentials can change while the process is running. A
provider can refresh an expiring bearer token before returning it, read the
latest API key after rotation, or return null after revocation.

Static `ApiKey` and `BearerToken` values remain useful for tests and short-lived
tools, but changing those properties after client registration is not the
recommended rotation mechanism.

## Transport and Failure Behavior

The SDK refuses to send credentials over remote plain HTTP. HTTPS is required
whenever static credentials or credential providers are configured. The only
HTTP exception is loopback or `localhost` for local development.

If a provider throws or its cancellation token is canceled, the current SDK call
fails before the transport request is sent. Authentication failures from the
server, such as `401` or `403`, are not retried by the SDK retry policy. HTTP
clients retry safe methods only on `429`, `502`, and `503`; the gRPC client
retries read queries only on `Unavailable` and `Internal` according to its
configured retry policy. Treat the provider as the place to refresh before a
request, not as a response-time recovery hook for expired credentials.
