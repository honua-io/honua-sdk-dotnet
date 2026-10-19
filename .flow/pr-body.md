fix(security): pin source-import consumer restore by NuGet content hash
## Summary

Addresses the open Scorecard **PinnedDependencies** medium alert #3188 (`nugetCommand not pinned by hash`, `certification/geoservices-source-import/run.sh:213`).

The installed-package certification restored its consumer without a lock, so only `Honua.Sdk.GeoServices` was checked by hash (the consumer compares the nupkg SHA-512). The rest of the transitive graph was unpinned. Now:

- `run.sh` restores with `dotnet restore --locked-mode` against a lock file. For published runs, that is the committed `consumer/locks/<SDK_PACKAGE_VERSION>.packages.lock.json`. For `LOCAL_PACKAGE_DIR` runs, the caller supplies `LOCAL_PACKAGE_LOCK`.
- If no lock exists, the script stops before restore (fails closed).
- `consumer/locks/1.10.0.packages.lock.json` pins all 40 packages of the default published 1.10.0 graph by NuGet content hash.
- The README documents how to generate a lock and the new `LOCAL_PACKAGE_LOCK` variable.

The lock uses a `<version>.` prefix, so the `**/packages.lock.json` sets that CI and release-please regenerate do not include it.

## CodeQL generated-source warnings (dispatch premise): already fixed on trunk

The `cs/useless-cast-to-self` alerts under `obj/**/generated/System.Text.Json.SourceGeneration` were closed by #337 (merged 2026-09-13). That PR added `.github/codeql/codeql-config.yml` with `paths-ignore: ['**/obj/**', '**/generated/**']` and `build-mode: none`, and disabled no rules. Code-scanning API, checked 2026-10-19:

- Open alerts under `/obj/` or `/generated/`: **0**. Dismissed: **0**.
- Fixed ("no longer detected") alerts under generated paths: 2,656 `cs/useless-cast-to-self` on 2026-09-13, plus 31 earlier and 77 of other rules.
- Real code is still analysed. The trunk CodeQL analysis at `aada6d8` reports 267 results across 164 rules. Open `cs/useless-cast-to-self` alerts remain in `src/` (for example `src/Honua.Sdk.OgcFeatures/HonuaOgcFeaturesClient.cs`).

This PR does not change CodeQL configuration.

## Verification

Run natively with the .NET 10 SDK against a copy of `consumer/` and a fresh `NUGET_PACKAGES`:

1. Locked restore with the committed 1.10.0 lock passes, and `dotnet build --no-restore -c Release` passes with 0 warnings and 0 errors.
2. The same lock with one `contentHash` altered fails with `NU1403 Package content hash validation failed`.
3. `HonuaSdkPackageVersion=1.8.0` against the 1.10.0 lock fails with `NU1004`.
4. The `run.sh` guard was run in isolation:
   - 1.10.0 published: resolves the committed lock.
   - 1.8.0 published: exits 1 (no lock).
   - Local mode without `LOCAL_PACKAGE_LOCK`: exits 1.
   - Local mode with a relative `LOCAL_PACKAGE_LOCK`: resolves an absolute path.
5. `bash -n run.sh` passes.

Scorecard re-scans trunk after merge. #3188 is expected to close as fixed then; it cannot be observed before merge.

## Follow-ups (not in this PR)

- Scorecard #3229 (`release-please.yml:118`, medium) is new from #442. It flags the deliberate lock-regeneration restore (`--force-evaluate /p:RestoreLockedMode=false`), which is followed by a `--locked-mode` restore and a `git diff --exit-code` on the locks. It needs a decision, not a mechanical fix, and has not been dismissed.
- Re-running the historical 1.7.0 or 1.8.0 receipts, or moving the default `SDK_PACKAGE_VERSION`, now requires committing that version's lock first.
