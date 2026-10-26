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

Spec, Scene, Routing, and Geocoding operations remain required release
obligations when deterministic fixtures are missing. Each operation has its own
live test result. A missing or skipped result fails qualification, including
when diagnostic nonpassing evidence is allowed. Missing fixtures do not establish
unsupported behavior and cannot remove an operation from the release denominator.

Routing certification requires the service ID and all route, service-area, and
closest-facility layer names, plus explicit coordinate pairs for the origin,
destination, service-area center, incident, and two facilities. Configure each
pair with `HONUA_PROTOCOL_<POINT>_LONGITUDE` and `_LATITUDE`, where `<POINT>` is
`ROUTE_ORIGIN`, `ROUTE_DESTINATION`, `SERVICE_AREA_CENTER`, `ROUTE_INCIDENT`,
`ROUTE_FACILITY_A`, or `ROUTE_FACILITY_B`. Coordinates must be finite and within
longitude/latitude ranges. Each operation executes unconditionally in its own
test. Scene and Spec certification require their IDs; Geocoding requires search
text and reverse-geocode coordinates. These settings must identify deterministic
fixtures provisioned by the exact candidate's governed seed/configuration.

Realtime stream operations stay `non-addressable`, owned by
[honua-sdk-dotnet#308](https://github.com/honua-io/honua-sdk-dotnet/issues/308),
because there is no concrete stream client. FeatureServer edits stay in the
denominator. The certification server sets `Licensing__Mode=Disabled`, the
non-secret 2026.1 entitlement, and the edit round-trip must pass. A 402 response
is a failure. Catalog lookup stays on `GET /api/v1/admin/services/`.

## FeatureServer edits

The certification server starts with `Licensing__Mode=Disabled`, the supported
2026.1 mode. That activates every catalog entitlement, including
`editing.featureserver-edits`, and does not use `Licensing__DevGrantEdition`.
`FeatureServerApplyEdits_AddUpdateDelete_RoundTrips` adds, updates, and deletes
on that target. Cleanup is fail-closed. A 402 is a failed edit, not a skip.
The pinned server image and published `Honua.Sdk` package are recorded in
[`certification/candidate-pins.json`](../certification/candidate-pins.json); see
[`certification/README.md`](../certification/README.md) to advance them.
A scheduled workflow follows the published honua-release package coordinate,
and advances a server pin that has fallen behind the manifest snapshot, so
that pin does not wait on a hand bump. A newer server pin is left in place.
The operation ledger is regenerated
only when the public client surface changes, not when the pin moves.
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

`certification/geoservices-source-import/` certifies the supported ArcGIS
service/layer source representations through the installed `Honua.Sdk.GeoServices` package, for
[honua-sdk-dotnet#341](https://github.com/honua-io/honua-sdk-dotnet/issues/341).
Unlike the operation ledger, it treats the digest-pinned candidate as a live
ArcGIS REST *source*. A versioned fixture is published through the supported
admin API, and a consumer restores the SDK from nuget.org by version.

Metadata cells require every member of the source's own JSON to survive the
typed model. Data cells compare decoded values with an oracle written by hand
from the fixture. A negative-control run with a corrupted oracle must fail. See
the [directory README](https://github.com/honua-io/honua-sdk-dotnet/blob/trunk/certification/geoservices-source-import/README.md)
for the cells, released cells and run instructions.

### Import fidelity for 2026.1

The [source-import support matrix](compatibility.md#20261-source-import-support-matrix)
and the [2026.1 operator ruling](https://github.com/honua-io/honua-sdk-dotnet/issues/341#issuecomment-5883909477)
define the source-side support limits consumed by
[honua-release#317](https://github.com/honua-io/honua-release/issues/317).
Keep the following findings visible in the import-fidelity accounting:

| Source-import cell | 2026.1 support | Evidence disposition | Reason |
| --- | --- | --- | --- |
| `data.attachments` | **Not supported in 2026.1** | `released`, not `pass` | No supported source authoring path enables live attachments. |
| `schema.subtypes` | **Not supported in 2026.1** | `released`, not `pass` | No supported source authoring path publishes subtype definitions. |
| `geometry.true-curves` | **Not supported in 2026.1** | `released`, not `pass` | The source has `supportsTrueCurve=false` and cannot publish true-curve geometry. |

Published `Honua.Sdk.GeoServices` 1.10.0 on server source
`87966c3f7b6c840ffc4d4da0b451714ab717b18a`, image
`sha256:069f196bfa5c7201223d4d89868934242c4ace8805a6e48c122a88d84fa6eb1a`,
records **41 pass / 0 fail / 3 released** in the
[executed receipt](https://github.com/honua-io/honua-sdk-dotnet/blob/be77e0789afc70ea3e8f269f6fa5e1119ebeb7d5/certification/geoservices-source-import/evidence/87966c3/published-1.10.0/receipt.json).
One passing cell is an authored transport fixture, not live-source evidence.
The [negative control](https://github.com/honua-io/honua-sdk-dotnet/blob/be77e0789afc70ea3e8f269f6fa5e1119ebeb7d5/certification/geoservices-source-import/evidence/87966c3/published-1.10.0/negative-control-receipt.json)
fails exactly `data.int64-precision` against the deliberately corrupted oracle.
These are prior executed results; this disposition does not rewrite the receipts
or remove cells from their denominator.

The ruling accepts the three unsupported findings for closing SDK #341 once
documented. Source authoring and lossless round-trip support remain 2026.2 work.
The **full 44-cell certification must be rerun on the RC**, retaining all 41
passing and 3 released cells, with the rest of the SDK evidence;
this pre-cut disposition does not certify that future candidate or complete
release#317's downstream source-to-target reconciliation.

## Governed receipt context

Certification evidence requires the retained full Honua release requirements
catalog and an independently expected revision. The producer emits receipt v2
with that row's `maturity`, `required_tier` and `requirements_revision`. It refuses
missing, duplicate or stale operation/client/version identities and mismatched
fixture, contract, authentication or facet context. The consumer does not infer
this context. The source catalog is retained alongside the TRX and receipt.

`certification/requirements-pins.json` binds the default catalog and strict release
consumer to immutable reviewed bytes. The fetcher verifies the canonical release
repository, protected `trunk`, source ancestry, Git blob identities and SHA-256
pins before retaining either file. Release dispatches must explicitly supply
`requirements_ref` (a full protected-trunk ancestor commit) and
`requirements_revision`; mutable branch names are refused. An override retains
its exact source and byte digests. No release Python from an unreviewed feature
branch is executed.

The producer/consumer self-test uses the actual retained release consumer. Its
fixture positive and stale-context/v1 refusal checks establish contract behavior;
they are not live candidate certification. Current historical requirements and
published SDK pins may differ. That mismatch remains blocked until canonical
source/pin convergence; a source change or closed issue cannot qualify new bytes.

Whether a row is licensed comes only from the pinned honua-release catalog
(`licensed` and `entitlement_policy_revision` on each row). The generator never
infers it from the client type, and it refuses a row whose two fields disagree or
that names an entitlement policy it cannot prove. The 2026.1 catalog marks every
FeatureServer edit row unlicensed, so an executed edit row needs no entitlement
assertion. For a row the catalog marks licensed, a passing TRX or a licensing
configuration flag cannot supply the consumer's live entitlement and
license-fingerprint assertion. Until an actual bound probe record is retained,
such rows remain non-pass with their existing
[qualification dependency](https://github.com/honua-io/honua-sdk-dotnet/issues/308).
No license value or secret is inferred, and no requirement is removed.

## SDK and workload identity

`global.json` pins both the SDK and its matching workload set. The SDK version
alone does not fix the browser dependency graph: a newer installed Mono workload
manifest can override the implicit WebAssembly SDK package version.

Before restore, CI, release lock generation, and publishing run
`python3 scripts/ci/prepare-dotnet-toolchain.py`. This provisions the exact set
using a synthetic project outside the checkout, records ambient and selected
identities and manifest import hashes, and rejects implicit WebAssembly or ILLink
versions that differ from the committed browser lock. It does not evaluate
repository MSBuild source while provisioning the toolchain. A successful setup
is toolchain evidence; it does not qualify a server/client candidate.

### Managed release PR readiness

ReleasePlease creates draft release PRs. Lock generation refuses a ready PR, so
return an existing release PR to draft before refreshing it. Keep it draft until
the final source changes have landed and the read-only generation and data-only
apply jobs have completed against a stable head. Their workflow dispatches provide
diagnostics; workflow-dispatched job checks do not satisfy branch rulesets, as
explained in [GitHub’s required status check guidance](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks#checks-from-some-workflow-jobs-are-not-evaluated).

After verifying the final apply run and current head, an authenticated maintainer
uses `gh pr ready <release-pr-number> --repo honua-io/honua-sdk-dotnet`.
The resulting `pull_request: ready_for_review` event starts eligible CI, browser,
CodeQL, docs, and conformance verification. Wait for every protected exact-head
context and resolve review findings before the normal merge. Any subsequent
managed refresh requires returning the PR to draft and repeating this sequence.
No check result, candidate pin, package version, or publication is synthesized.
