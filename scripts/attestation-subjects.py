#!/usr/bin/env python3
"""Derive publisher-attestation subjects from verified registry proofs.

honua-release's post-gate collector downloads each pinned package from nuget.org,
hashes the bytes it receives, and looks up
repos/<repo>/attestations/sha256:<that digest>. nuget.org repository-signs every
upload, so those bytes are not the unsigned package this workflow pushed. They
are attestable only because check-package-coordinates.py --require-present has
already proven that the public package is semantically identical to this run's
build: same identity and repository commit, and every member except the
repository signature byte-identical.

This script accepts only passing verify-mode proofs bound to the workflow
source revision. It cross-checks every local digest against the downloaded
unsigned inputs and writes:

* a checksums file for actions/attest-build-provenance (subject-checksums),
  naming both the unsigned build output and each verified registry payload; and
* a JSON matrix of {package, digest, name} for one SBOM attestation per package
  on the registry digest consumers download.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys

HEX = re.compile(r"[0-9a-f]{64}\Z")


class SubjectError(ValueError):
    pass


def _proof(path: Path, source_revision: str, version: str) -> list[dict]:
    try:
        proof = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError) as exc:
        raise SubjectError(f"{path.name}: unreadable proof: {exc}") from exc
    expected = {"mode": "verify", "status": "pass", "sourceRevision": source_revision,
                "packageVersion": version}
    drift = {key: proof.get(key) for key, value in expected.items() if proof.get(key) != value}
    if drift or proof.get("failures"):
        raise SubjectError(f"{path.name}: not a passing verify proof for {source_revision}: {drift}")
    rows = proof.get("packages")
    if not isinstance(rows, list) or not rows:
        raise SubjectError(f"{path.name}: proof lists no packages")
    for row in rows:
        if row.get("state") != "present-identical":
            raise SubjectError(f"{path.name}: {row.get('packageId')} is {row.get('state')}, not present-identical")
        for side in ("local", "public"):
            facts = row.get(side) or {}
            if not HEX.fullmatch(str(facts.get("rawSha256", ""))):
                raise SubjectError(f"{path.name}: {row.get('packageId')} has no {side} sha256")
            if facts.get("repositoryCommit") != source_revision:
                raise SubjectError(f"{path.name}: {row.get('packageId')} {side} commit is not {source_revision}")
    return rows


def derive(*, package_dir: Path, nuget_proof: Path | None, github_proof: Path,
           source_revision: str, version: str) -> tuple[list[tuple[str, str]], list[dict]]:
    subjects: dict[str, str] = {}
    matrix: list[dict] = []

    def add(digest: str, name: str) -> None:
        if subjects.setdefault(digest, name) != name:
            raise SubjectError(f"sha256:{digest} is named both {subjects[digest]} and {name}")

    proofs = [("github-packages", github_proof)] + ([("nuget.org", nuget_proof)] if nuget_proof else [])
    consumed = {}
    for registry, path in proofs:
        for row in _proof(path, source_revision, version):
            local = row["local"]
            filename = Path(str(local.get("filename", ""))).name
            built = package_dir / filename
            if not filename.endswith(".nupkg") or not built.is_file():
                raise SubjectError(f"{registry}: {filename!r} is not one of this run's packages")
            actual = hashlib.sha256(built.read_bytes()).hexdigest()
            if actual != local["rawSha256"]:
                raise SubjectError(f"{registry}: {filename} on disk does not match the proof")
            add(actual, f"unsigned/{filename}")
            public = row["public"]["rawSha256"]
            if public != actual:
                add(public, f"{registry}/{filename}")
            consumed[row["packageId"]] = {"package": row["packageId"], "digest": f"sha256:{public}",
                                          "name": f"{registry}/{filename}" if public != actual
                                          else f"unsigned/{filename}"}
    # nuget.org is read last, so its payload wins: that is what consumers and the collector download.
    matrix = [consumed[key] for key in sorted(consumed)]
    return sorted(subjects.items(), key=lambda item: item[1]), matrix


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--package-dir", type=Path, required=True)
    parser.add_argument("--nuget-proof", type=Path)
    parser.add_argument("--github-proof", type=Path, required=True)
    parser.add_argument("--source-revision", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--checksums-out", type=Path, required=True)
    parser.add_argument("--matrix-out", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        subjects, matrix = derive(package_dir=args.package_dir, nuget_proof=args.nuget_proof,
                                  github_proof=args.github_proof, source_revision=args.source_revision,
                                  version=args.version)
    except SubjectError as exc:
        print(f"::error::{exc}", file=sys.stderr)
        return 1
    args.checksums_out.write_text("".join(f"{digest}  {name}\n" for digest, name in subjects), encoding="utf-8")
    args.matrix_out.write_text(json.dumps(matrix, separators=(",", ":")), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
