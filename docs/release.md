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

Before a stable tag is created, confirm the repository has a `NUGET_API_KEY`
secret and that the pinned **stable** `Geospatial.Grpc` version is available from
nuget.org. The workflow fails before publishing when either prerequisite is
missing, because otherwise the public SDK packages would not be restorable
from the public feed. Repository admins can verify secret presence with
`gh secret list --repo honua-io/honua-sdk-dotnet`; the preflight never prints
the credential. nuget.org does not expose a non-mutating API-key permission
check, so account/package scope is finally proven by the first push.

The tag version must match the MSBuild `PackageVersion` resolved from the SDK
projects. The workflow fails before publishing if they differ.

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

- Stable versions publish to both nuget.org and GitHub Packages. The workflow first waits for every
  shipped package to be indexed, then clean-installs the `Honua.Sdk` umbrella, representative
  `Honua.Sdk.Admin` / `Honua.Sdk.Grpc` leaves, and the `Honua.Sdk.Cli` tool using a NuGet.config that
  contains only nuget.org. GitHub Packages publication happens only after that public-feed proof.
- Prerelease versions publish to GitHub Packages only.
- Dry runs build, inspect, and install the local packages without pushing to
  either feed. They sign only when signing credentials are configured and keep
  both primary and symbol packages as workflow artifacts.

The workflow uses `NUGET_API_KEY` for nuget.org and `GITHUB_TOKEN` for GitHub
Packages. It restores GitHub-hosted dependencies such as `Geospatial.Grpc` from
`nuget.pkg.github.com/honua-io` during build validation. Stable public publishing
remains blocked until the same dependency version is available from nuget.org.
Release-tag signing and verification continue to cover both primary `.nupkg` and symbol `.snupkg`
artifacts. GitHub Packages receives that author-signed set. If the author certificate chains to a
publicly trusted root, nuget.org receives the same set; otherwise it receives the preserved unsigned
set and adds its own repository signature. Registering a publicly trusted author certificate remains
a hardening action, not a prerequisite for nuget.org's repository-signed publication path.

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
