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

The 2026.1 exclusions are limited to operations whose deterministic Spec,
Scene, Routing, Realtime, or Geocoding fixture is absent from the pinned
candidate, plus FeatureServer edits when that candidate has no immutable
governed entitlement. Nightly certification retains these operations as
required gaps so broader support cannot be inferred from the narrower release
profile. Adding or removing an exclusion changes the generated ledger and its
tested denominator partition.

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
