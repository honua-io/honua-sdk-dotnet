# Admin Bootstrap Console

Runnable console sample that bootstraps a PostGIS table through `Honua.Sdk.Admin` and then verifies the published layer through `Honua.Sdk.Grpc`.

## What It Does

The sample performs one end-to-end operator flow:

1. Calls `CheckCompatibilityAsync` before any other remote call.
2. Tests the configured database connection draft.
3. Reuses or creates the named admin connection.
4. Discovers the target table and reuses or publishes the target layer.
5. Enables the layer and union-adds the `Grpc` protocol when needed.
6. Runs a bounded `QueryFeaturesAsync` call against the published layer.

## Contract Notes

- `CheckCompatibilityAsync` is a hard gate. The sample stops before any admin
  mutation unless the server reports version `0.1.0` or newer, release channel
  `preview` or newer, control-plane API major `1`, and base path
  `/api/v1/admin`.
- Set either `HONUA_BOOTSTRAP_DB_PASSWORD` or
  `HONUA_BOOTSTRAP_DB_SECRET_REFERENCE`. When using
  `HONUA_BOOTSTRAP_DB_SECRET_REFERENCE`, you must also set
  `HONUA_BOOTSTRAP_DB_SECRET_TYPE`, and you must clear any password override
  first by setting `HONUA_BOOTSTRAP_DB_PASSWORD=` or removing
  `HonuaBootstrap:DbPassword` from `appsettings.json`.
- The SDK only sends `HONUA_BOOTSTRAP_API_KEY` and
  `HONUA_BOOTSTRAP_BEARER_TOKEN` over HTTPS, except loopback or `localhost`
  HTTP for local development.
- Re-runs are safe by default: same-name connections are reused only when host,
  port, database, username, and SSL settings match; same-name layers are reused
  only when service and source table match.
- Publish requires discovery metadata for a geometry column, geometry type,
  SRID, and exactly one primary key column. Tables without a primary key or
  with composite primary keys fail before publish.
- Verification is intentionally bounded. It calls `QueryFeaturesAsync` with
  `Where = "1=1"`, `ReturnGeometry = false`, `ResultRecordCount = 3`,
  `OrderBy = primary key`, and `OutFields = primary key + first two
  non-geometry columns returned by table discovery`.
- A zero-row verification result is still success. The sample reports that the
  layer is queryable but currently has no rows.
- With the included `sdk-demo-table.sql`, the verification fields resolve to
  `id`, `name`, and `category` because discovery preserves non-geometry column
  order.
- Exit codes are stable for automation: `0` success, `1` unexpected,
  `2` configuration, `3` compatibility, `4` admin, `5` verification,
  `130` cancelled.

## Prerequisites

- .NET 10 SDK
- `psql`
- A local `honua-server` checkout, referenced below as `HONUA_SERVER_ROOT`

Start the local server stack from the server repo:

From the SDK repo root, the default below points at a sibling
`../honua-server` checkout. Otherwise, set `HONUA_SERVER_ROOT` explicitly.

```bash
export HONUA_SERVER_ROOT="${HONUA_SERVER_ROOT:-../honua-server}"
cd "$HONUA_SERVER_ROOT"
docker compose up -d
```

That path starts Honua on `http://localhost:8080` and PostGIS on `localhost:5432`, while the server reaches the database internally at host `postgres`.

## Seed Demo Data

From the SDK repo root, load the deterministic sample table into the compose
PostGIS instance:

```bash
psql postgresql://honua_user:honua_password@localhost:5432/honua_dev -f examples/AdminBootstrapConsole/sql/sdk-demo-table.sql
```

## Configuration

`appsettings.json` already carries the local Docker Compose target defaults:

- server URL: `http://localhost:8080`
- connection name: `sdk-demo-postgres`
- database host for Honua server: `postgres`
- database: `honua_dev`
- database user: `honua_user`
- service: `sdk_demo`
- table/layer: `public.sdk_demo_points`

If you do not set any credential overrides, the sample falls back to the local
compose password `honua_password`.

Environment variables override `appsettings.json` when you need a different
target or want to make the local password path explicit:

```bash
export HONUA_BOOTSTRAP_SERVER_URL=http://localhost:8080
export HONUA_BOOTSTRAP_API_KEY=
export HONUA_BOOTSTRAP_BEARER_TOKEN=
export HONUA_BOOTSTRAP_CONNECTION_NAME=sdk-demo-postgres
export HONUA_BOOTSTRAP_DB_HOST=postgres
export HONUA_BOOTSTRAP_DB_PORT=5432
export HONUA_BOOTSTRAP_DB_NAME=honua_dev
export HONUA_BOOTSTRAP_DB_USER=honua_user
export HONUA_BOOTSTRAP_DB_PASSWORD=honua_password
export HONUA_BOOTSTRAP_DB_SECRET_REFERENCE=
export HONUA_BOOTSTRAP_DB_SECRET_TYPE=
export HONUA_BOOTSTRAP_DB_SSL_REQUIRED=false
export HONUA_BOOTSTRAP_DB_SSL_MODE=Prefer
export HONUA_BOOTSTRAP_SERVICE_NAME=sdk_demo
export HONUA_BOOTSTRAP_SCHEMA=public
export HONUA_BOOTSTRAP_TABLE=sdk_demo_points
export HONUA_BOOTSTRAP_LAYER_NAME=sdk_demo_points
```

To switch to secret-reference authentication, clear the password override and
set the secret fields instead:

```bash
export HONUA_BOOTSTRAP_DB_PASSWORD=
export HONUA_BOOTSTRAP_DB_SECRET_REFERENCE=projects/demo/secrets/postgres
export HONUA_BOOTSTRAP_DB_SECRET_TYPE=gcp-secret-manager
```

For non-local targets, prefer `HONUA_BOOTSTRAP_DB_SSL_REQUIRED=true` with `HONUA_BOOTSTRAP_DB_SSL_MODE=Require` or stricter.

## Run

From the repo root:

```bash
dotnet run --project examples/AdminBootstrapConsole/AdminBootstrapConsole.csproj
```

Expected output shape:

```text
=== Preflight ===
Server: http://localhost:8080 | Version: 0.x.y | Release channel: stable
Compatibility check passed.

=== Connection ===
Draft connection test passed for 'sdk-demo-postgres' targeting postgres:5432/honua_dev.
Created connection 'sdk-demo-postgres' (...)

=== Discovery ===
Found table 'public.sdk_demo_points' with geometry column 'geom', geometry type 'POINT', SRID 4326, estimated rows 3.

=== Publish ===
Published layer 'sdk_demo_points' (layerId=...) to service 'sdk_demo'.

=== Configure ===
Enabled layer 'sdk_demo_points' (layerId=...) on service 'sdk_demo'.
Enabled Grpc for service 'sdk_demo'.

=== Verify ===
Querying service 'sdk_demo', layerId=..., fields: id, name, category.
Bounded gRPC query returned 3 row(s).
  [1] id=1, name=Honolulu Harbor, category=port

=== Summary ===
Connection: sdk-demo-postgres (created)
Layer: sdk_demo_points on service 'sdk_demo' (published)
Protocols: FeatureServer, Grpc
Verification: query succeeded with 3 row(s).
```

The exact field list comes from discovery metadata. If you point the sample at a
different table schema, the verification `OutFields` and printed attributes will
change to match that table's primary key and first two non-geometry columns.

If the target layer is empty, the verify step prints a success message saying
the layer is queryable but currently has no rows instead of listing features.

On reruns, the connection and layer summary may report `reused` instead of
`created` / `published`, and the protocol list may include other protocols that
were already enabled on the target service.

If the sample reports an existing connection or layer that points somewhere
else, change the configured name instead of overwriting shared resources.
