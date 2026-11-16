#!/usr/bin/env python3
"""Check that a CycloneDX SBOM describes the selected release package."""

import argparse
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
import zipfile


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


def package_dependencies(path: Path, identity: tuple[str, str]) -> list[dict]:
    with zipfile.ZipFile(path) as archive:
        names = [name for name in archive.namelist() if name.endswith(".nuspec")]
        if len(names) != 1:
            raise ValueError("expected exactly one packed nuspec")
        root = ET.fromstring(archive.read(names[0]))
    for element in root.iter():
        element.tag = element.tag.rsplit("}", 1)[-1]
    metadata = root.find("metadata")
    if metadata is None or (metadata.findtext("id"), metadata.findtext("version")) != identity:
        raise ValueError("packed nuspec does not match selected package identity")
    return [element.attrib for element in metadata.findall("dependencies//dependency")]


def component_index(bom: dict) -> dict[str, dict]:
    components = [bom["metadata"]["component"], *bom["components"]]
    index = {}
    for component in components:
        if not isinstance(component, dict):
            raise ValueError("invalid component")
        reference = component.get("bom-ref")
        if not isinstance(reference, str) or not reference or reference in index:
            raise ValueError("missing or duplicate component bom-ref")
        if not component.get("name") or not component.get("version"):
            raise ValueError("component must have a name and version")
        index[reference] = component
    return index


def dependency_graph(bom: dict, components: dict) -> dict[str, set[str]]:
    dependencies = bom.get("dependencies")
    if not isinstance(dependencies, list):
        raise ValueError("missing dependency graph")
    graph = {}
    for dependency in dependencies:
        if not isinstance(dependency, dict):
            raise ValueError("invalid dependency graph entry")
        reference = dependency.get("ref")
        targets = dependency.get("dependsOn", [])
        if reference not in components or reference in graph:
            raise ValueError("unresolved or duplicate dependency graph reference")
        if not isinstance(targets, list) or any(not isinstance(target, str) for target in targets):
            raise ValueError("invalid dependency graph targets")
        if any(target not in components for target in targets):
            raise ValueError("unresolved dependency edge")
        graph[reference] = set(targets)
    return graph


def check_dependencies(path: Path, package: Path) -> None:
    bom = json.loads(path.read_text(encoding="utf-8"))
    components = component_index(bom)
    graph = dependency_graph(bom, components)
    root = bom["metadata"]["component"]
    identity = (root["name"], root["version"])
    if root["bom-ref"] not in graph:
        raise ValueError("missing root dependency graph entry")
    direct = [components[ref] for ref in graph.get(root["bom-ref"], set())]
    for dependency in package_dependencies(package, identity):
        name = dependency.get("id", "")
        matches = [component for component in direct if component["name"].lower() == name.lower()]
        if not matches:
            raise ValueError(f"packed dependency {name} missing from root dependency edges")
        if name.lower().startswith("honua.sdk"):
            if dependency.get("version") != identity[1]:
                raise ValueError(f"packed SDK dependency {name} must use release version {identity[1]}")
            if any(component["version"] != identity[1] for component in matches):
                raise ValueError(f"SDK dependency {name} must identify release version {identity[1]}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("sbom", type=Path)
    parser.add_argument("--package-id", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--package", type=Path, required=True)
    args = parser.parse_args()
    try:
        check(args.sbom, args.package_id, args.version)
        check_dependencies(args.sbom, args.package)
    except (OSError, ValueError, ET.ParseError, zipfile.BadZipFile, TypeError) as exc:
        print(f"{args.sbom}: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
