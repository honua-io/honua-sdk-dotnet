# Architecture overview

A one-page map of how the Honua .NET SDK packages relate. Use this when
you're choosing which package(s) to take a dependency on, or when you need
to reason about how data flows through the SDK.

## The layers

`Honua.Sdk` is a meta / umbrella package that fans out across the 12
sub-packages below. Installing it brings in every other `Honua.Sdk.*` package
and exposes a single `AddHonua(o => o.BaseAddress = ...)` DI extension. The
per-package `AddHonua*` extensions remain available unchanged.

```
                          your application code
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Honua.Sdk  (meta / umbrella; fans out across the 12 sub-packages)  │
└─────────────────────────────────────────────────────────────────────┘
                                   │
                                   ▼
┌─────────────────────────────────────────────────────────────────────┐
│                  Honua.Sdk.Abstractions                             │
│ Provider-neutral interfaces:                                        │
│   IHonuaFeatureQueryClient  IHonuaFeatureEditClient                 │
│   IHonuaFeatureAttachmentClient  IHonuaFeatureStreamClient          │
│ Shared types: SourceDescriptor, FeatureQueryRequest, exceptions     │
│ Offline sync contracts: manifests, sync state, checkpoints,         │
│   conflicts, storage (under `Honua.Sdk.Offline.Abstractions.*`)     │
└─────────────────────────────────────────────────────────────────────┘
        ▲                  ▲                  ▲                  ▲
        │                  │                  │                  │
┌───────┴─────┐ ┌──────────┴─────────┐ ┌──────┴────┐ ┌───────────┴────┐
│   Grpc      │ │ OgcFeatures        │ │GeoServices│ │  Catalogs      │
│             │ │ (incl. WFS 2.0)    │ │           │ │ (STAC + Records)│
│ Native gRPC │ │ OGC API Features + │ │FeatureSrvr│ │ Catalog        │
│ FeatureSvc  │ │ WFS read/query     │ │ +Routing  │ │ search         │
│ +ProcessSvc │ │                    │ │           │ │                │
└─────────────┘ └────────────────────┘ └───────────┘ └────────────────┘
        ▲                                                       ▲
        │           (other clients use these for queries)       │
┌───────┴─────────────────────────────────────────────────────┴────┐
│  Honua.Sdk.Admin     (REST control-plane + Catalog + Geocoding)   │
│  Honua.Sdk.Processes (OGC API Processes REST + job models)        │
│  Honua.Sdk.Spec      (validate / plan / apply stream)             │
│  Honua.Sdk.Scenes    (scene metadata, render endpoints)           │
└───────────────────────────────────────────────────────────────────┘

           ┌──────────────────────────────┬──────────────────────────┐
           │                              │                          │
           ▼                              ▼                          ▼
┌─────────────────────┐    ┌─────────────────────────┐  ┌────────────────────┐
│ Honua.Sdk.Geometry  │    │ Honua.Sdk.Offline       │  │ Honua.Sdk.Field    │
│ NTS / ProjNet:      │    │ Push/pull planner over  │  │ Form / validation  │
│ planar, CRS,        │    │ shared query/edit/stream│  │ / workflow         │
│ geofence, indices   │    │ (contracts live in      │  │ contracts          │
│                     │    │ Honua.Sdk.Abstractions) │  │                    │
└─────────────────────┘    └─────────────────────────┘  └────────────────────┘
```

## Picking the right package

| You want to … | Take a dependency on |
|---|---|
| Run typed gRPC queries / edits against a Honua server | `Honua.Sdk.Grpc` |
| Read OGC API Features / WFS / FeatureServer / STAC / OGC API Records | `Honua.Sdk.OgcFeatures` (includes WFS 2.0) / `Honua.Sdk.GeoServices` / `Honua.Sdk.Catalogs` (STAC + OGC API Records) |
| Manage services, layers, connections, styles, metadata, roles, users, alerts, observability, feature-event replay, and streaming subscriber operations | `Honua.Sdk.Admin` |
| Work with OGC API Processes jobs over browser-safe REST | `Honua.Sdk.Processes` |
| Use native ProcessService job lifecycle calls | `Honua.Sdk.Grpc` (`IHonuaProcessGrpcClient`) |
| Forward / reverse / autocomplete geocode | `Honua.Sdk.Admin` (`IHonuaGeocodingClient`) |
| Validate / plan / apply spec workspaces | `Honua.Sdk.Spec` |
| Discover or resolve scenes / offline scene packages | `Honua.Sdk.Scenes` |
| Perform CRS transforms, planar predicates, geofence evaluation | `Honua.Sdk.Geometry` |
| Build offline sync (push/pull, conflicts, manifests) | `Honua.Sdk.Offline` (offline contracts ship in `Honua.Sdk.Abstractions`) |
| Define / validate field forms, run record workflows | `Honua.Sdk.Field` |
| Write a library that's agnostic to transport | `Honua.Sdk.Abstractions` only |
| Model Console shells, route guards, environment profiles, and mTLS state | `Honua.Sdk.Abstractions` |

## How clients compose

- Feature protocol clients also implement the shared
  `IHonuaFeatureQueryClient` / `IHonuaFeatureEditClient` /
  `IHonuaFeatureAttachmentClient`. The same `AddHonua*` extension that
  registers the native interface also registers the abstractions, so
  consumer code can ask for either.
- `HonuaSource` (in `Honua.Sdk.Abstractions`) wraps a `SourceDescriptor` plus
  a query / edit client and exposes a `Protocol<T>()` escape hatch to recover
  the native interface when you need protocol-specific calls.
- Exceptions all derive from `Honua.Sdk.Abstractions.HonuaException` —
  including configuration failures (`HonuaConfigurationException`) and each
  protocol-specific failure type. One `catch (HonuaException)` covers the SDK.

## Authentication and transport

All clients share the same `IHonuaAuthenticationOptions` surface
(`ApiKey` / `BearerToken` plus refreshable provider delegates and
`IHonuaAccessTokenProvider`). HTTPS is required for credential transport,
with a loopback exception for local development.

See [authentication.md](authentication.md) for the full storage / refresh /
failure story and [troubleshooting.md](troubleshooting.md) for common
failure modes.
