#!/usr/bin/env python3
"""Fail-closed preflight for a stable Honua SDK publication to nuget.org."""
from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from collections.abc import Callable
from pathlib import Path


def dependency_version(path: Path, package: str) -> str:
    try:
        root = ET.fromstring(path.read_text(encoding="utf-8"))
    except (OSError, ET.ParseError) as exc:
        raise ValueError(f"dependency manifest is unreadable: {exc}") from exc
    versions = [
        element.get("Version", "").strip()
        for element in root.iter("PackageVersion")
        if element.get("Include") == package
    ]
    versions = [version for version in versions if version]
    if len(versions) != 1:
        raise ValueError(
            f"expected exactly one centrally managed {package} version, found {len(versions)}"
        )
    return versions[0]


def public_versions(url: str) -> list[str]:
    request = urllib.request.Request(url, headers={"User-Agent": "honua-sdk-release-preflight"})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:  # noqa: S310 - fixed registry URL
            payload = json.load(response)
    except (OSError, urllib.error.URLError, json.JSONDecodeError) as exc:
        raise ValueError(f"nuget.org dependency index is unreadable: {exc}") from exc
    versions = payload.get("versions") if isinstance(payload, dict) else None
    if not isinstance(versions, list) or any(not isinstance(value, str) for value in versions):
        raise ValueError("nuget.org dependency index has no trustworthy versions array")
    return versions


def evaluate(
    *,
    manifest: Path,
    package: str,
    index_url: str,
    api_key_present: bool,
    fetch_versions: Callable[[str], list[str]] = public_versions,
) -> dict:
    checks: list[dict[str, str]] = []

    def record(name: str, ok: bool, why: str) -> None:
        checks.append({"check": name, "status": "pass" if ok else "fail", "why": why})

    record(
        "nuget-api-key-present",
        api_key_present,
        "NUGET_API_KEY is configured"
        if api_key_present
        else "NUGET_API_KEY repository secret is missing",
    )

    version = ""
    try:
        version = dependency_version(manifest, package)
        record("dependency-version-resolved", True, f"{package} resolves to {version}")
    except ValueError as exc:
        record("dependency-version-resolved", False, str(exc))

    stable = bool(version) and "-" not in version
    record(
        "dependency-is-stable",
        stable,
        f"{package} {version} is stable"
        if stable
        else f"stable SDK publication requires a stable {package} dependency, got {version or '<missing>'}",
    )

    if version:
        try:
            versions = fetch_versions(index_url)
            present = version in versions
            record(
                "dependency-on-nuget-org",
                present,
                f"{package} {version} is available on nuget.org"
                if present
                else f"{package} {version} is absent from nuget.org",
            )
        except ValueError as exc:
            record("dependency-on-nuget-org", False, str(exc))
    else:
        record("dependency-on-nuget-org", False, f"cannot query {package} without a version")

    failed = [check for check in checks if check["status"] != "pass"]
    return {
        "gate": "stable-nuget-org-prerequisites",
        "status": "fail" if failed else "pass",
        "dependency": {"package": package, "version": version},
        "checks": checks,
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dependency-manifest", required=True, type=Path)
    parser.add_argument("--package", default="Geospatial.Grpc")
    parser.add_argument(
        "--index-url",
        default="https://api.nuget.org/v3-flatcontainer/geospatial.grpc/index.json",
    )
    parser.add_argument("--evidence-out", type=Path)
    args = parser.parse_args(argv)

    evidence = evaluate(
        manifest=args.dependency_manifest,
        package=args.package,
        index_url=args.index_url,
        api_key_present=bool(os.environ.get("NUGET_API_KEY", "").strip()),
    )
    if args.evidence_out:
        args.evidence_out.write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    for check in evidence["checks"]:
        annotation = "notice" if check["status"] == "pass" else "error"
        print(f"::{annotation}::{check['check']}: {check['why']}")
    return 0 if evidence["status"] == "pass" else 1


if __name__ == "__main__":
    sys.exit(main())
