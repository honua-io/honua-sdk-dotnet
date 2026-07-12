# Public API approval

Every shipped `Honua.Sdk.*` project has two checked-in API approval files:

- `PublicAPI.Shipped.txt` is the released 1.x surface and changes only when a
  reviewed release promotes approved entries.
- `PublicAPI.Unshipped.txt` contains additions and explicit removals proposed
  for the next release.

`Directory.Build.targets` enables `Microsoft.CodeAnalysis.PublicApiAnalyzers`
for every packable SDK project that declares a `PackageId`. The solution treats analyzer
warnings as errors, so an unapproved addition, removal, signature change, or
nullability change fails both local builds and CI. The build also fails when a
shipped project omits either approval file.

## Approving a change

1. Build the affected package and inspect the `RS0016`, `RS0017`, or `RS0036`
   diagnostic. Do not suppress the diagnostic.
2. For a compatible addition, apply the analyzer code fix or copy its exact API
   declaration into that package's `PublicAPI.Unshipped.txt`.
3. For a removal, add the analyzer-prescribed `*REMOVED*` entry. Removing a
   shipped API is still prohibited in a stable 1.x release unless the explicit
   major-release compatibility process approves it.
4. Review the source change and approval-file diff together. The approval file
   records intent; it does not replace the repository's binary compatibility
   check against the latest stable tag.
5. When cutting a release, move its reviewed unshipped declarations into
   `PublicAPI.Shipped.txt`, preserving the analyzer-generated ordering and
   `#nullable enable`.

New packages must add both files before joining the solution. This keeps public
surface review active from the package's first commit instead of waiting for a
published compatibility baseline.
