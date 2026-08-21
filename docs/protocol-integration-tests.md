# Protocol Integration Tests

`tests/Honua.Sdk.ProtocolIntegration.Tests` is the SDK-side Testcontainers
integration suite for real Honua Server protocol coverage. It is skipped by
default and becomes active only when explicitly configured with a server image
or an external test server URL.

This suite complements, rather than replaces:

- package/unit tests that use deterministic mock HTTP/gRPC handlers;
- `tests/Honua.Sdk.IntegrationTests`, which validates deployed staging on a
  schedule or release path;
- server-side fixture tests in `honua-server`.

## Local Configuration

Run against a Testcontainers-managed Honua Server image:

```bash
export HONUA_PROTOCOL_INTEGRATION=true
export HONUA_PROTOCOL_SERVER_IMAGE=ghcr.io/honua-io/honua-server:test-fixture
export HONUA_PROTOCOL_SERVER_PORT=8080
export HONUA_PROTOCOL_SERVER_HEALTH_PATH=/health
export HONUA_PROTOCOL_API_KEY=replace-me
export HONUA_PROTOCOL_SEED_PROFILE=sdk-protocol

dotnet test tests/Honua.Sdk.ProtocolIntegration.Tests/Honua.Sdk.ProtocolIntegration.Tests.csproj --configuration Release
```

Run against an already-started server:

```bash
export HONUA_PROTOCOL_INTEGRATION=true
export HONUA_PROTOCOL_EXTERNAL_BASE_URL=http://localhost:5000
export HONUA_PROTOCOL_API_KEY=replace-me

dotnet test tests/Honua.Sdk.ProtocolIntegration.Tests/Honua.Sdk.ProtocolIntegration.Tests.csproj --configuration Release
```

Common fixture identifiers:

- `HONUA_PROTOCOL_SERVICE_NAME`, default `sdk_integration`
- `HONUA_PROTOCOL_LAYER_ID`, default `0`
- `HONUA_PROTOCOL_WFS_TYPENAME`, default `public:sdk_integration_points`
- `HONUA_PROTOCOL_OGC_COLLECTION_ID`, default `sdk_integration_points`
- `HONUA_PROTOCOL_SCENE_ID`, required for scene tests
- `HONUA_PROTOCOL_SPEC_ID`, required for Spec tests
- `HONUA_PROTOCOL_ROUTE_SERVICE_ID`, required with
  `HONUA_PROTOCOL_ROUTE_LAYER` for routing tests
- `HONUA_PROTOCOL_SERVICE_AREA_LAYER`, optional routing service-area coverage
- `HONUA_PROTOCOL_CLOSEST_FACILITY_LAYER`, optional closest-facility coverage
- `HONUA_PROTOCOL_GEOCODE_TEXT`, required with reverse geocode coordinates for
  geocoding tests
- `HONUA_PROTOCOL_REVERSE_GEOCODE_LATITUDE`
- `HONUA_PROTOCOL_REVERSE_GEOCODE_LONGITUDE`

Destructive/write tests require:

- `HONUA_PROTOCOL_DESTRUCTIVE=true`
- `HONUA_PROTOCOL_FEATURESERVER_EDIT_ADD_ATTRIBUTES_JSON`
- `HONUA_PROTOCOL_FEATURESERVER_EDIT_UPDATE_ATTRIBUTES_JSON`
- `HONUA_PROTOCOL_FEATURESERVER_EDIT_GEOMETRY_JSON`, optional

## CI Lane

Keep protocol integration CI opt-in until the Honua Server image and fixture
contract are stable enough to make it a required check. A workflow should be
`workflow_dispatch` only, accept either a server image or external URL, forward
the `HONUA_PROTOCOL_*` variables and secrets listed above, and run:

```bash
dotnet test tests/Honua.Sdk.ProtocolIntegration.Tests/Honua.Sdk.ProtocolIntegration.Tests.csproj \
  --configuration Release \
  /p:TreatWarningsAsErrors=true \
  /p:EnforceCodeStyleInBuild=true
```

With no image or URL, all protocol integration facts are skipped with a clear
reason.

## Coverage Matrix

| Client surface | Testcontainers checks |
|----------------|-----------------------|
| Admin | Compatibility, service settings, enabled protocol assertions |
| Catalog | Service listing and service lookup through `IHonuaCatalogClient` |
| Geocoding | Forward, reverse, suggest, and batch when geocoder fixture values are configured |
| Spec | Validate canonical spec JSON, plan, apply SSE stream |
| gRPC | Bounded `QueryFeaturesAsync`, geometry omitted |
| WFS | `GetCapabilities`, `DescribeFeatureType`, `GetFeatures`, hits/count |
| GeoServices FeatureServer | Service info, layer info, bounded query, count, IDs |
| FeatureServer edits | Add/update/delete round-trip in destructive lane |
| FeatureServer attachments | Planned destructive lane once fixture exposes attachment-enabled layer |
| OGC API Features | Collections, collection, queryables, bounded items, item by id |
| OGC edits | Planned destructive lane after fixture advertises writable OGC collection |
| Scenes | List, metadata get, resolve 3D Tiles-capable scene |
| Routing/NAServer | Metadata, directions, optional service area, optional closest facility |
| Realtime | Placeholder skipped until server realtime fixture exists |
| Source facade | Equivalent bounded query across gRPC, FeatureServer, WFS, and OGC |

## Server Fixture Contract

The eventual server seed should expose:

- one non-empty service/layer pair with point, line, polygon, object ID, string,
  numeric, temporal, and nullable fields;
- enabled gRPC, FeatureServer, WFS, and OGC API Features for the same data;
- known WFS type name and OGC collection id;
- a writable disposable layer for destructive edit tests;
- an attachment-enabled layer for attachment CRUD;
- a scene fixture with 3D Tiles endpoint metadata;
- a spec fixture that validates, plans, applies, streams progress, and supports
  cancellation;
- routing fixture metadata if NAServer coverage is expected locally;
- realtime stream fixture after server transport lands.

Until those server prerequisites are all available, the suite is intentionally
useful as a scaffold and opt-in smoke lane rather than a PR-blocking gate.

## Contract Conformance Gate

`tests/Honua.Sdk.Conformance.Tests` is a PR-blocking gate
(`.github/workflows/conformance.yml`) that runs the **shared `geospatial-grpc`
conformance fixtures** against a **pinned `honua-server:nightly`** image and
fails on contract drift. It is the `honua-sdk-dotnet` child (#181) of the
Compatibility Train epic (`geospatial-grpc#18`), and the SDK-side answer to
`honua-server#1238`. See [`conformance/README.md`](../conformance/README.md) and
[`conformance/PINS.md`](../conformance/PINS.md) for the full design and pins.

The fixtures are **consumed from `geospatial-grpc`**, never copied here: the CI
job runs `conformance/fetch-fixtures.sh --version <X.Y.Z>` to pull and verify the
pinned release asset (mechanism delivered in `geospatial-grpc#19`). The fixture
version equals the SDK's `Geospatial.Grpc` pin (enforced by
`conformance/check-version.sh`), so a fixture set maps 1:1 to a `geospatial.v1`
schema release.

The suite has two tiers:

- **Schema conformance** (`SchemaConformanceTests`): round-trips every canonical
  FeatureService fixture through the SDK's pinned generated gRPC client and its
  public converter. No server required; runs whenever
  `HONUA_CONFORMANCE_FIXTURES_DIR` points at an extracted bundle. Injected drift
  (renamed/removed field, type/enum change, projection shape change) turns it
  red.
- **Live conformance** (`LiveConformanceTests`): drives the canonical workflows
  through the real protocol clients (gRPC, FeatureServer, WFS, OGC API Features)
  against the pinned server (Testcontainers / external URL via the same
  `HONUA_PROTOCOL_*` variables as above). `HONUA_PROTOCOL_GRPC_BASE_URL` targets
  the h2c gRPC endpoint when it differs from the HTTP base.

### Known-expected-failing server gaps

Already-tracked nightly server gaps are marked known-expected-failing in the
suite — the affected live assertions skip with an explicit `honua-server#NNNN`
reference (never silently, never via blanket `continue-on-error`), so the job is
green and the harness is in place while any new/untracked drift still fails:
`#1166` (temporal), `#1167` (replica), and `#1237` (analysis list/estimate).
The former `#1238` FeatureServer/OGC JSONB projection gap is now a required,
typed live assertion. When another gap lands server-side, drop
its `knownGap` marker so the assertion becomes a required check.

### Recorded pins

- Fixtures: `0.2.0-alpha.1` (`conformance/FIXTURE_VERSION` = `Geospatial.Grpc`
  pin).
- Server image: `ghcr.io/honua-io/honua-server@sha256:d7a45c871bf318b4882ec8e1c32004803e6d0210246be30120751f05dee1a14d`
  (`rc-cert-e3ab87c-e3ab87c`, dated `20260821`).
