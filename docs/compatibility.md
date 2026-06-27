# Compatibility Policy

This SDK treats compatibility as two separate contracts: server compatibility
for runtime behavior and package API compatibility for released .NET packages.

## Server Compatibility Matrix

| SDK package baseline | Honua Server baseline | Release channel baseline | Admin API major | Admin API base path |
|----------------------|-----------------------|--------------------------|-----------------|---------------------|
| `1.x` | `0.1.0` or newer | `preview` or later | `1` | `/api/v1/admin` |

`Honua.Sdk.Admin` evaluates this matrix through
`HonuaAdminCompatibility.Evaluate()` and `IHonuaAdminClient.CheckCompatibilityAsync()`.
A server is unsupported when it omits compatibility metadata, reports a lower
server version, advertises a lower release channel, changes the control-plane
API major, changes the admin base path, or marks the advertised control-plane
API as deprecated.

Supported release channels, from lowest to highest, are `nightly`, `dev`,
`alpha`, `preview`, `beta`, `rc`, `stable`, and `lts`. The current SDK baseline
requires `preview` or higher.

## Package API Compatibility Gate

CI validates public package API compatibility by packing the baseline ref and
the current checkout, then comparing the resulting `.nupkg` files with
`Microsoft.DotNet.ApiCompat.Tool`.

The gate runs for:

- Pull requests and pushes in `.github/workflows/ci.yml`.
- Package publish dry runs and tag publishes in
  `.github/workflows/publish-dotnet-sdk.yml`.

Run the same check locally with:

```bash
scripts/validate-api-compat.sh origin/trunk
```

Breaking public API changes should be avoided for the current `1.x` package
line unless the release plan explicitly accepts the break. When the server
compatibility baseline changes, update `HonuaAdminCompatibility`, the
compatibility matrix tests, and this document in the same pull request.
