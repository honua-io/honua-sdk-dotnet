# Capability coverage snapshot (`sdk-coverage.v1.json`)

This SDK publishes a per-capability coverage snapshot,
[`contracts/sdk-coverage.v1.json`](../contracts/sdk-coverage.v1.json), for the
cross-repo capability matrix (honua-io/honua-server#2892). It is this SDK's
answer to "which canonical capabilities does the .NET SDK's source actually
implement, and how completely" -- generated from the SDK's own source, not
hand-guessed, and validated in CI against the published key list.

## Consume, never copy

The 110-key canonical capability vocabulary is owned by
[honua-io/honua-server#2893](https://github.com/honua-io/honua-server) and
published at `docs/gis/data/capability-keys.v1.json` (schema documented in
that repo's `docs/gis/capability-keys-schema.md`). This SDK never redefines
that vocabulary; `scripts/generate-sdk-coverage.py` only references keys from
it, and fails the build if a mapped key does not actually exist in the
resolved list.

Key-list resolution order (same pattern used by honua-samples and
honua-esri-assess):

1. **`KEY_LIST_URL` env var**, if set to an `http(s)` URL -- fetched at run
   time. This is the one-line swap point if CI ever needs to pin a different
   ref of the published list.
2. **`contracts/fixtures/capability-keys.fixture.json`** -- a pinned,
   byte-for-byte mirror of the published file, committed so CI and offline
   runs never depend on network access. Refresh it by re-downloading
   <https://raw.githubusercontent.com/honua-io/honua-server/trunk/docs/gis/data/capability-keys.v1.json>
   over the existing file; nothing else needs to change.

## Schema

```jsonc
{
  "schemaVersion": "1.0.0",
  "generator": "scripts/generate-sdk-coverage.py",
  "trackingIssue": "#273",
  "sourceRepository": "honua-io/honua-sdk-dotnet",
  "description": "...",
  "sdkAvailability": {
    "status": "source-preview",       // this SDK has no published NuGet package yet
    "publishedVersion": null,
    "note": "..."
  },
  "capabilityKeyList": {
    "canonicalUrl": "https://raw.githubusercontent.com/honua-io/honua-server/trunk/docs/gis/data/capability-keys.v1.json",
    "resolvedFrom": "contracts/fixtures/capability-keys.fixture.json (pinned mirror -- see KEY_LIST_URL)",
    "keyCount": 110
  },
  "coverage": [
    {
      "key": "serve.geoservices-featureserver",
      "status": "covered",            // "covered" | "partial"
      "sinceVersion": "unreleased",
      "entrypoints": ["Honua.Sdk.GeoServices.FeatureServer.HonuaFeatureServerClient"]
    },
    {
      "key": "serve.wfs",
      "status": "partial",
      "sinceVersion": "unreleased",
      "entrypoints": ["Honua.Sdk.OgcFeatures.Wfs.IHonuaWfsClient"],
      "note": "WFS 2.0 read/query only; no WFS-T Transaction support ships in this SDK."
    }
  ]
}
```

Field semantics:

- **`status`**: `covered` (the SDK implements the full capability as this SDK
  understands it) or `partial` (the SDK implements part of it). There is no
  `none` status in the emitted file -- a capability this SDK does not touch is
  **omitted from `coverage` entirely**. The generator refuses to emit a
  literal `"none"` entry (see `_validate_entries` in the generator script);
  padding the list with `none` rows would misrepresent silence as a
  deliberate, reviewed judgment.
- **`note`** (partial only, required and enforced by the generator): says
  concretely where SDK coverage stops -- e.g. "read-only", "no Transaction
  support", "contract-only, no concrete implementation ships yet". A `partial`
  entry with an empty or missing note fails validation. A `covered` entry must
  **not** carry a `note` (if there's a caveat worth writing down, the entry is
  `partial` by definition).
- **`sinceVersion`**: always the literal string `"unreleased"`. This SDK ships
  as a **source preview** -- no `Honua.Sdk.*` NuGet package has published yet
  (see honua-site's `data/sdk-availability.v1.json`, `productArea:
  "sdk-dotnet"`, `availability: "Source preview"`, `publishedVersion: null`).
  Repo tags such as `dotnet-sdk-v1.5.0` are pre-publish `release-please`
  source tags, not registry releases, and must never be used as a
  `sinceVersion` value -- that would imply a version boundary that does not
  exist for anyone installing from a registry today. When a package first
  publishes, real per-capability `sinceVersion` values become meaningful and
  this doc (and the generator) should be updated together.
- **`entrypoints`**: fully-qualified public type and/or method names that
  provide the coverage -- always a real symbol in `src/`, never a paraphrase.
  This is machine-enforced, not just convention: the generator indexes every
  *publicly declared* type under `src/` (real declaration syntax, matched on
  comment/string-stripped source) and fails if an entrypoint no longer
  resolves (`Namespace.Type` must be a public type declaration;
  `Namespace.Type.Member` must additionally match an accessible,
  declaration-shaped member in a file declaring the type). Occurrences that
  survive only in comments, XML doc prose, or string literals never count,
  and a type demoted to `internal` stops resolving -- so a rename, deletion,
  or de-publicizing of a mapped surface fails the CI drift gate until the
  inventory and snapshot are updated together.

## Generating and validating

```bash
# Regenerate contracts/sdk-coverage.v1.json from the curated inventory in
# scripts/generate-sdk-coverage.py:
python3 scripts/generate-sdk-coverage.py

# Drift gate: fails if the committed file doesn't match what the generator
# would produce right now (unknown keys, missing partial notes, entrypoints
# that no longer resolve against src/, and stale content all fail this):
python3 scripts/generate-sdk-coverage.py --check
```

CI runs the `--check` form (see `.github/workflows/ci.yml`, job
`sdk-coverage`) on every push and pull request, and additionally uploads
`contracts/sdk-coverage.v1.json` as a build artifact on `trunk` pushes so
honua-evidence (honua-io/honua-evidence#1) can pull the latest snapshot.
`scripts/tests/test_sdk_coverage.py` unit-tests the validation rules
(unknown-key rejection, partial-requires-note, no-note-on-covered, duplicate
keys, entrypoint source-truth resolution) plus an end-to-end `--check` smoke
test, and runs as part of the `workflow-validation` job's existing
`python3 -m unittest discover -s scripts/tests -p 'test_*.py'` step.

## Updating the inventory

When SDK source gains, loses, or changes the shape of coverage for a
capability:

1. Edit the `COVERAGE_ENTRIES` list in `scripts/generate-sdk-coverage.py` --
   point `entrypoints` at the real public symbol(s), and add/update a `note`
   if the status is (or becomes) `partial`.
2. Run `python3 scripts/generate-sdk-coverage.py` to regenerate
   `contracts/sdk-coverage.v1.json`.
3. Commit both files together. CI's drift gate fails a PR that changes
   coverage-relevant source without regenerating the snapshot.

## Downstream ingestion

honua-evidence (honua-io/honua-evidence#1) ingests this snapshot end-to-end:
its aggregator fetches
`https://raw.githubusercontent.com/honua-io/honua-sdk-dotnet/trunk/contracts/sdk-coverage.v1.json`,
joins each entry into `data/capability-matrix.v1.json` under
`capabilities[].sdks.dotnet` (status, `sinceVersion`, entrypoints), and tracks
this producer in its freshness ledger (30-day staleness threshold). Pushes to
`trunk` that change the snapshot also fire a `producer-updated`
repository_dispatch to honua-evidence via
`.github/workflows/notify-evidence.yml`, so re-aggregation is event-driven
rather than waiting on the daily schedule.
