# Authentication

Honua SDK clients accept static credentials for simple deployments and provider
delegates for production token refresh, key rotation, and revocation.

## Supported Clients

These option types support `ApiKey`, `BearerToken`, `ApiKeyProvider`, and
`BearerTokenProvider`:

- `HonuaAdminClientOptions` for Admin and Geocoding
- `HonuaGrpcClientOptions` for gRPC
- `HonuaWfsClientOptions` for WFS
- `HonuaGeoServicesClientOptions` for GeoServices FeatureServer
- `HonuaOgcFeaturesClientOptions` for OGC API Features

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
    o.Address = "https://honua.example.com";
    o.ApiKeyProvider = ct => apiKeyStore.GetCurrentApiKeyAsync(ct);
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
