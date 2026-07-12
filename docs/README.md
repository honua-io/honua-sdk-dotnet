# Honua .NET SDK documentation

> Hosted API reference: <https://honua-io.github.io/honua-sdk-dotnet/> (deployed from `trunk` via `.github/workflows/docs.yml`).

An index of the public documentation that ships alongside the
[Honua .NET SDK](../README.md). Pick the doc that matches your task.

## Get started

- [Architecture overview](architecture.md) — a one-page map of the 15 packages (meta plus 14 sub-packages) and the separate CLI tool, how they compose, and which one to depend on.
- [Quickstart](quickstart.md) — build a console app that talks gRPC + REST in 5 minutes.
- [API reference](api-reference.md) — how to browse the full XML-doc API surface (IDE hover, local DocFX site, or GitHub source).
- [INSTALL.md](../INSTALL.md) — NuGet, GitHub Packages, version policy, server compatibility baseline.
- [Authentication](authentication.md) — credential providers, refresh, HTTPS-only transport, diagnostics.
- [Console client contracts](console-client-contracts.md) — Blazor Web and MAUI host contract map, route guards, environment profiles, Studio reports/artifacts, native mTLS state, and fixtures.
- [Browser / WASM support](browser-wasm-support.md) — supported surface, gRPC-Web, host-side constraints.
- [Troubleshooting](troubleshooting.md) — concrete failure modes and fixes for configuration, auth, retry, CORS, compatibility, Catalog/Records/STAC/Scenes, and offline sync.
- [Sanitized diagnostic bundles](diagnostic-bundles.md) — `honua doctor` capture, consent, schema provenance, privacy boundary, and read-only replay.

## Capability guides

- [Client behavior](client-behavior.md) — timeout, retry, error, pagination, and typed endpoint coverage behavior.
- [Feature edits](feature-edits.md) — shared edit abstraction, gRPC support, provider-specific write surface.
- [Geometry analysis](geometry-analysis.md) — NetTopologySuite + ProjNet geometry, CRS transforms, planar predicates.
- [Geofencing](geofencing.md) — evaluation contracts, dwell logic, geofence sources.
- [Scenes](scenes.md) — scene discovery, render endpoint resolution, offline scene packages.
- [Offline sync core](offline-sync-core.md) — planner, checkpoints, conflicts, change journals, storage.
- [Spec workspace contracts](spec-workspace-contracts.md) — package ownership and fixtures for spec validate/plan/apply and cached artifact retrieval.
- [Source facade](source-facade.md) — source descriptors, protocol aliases, capabilities, native escape hatches.
- [Plugin contracts](plugin-contracts.md) — host-neutral plugin manifests, permissions, compatibility.
- [Metadata catalog parity](metadata-catalog-parity.md) — Catalog vs OGC API Records vs STAC surface comparison.
- [Capability feature map](features/README.md) — current capability snapshot per protocol.

## Operations

- [Release and NuGet publishing](release.md) — versioning, release tags, dry runs, nuget.org, and GitHub Packages.
- [Compatibility](compatibility.md) — server matrix and CI API compatibility gate before publish.
- [Staging integration](staging-integration.md) — staging environment inputs, CI evidence, follow-on tickets.
- [Protocol integration tests](protocol-integration-tests.md) — Testcontainers-backed coverage and fixture contract.
- [Demo suite](demo-suite.md) — deterministic end-to-end demo workflows used in CI.

## Contributor / internal

[`docs/internal/`](internal/README.md) holds contributor-facing material:
backlog cadence, capability backlog, repo-boundary and contract-harmonization
notes, and server transport ownership. These are not part of the consumer
documentation surface.
