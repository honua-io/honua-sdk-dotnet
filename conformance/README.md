# Conformance gate

This directory drives the **contract conformance gate** for the .NET SDK: it
runs the **shared `geospatial-grpc` conformance fixtures** against a **pinned
`honua-server:nightly`** image and fails CI on contract drift. It is the
`honua-sdk-dotnet` child (#181) of the Compatibility Train epic
(`geospatial-grpc#18`).

The motivating regression is `honua-server#1238`: a server-side data-projection
change altered the on-the-wire shape of canonical FeatureServer / OGC responses
and nothing systematically verified that real SDK clients still round-tripped
those payloads. This gate is the mechanism that catches that class of drift at
the SDK boundary.

## How it fits together

- **Fixtures are owned by `geospatial-grpc`**, published as a versioned release
  asset, and consumed here via `fetch-fixtures.sh` (the mechanism delivered in
  `geospatial-grpc#19`). They are **never copied or forked into this repo**.
- A fixture set maps **1:1 to a `geospatial.v1` schema release**, and the SDK's
  generated gRPC client is built against the same `Geospatial.Grpc` package
  version. `check-version.sh` enforces that equality.
- Pins (fixture version + server image digest + known gaps) live in
  [`PINS.md`](PINS.md).

## Files

| File | Purpose |
|------|---------|
| `fetch-fixtures.sh` | Pull + verify a pinned fixture set from the geospatial-grpc release. |
| `check-version.sh` | Assert `FIXTURE_VERSION` equals the SDK's `Geospatial.Grpc` pin. |
| `FIXTURE_VERSION` | The pinned fixture/schema version (currently `0.1.0-alpha.2`). |
| `PINS.md` | Recorded pins: fixtures, server image digest, known-expected-failing gaps. |

## Two-tier conformance suite

The suite lives in `tests/Honua.Sdk.Conformance.Tests/`:

1. **Schema conformance** (`SchemaConformanceTests`) — round-trips every
   canonical FeatureService fixture through the SDK's pinned generated gRPC
   client (`Geospatial.V1.*`) and its public converter
   (`HonuaGrpcProtoConverter`). Strict JSON parsing means a renamed/removed
   field, a changed type, or a dropped enum value fails the parse or loses data
   on conversion. **No server required**, so it runs in the normal CI matrix and
   as the schema half of the gate. Set `HONUA_CONFORMANCE_FIXTURES_DIR` to the
   extracted bundle to run it locally.

2. **Live conformance** (`LiveConformanceTests`) — drives the canonical fixture
   workflows through the real SDK protocol clients (gRPC, GeoServices
   FeatureServer, WFS, OGC API Features) against a pinned, Testcontainers-managed
   `honua-server:nightly`, asserting responses round-trip without drift. This is
   the tier that catches a server-side projection change like #1238. Activated by
   `HONUA_PROTOCOL_INTEGRATION=true` plus a server target, exactly like the
   protocol-integration suite.

### Known-expected-failing server gaps

Already-tracked nightly server gaps are marked **known-expected-failing**: the
affected live assertions skip with an explicit `honua-server#NNNN` reference
(never silently, never via blanket `continue-on-error`), so the job stays green
and the harness is in place, while any **new/untracked** drift still fails the
gate. When a gap lands server-side, remove its `knownGap` argument and the
assertion becomes a required check. See
`tests/Honua.Sdk.Conformance.Tests/ConformanceKnownGaps.cs` and `PINS.md`.

## Run it locally

```bash
# 1. Pull the pinned fixtures.
conformance/fetch-fixtures.sh --version 0.1.0-alpha.2 --dest conformance/.fixtures

# 2. Schema conformance (no server).
export HONUA_CONFORMANCE_FIXTURES_DIR="$PWD/conformance/.fixtures"
dotnet test tests/Honua.Sdk.Conformance.Tests/Honua.Sdk.Conformance.Tests.csproj \
  --configuration Release --filter "FullyQualifiedName~SchemaConformanceTests"

# 3. Live conformance against a running honua-server (see docs/protocol-integration-tests.md).
export HONUA_PROTOCOL_INTEGRATION=true
export HONUA_PROTOCOL_EXTERNAL_BASE_URL=http://localhost:8080
export HONUA_PROTOCOL_GRPC_BASE_URL=http://localhost:8081
dotnet test tests/Honua.Sdk.Conformance.Tests/Honua.Sdk.Conformance.Tests.csproj \
  --configuration Release --filter "FullyQualifiedName~LiveConformanceTests"
```

To verify fixture payloads with the bundled buf harness (no repo checkout),
supply a schema descriptor built from the matching geospatial-grpc tag:

```bash
# in a geospatial-grpc checkout at the matching tag:
buf build -o image.binpb
# then:
CONFORMANCE_IMAGE=image.binpb conformance/.fixtures/run.sh
```

## Bumping the pins

- **Fixtures / schema:** update `conformance/FIXTURE_VERSION` and the
  `Geospatial.Grpc` `<GeospatialGrpcVersion>` in `Directory.Build.props`
  together; `check-version.sh` rejects a mismatch.
- **Server image:** update the digest in `PINS.md` and
  `.github/workflows/conformance.yml`.

## Moving compatibility canary

Pull requests and trunk pushes always use the committed pins above. A weekly
scheduled lane additionally resolves the newest `geospatial-grpc` release that
contains conformance fixture assets and the current official
`honua-server:nightly` digest/revision. It runs the same schema and live suites
without changing committed pins. The workflow supplies the resolved candidate
through `HONUA_CONFORMANCE_EXPECTED_FIXTURE_VERSION`; local runs omit that
override and continue to require the committed pin.

The lane uploads a 90-day `compatibility-canary-evidence-*` artifact containing
the committed and candidate pins plus per-surface outcomes. Test drift creates
or updates one GitHub issue with the failing schema/live surface. Fixture and
package publication are checked independently: for example, alpha.3 fixtures
may exist before a matching `Geospatial.Grpc` package is published. That is
reported as distribution drift even if the older SDK can parse the fixtures,
because the fixture pin cannot be promoted safely yet.

Pin promotion is always explicit. After a successful canary with a matching
published package, use its evidence artifact to update the fixture/package and
server pins in a reviewed pull request, then let the deterministic gate certify
the promoted values. The canary never writes pin changes itself.
