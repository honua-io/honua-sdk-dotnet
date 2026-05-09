# .NET SDK Release and NuGet Publishing

The SDK packages share one version source in `Directory.Build.props`.
Release Please updates the `HonuaSdkVersion` property when it opens a release
PR.

## Packages

The publish workflow builds and packs:

- `Honua.Sdk.Abstractions`
- `Honua.Sdk.Admin`
- `Honua.Sdk.Spec`
- `Honua.Sdk.Grpc`
- `Honua.Sdk.Wfs`
- `Honua.Sdk.GeoServices`
- `Honua.Sdk.Scenes`
- `Honua.Sdk.Field`
- `Honua.Sdk.OgcFeatures`
- `Honua.Sdk.OgcRecords`
- `Honua.Sdk.Stac`
- `Honua.Sdk.Offline.Abstractions`
- `Honua.Sdk.Offline`

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
   Example: `dotnet-sdk-v0.1.0-alpha.1`.

The tag version must match the MSBuild `PackageVersion` resolved from the SDK
projects. The workflow fails before publishing if they differ.

## Publishing Targets

All tag releases publish package artifacts and push packages to GitHub Packages.
The release workflow also restores GitHub-hosted dependencies such as
`Geospatial.Grpc` from `nuget.pkg.github.com/honua-io` using `GITHUB_TOKEN`.

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
  src/Honua.Sdk.Spec/Honua.Sdk.Spec.csproj \
  src/Honua.Sdk.Grpc/Honua.Sdk.Grpc.csproj \
  src/Honua.Sdk.Wfs/Honua.Sdk.Wfs.csproj \
  src/Honua.Sdk.GeoServices/Honua.Sdk.GeoServices.csproj \
  src/Honua.Sdk.Scenes/Honua.Sdk.Scenes.csproj \
  src/Honua.Sdk.Field/Honua.Sdk.Field.csproj \
  src/Honua.Sdk.OgcFeatures/Honua.Sdk.OgcFeatures.csproj \
  src/Honua.Sdk.OgcRecords/Honua.Sdk.OgcRecords.csproj \
  src/Honua.Sdk.Stac/Honua.Sdk.Stac.csproj \
  src/Honua.Sdk.Offline.Abstractions/Honua.Sdk.Offline.Abstractions.csproj \
  src/Honua.Sdk.Offline/Honua.Sdk.Offline.csproj
do
  dotnet pack "$project" --configuration Release -o ./nupkgs
done
```
