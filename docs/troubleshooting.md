# Troubleshooting

Common issues and how to fix them when integrating the Honua .NET SDK.

If you cannot find your issue here, open a [GitHub issue](https://github.com/honua-io/honua-sdk-dotnet/issues)
with the package name, version, server version (`AdminClient.CheckCompatibilityAsync()` output is
ideal), the call you're making, and the exception message and stack.

---

## Configuration

### `InvalidOperationException: Honua <X> base address must be configured.`

You instantiated an SDK client without setting `BaseAddress` (or `Address` for `Honua.Sdk.Grpc`).
Set the server URL in `AddHonua*(o => o.BaseAddress = new Uri("https://..."))`. The SDK no longer
defaults to a baked-in `localhost` URL on purpose -- silent localhost defaults masked
mis-configuration in production.

### `Honua <X> base address must use HTTP or HTTPS.`

`BaseAddress` was set to a non-HTTP(S) scheme (e.g. `file://`, `ftp://`, or a relative URI). Use
an absolute `http://` or `https://` URL.

### `Honua <X> timeout must be greater than 10 milliseconds and less than 24 hours.`

`Options.Timeout` is out of range. Common cause: assigning `TimeSpan.Zero` or a negative span.
Pick a value in `[11ms, 24h)`.

---

## Authentication

### Credentials silently absent / `401 Unauthorized` on every call

By design, the SDK only sends `ApiKey` / `BearerToken` (or the values returned by
`ApiKeyProvider` / `BearerTokenProvider` / `AccessTokenProvider`) **over HTTPS**, with one
exception: loopback / `localhost` HTTP for local development.

If you're hitting `http://your-internal-host` (non-loopback) with credentials configured, the
auth header is intentionally dropped and the server returns 401. Use HTTPS or terminate TLS
upstream.

See [authentication.md](authentication.md) for the storage, refresh, and failure model and how
`AccessTokenProvider` takes precedence over `BearerTokenProvider` / `BearerToken`.

### Token provider raises -- requests still fire without credentials

If your `*TokenProvider` delegate returns `null` or an empty string, the SDK omits the credential
header on that request. To fail closed instead, throw from the provider; the SDK propagates the
exception to the caller.

---

## gRPC

### `Grpc.Core.RpcException: Status(StatusCode="Unavailable", Detail="...")` in the browser

Browsers cannot speak native gRPC. From Blazor WebAssembly / browser JS hosts you must talk
gRPC-Web through the server's gRPC-Web endpoint, not the native gRPC port. See
[docs/browser-wasm-support.md](browser-wasm-support.md) -- it documents the supported browser
surface (REST clients only) plus the recommended escape hatch for native gRPC.

### Channel disposed / `ObjectDisposedException` on reuse

`HonuaGrpcClient` and `HonuaProcessGrpcClient` each own a `GrpcChannel` when
constructed from options. The DI extension `AddHonuaGrpc` registers
`IHonuaGrpcClient` and `IHonuaProcessGrpcClient` as singletons so those channels
survive across requests. If you construct either concrete client manually and
dispose it, you cannot reuse it -- the underlying channel is gone. Either let
DI own the lifetime, or pass a long-lived `GrpcChannel` to the matching
channel-based constructor.

### `BaseAddress` vs `Address` on gRPC

Both work. `BaseAddress` (`Uri`) is preferred for parity with the REST SDK clients; `Address`
(`string`) remains supported. If both are set, `BaseAddress` wins.

---

## Retry, timeout, and resilience

### Calls return immediately with `OperationCanceledException` even though the server is healthy

Your `Options.Timeout` is shorter than the request actually needs. The same value gates both the
per-attempt and the total-pipeline budget. Raise `Timeout`, or disable retries with
`EnableRetry = false` while you isolate the slow call.

### Setting `MaxRetryAttempts` throws `ArgumentOutOfRangeException`

`MaxRetryAttempts` must be in the inclusive range `[2, 5]` (Polly's standard
resilience pipeline minimum is 2 attempts including the original call). Values
outside that range fail at options assignment time for every package, including
`Honua.Sdk`, `Honua.Sdk.Processes`, and `Honua.Sdk.Grpc`.

### Unsafe HTTP methods aren't retried

Intentional. `Retry.DisableForUnsafeHttpMethods()` is set across every REST client to avoid
duplicate writes. If you need POST/PUT/DELETE retries for an idempotent endpoint, register a
custom resilience pipeline on the `HttpClient` after `AddHonua*`.

---

## CORS / browser

### Browser requests are blocked by CORS

The SDK does not configure CORS -- that's the server's responsibility. Confirm the Honua server
allow-lists your origin and that pre-flight (`OPTIONS`) responses include the headers you set
(e.g. `Authorization`, `X-API-Key`). For browser hosts, also see
[docs/browser-wasm-support.md](browser-wasm-support.md).

---

## Compatibility gate

### `CheckCompatibilityAsync` returns `IsSupported = false`

`Honua.Sdk.Admin` requires a minimum server version and release channel; see
[docs/compatibility.md](compatibility.md). The `UnsupportedReason` string tells you the gap.
Common cases:

- The server is older than `HonuaAdminCompatibility.MinimumSupportedServerVersion`. Upgrade the
  server, or pin the SDK to a matching prior pre-release.
- The server's release channel (`dev`, `preview`, `stable`) is below the SDK's baseline. The
  capabilities endpoint reports the channel; promote the server or use a matching SDK build.

---

## Catalog / Records / STAC / Scenes

### `404 Not Found` on STAC landing page / OGC API Records root

The Honua server must explicitly enable the public OGC API Records catalog
(`/records`), the STAC catalog (`/stac`), or the OGC API Features landing page
under its `/api/v1` base. If the server returns 404, confirm:

1. The server is on a version that exposes the catalog (see
   [compatibility.md](compatibility.md)).
2. The matching capability flag is enabled in
   `adminClient.GetCapabilitiesAsync()`.
3. Your `BaseAddress` does *not* include the protocol-specific suffix — pass the
   bare server root (e.g. `https://your-host`), not `https://your-host/stac`.
   The clients append their own protocol prefix.

### `StacException: STAC response did not include the expected ...` / extension fields not surfaced

Use the `*JsonAsync` or `*RawAsync` escape hatches on `IHonuaStacClient` to read
the raw response — these methods are intentionally `Raw` / `Json`-suffixed and
return caller-owned `JsonDocument` / `HttpResponseMessage`. Dispose them when
finished. Then file an issue if a stable STAC extension is worth promoting to
a typed property.

### Records search returns nothing although the catalog is populated

Records searches default to the `default` collection. Pass the explicit
collection name your server exposes via
`IHonuaOgcRecordsClient.SearchAsync(collection, query, ct)`, or list collections
first with `ListCollectionsAsync(ct)` to confirm the catalog IDs.

### Scene `ResolveAsync` returns endpoints your renderer cannot reach

`IHonuaSceneClient.ResolveAsync` returns the server's render endpoint
recommendation. The SDK does *not* validate the endpoint is reachable from the
calling host. In browsers, you may still need to (a) terminate TLS at a
gateway your scene server allows-lists, or (b) use the offline scene package
contract (`Honua.Sdk.Scenes` + offline contracts in `Honua.Sdk.Abstractions`) to pre-stage
assets.

---

## Offline sync

### Pull plans pull "everything" on first run

Expected. The offline planner records a checkpoint after the first successful pull; subsequent
pulls send the checkpoint so the server returns only deltas. If you do not see this, confirm
your storage adapter persists the checkpoint between runs -- the planner is checkpoint-driven,
not timestamp-driven. See [docs/offline-sync-core.md](offline-sync-core.md).

### Conflict envelopes look empty

The planner only emits conflict envelopes when both sides modified the same row's tracked
fields since the last checkpoint. Pure-server or pure-client edits with no overlap are not
conflicts.

---

## Build and packaging

### `error NU1101: Unable to find package Honua.Sdk.*`

No Honua SDK package is on nuget.org yet -- every release, stable or
prerelease, currently ships to the authenticated GitHub Packages feed only, so
a bare `dotnet add package Honua.Sdk*` (or `dotnet tool install`) resolving
against nuget.org always fails with `NU1101`. Add the feed as documented in
[INSTALL.md](../INSTALL.md#install-from-github-packages-current-channel) and
install with `--source honua` (or `--add-source` for `dotnet tool install`).
If your `NuGet.config` uses `<packageSourceMapping>`, the `Honua.Sdk`,
`Honua.Sdk.*`, and `Geospatial.Grpc` patterns must map to that feed. Dry runs
are not published to either feed; their artifacts are attached to the
corresponding GitHub Actions run.

### `401 Unauthorized` / `403 Forbidden` from `nuget.pkg.github.com`

The GitHub Packages NuGet endpoint requires authentication even for public
packages, and it only accepts a **classic** personal access token with the
`read:packages` scope -- fine-grained tokens are rejected. A `401` usually
means no credentials reached the feed (source added without
`--username`/`--password`, or credentials stored under a different source URL);
a `403` usually means the token is fine-grained, expired, or missing
`read:packages`. Re-run the `dotnet nuget add source` / `update source` step in
[INSTALL.md](../INSTALL.md#install-from-github-packages-current-channel) with a
classic PAT.

### Build fails with `TreatWarningsAsErrors`

The SDK ships with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in
`Directory.Build.props`. If your downstream project enables analyzers we don't, you may see new
warnings against generated/imported code. The clean fix is to suppress specific analyzer IDs in
your own `.editorconfig` rather than relaxing the SDK build.

---

## Reporting an issue

When opening an issue, include:

1. SDK package name(s) and version(s) (`dotnet list package` output is fine).
2. .NET runtime version (`dotnet --info`).
3. Honua server version (`adminClient.GetCapabilitiesAsync()` JSON dump if accessible).
4. The full exception type, message, and stack trace -- not just the message.
5. A minimal `Program.cs` (or `dotnet new console` snippet) that reproduces the issue.
