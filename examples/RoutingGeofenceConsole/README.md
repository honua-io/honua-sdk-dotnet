# Routing Geofence Console

Runnable demo for route solving and host-neutral geofence evaluation. The
default mode uses a deterministic routing fixture so the sample can run without
a configured NAServer. Geofence evaluation always uses real
`HonuaGeofenceEvaluator` logic over local NTS geometry.

## Run With Simulated Routing

```bash
dotnet run --project examples/RoutingGeofenceConsole/RoutingGeofenceConsole.csproj
```

## Run Against Honua Server NAServer

```bash
export HONUA_ROUTE_MODE=server
export HONUA_ROUTE_SERVER_URL=https://your-honua.example
export HONUA_ROUTE_API_KEY=
export HONUA_ROUTE_BEARER_TOKEN=
export HONUA_ROUTE_SERVICE_ID=Routing
export HONUA_ROUTE_ROUTE_LAYER=Route

dotnet run --project examples/RoutingGeofenceConsole/RoutingGeofenceConsole.csproj
```

Credentials are optional for unauthenticated local targets. If
`HONUA_ROUTE_API_KEY` or `HONUA_ROUTE_BEARER_TOKEN` is set, use HTTPS except
for loopback HTTP during local development.

## Expected Output Shape

```text
Mode: simulated
Routing provider: simulated-geoservices-naserver

Route:
  Honolulu Harbor to Dispatch Yard distance=4200m time=9.5m steps=2
  1. Leave Honolulu Harbor (600m)
  2. Arrive at Dispatch Yard (3600m)

Geofence:
  12:00:00 truck-7 Proximity Approached distance=4m
  12:00:10 truck-7 Inside Entered distance=0m
  12:00:20 truck-7 Proximity Exited distance=4m
  12:00:30 truck-7 Outside Departed distance=15m
```

## Required Capabilities

- Simulated mode: `Honua.Sdk.Geometry` and the provider-neutral routing
  contract.
- Server mode: GeoServices-compatible NAServer route layer with directions
  support.

## Validation

- `dotnet build examples/RoutingGeofenceConsole/RoutingGeofenceConsole.csproj`
- `dotnet run --project examples/RoutingGeofenceConsole/RoutingGeofenceConsole.csproj`
- `dotnet test tests/DemoSuite.Tests/DemoSuite.Tests.csproj`
