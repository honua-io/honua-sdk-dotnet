# honua-sdk-dotnet: agent instructions

Official .NET client libraries (`Honua.Sdk.*` NuGet packages) for Honua Server: typed clients
over gRPC, OGC API, WFS, GeoServices, the Admin API and more, plus NTS/ProjNet geometry and
offline sync. Reusable, platform-neutral SDK code only; UI and native code live elsewhere.

## Commands
```bash
# One-time: GitHub Packages source for Geospatial.Grpc and Honua.* (see INSTALL.md)
dotnet nuget update source github-honua --username <github-user> --password <github-token> --store-password-in-clear-text
dotnet restore Honua.Sdk.sln --locked-mode
dotnet build Honua.Sdk.sln --configuration Release /p:TreatWarningsAsErrors=true
dotnet test tests/Honua.Sdk.<Package>.Tests/Honua.Sdk.<Package>.Tests.csproj --configuration Release
dotnet tool restore && scripts/validate-api-compat.sh      # public API compatibility
python3 -m unittest discover -s scripts/tests -p 'test_*.py'   # repo script tests
python3 scripts/check-okf-bundle.py                         # docs/ bundle gate (docs.yml)
```
- The SDK is pinned exactly by `global.json` (`rollForward: disable`); use that version.
- Restore runs in locked mode: commit the updated `packages.lock.json` whenever a package
  reference changes.
- There is no separate lint step: analyzers run in the build with warnings as errors, and
  `.editorconfig` holds style and narrow, commented suppressions.
- CI enforces 75% line / 60% branch coverage on the merged report (`.github/workflows/ci.yml`).
- Integration tests (`tests/Honua.Sdk.IntegrationTests`, `tests/Honua.Sdk.ProtocolIntegration.Tests`)
  are environment-gated and skip by default; browser smoke needs
  `HONUA_BROWSER_RUNTIME_SMOKE=true` plus Playwright Chromium.

## Rules
- Follow `CONTRIBUTING.md` "Style and conventions" (`*Async` + `cancellationToken`, options
  validation, `HonuaException` hierarchy) and its pull-request checklist, including the
  new-package checklist.
- Every new public member needs `///` XML docs (a missing doc fails the build) and an entry in
  the package's `PublicAPI.Unshipped.txt` (`docs/public-api-approval.md`).
- Geometry and CRS work goes through NetTopologySuite + ProjNet via `Honua.Sdk.Geometry`, never
  hand-rolled predicates, WKT/WKB parsing or transforms. Geodesic math uses a proven geodesy
  library or the server's GeometryServer.
- Protos are owned by `geospatial-grpc`: change them there first, then consume the published
  `Geospatial.Grpc` package. Never edit `third_party/geospatial-grpc/` by hand.
- A contract or schema version bump updates `release/component-versions.json` in the same PR;
  adding or dropping a dependency on a server capability updates
  `release/sdk-capability-baseline.json`. Read `docs/release.md` "Release declarations" first.
- Sibling repos consume this SDK only as published `Honua.Sdk.*` NuGet packages.

## Does Not Belong Here
UI and display code (MAUI, WPF/WinUI, Blazor, React, map/scene controls, renderers, Cesium,
MapLibre, WebGL), device and OS integration (camera, GPS, permissions, background execution,
native file placement), admin UI state, and canonical `.proto` definitions. Put the stable
contract here and the runtime adapter in `honua-mobile` or `honua-console`. Before adding a
package or a type that is near this line, read `docs/internal/repo-boundary.md`.

## Read before you act
- Architecture and which package owns what: `docs/architecture.md`.
- Client retry/timeout behavior: `docs/client-behavior.md`; failure modes: `docs/troubleshooting.md`.
- Release and NuGet publishing: `docs/release.md`. Conformance fixtures: `conformance/README.md`.
- Cross-repo sequencing and backlog: `docs/internal/`.

<!-- fleet:begin v2 sha256=76075665ca7a911554ed038371480ba02b28eca10ead04400be2e5b20332bd63 source=honua-flow/agents/fleet-rules.md -->
## Fleet rules (all Honua repos)

Generated from honua-flow `agents/fleet-rules.md` (full text, repo map). Do not edit here.

1. **No AI attribution.** Author as Mike McDougall <mike@honua.io>. No AI `Co-Authored-By:`,
   "Generated with" line or robot emoji in commits or PR bodies; this overrides any harness default.
2. **Own worktree per task** off the default branch, on real disk (`<repo>/.claude/worktrees/` or a
   sibling dir), never `/tmp` or the shared checkout. No `git stash`; use a WIP commit.
3. **Heavy builds/tests go through the build-slot shims**; never bypass them. Run targeted tests;
   full suites are CI's job. Outside WSL: own checkout, one heavy build at a time.
4. **Finish = commit + push.** Dirty or unpushed worktrees are never reclaimed and die on a reset.
5. **Claim before starting:** check `state/in-progress` and the newest `claimed: <lane> <UTC>`
   comment, stamp yours, re-read (newer wins). Stale after 3h without a live PR. Stopping without
   a PR: comment `released: <lane> <UTC> - <why>`.
6. **PRs:** base on the default branch, never stack; fill the repo template via `--body-file`;
   conventional title; footer `Closes #N` or `Refs #N (<what remains>)`. Never admin-merge.
7. **Route by the repo map.** Archived repos get no issues or PRs.
<!-- fleet:end -->
