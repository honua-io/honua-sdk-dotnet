#!/usr/bin/env python3
"""Check that a CycloneDX SBOM describes the selected release package."""

import argparse
import json
from pathlib import Path
import sys


def check(path: Path, package_id: str, version: str) -> None:
    bom = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(bom, dict) or bom.get("bomFormat") != "CycloneDX":
        raise ValueError("expected a CycloneDX document")
    components = bom.get("components")
    if not isinstance(components, list) or not components:
        raise ValueError("expected non-empty CycloneDX components")
    metadata = bom.get("metadata")
    component = metadata.get("component") if isinstance(metadata, dict) else None
    if not isinstance(component, dict):
        raise ValueError("missing metadata.component")
    if component.get("name") != package_id or component.get("version") != version:
        raise ValueError(f"metadata.component must identify {package_id}@{version}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sbom", type=Path)
    parser.add_argument("--package-id", required=True)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()
    try:
        check(args.sbom, args.package_id, args.version)
    except (OSError, ValueError) as exc:
        print(f"{args.sbom}: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
