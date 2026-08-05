# Honua.Sdk.Cli

Support-safe Honua command-line diagnostics, installed as the global .NET tool
`honua`. Its single command, `honua doctor`, produces a local, sanitized,
schema-pinned diagnostic bundle from a captured HTTP exchange and/or an
anonymous server capability probe, and can re-run a previously captured bundle
in bounded read-only replay mode. It validates every bundle against the pinned
support v1 schema and never uploads anything automatically.

Part of the [Honua .NET SDK](https://github.com/honua-io/honua-sdk-dotnet) — see the
repo README for the full package catalog, browser/WASM support, authentication, and
release policy.

## Install

Honua SDK packages are currently published to the authenticated GitHub Packages
feed only — nuget.org publishing is planned but not yet available. `dotnet tool
install` does not read a repository `NuGet.config`, so the feed must be passed
with `--add-source`, and the feed credentials (a GitHub **classic** PAT with the
`read:packages` scope) must already be configured for that source URL — for
example via `dotnet nuget add source` in your user-level NuGet config. Full
setup: [INSTALL.md](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md).

```bash
# One-time feed credential setup (user-level NuGet config):
dotnet nuget add source https://nuget.pkg.github.com/honua-io/index.json \
  --name honua --username YOUR_GITHUB_USERNAME --password YOUR_CLASSIC_PAT \
  --store-password-in-clear-text

# Install the tool (the feed must also be passed explicitly here):
dotnet tool install --global Honua.Sdk.Cli \
  --add-source https://nuget.pkg.github.com/honua-io/index.json
honua doctor --help
```

## Quick usage

Create a sanitized support bundle from a captured HTTP exchange:

```bash
honua doctor --exchange failure.json --classification customer-data \
  --redaction-acknowledged=true --share-with-support=false \
  --output diagnostic-bundle.json
```

Re-run a previously captured bundle against a server in bounded read-only
replay mode:

```bash
honua doctor --replay diagnostic-bundle.json \
  --base-url https://your-honua-server \
  --output replay.json --json
```

## Command reference

`honua doctor` is the only command. Capture mode requires `--classification`,
explicit `--redaction-acknowledged=<true|false>` and
`--share-with-support=<true|false>` consent flags, and at least one input:
`--exchange <capture.json>` and/or `--base-url <url>` (or the `HONUA_BASE_URL`
environment variable) for the anonymous capability probe. Replay mode
(`--replay <bundle.json>`) requires `--base-url`/`HONUA_BASE_URL` and cannot be
combined with the capture options.

| Option | Meaning |
|---|---|
| `--output <file.json>` | Required. Destination for the validated bundle or replay result. |
| `--exchange <capture.json>` | Captured HTTP exchange to sanitize into the bundle. |
| `--base-url <url>` | Server base URL for the capability probe / replay (or `HONUA_BASE_URL`). |
| `--classification <value>` | Required in capture mode. Data classification recorded in the bundle. |
| `--redaction-acknowledged=<bool>` | Required in capture mode. Explicit redaction consent. |
| `--share-with-support=<bool>` | Required in capture mode. Explicit sharing consent. |
| `--replay <bundle.json>` | Re-run a validated bundle in bounded read-only replay mode. |
| `--granted-by <name>` | Optional consent grantor recorded in the bundle. |
| `--bundle-id <id>` | Optional bundle identifier override. |
| `--preview-bytes <n>` | Optional sanitized body preview size cap. |
| `--timeout-ms <n>` | Optional network timeout (default 10000, max 30000). |
| `--json` | Emit the run summary as `honua.doctor-result.v1` JSON. |

Exit codes: `0` success, `1` safe failure (no diagnostic artifact written),
`2` usage / argument error.

## Documentation

- [Sanitized diagnostic bundles](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/diagnostic-bundles.md)
  — privacy boundary, capability probe, and read-only replay contract
- [Troubleshooting](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/docs/troubleshooting.md)
- [Install guide](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/INSTALL.md)

## License

[Apache 2.0](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/LICENSE)
