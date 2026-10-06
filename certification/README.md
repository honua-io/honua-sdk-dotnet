# Certification inputs

`.NET SDK Protocol Certification` (`.github/workflows/sdk-certification.yml`)
installs the published `Honua.Sdk` package and runs the protocol suites against
an attested honua-server image. Both are pinned in one file,
[`candidate-pins.json`](candidate-pins.json):

| Field | Meaning | Source of truth |
|---|---|---|
| `sdkPackageId` | Always `Honua.Sdk` | — |
| `sdkPackageVersion` | Published package version | nuget.org; `clientArtifacts.honua-sdk-dotnet.version` in the honua-release manifest |
| `sdkPackageRegistry` | Always `nuget.org` | — |
| `sdkPackageSha512` | Base64 SHA-512 of the nupkg | nuget.org catalog `packageHash` |
| `sdkPackageDigest` | `sha256:` of the same nupkg | `clientArtifacts.honua-sdk-dotnet.digest` |
| `sdkSourceSha` | Commit the package was built from | nuget.org catalog `repository.commit`, nuspec, `clientArtifacts.honua-sdk-dotnet.sourceSha` |
| `serverSourceSha` | honua-server commit of the release candidate | the honua-release nightly resolver's selection |
| `serverImageDigest` | Digest of `ghcr.io/honua-io/honua-server:nightly-<sha7>` | GHCR |
| `fixtureRevision` | `sha256:` of honua-server `tests/seed/base-schema.sql` at `serverSourceSha` | honua-server |

`scripts/prepare-installed-package-certification.py` is the only reader. The
workflow loads the file through `--emit-env`. The restore step installs exactly
`sdkPackageVersion` from nuget.org into a clean consumer, with signature
validation left on. It then fails closed unless the installed nupkg matches
`sdkPackageSha512`, `sdkPackageDigest` and `sdkSourceSha`.

## Checking the pins

```bash
python3 scripts/prepare-installed-package-certification.py --check
```

`--check` needs `gh` (authenticated or `GH_TOKEN`) and PyYAML. It downloads the
package and verifies the following:

- nuget.org lists the version, its catalog SHA-512 and source commit match the
  pins, and the downloaded bytes match the catalog and both pinned digests.
- The honua-release `platform-manifest.yaml` on `trunk` pins the same package.
  This covers version, digest, source commit, registry and `published` state
  under `clientArtifacts.honua-sdk-dotnet`, plus `components.honua-sdk-dotnet`.
- `serverSourceSha` is the manifest's `components.honua-server.sha` or a newer
  commit on top of it. The manifest keeps a hand snapshot while the nightly
  resolver selects newer trunk candidates. When the two commits are equal, the
  digests must match too.
- `nightly-<sha7>` on GHCR resolves to `serverImageDigest`, and the seed at
  `serverSourceSha` hashes to `fixtureRevision`.

Each drift is reported on its own `::error::` line naming the field, the pinned
value and the published value. Scheduled and dispatched runs execute `--check`
before restoring. Pull requests execute it only when they change the pin file,
so external drift does not fail unrelated PRs.

## Advancing a pin

1. Read the new values:
   - Package: the catalog entry linked from
     `https://api.nuget.org/v3/registration5-gz-semver2/honua.sdk/<version>.json`
     (`packageHash`, `repository.commit`). For the `sha256:`, run `sha256sum` on the
     downloaded nupkg.
   - Server: the honua-release nightly resolver's selected honua-server commit
     (`RESOLVED honua-server <sha> <image@digest>`).
   - Image: `docker buildx imagetools inspect ghcr.io/honua-io/honua-server:nightly-<sha7>`.
   - Fixture: `gh api "repos/honua-io/honua-server/contents/tests/seed/base-schema.sql?ref=<sha>" -H "Accept: application/vnd.github.raw" | sha256sum`.
2. Edit `certification/candidate-pins.json`. Change only the fields that moved.
3. Run `--check` until it exits 0, and run
   `python3 -m unittest discover -s scripts/tests -p 'test_*.py'`.
4. Open a pull request. Changing the pin file runs `certify`, including `--check`.
   For full coverage, dispatch the workflow with `tier=nightly` on the branch:
   `gh workflow run sdk-certification.yml --ref <branch> -f tier=nightly`.

Do not advance the package past what honua-release has adopted in
`clientArtifacts.honua-sdk-dotnet`, because `--check` refuses it. Advance
honua-release first (R25), then this file.

## Following the release manifest

`.github/workflows/certification-pin-follow.yml` runs daily and on
`workflow_dispatch`. It compares this file with
`clientArtifacts.honua-sdk-dotnet` on honua-release `trunk`. When that
published coordinate differs, `--follow-manifest` updates only the SDK package
fields. Version, digest, and source commit come from the manifest. The SHA-512
comes from the nuget.org bytes of that version. The workflow then runs
`--check`. Server pins stay as they are: the manifest's server snapshot may
lag the nightly resolver.

An unpublished `publicationState`, or a version that nuget.org does not serve,
fails the run and does not open a pull request. The workflow opens or updates
one pull request, branch `chore/certification-pin-follow` into `trunk`. It
does not merge. The push uses the repository `GITHUB_TOKEN`, which does not
start `pull_request` workflows, so the same job dispatches
`sdk-certification.yml` with `tier=pr` on that commit. Commits on the branch
use the repository owner identity.
