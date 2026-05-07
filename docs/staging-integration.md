# Staging Integration Guide

The staging suite for this repository lives in
`tests/Honua.Sdk.IntegrationTests` and runs through
`.github/workflows/staging-integration.yml`. The default lane is intentionally
read-only and does not execute the mutating bootstrap sample against shared
staging. A FeatureServer edit round-trip can be enabled only against a
dedicated disposable edit fixture.

For local throwaway server coverage, use the Testcontainers suite documented in
[`protocol-integration-tests.md`](protocol-integration-tests.md). Staging
validates a deployed environment; protocol integration validates SDK clients
against a deterministic local server fixture.

## What The Suite Covers

- Admin compatibility and service settings through `IHonuaAdminClient`
- A bounded gRPC `QueryFeaturesAsync()` call with `ReturnGeometry = false`
- WFS `GetCapabilitiesAsync()` and bounded `GetFeaturesAsync()`
- FeatureServer metadata plus a bounded `QueryAsync()`
- Source-facade queries across gRPC, FeatureServer, WFS, and OGC API Features
  using `IHonuaSource.QueryAsync()`
- Optional FeatureServer add/update/delete round-trip against a dedicated edit
  fixture
- OGC API Features collections, bounded items, and a single item lookup

Each request uses a small row limit and deterministic fixture identifiers so the
lane stays low-overhead and does not mutate shared staging state.

## Workflow Triggers

- `pull_request` is intentionally out of scope. External staging checks do not
  run on the PR-blocking path.
- `.github/workflows/staging-integration.yml` runs on `schedule` and
  `workflow_dispatch`.
- `.github/workflows/publish-dotnet-sdk.yml` reuses the same staging workflow on
  the release path before package publish.

## Required Configuration

Configure the GitHub `staging` environment with these values.

Repository or environment variables:

- `HONUA_STAGING_BASE_URL`
- `HONUA_STAGING_SERVICE_NAME`
- `HONUA_STAGING_LAYER_ID`
- `HONUA_STAGING_WFS_TYPENAME`
- `HONUA_STAGING_OGC_COLLECTION_ID`

Environment secrets:

- `HONUA_STAGING_API_KEY` or `HONUA_STAGING_BEARER_TOKEN`

Workflow-managed values:

- `HONUA_STAGING_EVIDENCE_PATH`
- `HONUA_STAGING_RUN_ID`

Optional evidence metadata:

- `HONUA_STAGING_SERVER_COMMIT`
- `HONUA_STAGING_SERVER_IMAGE`
- `HONUA_STAGING_SEED_PROFILE`

Optional FeatureServer edit fixture values:

- `HONUA_STAGING_ENABLE_FEATURESERVER_EDITS=true`
- `HONUA_STAGING_FEATURESERVER_EDIT_ADD_ATTRIBUTES_JSON`
- `HONUA_STAGING_FEATURESERVER_EDIT_UPDATE_ATTRIBUTES_JSON`
- `HONUA_STAGING_FEATURESERVER_EDIT_GEOMETRY_JSON`

Local execution uses the same environment variables. Example:

```bash
export HONUA_STAGING_BASE_URL=https://staging.example.honua.test
export HONUA_STAGING_API_KEY=replace-me
export HONUA_STAGING_SERVICE_NAME=sdk_demo
export HONUA_STAGING_LAYER_ID=0
export HONUA_STAGING_WFS_TYPENAME=public:sdk_demo_points
export HONUA_STAGING_OGC_COLLECTION_ID=sdk_demo_points

# Optional: enable only against a disposable edit fixture.
export HONUA_STAGING_ENABLE_FEATURESERVER_EDITS=true
export HONUA_STAGING_FEATURESERVER_EDIT_ADD_ATTRIBUTES_JSON='{"name":"sdk-add","status":"new"}'
export HONUA_STAGING_FEATURESERVER_EDIT_UPDATE_ATTRIBUTES_JSON='{"name":"sdk-update","status":"updated"}'

dotnet test tests/Honua.Sdk.IntegrationTests/Honua.Sdk.IntegrationTests.csproj --configuration Release
```

## Evidence And Artifacts

Successful workflow runs emit all of the following:

- TRX results under the `...-test-results` artifact
- JSON evidence at `artifacts/evidence/ci-report.json`, uploaded as the
  `...-evidence` artifact
- A concise `GITHUB_STEP_SUMMARY` section with the endpoint, fixture
  identifiers, and per-check pass/fail details

The JSON evidence includes:

- base URL
- SDK package versions
- server commit and image when configured
- seed profile when configured
- whether the FeatureServer edit check ran
- service/layer/type/collection identifiers
- protocol surfaces under test
- per-check status, duration, and detail string; failed checks include the SDK
  method, request path or gRPC method, status when available, and a short
  response body summary
- a total passed / failed / not-run summary

## Troubleshooting

- Missing environment configuration fails the workflow before `dotnet test`
  starts. Fix the missing `HONUA_STAGING_*` variable or auth secret in the
  GitHub `staging` environment first.
- `HonuaAdminApiException` usually means auth, routing, or status-code failures
  on the admin REST surface. Re-check the base URL, credentials, and that the
  target service exists.
- `HonuaAdminOperationException` indicates a read contract or compatibility
  issue surfaced by the admin client, including unsupported server baselines.
- `HonuaGrpcException` on the bounded query usually means the configured
  service/layer pair is wrong or the gRPC endpoint is unavailable.
- `HonuaWfsException` usually means WFS is disabled, the configured type name is
  wrong, or the server is returning a WFS exception report.
- `HonuaFeatureServerException` and `HonuaOgcFeaturesException` usually mean the
  configured FeatureServer or OGC API fixture identifiers are wrong, or those
  protocol surfaces are disabled on staging.
- xUnit assertion failures with messages such as "expected 1 to 3 rows" usually
  mean the shared staging fixture is empty, changed names, or no longer exposes
  the expected protocols.

## Bounded Cross-Repo Follow-Ons

This SDK ticket stays single-repo. If staging prerequisites are missing, track
them as separate child tickets in `honua-server` or the environment-owning repo
instead of expanding this repository's scope.

- Provide a stable staging certification fixture with known non-empty data,
  protocol availability, and durable identifiers for service, layer, WFS type,
  and OGC collection.
- Wire the `staging` GitHub environment with the required variables and auth
  secret if that configuration does not already exist.
