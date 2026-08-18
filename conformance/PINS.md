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
| Conformance fixtures | `0.1.0-alpha.2` | `conformance/FIXTURE_VERSION` |
| `Geospatial.Grpc` package | `0.1.0-alpha.2` | `Directory.Packages.props` (central `PackageVersion`) |

These two **must stay equal** — a fixture set maps 1:1 to a `geospatial.v1`
schema release, and the SDK's generated gRPC client is built against the same
schema version. `conformance/check-version.sh` enforces the equality and the
conformance CI job runs it before anything else.

The fixtures are pulled at build time from the geospatial-grpc `v0.1.0-alpha.2`
GitHub Release:

```bash
conformance/fetch-fixtures.sh --version 0.1.0-alpha.2 --dest conformance/.fixtures
```

The alpha.2 pin is intentional: when selected, it was the newest fixture
version with a matching `Geospatial.Grpc` package published to GitHub Packages.
Alpha.3 fixture assets existed without an alpha.3 NuGet package, so promoting
only the fixtures would have broken the required fixture/package equality.

As of 2026-08-17 the newest fixture release is `0.2.0-alpha.1` (published
2026-08-17) and it *does* have a matching published package, so the
fixture/package equality gate is no longer what blocks promotion. The remaining
blocker is the server side: `honua-server` trunk still consumes
`Geospatial.Grpc 0.1.0-alpha.2`, so the pinned nightly image speaks the alpha.2
contract. Promoting the SDK alone would put the client ahead of every server
build it is certified against. Promotion therefore stays coordinated — see the
promotion section below.

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

## Moving canary and promotion

The weekly scheduled run of `.github/workflows/conformance.yml` leaves these
deterministic pins untouched and tests the newest published fixture asset plus
the current official `honua-server:nightly` image. Its evidence artifact records
both the values in this file and the moving candidates. A drift verdict creates
or updates a single drift issue with schema and live-protocol outcomes.

### Where the drift signal goes (#300)

The moving canary is **not** part of any commit's build verdict. It runs
*moving* fixtures against the *pinned* `Geospatial.Grpc` package and the pinned
server image, so it is designed to be able to go red for exactly the reason it
exists — a true-positive early warning is the canary working correctly.

A scheduled workflow run always has `trunk` HEAD as its head SHA, so GitHub
attaches its job check-runs to that commit; that cannot be turned off. What the
canary no longer does is let those check-runs carry a **failure** conclusion:
every step of the scheduled `conformance` job is non-fatal, and the
`Enforce conformance outcomes` step publishes a `verdict` output (`clean` /
`drift`) and exits 0. Pull requests and trunk pushes are unchanged — there the
same step still fails the job on any non-success surface, because those runs
*are* the commit's build verdict.

The drift signal therefore travels entirely through:

- the **`Moving compatibility canary drift detected`** issue, created/updated by
  the `Moving Canary Evidence and Alert` job — now keyed off the `verdict`
  output rather than the conformance job's result, so making the job green does
  not silence the alert;
- the **`compatibility-canary-evidence`** artifact (90-day retention), which
  records the verdict, the failing surfaces, and both pin tables;
- the run's step summary.

The alert job itself is still allowed to fail. That is deliberate: a broken
alert path is a real, actionable red, not a correctly-firing canary.

Consumers such as `honua-release`'s `gate_build_test`, which read the
check-runs attached to a pinned commit, therefore see the canary only as
build/analysis-shaped signal and are never reddened by a compatibility warning
about pins that the commit does not control.

Fixture publication and NuGet publication can move independently. In
particular, a newer fixture release such as alpha.3 may be available while the
matching `Geospatial.Grpc` package is not. The canary records that as a distinct
distribution-drift signal and does not treat the candidate as promotable.

Promote only a successful candidate with a matching published package: review
the evidence, update `conformance/FIXTURE_VERSION`, the `Geospatial.Grpc`
`PackageVersion` in `Directory.Packages.props` (the two are equality-checked by
`conformance/check-version.sh`), the server digest/ref in this file and
`.github/workflows/conformance.yml`, and send those changes through the normal
pull-request gate. Promotion is never automatic.

### Harness failures are not drift

The canary runs harness surfaces (fixture download, conformance build, server
seed fetch/apply, server start) before the two contract surfaces (schema, live).
If a harness surface fails, schema and live never run and the canary has no
compatibility verdict — the drift alert says so explicitly and lists the harness
outcomes, rather than presenting skipped contract surfaces as drift (#272). Fix
the harness failure and re-run before reading the pin table as a signal.

A harness failure can come from outside this repo entirely: a new NuGet
advisory against a transitive dependency fails the warnings-as-errors restore
and takes the whole gate down. That is what happened in 2026-08 (GHSA-q939-rpr3-3284
against `SSH.NET <= 2025.1.0`, reached through `Testcontainers`), and it is why
the alert now separates the two categories.
