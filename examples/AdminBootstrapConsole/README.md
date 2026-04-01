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

## Prerequisites

- .NET 10 SDK
- `psql`
- A running local `honua-server` checkout at `/home/makani/honua-server`

Start the local server stack from the server repo:

```bash
cd /home/makani/honua-server
docker compose up -d
```

That path starts Honua on `http://localhost:8080` and PostGIS on `localhost:5432`, while the server reaches the database internally at host `postgres`.

## Seed Demo Data

Load the deterministic sample table into the compose PostGIS instance:

```bash
psql postgresql://honua_user:honua_password@localhost:5432/honua_dev -f /home/makani/worktrees/honua-sdk-dotnet/23/examples/AdminBootstrapConsole/sql/sdk-demo-table.sql
```

## Configuration

`appsettings.json` already carries local Docker Compose defaults:

- server URL: `http://localhost:8080`
- connection name: `sdk-demo-postgres`
- database host for Honua server: `postgres`
- database: `honua_dev`
- database user: `honua_user`
- database password: `honua_password`
- service: `sdk_demo`
- table/layer: `public.sdk_demo_points`

Environment variables override `appsettings.json` when you need a different target:

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
Querying service 'sdk_demo', layerId=..., fields: id, name, status.
Bounded gRPC query returned 3 row(s).

=== Summary ===
Connection: sdk-demo-postgres (created)
Layer: sdk_demo_points on service 'sdk_demo' (published)
Protocols: FeatureServer, Grpc
Verification: query succeeded with 3 row(s).
```

If the sample reports an existing connection or layer that points somewhere else, change the configured name instead of overwriting shared resources.
