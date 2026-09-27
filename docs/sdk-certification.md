# SDK protocol certification

The .NET SDK certification lane proves public SDK operations against a real,
deterministically seeded Honua Server. The source-derived ledger at
`contracts/sdk-certification.v1.json` is authoritative for operation-level
completeness. It catalogs every async member declared by a public
`IHonua*Client` interface and fails CI when the public surface changes without a
corresponding regenerated ledger.
For provider-neutral contracts, the ledger also records every concrete client
implementation and binds canonical tests to each implementation. An operation
is exercised only when every concrete implementation has live evidence.

## Compliance tiers

| Tier | Trigger | Required scope | Failure semantics |
| --- | --- | --- | --- |
| PR | Relevant pull request | Bounded gRPC, FeatureServer, and OGC API Features reads | Missing, skipped, or failed required cells fail closed. |
| Nightly | Daily schedule or manual dispatch | Every concrete public client operation | Gaps and skips remain owned defects and fail the certification verdict. |
| Release | Manual release-train dispatch | The explicit `honua-sdk-dotnet-2026.1` operation denominator on the exact candidate | Also requires a digest-addressed image, full source SHA, matching seed revision, fixture revision, and independently frozen release cut. |

Provider-neutral interfaces without a concrete Honua transport are recorded as
`non-addressable`; they are not passes. Concrete methods without an executable
live test are recorded as `gap` and owned by
[honua-sdk-dotnet#31](https://github.com/honua-io/honua-sdk-dotnet/issues/31).
Raster implementation gaps remain owned by
[honua-sdk-dotnet#294](https://github.com/honua-io/honua-sdk-dotnet/issues/294).

## 2026.1 release denominator

The generated ledger's `releaseProfile` object is the authoritative 2026.1
denominator. Every public operation is marked `included`, `excluded`, or
`non-addressable` by `releaseDenominator`; the profile count must equal the
number marked `included`. An excluded operation is unsupported by the 2026.1
release certification profile. It is not silently skipped or counted as a
pass: the ledger enumerates its operation ID, owner issue, and reason, and its
cell remains an owned `gap` or `non-addressable` entry.

The 2026.1 exclusions are the Spec, Scene, Routing, and Geocoding operations
whose deterministic fixture is absent from the pinned candidate. Realtime
stream operations stay `non-addressable` and are owned by
[honua-sdk-dotnet#308](https://github.com/honua-io/honua-sdk-dotnet/issues/308)
because that candidate has no certification transport and no concrete stream
client. FeatureServer edits stay in the denominator. The certification server
sets `Licensing__Mode=Disabled`, the non-secret 2026.1 entitlement, and the
edit round-trip must pass. A 402 response is a failure, not evidence of a
successful mutation. Catalog lookup stays on `GET /api/v1/admin/services/`.
Nightly certification retains excluded operations as required gaps so broader
support cannot be inferred from the narrower release profile. Adding or
removing an exclusion changes the generated ledger and its tested denominator
partition.

## FeatureServer edits

The certification server starts with `Licensing__Mode=Disabled`, the supported
2026.1 mode. That activates every catalog entitlement, including
`editing.featureserver-edits`, and does not use `Licensing__DevGrantEdition`.
`FeatureServerApplyEdits_AddUpdateDelete_RoundTrips` adds, updates, and deletes
on that target. Cleanup is fail-closed. A 402 is a failed edit, not a skip.
The pinned image is server `87966c3f7b6c840ffc4d4da0b451714ab717b18a`
(`sha256:069f196bfa5c7201223d4d89868934242c4ace8805a6e48c122a88d84fa6eb1a`).
Certification refuses floating `:nightly` and `:nightly-aot` tags.
The earlier rc-cert image `e3ab87ce` / `sha256:d7a45c87` remains the historical
unentitled 402 receipt and is not rewritten as a pass.
Postgres starts empty apart from PostGIS. The server applies its migrations,
and only then does the lane load `tests/seed/base-schema.sql`. Loading that
seed first leaves migration-owned tables without a journal and the candidate
never becomes ready.

## Commands

Regenerate the operation ledger after changing a public client:

```bash
python3 scripts/generate-sdk-certification.py
```

Check that the committed ledger matches source and canonical test calls:

```bash
python3 scripts/generate-sdk-certification.py --check
```

The workflow parses TRX results and publishes normalized cells containing the
surface, operation, SDK version, exact deployment target, scenario facets,
required tier, verdict, tests, and owned disposition. A gap, unowned skip,
missing result, identity mismatch, or observed failure can never be converted
to a passing cell.
The evidence `operation_scope` binds the release-profile ID, denominator count,
and exact excluded operation IDs to the matrix digest.

## Source-import certification

`certification/geoservices-source-import/` certifies lossless ArcGIS
service/layer import through the installed `Honua.Sdk.GeoServices` package, for
[honua-sdk-dotnet#341](https://github.com/honua-io/honua-sdk-dotnet/issues/341).
Unlike the operation ledger, it treats the digest-pinned candidate as a live
ArcGIS REST *source*. A versioned fixture is published through the supported
admin API, and a consumer restores the SDK from nuget.org by version.

Metadata cells require every member of the source's own JSON to survive the
typed model. Data cells compare decoded values with an oracle written by hand
from the fixture. A negative-control run with a corrupted oracle must fail. See
the [directory README](../certification/geoservices-source-import/README.md)
for the cells, released cells and run instructions.
