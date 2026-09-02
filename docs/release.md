# .NET SDK Release and NuGet Publishing

The SDK packages share one version source in `Directory.Build.props`.
Release Please updates the `HonuaSdkVersion` property when it opens a release
PR.

## Packages

The publish workflow builds and packs:

- `Honua.Sdk.Abstractions`
- `Honua.Sdk.Admin`
- `Honua.Sdk.Processes`
- `Honua.Sdk.Geometry`
- `Honua.Sdk.Spec`
- `Honua.Sdk.Studio`
- `Honua.Sdk.Grpc`
- `Honua.Sdk.GeoServices`
- `Honua.Sdk.Scenes`
- `Honua.Sdk.Field`
- `Honua.Sdk.OgcFeatures`
- `Honua.Sdk.Catalogs`
- `Honua.Sdk.Offline`
- `Honua.Sdk.ConsoleShare`
- `Honua.Sdk.Cli` (.NET tool package)
- `Honua.Sdk`

## Release Flow

1. Merge changes to `trunk` using conventional commits.
2. Let the Release Please workflow open or update the release PR.
3. Confirm the release PR includes `Directory.Build.props`,
   `.release-please-manifest.json`, and `CHANGELOG.md`.
4. Run the `Publish .NET SDK Packages` workflow manually with `dry_run=true`.
   This builds, tests, validates API compatibility, audits dependencies, packs,
   and runs package install smoke without publishing.
5. Merge the release PR so Release Please creates the tag named
   `dotnet-sdk-v<PackageVersion>`.
   Example: `dotnet-sdk-v1.0.0`.

Before a release tag is created, configure a nuget.org **Trusted Publishing**
policy for owner `makanikai`, repository `honua-io/honua-sdk-dotnet`, workflow
`publish-dotnet-sdk.yml`, environment `public-nuget`, and package glob
`Honua.Sdk*`. No long-lived `NUGET_API_KEY` or author-signing secret exists or
may be created. The publish job uses its `id-token: write` permission and the
pinned `NuGet/login` action to exchange its GitHub OIDC identity for a
short-lived key; an empty exchange fails closed before any registry mutation.
The pinned **stable** `Geospatial.Grpc` version must also be available from
nuget.org. The environment must
allow only selected `dotnet-sdk-v*` tags, require a reviewer, and disallow admin
bypass. The dependency preflight runs before package construction; the
short-lived credential is resolved only inside the protected publish job and
is validated before any registry mutation. The workflow never prints the
credential. Account/package scope is finally proven by the first push.

The tag version must match the MSBuild `PackageVersion` resolved from the SDK
projects, the tag commit must be contained in `origin/trunk`, and required
staging integration must pass. The workflow fails before publishing if any of
those bindings fail. Release build and publish jobs use the exact .NET SDK
`10.0.400` so a rerun cannot silently select a newer feature-band SDK.

## Version bumps

The SDK follows standard [SemVer](https://semver.org/):

- **Patch** (`1.0.x`) -- bug fixes and internal changes with no public-API
  movement. Driven by `fix:` commits.
- **Minor** (`1.x.0`) -- additive public surface (new types, new methods,
  new options). Driven by `feat:` commits. Must remain source- and
  binary-compatible with the previous minor.
- **Major** (`x.0.0`) -- intentional breaking change. Must include a
  `BREAKING CHANGE:` footer or `!` suffix on the conventional-commit type, and
  is coordinated across the wider Honua SDK fleet.

Pre-release suffixes (`*-rc.N`, `*-beta.N`) are reserved for deliberate
previews of a future major.

## Publishing Targets

- Stable versions publish to both nuget.org and GitHub Packages. Before the
  first mutation, the workflow audits every exact coordinate on both feeds.
  Absent coordinates are eligible for publication, semantically identical
  payloads are safe to resume, and an occupied divergent payload fails closed.
  The same preflight applies to every `.snupkg` at nuget.org's HTTPS symbol
  package endpoint. The workflow submits only absent symbol coordinates,
  without duplicate acceptance, then downloads and semantically compares every
  remote symbol package (including its portable-PDB payload) before proceeding.
  An unavailable or divergent symbol coordinate fails closed. After publication
  it also downloads and compares every public primary package with the
  build-once payload, then
  clean-installs the `Honua.Sdk` umbrella, representative `Honua.Sdk.Admin` /
  `Honua.Sdk.Grpc` leaves, and the `Honua.Sdk.Cli` tool using a NuGet.config that
  contains only nuget.org. GitHub Packages publication happens only after that
  public-feed proof.
- Prerelease versions publish to GitHub Packages only.
- Dry runs build, inspect, and install the local packages without pushing to
  either feed. They never invoke Trusted Publishing and keep immutable primary
  and symbol packages as workflow artifacts. The `run_staging` input can add
  staging to a dry run; staging is mandatory for every non-dry tag publish.

The workflow uses the Trusted Publishing exchange's short-lived key for
nuget.org and the job-scoped `GITHUB_TOKEN` for GitHub Packages. Build and
package-install validation restore the stable `Geospatial.Grpc` dependency
from nuget.org; the GitHub Packages credential is reserved for the secondary
SDK publication target. Packages are not author-signed: nuget.org
repository-signs every package on ingestion, while modern CA-issued
code-signing certificates are HSM-bound and cannot be stored as base64 GitHub
secrets. Both registries receive the same immutable build-once package payload.

Every build-once primary and symbol archive is covered by a committed-run
`SHA256SUMS`. The publish job rechecks those hashes after artifact download and
uploads machine-readable preflight and post-publication receipts. NuGet's
repository signature can change raw archive bytes; the public comparison hashes
the package payload while excluding only NuGet signature/container plumbing.
Do not use a new workflow dispatch to recover a partially completed release.
Use GitHub's rerun mechanism for the same run so it reuses the immutable tag and
coordinate audit. Registry evidence uploads use an `always()` boundary so a
failed or partial publish retains the preflight/proof files that were produced.
Both failed-job and full-run retries are supported.

## Local Checks

Resolve the package version:

```bash
dotnet msbuild src/Honua.Sdk.Admin/Honua.Sdk.Admin.csproj -nologo -getProperty:PackageVersion
```

For emergency manual releases, update `HonuaSdkVersion` in
`Directory.Build.props` and create a matching `dotnet-sdk-v<PackageVersion>`
tag.

Pack all packages locally:

```bash
mkdir -p ./nupkgs
for project in \
  src/Honua.Sdk.Abstractions/Honua.Sdk.Abstractions.csproj \
  src/Honua.Sdk.Admin/Honua.Sdk.Admin.csproj \
  src/Honua.Sdk.Processes/Honua.Sdk.Processes.csproj \
  src/Honua.Sdk.Geometry/Honua.Sdk.Geometry.csproj \
  src/Honua.Sdk.Spec/Honua.Sdk.Spec.csproj \
  src/Honua.Sdk.Studio/Honua.Sdk.Studio.csproj \
  src/Honua.Sdk.Grpc/Honua.Sdk.Grpc.csproj \
  src/Honua.Sdk.GeoServices/Honua.Sdk.GeoServices.csproj \
  src/Honua.Sdk.Scenes/Honua.Sdk.Scenes.csproj \
  src/Honua.Sdk.Field/Honua.Sdk.Field.csproj \
  src/Honua.Sdk.OgcFeatures/Honua.Sdk.OgcFeatures.csproj \
  src/Honua.Sdk.Catalogs/Honua.Sdk.Catalogs.csproj \
  src/Honua.Sdk.Offline/Honua.Sdk.Offline.csproj \
  src/Honua.Sdk.ConsoleShare/Honua.Sdk.ConsoleShare.csproj \
  src/Honua.Sdk.Cli/Honua.Sdk.Cli.csproj \
  src/Honua.Sdk/Honua.Sdk.csproj
do
  dotnet pack "$project" --configuration Release -o ./nupkgs
done
```
