#!/usr/bin/env python3
"""Generate and evaluate the public .NET SDK operation certification ledger."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
SRC_ROOT = ROOT / "src"
TEST_ROOTS = (
    ROOT / "tests" / "Honua.Sdk.ProtocolIntegration.Tests",
    ROOT / "tests" / "Honua.Sdk.Conformance.Tests",
)
OUTPUT_PATH = ROOT / "contracts" / "sdk-certification.v1.json"
TRACKING_ISSUE = "https://github.com/honua-io/honua-sdk-dotnet/issues/31"
RASTER_ISSUE = "https://github.com/honua-io/honua-sdk-dotnet/issues/294"

PR_OPERATIONS = frozenset(
    {
        "IHonuaGrpcClient.QueryFeaturesAsync",
        "IHonuaFeatureServerClient.GetServiceInfoAsync",
        "IHonuaFeatureServerClient.GetLayerInfoAsync",
        "IHonuaFeatureServerClient.QueryAsync",
        "IHonuaFeatureServerClient.QueryCountAsync",
        "IHonuaFeatureServerClient.QueryIdsAsync",
        "IHonuaOgcFeaturesClient.ListCollectionsAsync",
        "IHonuaOgcFeaturesClient.GetCollectionAsync",
        "IHonuaOgcFeaturesClient.GetQueryablesAsync",
        "IHonuaOgcFeaturesClient.GetItemsAsync",
        "IHonuaOgcFeaturesClient.GetItemAsync",
    }
)

MUTATION_PREFIXES = (
    "Add", "Apply", "Cancel", "Create", "Delete", "Deprovision", "Dismiss",
    "Commit", "Disconnect", "Execute", "Import", "Patch", "Pause", "Publish", "Remove",
    "Reopen", "Resume", "Revoke", "Rollback", "Rotate", "Set", "Start", "Submit",
    "Synchronize", "Trigger", "Unpublish", "Unsubscribe", "Update", "Upload",
)
PAGINATION_PREFIXES = ("List", "Query", "Search", "GetItems", "GetFeatures")

UNSEEDED_TESTS = frozenset(
    {
        "Honua.Sdk.ProtocolIntegration.Tests.AdminProtocolIntegrationTests.Geocoding_ForwardReverseSuggestAndBatch_AreReachable",
        "Honua.Sdk.ProtocolIntegration.Tests.SpecSceneRoutingProtocolIntegrationTests.SpecValidatePlanAndApplyStream_AreReachable",
        "Honua.Sdk.ProtocolIntegration.Tests.SpecSceneRoutingProtocolIntegrationTests.SceneListGetAndResolve_AreReachable",
        "Honua.Sdk.ProtocolIntegration.Tests.SpecSceneRoutingProtocolIntegrationTests.RoutingMetadataDirectionsServiceAreaAndClosestFacility_AreReachable",
    }
)


def _strip_comments_and_strings(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), text, flags=re.S)
    text = re.sub(r"//[^\n]*", "", text)
    text = re.sub(r'@?"(?:""|\\.|[^"\n])*"', '""', text)
    return re.sub(r"'(?:\\.|[^'\n])'", "''", text)


def _block(text: str, opening_brace: int) -> str:
    depth = 0
    for index in range(opening_brace, len(text)):
        if text[index] == "{":
            depth += 1
        elif text[index] == "}":
            depth -= 1
            if depth == 0:
                return text[opening_brace + 1:index]
    raise ValueError("unbalanced C# declaration block")


def _closing_delimiter(text: str, opening: int, opener: str = "(", closer: str = ")") -> int:
    depth = 0
    for index in range(opening, len(text)):
        if text[index] == opener:
            depth += 1
        elif text[index] == closer:
            depth -= 1
            if depth == 0:
                return index
    raise ValueError(f"unbalanced C# {opener}{closer} delimiters")


def _split_top_level(text: str) -> list[str]:
    parts: list[str] = []
    start = 0
    depths = {"(": 0, "[": 0, "{": 0, "<": 0}
    pairs = {")": "(", "]": "[", "}": "{", ">": "<"}
    for index, char in enumerate(text):
        if char in depths:
            depths[char] += 1
        elif char in pairs and depths[pairs[char]]:
            depths[pairs[char]] -= 1
        elif char == "," and not any(depths.values()):
            parts.append(text[start:index].strip())
            start = index + 1
    tail = text[start:].strip()
    if tail:
        parts.append(tail)
    return parts


def _parameters(text: str) -> list[dict[str, Any]]:
    parameters: list[dict[str, Any]] = []
    for raw in _split_top_level(text):
        declaration = re.sub(r"\[[^\]]+\]\s*", "", raw).strip()
        optional = "=" in declaration
        declaration = declaration.split("=", 1)[0].strip()
        tokens = declaration.split()
        if len(tokens) < 2:
            raise ValueError(f"cannot parse C# parameter declaration: {raw!r}")
        name = tokens[-1]
        type_name = " ".join(token for token in tokens[:-1] if token not in {"in", "out", "ref", "params", "this"})
        parameters.append({"name": name, "type": re.sub(r"\s+", "", type_name), "optional": optional})
    return parameters


def _method_declarations(body: str) -> list[dict[str, Any]]:
    declarations: list[dict[str, Any]] = []
    method_re = re.compile(r"\b([A-Za-z_]\w*Async\w*)(\s*<[^(){};]+>)?\s*\(")
    for match in method_re.finditer(body):
        opening = match.end() - 1
        closing = _closing_delimiter(body, opening)
        terminator = body[closing + 1:].lstrip()
        if not terminator.startswith((";", "=>", "{")):
            continue
        generic = re.sub(r"\s+", "", match.group(2) or "")
        parameters = _parameters(body[opening + 1:closing])
        signature = f"{match.group(1)}{generic}({','.join(parameter['type'] for parameter in parameters)})"
        declarations.append({"name": match.group(1), "signature": signature, "parameters": parameters})
    return declarations


def _namespace(text: str) -> str:
    match = re.search(r"^\s*namespace\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)", text, re.M)
    return match.group(1) if match else ""


def _surface(path: Path) -> str:
    package = path.relative_to(SRC_ROOT).parts[0]
    return package.removeprefix("Honua.Sdk.").replace(".", "-").lower()


def _interface_ancestors(stripped_files: dict[Path, str]) -> dict[str, set[str]]:
    direct: dict[str, set[str]] = {}
    declaration = re.compile(r"\bpublic\s+interface\s+(IHonua\w*Client)\b([^\{]*)\{")
    for stripped in stripped_files.values():
        for match in declaration.finditer(stripped):
            direct[match.group(1)] = set(re.findall(r"\b(IHonua\w*Client)\b", match.group(2)))

    resolved: dict[str, set[str]] = {}

    def visit(interface: str, visiting: set[str]) -> set[str]:
        if interface in resolved:
            return resolved[interface]
        if interface in visiting:
            raise ValueError(f"cyclic client interface inheritance involving {interface}")
        ancestors = set(direct.get(interface, set()))
        for parent in tuple(ancestors):
            ancestors.update(visit(parent, visiting | {interface}))
        resolved[interface] = ancestors
        return ancestors

    for interface in direct:
        visit(interface, set())
    return resolved


def _declared_operations() -> list[dict[str, Any]]:
    source_files = sorted(SRC_ROOT.rglob("*.cs"))
    direct_implementations: set[str] = set()
    stripped_files: dict[Path, str] = {}
    for path in source_files:
        stripped = _strip_comments_and_strings(path.read_text(encoding="utf-8", errors="replace"))
        stripped_files[path] = stripped
        for match in re.finditer(r"\bclass\s+\w+(?:<[^>{}]+>)?\s*:\s*([^\{]+)\{", stripped):
            direct_implementations.update(re.findall(r"\b(IHonua\w*Client)\b", match.group(1)))

    ancestors = _interface_ancestors(stripped_files)
    implementations = set(direct_implementations)
    for interface in direct_implementations:
        implementations.update(ancestors.get(interface, set()))

    operations: dict[str, dict[str, Any]] = {}
    interface_re = re.compile(r"\bpublic\s+interface\s+(IHonua\w*Client)\b[^\{]*\{")
    for path, stripped in stripped_files.items():
        namespace = _namespace(stripped)
        for declaration in interface_re.finditer(stripped):
            interface = declaration.group(1)
            body = _block(stripped, declaration.end() - 1)
            methods = sorted(_method_declarations(body), key=lambda item: item["signature"])
            for method in methods:
                operation_id = f"{namespace}.{interface}.{method['signature']}"
                operations[operation_id] = {
                    "id": operation_id,
                    "surface": _surface(path),
                    "client": f"{namespace}.{interface}",
                    "operation": method["name"],
                    "signature": method["signature"],
                    "implemented": interface in implementations,
                    "_parameters": method["parameters"],
                }
    return [operations[key] for key in sorted(operations)]


def _expression_type_hint(expression: str) -> str | None:
    expression = expression.strip()
    if expression in {"true", "false"}:
        return "bool"
    if expression == '""':
        return "string"
    if expression.endswith(".Token") or "CancellationToken.None" in expression:
        return "CancellationToken"
    created = re.match(r"new\s+(?:[A-Za-z_]\w*\.)*([A-Za-z_]\w*)", expression)
    return created.group(1) if created else None


def _simple_type(type_name: str) -> str:
    type_name = type_name.rstrip("?")
    return re.sub(r"<.*", "", type_name).rsplit(".", 1)[-1]


def _matches_arguments(operation: dict[str, Any], arguments: list[str]) -> bool:
    parameters = operation["_parameters"]
    if len(arguments) < sum(not parameter["optional"] for parameter in parameters) or len(arguments) > len(parameters):
        return False
    positional = 0
    used: set[str] = set()
    for argument in arguments:
        named = re.match(r"^([A-Za-z_]\w*)\s*:\s*(.*)$", argument, re.S)
        if named:
            candidates = [parameter for parameter in parameters if parameter["name"] == named.group(1)]
            if not candidates or named.group(1) in used:
                return False
            parameter = candidates[0]
            expression = named.group(2)
            used.add(parameter["name"])
        else:
            while positional < len(parameters) and parameters[positional]["name"] in used:
                positional += 1
            if positional >= len(parameters):
                return False
            parameter = parameters[positional]
            expression = argument
            used.add(parameter["name"])
            positional += 1
        hint = _expression_type_hint(expression)
        if hint and hint != _simple_type(parameter["type"]):
            return False
    return all(parameter["optional"] or parameter["name"] in used for parameter in parameters)


def _test_mappings(operations: list[dict[str, Any]], ancestors: dict[str, set[str]]) -> dict[str, list[str]]:
    fixture_clients: dict[str, str] = {}
    test_files: list[Path] = []
    for root in TEST_ROOTS:
        test_files.extend(sorted(root.glob("*.cs")))
    stripped_files = {
        path: _strip_comments_and_strings(path.read_text(encoding="utf-8", errors="replace"))
        for path in test_files
    }
    for stripped in stripped_files.values():
        fixture_clients.update(
            (property_name, interface)
            for interface, property_name in re.findall(
                r"\bpublic\s+(IHonua\w*Client)\s+(\w+)\s*=>", stripped
            )
        )

    mappings: dict[str, set[str]] = defaultdict(set)
    by_interface_method: dict[tuple[str, str], list[dict[str, Any]]] = defaultdict(list)
    for operation in operations:
        by_interface_method[(operation["client"].rsplit(".", 1)[-1], operation["operation"])].append(operation)

    def record(interface: str, operation_name: str, arguments: list[str], test_name: str) -> None:
        interfaces = {interface, *ancestors.get(interface, set())}
        candidates = [
            operation
            for candidate_interface in interfaces
            for operation in by_interface_method.get((candidate_interface, operation_name), [])
            if _matches_arguments(operation, arguments)
        ]
        if len(candidates) == 1:
            mappings[candidates[0]["id"]].add(test_name)

    method_re = re.compile(r"\bpublic\s+(?:async\s+)?Task(?:<[^;{}]+>)?\s+(\w+)\s*\([^)]*\)\s*\{")
    class_re = re.compile(r"\bpublic\s+(?:sealed\s+)?class\s+(\w+)")
    for path, stripped in stripped_files.items():
        namespace = _namespace(stripped)
        for method_match in method_re.finditer(stripped):
            preceding = list(class_re.finditer(stripped, 0, method_match.start()))
            if not preceding:
                continue
            class_name = preceding[-1].group(1)
            test_name = f"{namespace}.{class_name}.{method_match.group(1)}"
            body = _block(stripped, method_match.end() - 1)
            local_clients = {
                variable: interface
                for variable, interface in re.findall(
                    r"\b(?:var|IHonua\w*Client)\s+(\w+)\s*=\s*(?:(?!;).)*?GetServices<\s*(IHonua\w*Client)\s*>",
                    body,
                    re.S,
                )
            }
            call_suffix = r"(\w*Async\w*)(?:\s*<[^(){};]+>)?\s*\("
            for call in re.finditer(r"_fixture\.(\w+)\." + call_suffix, body):
                property_name, operation = call.group(1), call.group(2)
                interface = fixture_clients.get(property_name)
                if interface:
                    closing = _closing_delimiter(body, call.end() - 1)
                    record(interface, operation, _split_top_level(body[call.end():closing]), test_name)
            for call in re.finditer(r"\b(\w+)\." + call_suffix, body):
                variable, operation = call.group(1), call.group(2)
                interface = local_clients.get(variable)
                if interface:
                    closing = _closing_delimiter(body, call.end() - 1)
                    record(interface, operation, _split_top_level(body[call.end():closing]), test_name)
    return {key: sorted(value) for key, value in mappings.items()}


def build_document() -> dict[str, Any]:
    stripped_files = {
        path: _strip_comments_and_strings(path.read_text(encoding="utf-8", errors="replace"))
        for path in sorted(SRC_ROOT.rglob("*.cs"))
    }
    ancestors = _interface_ancestors(stripped_files)
    operations = _declared_operations()
    mappings = _test_mappings(operations, ancestors)
    cells: list[dict[str, Any]] = []
    for operation in operations:
        short_client = operation["client"].rsplit(".", 1)[-1]
        short_id = f"{short_client}.{operation['operation']}"
        mapped_tests = set(mappings.get(operation["id"], []))
        unseeded_tests = sorted(mapped_tests & UNSEEDED_TESTS)
        tests = sorted(mapped_tests - UNSEEDED_TESTS)
        implemented = operation.pop("implemented")
        operation.pop("_parameters")
        if not implemented:
            status = "non-addressable"
            owner = RASTER_ISSUE if operation["surface"] == "abstractions" else TRACKING_ISSUE
            disposition = "Public provider-neutral contract has no concrete Honua client implementation."
            tiers: list[str] = []
        elif tests:
            status = "exercised"
            owner = None
            disposition = None
            tiers = ["nightly", "release"]
            if short_id in PR_OPERATIONS:
                tiers.insert(0, "pr")
        else:
            status = "gap"
            owner = TRACKING_ISSUE
            disposition = (
                "Canonical live test exists but its deterministic server fixture is not seeded."
                if unseeded_tests
                else "Concrete public SDK operation has no canonical live certification test."
            )
            tiers = ["nightly", "release"]

        facets = []
        if operation["operation"].startswith(MUTATION_PREFIXES):
            facets.append("mutation")
        else:
            facets.append("read-only")

        cell = {
            **operation,
            "status": status,
            "requiredTiers": tiers,
            "scenarioFacets": facets,
            "tests": tests,
        }
        if unseeded_tests:
            cell["unseededTests"] = unseeded_tests
        if owner:
            cell["ownerIssue"] = owner
        if disposition:
            cell["disposition"] = disposition
        cells.append(cell)

    counts = {status: sum(cell["status"] == status for cell in cells) for status in ("exercised", "gap", "non-addressable")}
    return {
        "schemaVersion": "1.0.0",
        "generator": "scripts/generate-sdk-certification.py",
        "sourceRepository": "honua-io/honua-sdk-dotnet",
        "trackingIssue": TRACKING_ISSUE,
        "complete": counts["gap"] == 0,
        "summary": {"total": len(cells), **counts},
        "operations": cells,
    }


def _render(document: dict[str, Any]) -> str:
    return json.dumps(document, indent=2, ensure_ascii=False) + "\n"


def _trx_results(paths: list[Path]) -> dict[str, list[str]]:
    results: dict[str, list[str]] = defaultdict(list)
    for path in paths:
        root = ET.parse(path).getroot()
        for result in root.iter():
            if result.tag.rsplit("}", 1)[-1] != "UnitTestResult":
                continue
            name = result.attrib.get("testName", "").split("(", 1)[0]
            outcome = result.attrib.get("outcome", "Unknown")
            if name:
                results[name].append(outcome)
    return results


def _identity(args: argparse.Namespace) -> dict[str, Any]:
    values = {
        "sdkCommit": args.sdk_commit or os.environ.get("GITHUB_SHA"),
        "sdkVersion": args.sdk_version or "unreleased",
        "serverSourceSha": args.server_source_sha,
        "imageSourceRevision": args.image_source_revision,
        "serverImage": args.server_image,
        "serverImageDigest": args.server_image.rsplit("@", 1)[-1] if "@" in args.server_image else None,
        "releaseCut": args.release_cut,
        "fixtureRevision": args.fixture_revision,
        "seedRevision": args.seed_revision,
    }
    if args.tier == "release":
        missing = [key for key, value in values.items() if not value]
        if missing:
            raise ValueError(f"release identity is missing: {', '.join(missing)}")
        if not re.fullmatch(r"[0-9a-f]{40}", args.server_source_sha or "", re.I):
            raise ValueError("release server source SHA must be a full 40-character commit")
        if args.seed_revision != args.server_source_sha:
            raise ValueError("release seed revision must exactly equal the server source SHA")
        if args.image_source_revision != args.server_source_sha:
            raise ValueError("verified image source revision must exactly equal the release server source SHA")
        if not re.search(r"@sha256:[0-9a-f]{64}$", args.server_image, re.I):
            raise ValueError("release server image must be immutable and addressed by sha256 digest")
        if not re.fullmatch(r"sha256:[0-9a-f]{64}", args.fixture_revision or "", re.I):
            raise ValueError("release fixture revision must be the SHA-256 of the applied fixture")
        try:
            cut = datetime.fromisoformat((args.release_cut or "").replace("Z", "+00:00"))
        except ValueError as error:
            raise ValueError("release cut must be an ISO-8601 timestamp") from error
        if cut.tzinfo is None or cut > datetime.now(timezone.utc):
            raise ValueError("release cut must be timezone-aware and not in the future")
    return values


def write_evidence(args: argparse.Namespace, document: dict[str, Any]) -> int:
    identity = _identity(args)
    results = _trx_results(args.trx)
    now = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    cells: list[dict[str, Any]] = []
    incomplete = False
    for operation in document["operations"]:
        if args.tier not in operation["requiredTiers"]:
            continue
        outcomes_by_test = {test: results.get(test, []) for test in operation["tests"]}
        outcomes = [outcome for test_outcomes in outcomes_by_test.values() for outcome in test_outcomes]
        if operation["status"] == "gap":
            verdict = "gap"
        elif any(not test_outcomes for test_outcomes in outcomes_by_test.values()):
            verdict = "missing"
        elif any(outcome == "Failed" for outcome in outcomes):
            verdict = "fail"
        elif all(any(outcome == "Passed" for outcome in test_outcomes) for test_outcomes in outcomes_by_test.values()):
            verdict = "pass"
        elif outcomes:
            verdict = "skip"
        else:
            verdict = "missing"
        incomplete |= verdict != "pass"
        cell = {
            "surface": operation["surface"],
            "operation": operation["id"],
            "canonicalClient": "Honua.Sdk.*",
            "canonicalClientVersion": identity["sdkVersion"],
            "deploymentTarget": identity["serverImage"],
            "scenarioFacets": operation["scenarioFacets"],
            "requiredTier": args.tier,
            "status": verdict,
            "tests": operation["tests"],
        }
        for key in ("ownerIssue", "disposition"):
            if key in operation:
                cell[key] = operation[key]
        cells.append(cell)

    evidence = {
        "schemaVersion": "1.0.0",
        "producer": "honua-io/honua-sdk-dotnet",
        "tier": args.tier,
        "complete": not incomplete,
        "startedAt": args.started_at or now,
        "completedAt": now,
        "generatedAt": now,
        "identity": identity,
        "matrixSha256": hashlib.sha256(_render(document).encode()).hexdigest(),
        "cells": cells,
    }
    args.evidence.parent.mkdir(parents=True, exist_ok=True)
    args.evidence.write_text(_render(evidence), encoding="utf-8")
    return 1 if incomplete else 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--tier", choices=("pr", "nightly", "release"))
    parser.add_argument("--trx", type=Path, action="append", default=[])
    parser.add_argument("--evidence", type=Path)
    parser.add_argument("--started-at")
    parser.add_argument("--sdk-commit")
    parser.add_argument("--sdk-version")
    parser.add_argument("--server-source-sha")
    parser.add_argument("--image-source-revision")
    parser.add_argument("--server-image", default="")
    parser.add_argument("--release-cut")
    parser.add_argument("--fixture-revision")
    parser.add_argument("--seed-revision")
    args = parser.parse_args(argv)

    try:
        document = build_document()
        rendered = _render(document)
        if args.check:
            if not OUTPUT_PATH.exists() or OUTPUT_PATH.read_text(encoding="utf-8") != rendered:
                print(f"::error::{OUTPUT_PATH} is stale; run scripts/generate-sdk-certification.py", file=sys.stderr)
                return 1
        elif args.evidence:
            if not args.tier or not args.trx:
                parser.error("--evidence requires --tier and at least one --trx")
            return write_evidence(args, document)
        else:
            OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
            OUTPUT_PATH.write_text(rendered, encoding="utf-8")
            print(f"Wrote {OUTPUT_PATH}: {document['summary']}")
    except (OSError, ValueError, ET.ParseError) as error:
        print(f"::error::{error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
