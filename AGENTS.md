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
- Every new public member needs a one-line `/// <summary>` that customers see in IntelliSense (a
  missing doc fails the build), no other XML doc tags, and an entry in the package's
  `PublicAPI.Unshipped.txt` (`docs/public-api-approval.md`). Internal members get no XML docs.
- Geometry and CRS work goes through NetTopologySuite + ProjNet via `Honua.Sdk.Geometry`, never
  hand-rolled predicates, WKT/WKB parsing or transforms. Geodesic math uses a proven geodesy
  library or the server's GeometryServer.
- Protos are owned by `geospatial-grpc`: change them there first, then consume the published
  `Geospatial.Grpc` package. Never edit `third_party/geospatial-grpc/` by hand.
- A contract or schema version bump updates `release/component-versions.json` in the same PR;
  adding or dropping a dependency on a server capability updates
  `release/sdk-capability-baseline.json`. Read `docs/release.md` "Release declarations" first.
- Sibling repos consume this SDK only as published `Honua.Sdk.*` NuGet packages.
- Vocabulary: public names, docs and error messages use the Honua terms in the
  [glossary](https://github.com/honua-io/honua-server/blob/trunk/docs/concepts/domain/README.md)
  and avoid the retired terms in the shared
  [policy](https://github.com/honua-io/honua-server/blob/trunk/scripts/ci/retired-terms.md).
  `scripts/tests/test_vocabulary.py` enforces `scripts/ci/vocabulary.v1.json`; the product is
  unreleased, so a rename replaces the public name outright (no `[Obsolete]` alias) and lists the old
  name in `renamedSymbols`. Keep server wire field names until the server migrates them.

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

## Review guidelines
Reviews (people, Codex, Claude) judge the lines a PR adds or changes by Robert C. Martin's *Clean Code*
and cite smell codes ([rubric](https://github.com/honua-io/.github/blob/main/clean-code/RUBRIC.md)). The
`Clean code` PR check enforces what a tool can decide; `quality-exception: <reason>` in the PR body is logged.

BLOCKING:
- G5 duplication: search first; reuse or consolidate, never copy a handler, parser or validator.
- C1 history, ticket/PR refs or reviewer notes in code; C2-C4 obsolete, redundant or rambling comments;
  C5 commented-out code. Comment only what the code cannot say.
- XML doc comments (`///`) in application code; published SDKs keep at most a one-line `<summary>`
  on public members.
- N1 names that hide intent; F1 more than 3 parameters; F2 output arguments; F3 boolean flag arguments.
- Over budget with no reason in the PR: functions ~40 lines or 2 nesting levels; classes, files ~400 lines.

WARNING: the rubric's other smells (N2-N7, G6-G36, T1-T9), e.g. G25 magic numbers or G12
single-implementation interfaces.

<!-- fleet:begin v3 sha256=4faeba041ae102508d105847af58b407641cbfdf1ae506488819a2ccdbe73e29 -->
## Fleet rules (all Honua repos)

Generated from the org fleet-rules source; do not edit this block. Start with org skill `honua-system-map`.

1. Author commits as the repository owner's configured git identity. No AI attribution in commits or PR bodies.
2. Use a separate worktree per task, off the default branch, on durable disk. Include current trunk before opening a PR. No shared-checkout edits or git stash; preserve work with commits.
3. Run .NET builds and tests only through the approved remote build-host shims. Never fall back to a local build. See org skill `honua-build-host`.
4. Commit and push completed work; keep existing branches, commits and worktrees until their retention rules permit cleanup.
5. Claim work before starting, reread the issue and current lane rulings, and release the claim after completion. See org skill `honua-dev-loop`.
6. Use a PR slot and bundle at least three issues, a plan row, or a permitted single-work reason. Current caps and exceptions come from current lane rulings.
7. Target the default branch with the repository's PR template and conventional title. Never force-push or use an admin merge.
8. Run pre-PR checks, obtain one independent review of the final head, and resolve every review thread. Merge on green required checks; arm auto-merge where enforced checks support it. See org skill `honua-pr-flow`.
9. Read failed required-check logs and fix in one push. For red trunk, fix forward or revert within the repository's current ruling deadline; see org skills `honua-red-pr` and `honua-trunk-red`.
10. Never commit secrets, deployment identifiers, private repository names or infrastructure details. Security findings belong in the private tracker. Archived repos get no issues or PRs; public instructions name org skills only.
<!-- fleet:end -->
