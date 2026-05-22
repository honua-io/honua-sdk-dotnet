# Honua .NET SDK Feature Map

This repository owns official .NET client libraries and shared host-neutral contracts.

## Current Capabilities

- Shared query, edit, stream, source facade, capability, plugin, offline, and field form abstractions.
- gRPC FeatureService client for typed queries, streaming, edits, and spatial filters.
- Admin REST client for services, layers, connections, styles, metadata, manifests, capabilities, identity, licensing, deployment, observability, secure connections, and publishing workflows.
- Spec workspace client for validate, plan, apply stream, cancel, and problem contracts.
- WFS 2.0, GeoServices FeatureServer, OGC API Features, OGC API Records, STAC, scene metadata, geocoding, routing, and geometry packages.
- Offline sync planner and engine over shared query/edit abstractions.
- Field form contracts for validation, calculated fields, duplicate detection, and record workflow.
- Browser/WASM-safe boundary documentation for contracts and REST clients.

## Source Evidence

- Package source: `src/Honua.Sdk.*`
- Admin and catalog clients: `src/Honua.Sdk.Admin/`
- Spec client: `src/Honua.Sdk.Spec/`
- Scene client: `src/Honua.Sdk.Scenes/`
- OGC Records + STAC catalog client: `src/Honua.Sdk.Catalogs/`
- Offline and field packages: `src/Honua.Sdk.Offline*`, `src/Honua.Sdk.Field/`
- Examples: `examples/`
- Compatibility and release docs: `docs/compatibility.md`, `docs/release.md`

## 3D Status

`Honua.Sdk.Scenes` supports scene list/detail/resolve contracts and offline scene package models. Unreal integration and richer 3D tooling are backlog items, not current SDK capabilities.
