# .NET SDK Release and NuGet Publishing

The SDK packages share one version source in `Directory.Build.props`.
Update `HonuaSdkVersionPrefix` and `HonuaSdkVersionSuffix` there before a
release. Leave `HonuaSdkVersionSuffix` empty for stable releases.

## Packages

The publish workflow builds and packs:

- `Honua.Sdk.Abstractions`
- `Honua.Sdk.Admin`
- `Honua.Sdk.Grpc`
- `Honua.Sdk.Wfs`
- `Honua.Sdk.GeoServices`
- `Honua.Sdk.OgcFeatures`

## Release Flow

1. Update `Directory.Build.props`.
2. Open a PR with the version bump and release notes.
3. Run the `Publish .NET SDK Packages` workflow manually with `dry_run=true`.
   This builds, tests, validates API compatibility, audits dependencies, packs,
   and runs package install smoke without publishing.
4. After merge, create and push a tag named `dotnet-sdk-v<PackageVersion>`.
   Example: `dotnet-sdk-v0.1.0-alpha.1`.

The tag version must match the MSBuild `PackageVersion` resolved from the SDK
projects. The workflow fails before publishing if they differ.

## Publishing Targets

All tag releases publish package artifacts and push packages to GitHub Packages.

Stable tags also publish to NuGet.org. A stable tag has no prerelease suffix,
for example `dotnet-sdk-v0.1.0`. Prerelease tags such as
`dotnet-sdk-v0.1.0-alpha.1`, `dotnet-sdk-v0.1.0-beta.1`, or
`dotnet-sdk-v0.1.0-rc.1` skip NuGet.org.

NuGet.org publishing requires the repository secret `NUGET_API_KEY`.

## Local Checks

Resolve the package version:

```bash
dotnet msbuild src/Honua.Sdk.Admin/Honua.Sdk.Admin.csproj -nologo -getProperty:PackageVersion
```

Pack all packages locally:

```bash
mkdir -p ./nupkgs
for project in \
  src/Honua.Sdk.Abstractions/Honua.Sdk.Abstractions.csproj \
  src/Honua.Sdk.Admin/Honua.Sdk.Admin.csproj \
  src/Honua.Sdk.Grpc/Honua.Sdk.Grpc.csproj \
  src/Honua.Sdk.Wfs/Honua.Sdk.Wfs.csproj \
  src/Honua.Sdk.GeoServices/Honua.Sdk.GeoServices.csproj \
  src/Honua.Sdk.OgcFeatures/Honua.Sdk.OgcFeatures.csproj
do
  dotnet pack "$project" --configuration Release -o ./nupkgs
done
```
