# Conformance gate pins

The conformance gate (`.github/workflows/conformance.yml`,
`tests/Honua.Sdk.Conformance.Tests/`) runs the **shared `geospatial-grpc`
conformance fixtures** against a **pinned `honua-server:nightly`** image and
fails CI on contract drift. This file records the exact pins so the gate is
deterministic; bumping a pin is a reviewable, single-file change.

This implements the `honua-sdk-dotnet` child (#181) of the Compatibility Train
epic (geospatial-grpc#18). Fixtures are consumed via the mechanism delivered in
geospatial-grpc#19 (`conformance/fetch-fixtures.sh`), never copied/forked here.

## Pinned fixture version

| Pin | Value | Source |
|-----|-------|--------|
| Conformance fixtures | `0.1.0-alpha.1` | `conformance/FIXTURE_VERSION` |
| `Geospatial.Grpc` package | `0.1.0-alpha.1` | `Directory.Build.props` (`<GeospatialGrpcVersion>`) |

These two **must stay equal** — a fixture set maps 1:1 to a `geospatial.v1`
schema release, and the SDK's generated gRPC client is built against the same
schema version. `conformance/check-version.sh` enforces the equality and the
conformance CI job runs it before anything else.

The fixtures are pulled at build time from the geospatial-grpc `v0.1.0-alpha.1`
GitHub Release:

```bash
conformance/fetch-fixtures.sh --version 0.1.0-alpha.1 --dest conformance/.fixtures
```

## Pinned server image

The live tier boots this exact `honua-server` image via Testcontainers:

| Pin | Value |
|-----|-------|
| Tag | `ghcr.io/honua-io/honua-server:nightly` |
| Digest | `sha256:1f92ffb3e404bdd0818d55f0a1fc12a802a9fa1c4461c71dfdc4318e66913865` |
| Build | `nightly-86042bd` (dated `20260530`, JIT/amd64+arm64 multi-arch index) |

The digest is what is actually pinned and recorded; the `:nightly` tag is a
moving pointer and is shown only for provenance. CI pulls
`ghcr.io/honua-io/honua-server@sha256:1f92…913865`. Bump the digest here when
intentionally moving the conformance target to a newer nightly, and record the
new build/date alongside it.

## Known-expected-failing server gaps

These nightly server gaps are already tracked. The live conformance tier marks
the affected assertions **xfail** (skip with an explicit issue reference) so the
job stays green and the harness is in place, while any *new/untracked* drift
still fails. When a gap lands server-side, flip its xfail to a required check.

| Server issue | Surface | Conformance impact |
|--------------|---------|--------------------|
| honua-server#1238 | FeatureServer / OGC API Features | JSONB attribute projection changes response shape |
| honua-server#1166 | Temporal | temporal field query/round-trip |
| honua-server#1167 | Replica | replica/offline sync surface |
| honua-server#1237 | Analysis | analysis list / estimate |

See `HONUA_CONFORMANCE_KNOWN_GAPS` in
`tests/Honua.Sdk.Conformance.Tests/ConformanceKnownGaps.cs`.
