#!/usr/bin/env python3
"""Generate and evaluate the public .NET SDK operation certification ledger."""

from __future__ import annotations

import argparse
import base64
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
from urllib.parse import quote, urljoin, urlsplit

sys.path.insert(0, str(Path(__file__).resolve().parent))
import certification_entitlement  # noqa: E402


ROOT = Path(__file__).resolve().parents[1]
SRC_ROOT = ROOT / "src"
TEST_ROOTS = (
    ROOT / "tests" / "Honua.Sdk.ProtocolIntegration.Tests",
    ROOT / "tests" / "Honua.Sdk.Conformance.Tests",
)
OUTPUT_PATH = ROOT / "contracts" / "sdk-certification.v1.json"
TRACKING_ISSUE = "https://github.com/honua-io/honua-sdk-dotnet/issues/31"
RASTER_ISSUE = "https://github.com/honua-io/honua-sdk-dotnet/issues/294"
RC_FIXTURE_GAP_ISSUE = "https://github.com/honua-io/honua-sdk-dotnet/issues/308"
RELEASE_PROFILE_ID = "honua-sdk-dotnet-2026.1"
CERTIFICATION_CLIENT_ID = "Honua SDK .NET"
CERTIFICATION_RUNNER_LANE = "sdk-dotnet-certification"
PRODUCER_REPOSITORY = "honua-io/honua-sdk-dotnet"
PRODUCER_WORKFLOW = "sdk-certification.yml"
NIGHTLY_DISPATCH_ID = re.compile(r"nightly-certification-[1-9][0-9]*-[1-9][0-9]*")
FEATURESERVER_EDIT_ENTITLEMENT_POLICY = "honua-pro-featureserver-edits-v1"

IMPLEMENTATION_TEST_PROVIDERS = {
    "Honua.Sdk.ProtocolIntegration.Tests.DestructiveProtocolIntegrationTests."
    "FeatureServerApplyEdits_AddUpdateDelete_RoundTrips": frozenset({"geoservices-featureserver"}),
    "Honua.Sdk.ProtocolIntegration.Tests.FeatureProtocolIntegrationTests."
    "SourceFacade_QueriesConfiguredFeatureProtocols": frozenset(
        {"grpc", "geoservices-featureserver", "wfs", "ogc-features"}
    ),
}

PR_OPERATIONS = frozenset(
    {
        "IHonuaGrpcClient.QueryFeaturesAsync",
        "IHonuaFeatureServerClient.GetServiceInfoAsync",
        "IHonuaFeatureServerClient.GetLayerInfoAsync",
        "HonuaFeatureServerClient.GetServiceMetadataAsync",
        "HonuaFeatureServerClient.GetLayerMetadataAsync",
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
    "Add", "Apply", "Cancel", "Connect", "Create", "Delete", "Deprovision", "Dismiss",
    "Commit", "Disconnect", "Execute", "Import", "Patch", "Pause", "Publish", "Remove",
    "Reconnect", "Reopen", "Resume", "Revoke", "Rollback", "Rotate", "Set", "Start", "Submit",
    "Subscribe", "Synchronize", "Trigger", "Unpublish", "UnRegister", "Unsubscribe", "Update", "Upload",
)
PAGINATED_OPERATIONS = frozenset(
    {
        "GetFeaturesAsyncEnumerable",
        "GetItemsPagesAsync",
        "GetRecordsPagesAsync",
        "PostSearchPagesAsync",
        "QueryPagesAsync",
        "SearchPagesAsync",
    }
)

# Missing deterministic fixtures remain release obligations. A skipped or missing
# live result is incomplete qualification, never an exclusion from the denominator.
RC_UNAVAILABLE_TESTS: frozenset[str] = frozenset()


RC_UNAVAILABLE_OPERATIONS = frozenset(
    {
        "Honua.Sdk.Abstractions.Features.IHonuaFeatureStreamClient.SubscribeAsync(FeatureStreamSubscribeRequest,CancellationToken)",
    }
)


def _certification_protocol_context(operation: dict[str, Any]) -> tuple[str, str]:
    """Return the wire-contract version and profile exercised by an SDK operation."""
    client = operation["client"]
    surface = operation["surface"]
    if surface == "grpc":
        return "1.0", "protobuf/gRPC"
    if "Geocoding" in client or surface == "geoservices":
        return "11.0", "GeoServices REST"
    if ".Wfs." in client:
        return "2.0.0", "core"
    if ".Styles." in client:
        return "1.0", "OGC API - Styles core"
    if surface == "ogcfeatures":
        return "1.0", "OGC API - Features core"
    if ".Records." in client:
        return "1.0", "OGC API - Records core"
    if ".Stac." in client:
        return "1.0.0", "STAC core"
    profiles = {
        "abstractions": "provider-neutral SDK contract",
        "admin": "Honua Admin REST",
        "catalogs": "catalog REST",
        "consoleshare": "Honua Console Share REST",
        "processes": "OGC API - Processes core",
        "spec": "Honua Spec REST",
        "studio": "Honua Studio REST",
    }
    try:
        return "1.0", profiles[surface]
    except KeyError as error:
        raise ValueError(f"no certification protocol context for surface {surface!r}") from error


def _certification_request_url(
    operation: dict[str, Any], identity: dict[str, Any]
) -> str | None:
    """Build the operation URL from the target context recorded for the live run."""
    client = operation["client"]
    method = operation["operation"]
    service = quote(identity["serviceId"], safe="")
    layer = quote(str(identity["layerId"]), safe="")
    collection = quote(identity["collectionId"], safe="")
    base_url = identity["baseUrl"]

    if operation["surface"] == "spec" or client.endswith(".IHonuaSpecClient"):
        action = {"ValidateAsync": "validate", "PlanAsync": "plan", "ApplyAsync": "apply"}.get(method)
        if action is None:
            return None
        path = f"/v1/spec/{action}"
    elif client.endswith(".IHonuaRoutingClient") or ".Routing.HonuaRoutingClient" in client:
        routes = {"GetServiceMetadataAsync": ("routeLayer", ""),
                  "GetDirectionsAsync": ("routeLayer", "/solve"),
                  "GetServiceAreaAsync": ("serviceAreaLayer", "/solveServiceArea"),
                  "FindClosestFacilityAsync": ("closestFacilityLayer", "/solveClosestFacility")}
        if method not in routes:
            return None
        layer_key, suffix = routes[method]
        targets = identity.get("operationTargets", {})
        if not targets.get("routeServiceId") or not targets.get(layer_key):
            raise ValueError("routing request evidence requires the explicit live fixture service and layer identities")
        route_service = quote(targets["routeServiceId"], safe="")
        route_layer = quote(targets[layer_key], safe="")
        path = f"/rest/services/{route_service}/NAServer/{route_layer}{suffix}"
    elif client.endswith(".IHonuaSceneClient"):
        path = "/api/scenes"
        if method in {"GetSceneAsync", "ResolveSceneAsync"}:
            scene_id = identity.get("operationTargets", {}).get("sceneId")
            if not scene_id:
                raise ValueError("scene request evidence requires the explicit live fixture scene identity")
            path += "/" + quote(scene_id, safe="")
            if method == "ResolveSceneAsync":
                path += "/resolve"
        elif method != "ListScenesAsync":
            return None
    elif operation["surface"] == "abstractions":
        # A facade can fan out to several protocols. Record one representative endpoint
        # that its proving integration test actually exercises; leave unproved families unset.
        if client.endswith(".IHonuaFeatureQueryClient") and method == "QueryAsync":
            path = f"/rest/services/{service}/FeatureServer/{layer}/query"
        elif client.endswith(".IHonuaFeatureEditClient") and method == "ApplyEditsAsync":
            path = f"/rest/services/{service}/FeatureServer/{layer}/applyEdits"
        else:
            return None
    elif operation["surface"] == "grpc":
        service_name = "ProcessService" if "ProcessGrpc" in client else "FeatureService"
        rpc_name = method.removesuffix("Async")
        path = f"/geospatial.v1.{service_name}/{rpc_name}"
        base_url = identity["grpcBaseUrl"]
    elif "Geocoding" in client:
        action = {
            "ForwardGeocodeAsync": "findAddressCandidates",
            "ReverseGeocodeAsync": "reverseGeocode",
            "SuggestAsync": "suggest",
        }.get(method, "geocodeAddresses")
        path = f"/rest/services/World/GeocodeServer/{action}"
    elif operation["surface"] == "geoservices":
        if ".GeometryServer." in client:
            path = f"/rest/services/Utilities/Geometry/GeometryServer/{method.removesuffix('Async').lower()}"
        elif ".ImageServer." in client:
            path = f"/rest/services/{service}/ImageServer"
        elif ".Routing." in client:
            path = f"/rest/services/{service}/NAServer/Route"
        else:
            action = {
                "GetServiceInfoAsync": "",
                "GetLayerInfoAsync": f"/{layer}",
                "GetServiceMetadataAsync": "",
                "GetLayerMetadataAsync": f"/{layer}",
                "GetEditCapabilitiesAsync": f"/{layer}",
                "GetFeatureAsync": f"/{layer}/query",
                "AddFeaturesAsync": f"/{layer}/addFeatures",
                "UpdateFeaturesAsync": f"/{layer}/updateFeatures",
                "DeleteFeaturesAsync": f"/{layer}/deleteFeatures",
                "ApplyEditsAsync": f"/{layer}/applyEdits",
            }.get(method, f"/{layer}/query")
            path = f"/rest/services/{service}/FeatureServer{action}"
    elif ".Wfs." in client:
        path = "/wfs"
    elif ".Styles." in client:
        path = "/ogc/styles"
    elif operation["surface"] == "ogcfeatures":
        path = {
            "GetLandingPageAsync": "/ogc/features",
            "GetConformanceAsync": "/ogc/features/conformance",
            "ListCollectionsAsync": "/ogc/features/collections",
            "GetCollectionAsync": f"/ogc/features/collections/{collection}",
            "GetQueryablesAsync": f"/ogc/features/collections/{collection}/queryables",
        }.get(method, f"/ogc/features/collections/{collection}/items")
    elif ".Records." in client:
        path = "/ogc/records"
    elif ".Stac." in client:
        path = "/stac"
    elif operation["surface"] == "admin":
        path = {
            "ListServicesAsync": "/api/v1/admin/services/",
            "GetServiceAsync": "/api/v1/admin/services/",
            "GetServiceSettingsAsync": f"/api/v1/admin/services/{service}/settings",
            "CheckCompatibilityAsync": "/api/v1/admin/capabilities",
        }.get(method, "/api/v1/admin")
    else:
        prefixes = {
            "catalogs": "/stac",
            "consoleshare": "/api/v1/console",
            "processes": "/ogc/processes",
            "studio": "/api/v1/studio",
        }
        path = prefixes[operation["surface"]]

    request_url = urljoin(base_url.rstrip("/") + "/", path)
    parsed = urlsplit(request_url)
    if parsed.scheme not in {"http", "https"} or not parsed.netloc:
        raise ValueError("certification request context must use absolute HTTP(S) base URLs")
    return request_url


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


def _method_declarations(body: str, *, require_public: bool = False) -> list[dict[str, Any]]:
    declarations: list[dict[str, Any]] = []
    method_re = re.compile(r"\b([A-Za-z_]\w*Async\w*)(\s*<[^(){};]+>)?\s*\(")
    for match in method_re.finditer(body):
        if require_public:
            boundary = max(
                body.rfind("{", 0, match.start()),
                body.rfind("}", 0, match.start()),
                body.rfind(";", 0, match.start()),
            )
            prefix = body[boundary + 1:match.start()]
            if not re.search(r"\bpublic\s+(?:async\s+)?[^;{}()=]+\s+$", prefix):
                continue
        prefix = body[:match.start()]
        if prefix.count("{") != prefix.count("}") or prefix.rstrip().endswith("=>"):
            continue
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


def _semantic_signature(method: dict[str, Any]) -> tuple[str, tuple[str, ...]]:
    """Compare C# signatures without namespace/alias spelling differences."""
    method_head = method["signature"].split("(", 1)[0]

    def normalize(type_name: str) -> str:
        without_alias = type_name.replace("global::", "")
        return re.sub(r"(?:\b[A-Za-z_]\w*\.)+([A-Za-z_]\w*)", r"\1", without_alias)

    parameters = method["parameters"] if "parameters" in method else method["_parameters"]
    return method_head, tuple(normalize(parameter["type"]) for parameter in parameters)


def _namespace(text: str) -> str:
    match = re.search(r"^\s*namespace\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)", text, re.M)
    return match.group(1) if match else ""


def _surface(path: Path, src_root: Path = SRC_ROOT) -> str:
    package = path.relative_to(src_root).parts[0]
    return package.removeprefix("Honua.Sdk.").replace(".", "-").lower()


def _interface_ancestors(stripped_files: dict[Path, str]) -> dict[str, set[str]]:
    direct: dict[str, set[str]] = {}
    declaration = re.compile(r"\bpublic\s+interface\s+(I\w*Client)\b([^\{]*)\{")
    for stripped in stripped_files.values():
        for match in declaration.finditer(stripped):
            direct[match.group(1)] = set(re.findall(r"\b(I\w*Client)\b", match.group(2)))

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


def _declared_operations(src_root: Path = SRC_ROOT) -> list[dict[str, Any]]:
    source_files = sorted(src_root.rglob("*.cs"))
    direct_implementations: dict[str, set[str]] = defaultdict(set)
    implementation_providers: dict[str, str] = {}
    stripped_files: dict[Path, str] = {}
    for path in source_files:
        source = path.read_text(encoding="utf-8", errors="replace")
        stripped = _strip_comments_and_strings(source)
        stripped_files[path] = stripped
        namespace = _namespace(stripped)
        provider = re.search(r'\bProviderName\s*=>\s*"([^"]+)"', source)
        for match in re.finditer(r"\bclass\s+(\w+)(?:<[^>{}]+>)?\s*:\s*([^\{]+)\{", stripped):
            implementation = f"{namespace}.{match.group(1)}"
            interfaces = re.findall(r"\b(I\w*Client)\b", match.group(2))
            for interface in interfaces:
                direct_implementations[interface].add(implementation)
            if provider:
                implementation_providers[implementation] = provider.group(1)

    ancestors = _interface_ancestors(stripped_files)
    implementations: dict[str, set[str]] = defaultdict(set)
    for interface, concrete_types in direct_implementations.items():
        implementations[interface].update(concrete_types)
        for ancestor in ancestors.get(interface, set()):
            implementations[ancestor].update(concrete_types)

    operations: dict[str, dict[str, Any]] = {}
    interface_re = re.compile(r"\bpublic\s+interface\s+(I\w*Client)\b[^\{]*\{")
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
                    "surface": _surface(path, src_root),
                    "client": f"{namespace}.{interface}",
                    "operation": method["name"],
                    "signature": method["signature"],
                    "implemented": bool(implementations.get(interface)),
                    "_implementations": sorted(implementations.get(interface, set())),
                    "_implementationProviders": {
                        implementation: implementation_providers.get(implementation)
                        for implementation in sorted(implementations.get(interface, set()))
                    },
                    "_parameters": method["parameters"],
                }
    for path, stripped in stripped_files.items():
        namespace = _namespace(stripped)
        concrete_re = re.compile(r"\bpublic\s+(?:sealed\s+)?class\s+(\w+Client)\b([^\{]*)\{")
        for declaration in concrete_re.finditer(stripped):
            concrete = declaration.group(1)
            implementation = f"{namespace}.{concrete}"
            represented_signatures = {
                _semantic_signature(operation)
                for operation in operations.values()
                if implementation in operation["_implementations"]
            }
            body = _block(stripped, declaration.end() - 1)
            for method in sorted(
                _method_declarations(body, require_public=True), key=lambda item: item["signature"]
            ):
                if _semantic_signature(method) in represented_signatures:
                    continue
                operation_id = f"{namespace}.{concrete}.{method['signature']}"
                operations[operation_id] = {
                    "id": operation_id,
                    "surface": _surface(path, src_root),
                    "client": f"{namespace}.{concrete}",
                    "operation": method["name"],
                    "signature": method["signature"],
                    "implemented": True,
                    "_implementations": [implementation],
                    "_implementationProviders": {
                        implementation: implementation_providers.get(implementation)
                    },
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


def _test_mappings(
    operations: list[dict[str, Any]], ancestors: dict[str, set[str]]
) -> tuple[dict[str, list[str]], dict[str, dict[str, list[str]]]]:
    facade_delegations = {
        ("HonuaSource", "QueryAsync"): ("IHonuaFeatureQueryClient", "QueryAsync"),
    }
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
                r"\bpublic\s+(I\w*Client|\w+Client)\s+(\w+)\s*=>", stripped
            )
        )

    mappings: dict[str, set[str]] = defaultdict(set)
    implementation_mappings: dict[str, dict[str, set[str]]] = defaultdict(lambda: defaultdict(set))
    by_interface_method: dict[tuple[str, str], list[dict[str, Any]]] = defaultdict(list)
    for operation in operations:
        by_interface_method[(operation["client"].rsplit(".", 1)[-1], operation["operation"])].append(operation)

    def add_mapping(operation: dict[str, Any], test_name: str) -> None:
        mappings[operation["id"]].add(test_name)
        implementations = operation["_implementations"]
        providers = IMPLEMENTATION_TEST_PROVIDERS.get(test_name, frozenset())
        if len(implementations) == 1:
            implementation_mappings[operation["id"]][implementations[0]].add(test_name)
            return
        for implementation in implementations:
            if operation["_implementationProviders"].get(implementation) in providers:
                implementation_mappings[operation["id"]][implementation].add(test_name)

    def record(interface: str, operation_name: str, arguments: list[str], test_name: str) -> None:
        interfaces = {interface, *ancestors.get(interface, set())}
        candidates = [
            operation
            for candidate_interface in interfaces
            for operation in by_interface_method.get((candidate_interface, operation_name), [])
            if _matches_arguments(operation, arguments)
        ]
        if len(candidates) == 1:
            add_mapping(candidates[0], test_name)

    def record_facade(interface: str, operation_name: str, test_name: str) -> None:
        interfaces = {interface, *ancestors.get(interface, set())}
        candidates = [
            operation
            for candidate_interface in interfaces
            for operation in by_interface_method.get((candidate_interface, operation_name), [])
        ]
        if len(candidates) == 1:
            add_mapping(candidates[0], test_name)

    method_re = re.compile(r"\bpublic\s+(?:async\s+)?Task(?:<[^;{}]+>)?\s+(\w+)\s*\([^)]*\)\s*\{")
    class_re = re.compile(r"\bpublic\s+(?:sealed\s+)?class\s+(\w+)")
    for path, stripped in stripped_files.items():
        namespace = _namespace(stripped)
        file_clients = {
            variable: client
            for client, variable in re.findall(
                r"\b(?:private|protected|internal|public)\s+(?:readonly\s+)?(\w+Client)\s+(\w+)",
                stripped,
            )
        }
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
                    r"\b(?:var|I\w*Client)\s+(\w+)\s*=\s*(?:(?!;).)*?GetServices<\s*(I\w*Client)\s*>",
                    body,
                    re.S,
                )
            }
            local_clients.update(file_clients)
            local_clients.update({
                variable: client
                for variable, client in re.findall(
                    r"\bvar\s+(\w+)\s*=\s*new\s+(\w+Client)\s*\(", body
                )
            })
            local_facades = {
                variable: facade
                for facade, variable in re.findall(r"\b(Honua\w+)\s+(\w+)\s+in\b", body)
                if any(known_facade == facade for known_facade, _ in facade_delegations)
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
                    continue
                delegation = facade_delegations.get((local_facades.get(variable, ""), operation))
                if delegation:
                    record_facade(*delegation, test_name)
    return (
        {key: sorted(value) for key, value in mappings.items()},
        {
            operation_id: {
                implementation: sorted(tests)
                for implementation, tests in sorted(by_implementation.items())
            }
            for operation_id, by_implementation in implementation_mappings.items()
        },
    )


def build_document(src_root: Path = SRC_ROOT) -> dict[str, Any]:
    """Build the operation matrix from the client source under ``src_root``.

    The default is this checkout's ``src``. Evidence for an installed package passes that
    package's own source tree, so the operations certified are the ones the package ships
    (Release #767), not newer trunk-only operations the governed catalog cannot know.
    """
    stripped_files = {
        path: _strip_comments_and_strings(path.read_text(encoding="utf-8", errors="replace"))
        for path in sorted(src_root.rglob("*.cs"))
    }
    ancestors = _interface_ancestors(stripped_files)
    operations = _declared_operations(src_root)
    mappings, implementation_mappings = _test_mappings(operations, ancestors)
    cells: list[dict[str, Any]] = []
    for operation in operations:
        short_client = operation["client"].rsplit(".", 1)[-1]
        short_id = f"{short_client}.{operation['operation']}"
        mapped_tests = set(mappings.get(operation["id"], []))
        unavailable_tests = sorted(mapped_tests & RC_UNAVAILABLE_TESTS)
        tests = sorted(mapped_tests - RC_UNAVAILABLE_TESTS)
        implemented = operation.pop("implemented")
        implementations = operation.pop("_implementations")
        operation.pop("_implementationProviders")
        operation.pop("_parameters")
        tests_by_implementation = {
            implementation: sorted(
                set(implementation_mappings.get(operation["id"], {}).get(implementation, []))
                - RC_UNAVAILABLE_TESTS
            )
            for implementation in implementations
        }
        missing_implementations = [
            implementation
            for implementation, implementation_tests in tests_by_implementation.items()
            if not implementation_tests
        ]
        if not implemented:
            status = "non-addressable"
            owner = (
                RC_FIXTURE_GAP_ISSUE
                if unavailable_tests or operation["id"] in RC_UNAVAILABLE_OPERATIONS
                else RASTER_ISSUE if short_client == "IHonuaRasterDataClient" else TRACKING_ISSUE
            )
            disposition = "Public provider-neutral contract has no concrete Honua client implementation."
            tiers: list[str] = []
        elif tests and not missing_implementations:
            status = "exercised"
            owner = None
            disposition = None
            tiers = ["nightly", "release"]
            if short_id in PR_OPERATIONS:
                tiers.insert(0, "pr")
        else:
            status = "gap"
            owner = (
                RC_FIXTURE_GAP_ISSUE
                if unavailable_tests or operation["id"] in RC_UNAVAILABLE_OPERATIONS
                else RASTER_ISSUE if short_client == "IHonuaRasterDataClient" else TRACKING_ISSUE
            )
            if missing_implementations and tests:
                disposition = (
                    "Canonical live tests do not cover every concrete implementation: "
                    + ", ".join(missing_implementations)
                    + "."
                )
            else:
                disposition = (
                    "The pinned 2026.1 candidate does not provision this deterministic release fixture."
                    if unavailable_tests
                    else "The pinned 2026.1 candidate does not expose a realtime certification transport fixture."
                    if operation["id"] in RC_UNAVAILABLE_OPERATIONS
                    else "Concrete public SDK operation has no canonical live certification test."
                )
            tiers = ["nightly", "release"]

        excluded_from_release = bool(
            unavailable_tests or operation["id"] in RC_UNAVAILABLE_OPERATIONS
        )
        if excluded_from_release and "release" in tiers:
            tiers.remove("release")

        facets = []
        if operation["operation"].startswith(MUTATION_PREFIXES):
            facets.append("mutation")
        else:
            facets.append("read-only")
        if operation["operation"] in PAGINATED_OPERATIONS:
            facets.append("pagination")

        cell = {
            **operation,
            "status": status,
            "requiredTiers": tiers,
            "scenarioFacets": facets,
            "tests": tests,
            "implementations": implementations,
            "implementationTests": tests_by_implementation,
            "releaseDenominator": (
                "non-addressable"
                if not implemented
                else "excluded" if excluded_from_release else "included"
            ),
        }
        if missing_implementations:
            cell["missingImplementations"] = missing_implementations
        if unavailable_tests:
            cell["unavailableTests"] = unavailable_tests
        if owner:
            cell["ownerIssue"] = owner
        if disposition:
            cell["disposition"] = disposition
        cells.append(cell)

    counts = {status: sum(cell["status"] == status for cell in cells) for status in ("exercised", "gap", "non-addressable")}
    release_exclusions = [
        {
            "operationId": cell["id"],
            "ownerIssue": cell["ownerIssue"],
            "reason": cell["disposition"],
        }
        for cell in cells
        if cell["releaseDenominator"] == "excluded"
    ]
    release_denominator = [
        cell for cell in cells if cell["releaseDenominator"] == "included"
    ]
    return {
        "schemaVersion": "1.0.0",
        "generator": "scripts/generate-sdk-certification.py",
        "sourceRepository": "honua-io/honua-sdk-dotnet",
        "trackingIssue": TRACKING_ISSUE,
        "complete": counts["gap"] == 0,
        "summary": {"total": len(cells), **counts},
        "releaseProfile": {
            "id": RELEASE_PROFILE_ID,
            "policy": (
                "All concrete public client operations except the explicitly enumerated "
                "2026.1 candidate-fixture exclusions below. Exclusions are unsupported by "
                "this release profile, remain owned gaps, and are not passing evidence."
            ),
            "denominator": len(release_denominator),
            "excluded": release_exclusions,
        },
        "operations": cells,
    }


def _render(document: dict[str, Any]) -> str:
    return json.dumps(document, indent=2, ensure_ascii=False) + "\n"


def _trx_results(
    paths: list[Path], *, require_summary: bool = False
) -> tuple[dict[str, list[str]], dict[Path, dict[str, list[str]]]]:
    results: dict[str, list[str]] = defaultdict(list)
    results_by_path: dict[Path, dict[str, list[str]]] = {}
    for path in paths:
        root = ET.parse(path).getroot()
        summary_outcomes: list[str] = []
        counter_blocks: list[dict[str, int]] = []
        path_results: dict[str, list[str]] = defaultdict(list)
        for result in root.iter():
            element_name = result.tag.rsplit("}", 1)[-1]
            outcome = result.attrib.get("outcome", "Unknown")
            if element_name == "ResultSummary":
                summary_outcomes.append(outcome)
            if element_name == "Counters":
                counter_blocks.append({
                    name: int(result.attrib.get(name, "0"))
                    for name in ("error", "timeout", "aborted", "disconnected", "inProgress", "pending")
                })
            if element_name == "RunInfo" and outcome == "Aborted":
                raise ValueError(f"TRX records an infrastructure failure in {path.name}: {outcome}")
            if element_name != "UnitTestResult":
                continue
            name = result.attrib.get("testName", "").split("(", 1)[0]
            if outcome not in {"Passed", "Failed", "NotExecuted"}:
                raise ValueError(f"TRX records an unsupported outcome in {path.name}: {outcome}")
            if name:
                results[name].append(outcome)
                path_results[name].append(outcome)
        if require_summary and len(summary_outcomes) != 1:
            raise ValueError(f"TRX must contain exactly one ResultSummary in {path.name}")
        if require_summary and len(counter_blocks) != 1:
            raise ValueError(f"TRX must contain exactly one Counters element in {path.name}")
        invalid_summaries = set(summary_outcomes) - {"Completed", "Failed"}
        if invalid_summaries:
            raise ValueError(
                f"TRX records an incomplete run in {path.name}: {', '.join(sorted(invalid_summaries))}"
            )
        infrastructure_counts = counter_blocks[0] if len(counter_blocks) == 1 else {}
        nonzero_infrastructure = {
            name: count for name, count in infrastructure_counts.items() if count != 0
        }
        if nonzero_infrastructure:
            details = ", ".join(
                f"{name}={count}" for name, count in sorted(nonzero_infrastructure.items())
            )
            raise ValueError(f"TRX records infrastructure counters in {path.name}: {details}")
        results_by_path[path] = path_results
    return results, results_by_path


def _identity(args: argparse.Namespace) -> dict[str, Any]:
    values = {
        "sdkCommit": args.sdk_commit or os.environ.get("GITHUB_SHA"),
        "sdkVersion": args.sdk_version or "unreleased",
        "sdkPackageId": getattr(args, "sdk_package_id", None),
        "sdkPackageDigest": getattr(args, "sdk_package_digest", None),
        "sdkPackageSourceSha": getattr(args, "sdk_package_source_sha", None),
        "serverSourceSha": args.server_source_sha,
        "imageSourceRevision": args.image_source_revision,
        "serverImage": args.server_image,
        "serverImageDigest": args.server_image.rsplit("@", 1)[-1] if "@" in args.server_image else None,
        "releaseCut": args.release_cut,
        "candidateCut": args.candidate_cut,
        "fixtureRevision": args.fixture_revision,
        "seedRevision": args.seed_revision,
        "evidenceUri": args.evidence_uri,
        "baseUrl": getattr(args, "base_url", None) or os.environ.get("HONUA_PROTOCOL_EXTERNAL_BASE_URL"),
        "grpcBaseUrl": getattr(args, "grpc_base_url", None) or os.environ.get("HONUA_PROTOCOL_GRPC_BASE_URL"),
        "serviceId": getattr(args, "service_id", None) or os.environ.get("HONUA_PROTOCOL_SERVICE_NAME", "test_service"),
        "layerId": getattr(args, "layer_id", None) or os.environ.get("HONUA_PROTOCOL_LAYER_ID", "0"),
        "collectionId": getattr(args, "collection_id", None) or os.environ.get("HONUA_PROTOCOL_OGC_COLLECTION_ID", "0"),
        "operationTargets": {
            key: getattr(args, argument, None) or os.environ.get(environment)
            for key, argument, environment in (
                ("routeServiceId", "route_service_id", "HONUA_PROTOCOL_ROUTE_SERVICE_ID"),
                ("routeLayer", "route_layer", "HONUA_PROTOCOL_ROUTE_LAYER"),
                ("serviceAreaLayer", "service_area_layer", "HONUA_PROTOCOL_SERVICE_AREA_LAYER"),
                ("closestFacilityLayer", "closest_facility_layer", "HONUA_PROTOCOL_CLOSEST_FACILITY_LAYER"),
                ("sceneId", "scene_id", "HONUA_PROTOCOL_SCENE_ID"),
            )
        },
    }
    if args.tier == "release" and not re.fullmatch(
        r"ghcr\.io/honua-io/honua-server@sha256:[0-9a-f]{64}", args.server_image or ""
    ):
        raise ValueError(
            "release server image must be digest-addressed and must not use a floating nightly tag"
        )
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
        if not re.fullmatch(r"sha256:[0-9a-f]{64}", args.fixture_revision or "", re.I):
            raise ValueError("release fixture revision must be the SHA-256 of the applied fixture")
        if values["sdkPackageId"] != "Honua.Sdk":
            raise ValueError("release client package must be Honua.Sdk")
        if not re.fullmatch(r"sha256:[0-9a-f]{64}", values["sdkPackageDigest"] or ""):
            raise ValueError("release client package digest must be a SHA-256")
        if not re.fullmatch(r"[0-9a-f]{40}", values["sdkPackageSourceSha"] or ""):
            raise ValueError("release client package source SHA must be a full commit")
        try:
            cut = datetime.fromisoformat((args.release_cut or "").replace("Z", "+00:00"))
        except ValueError as error:
            raise ValueError("release cut must be an ISO-8601 timestamp") from error
        if cut.tzinfo is None or cut > datetime.now(timezone.utc):
            raise ValueError("release cut must be timezone-aware and not in the future")
    return values


def _unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    value: dict[str, Any] = {}
    for key, item in pairs:
        if key in value:
            raise ValueError(f"duplicate requirements key: {key}")
        value[key] = item
    return value


def _requirements(args: argparse.Namespace) -> tuple[dict[str, Any], dict[tuple[str, ...], dict[str, Any]]]:
    path = getattr(args, "requirements", None)
    revision = getattr(args, "requirements_revision", None)
    if path is None or not isinstance(revision, str) or not revision.strip():
        raise ValueError("evidence requires retained --requirements and --requirements-revision")
    data = Path(path).read_bytes()
    if not 0 < len(data) <= 32 * 1024 * 1024:
        raise ValueError("requirements catalog exceeds its bounded size")
    catalog = json.loads(data, object_pairs_hook=_unique_object)
    if (not isinstance(catalog, dict)
            or catalog.get("schema") != "honua.protocol-certification-requirements/v1"
            or catalog.get("receipt_schema_min") != "v2" or catalog.get("complete") is not True
            or catalog.get("revision") != revision or not isinstance(catalog.get("requirements"), list)):
        raise ValueError("requirements must be a complete v2 catalog at the expected revision")
    fields = ("capability_key", "surface", "operation", "canonical_client", "client_version", "deployment_target")
    index: dict[tuple[str, ...], dict[str, Any]] = {}
    for row in catalog["requirements"]:
        if isinstance(row, dict) and row.get("client_lane") != CERTIFICATION_RUNNER_LANE:
            continue
        if not isinstance(row, dict) or any(not isinstance(row.get(key), str) or not row[key] for key in fields):
            raise ValueError("requirements contain an invalid row identity")
        key = tuple(row[field] for field in fields)
        if key in index:
            raise ValueError("requirements contain duplicate operation identities")
        if (row.get("maturity") not in {"supported", "preview", "experimental", "roadmap", "deprecated", "internal"}
                or row.get("required_tier") not in {"pr", "nightly", "release"}):
            raise ValueError("requirements contain invalid governed context")
        index[key] = row
    return catalog, index


def _contract_revision(identity: dict[str, Any]) -> str:
    # Release #767: the installed package defines the client contract. The
    # independent harness checkout is producer_source_sha outside the nightly; a nightly
    # dispatch names it harness_commit and binds producer_source_sha to the package source.
    source = identity.get("sdkPackageSourceSha")
    if not isinstance(source, str) or not re.fullmatch(r"[0-9a-f]{40}", source):
        raise ValueError("certification contract requires the exact installed package source commit")
    return f"sdk-dotnet-certification@{source}"


def _governed_context(index: dict[tuple[str, ...], dict[str, Any]], operation: dict[str, Any],
                      identity: dict[str, Any], facets: list[str]) -> dict[str, Any]:
    key = (f"sdk-dotnet.{operation['surface']}", operation["surface"], operation["id"],
           CERTIFICATION_CLIENT_ID, identity["sdkVersion"], "local-docker")
    row = index.get(key)
    if row is None:
        raise ValueError(f"requirements have no exact published-client operation identity: {operation['id']}")
    _catalog_licensing(row)
    if (row.get("client_lane") != CERTIFICATION_RUNNER_LANE or row.get("addressable_by_client") is not True
            or row.get("contract_revision") != _contract_revision(identity)
            or row.get("auth_policy_revision") != "api-key-protected-v1"
            or row.get("fixture_revision") != identity["fixtureRevision"]
            or not isinstance(row.get("scenario_facets"), list) or set(row["scenario_facets"]) != set(facets)):
        raise ValueError(f"requirements disagree with the actual operation contract: {operation['id']}")
    return row


def _catalog_licensing(row: dict[str, Any]) -> tuple[bool, str | None]:
    """The pinned honua-release catalog is the only source of a row's licensed state.

    The producer never derives licensing from the client type: 2026.1 runs with licensing
    disabled, so the governed FeatureServer edit rows are unlicensed there. A licensed row
    must name the one entitlement policy this producer can prove (certification_entitlement).
    """
    licensed, policy = row.get("licensed"), row.get("entitlement_policy_revision")
    if not isinstance(licensed, bool) or (policy is not None and not isinstance(policy, str)):
        raise ValueError(f"requirements carry malformed licensing: {row.get('operation')}")
    if licensed != (policy is not None):
        raise ValueError(f"requirements licensed state and entitlement policy disagree: {row.get('operation')}")
    if licensed and policy != FEATURESERVER_EDIT_ENTITLEMENT_POLICY:
        raise ValueError(f"requirements name an entitlement policy this producer cannot prove: {policy}")
    return licensed, policy


def _entitlement_observation(args: argparse.Namespace) -> Any:
    path = getattr(args, "entitlement_observation", None)
    if path is None:
        raise ValueError("no retained entitlement observation was supplied")
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except OSError as error:
        raise ValueError(f"entitlement observation is unreadable: {error}") from error


def _producer_run() -> dict[str, Any] | None:
    """The Actions run that observed the cells, only when honua-release's nightly dispatched it.

    certification/RECEIPT-V2.md "Nightly producer run identity": the nightly binds every
    executed receipt to the run it recorded, so a dispatch id outside the canonical
    workflow_dispatch of this workflow in this repository is refused rather than recorded.
    """
    dispatch = os.environ.get("HONUA_NIGHTLY_DISPATCH_ID", "")
    if not dispatch:
        return None
    workflow_ref = f"{PRODUCER_REPOSITORY}/.github/workflows/{PRODUCER_WORKFLOW}@"
    if (NIGHTLY_DISPATCH_ID.fullmatch(dispatch) is None
            or os.environ.get("GITHUB_ACTIONS") != "true"
            or os.environ.get("GITHUB_EVENT_NAME") != "workflow_dispatch"
            or os.environ.get("GITHUB_REPOSITORY") != PRODUCER_REPOSITORY
            or re.fullmatch(re.escape(workflow_ref) + r"refs/(?:heads|tags)/[A-Za-z0-9._/-]+",
                            os.environ.get("GITHUB_WORKFLOW_REF", "")) is None):
        raise ValueError("nightly dispatch identity requires the canonical Actions sdk-certification dispatch")
    run_id, attempt = os.environ.get("GITHUB_RUN_ID", ""), os.environ.get("GITHUB_RUN_ATTEMPT", "")
    if not re.fullmatch(r"[1-9][0-9]*", run_id) or not re.fullmatch(r"[1-9][0-9]*", attempt):
        raise ValueError("nightly dispatch identity requires a positive Actions run id and attempt")
    return {"repository": PRODUCER_REPOSITORY, "workflow": PRODUCER_WORKFLOW,
            "run_id": int(run_id), "run_attempt": int(attempt), "dispatch_id": dispatch}


def _dispatched_identity(identity: dict[str, Any]) -> dict[str, Any]:
    """Receipt identity a nightly-dispatched run adds (ruling sdk-harness-trunk-pinned-package-2026-10-27).

    The nightly runs the harness from this repository's trunk against the pinned published
    package, so the two are named separately: harness_commit is the checkout that ran
    (GITHUB_SHA), producer_source_sha is the published package's source, and the package
    name and nupkg digest ride inside the hashed identity so a consumer can bind them to
    the manifest pin.
    """
    harness = os.environ.get("GITHUB_SHA", "")
    if not re.fullmatch(r"[0-9a-f]{40}", harness) or identity["sdkCommit"] != harness:
        raise ValueError("nightly dispatch identity requires the harness checkout GITHUB_SHA as a full commit")
    if identity["sdkPackageId"] != "Honua.Sdk":
        raise ValueError("nightly dispatch identity requires the installed Honua.Sdk package")
    digest = identity["sdkPackageDigest"]
    if not isinstance(digest, str) or not re.fullmatch(r"sha256:[0-9a-f]{64}", digest):
        raise ValueError("nightly dispatch identity requires the installed package sha256:<hex> digest")
    source = identity["sdkPackageSourceSha"]
    if not isinstance(source, str) or not re.fullmatch(r"[0-9a-f]{40}", source):
        raise ValueError("nightly dispatch identity requires the installed package source commit")
    return {"harness_commit": harness, "producer_source_sha": source,
            "client_package": identity["sdkPackageId"], "client_package_digest": digest}


def write_evidence(args: argparse.Namespace, document: dict[str, Any]) -> int:
    identity = _identity(args)
    producer_run = _producer_run()
    dispatched = None if producer_run is None else _dispatched_identity(identity)
    producer_source_sha = identity["sdkCommit"] if dispatched is None else dispatched["producer_source_sha"]
    catalog, requirements = _requirements(args)
    for field in ("baseUrl", "grpcBaseUrl"):
        parsed = urlsplit(identity[field] or "")
        if parsed.scheme not in {"http", "https"} or not parsed.netloc:
            raise ValueError(f"{field} must be an absolute HTTP(S) URL")
    supplied_exit_codes = getattr(args, "trx_exit_code", None)
    results, results_by_path = _trx_results(
        args.trx, require_summary=supplied_exit_codes is not None
    )
    exit_codes = supplied_exit_codes
    if exit_codes is None:
        exit_codes = [0] * len(args.trx)
    elif len(exit_codes) != len(args.trx):
        raise ValueError("each TRX must have exactly one corresponding exit code")
    governed_tests = {
        test
        for operation in document["operations"]
        if args.tier in operation["requiredTiers"]
        for test in operation["tests"]
    }
    for path, exit_code in zip(args.trx, exit_codes, strict=True):
        if exit_code == 0:
            continue
        failed_tests = {
            name
            for name, outcomes in results_by_path[path].items()
            if "Failed" in outcomes
        }
        if not failed_tests or not failed_tests <= governed_tests:
            raise ValueError(
                f"test command failed without exclusively governed assertion failures in {path.name}"
            )
    payload_base64 = base64.b64encode(json.dumps(
        [
            {
                "name": path.name,
                "content_base64": base64.b64encode(path.read_bytes()).decode("ascii"),
            }
            for path in sorted(args.trx, key=lambda value: str(value))
        ],
        sort_keys=True, separators=(",", ":"),
    ).encode("utf-8")).decode("ascii")
    now = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
    observations: list[dict[str, Any]] = []
    has_nonpass = False
    has_incomplete_execution = False
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
        elif all(outcome == "Passed" for outcome in outcomes):
            verdict = "pass"
        elif outcomes:
            verdict = "skip"
        else:
            verdict = "missing"
        has_incomplete_execution |= (
            operation["status"] != "gap"
            and (verdict == "missing" or any(outcome == "NotExecuted" for outcome in outcomes))
        )
        has_nonpass |= verdict != "pass"
        result = verdict if verdict in {"pass", "fail"} else "skip"
        skip_reason = None if result != "skip" else (
            operation.get("ownerIssue") or operation.get("disposition") or verdict
        )
        scenario_facets = list(dict.fromkeys(
            "positive" if facet == "read-only" else facet
            for facet in operation["scenarioFacets"]
        ))
        governed = _governed_context(requirements, operation, identity, scenario_facets)
        started_at = args.started_at or now
        contract_revision = _contract_revision(identity)
        protocol_version, protocol_profile = _certification_protocol_context(operation)
        exercised_capabilities = scenario_facets if result == "pass" else []
        receipt_facets = {facet: result for facet in scenario_facets}
        licensed, entitlement_policy_revision = _catalog_licensing(governed)
        entitlement_assertion = None
        if licensed and result == "pass":
            # A green TRX or deployment setting cannot synthesize the live
            # entitlement/fingerprint assertion required by the strict consumer.
            # Only a retained, re-validated signed release observation can.
            try:
                if args.tier != "release":
                    raise ValueError("rehearsal entitlement is not release proof")
                entitlement_assertion = certification_entitlement.verified_entitlement(
                    _entitlement_observation(args), capability_key=f"sdk-dotnet.{operation['surface']}",
                    started_at=started_at, completed_at=now)
            except (ValueError, AssertionError) as refusal:
                entitlement_refusal = str(refusal)
        if licensed and result == "pass" and entitlement_assertion is None:
            result, verdict = "skip", "missing-entitlement-evidence"
            skip_reason = (f"missing retained live entitlement/fingerprint proof ({entitlement_refusal}); "
                           + RC_FIXTURE_GAP_ISSUE)
            exercised_capabilities = []
            receipt_facets = {facet: "skip" for facet in scenario_facets}
            has_nonpass = has_incomplete_execution = True
        try:
            request_url = _certification_request_url(operation, identity)
        except ValueError:
            # A skipped row never sent a request, so an unconfigured fixture target
            # (routing, scenes) makes no request claim. Executed rows still require it.
            if result != "skip":
                raise
            request_url = None
        evidence_receipt = None if result == "skip" else {
            "schema": "honua.certification-evidence-receipt/v2",
            "identity": {
                "capability_key": f"sdk-dotnet.{operation['surface']}",
                "surface": operation["surface"],
                "operation": operation["id"],
                "canonical_client": CERTIFICATION_CLIENT_ID,
                "client_version": identity["sdkVersion"],
                "deployment_target": "local-docker",
                "source_sha": identity["serverSourceSha"],
                "producer_source_sha": producer_source_sha,
                "image_digest": identity["serverImageDigest"],
                "fixture_revision": identity["fixtureRevision"],
                "contract_revision": contract_revision,
                "auth_policy_revision": "api-key-protected-v1",
                "maturity": governed["maturity"],
                "required_tier": governed["required_tier"],
                "requirements_revision": catalog["revision"],
                "started_at": started_at,
                "completed_at": now,
                **({"producer_run": producer_run} if producer_run is not None else {}),
                **({"harness_commit": dispatched["harness_commit"],
                    "client_package": dispatched["client_package"],
                    "client_package_digest": dispatched["client_package_digest"]}
                   if dispatched is not None else {}),
            },
            "result": result,
            "facets": receipt_facets,
            "payload_base64": payload_base64,
            **({"entitlement": entitlement_assertion} if entitlement_assertion is not None else {}),
        }
        evidence_digest = None if evidence_receipt is None else "sha256:" + hashlib.sha256(
            json.dumps(
                evidence_receipt, sort_keys=True, separators=(",", ":"), ensure_ascii=False
            ).encode("utf-8")
        ).hexdigest()
        observation = {
            "surface": operation["surface"],
            "operation": operation["id"],
            "scenario_facets": scenario_facets,
            "canonical_client": CERTIFICATION_CLIENT_ID,
            "client_id": CERTIFICATION_CLIENT_ID,
            "runner_lane": CERTIFICATION_RUNNER_LANE,
            "protocol_version": protocol_version,
            "protocol_profile": protocol_profile,
            "performed_by": CERTIFICATION_CLIENT_ID,
            "request_url": request_url,
            "exercised_capabilities": exercised_capabilities,
            "client_version": identity["sdkVersion"],
            "client_package": identity["sdkPackageId"],
            "client_package_digest": identity["sdkPackageDigest"],
            "client_package_source_sha": identity["sdkPackageSourceSha"],
            "deployment_target": "local-docker",
            "result": result,
            "skip_reason": skip_reason,
            "source_sha": identity["serverSourceSha"],
            "producer_source_sha": producer_source_sha,
            "image_digest": identity["serverImageDigest"],
            "fixture_revision": identity["fixtureRevision"],
            "contract_revision": contract_revision,
            "auth_policy_revision": "api-key-protected-v1",
            "licensed": licensed,
            "entitlement_policy_revision": entitlement_policy_revision,
            "evidence_uri": (
                None if result == "skip"
                else f"https://evidence.honua.io/data/sha256/{evidence_digest[7:]}"
            ),
            "evidence_digest": None if result == "skip" else evidence_digest,
            "evidence_receipt": None if result == "skip" else evidence_receipt,
            "facet_results": None if result == "skip" else {
                facet: {"result": receipt_facets[facet], "evidence_digest": evidence_digest}
                for facet in scenario_facets
            },
            "started_at": started_at,
            "completed_at": now,
        }
        observations.append(observation)

    evidence = {
        "schema": "honua.protocol-certification-fragment/v1",
        "producer": "honua-sdk-dotnet",
        "generated_at": now,
        "candidate": {
            "source_sha": identity["serverSourceSha"],
            "image_digest": identity["serverImageDigest"],
            "cut_at": identity["candidateCut"],
        },
        "client_artifact": {
            "package_id": identity["sdkPackageId"],
            "package_version": identity["sdkVersion"],
            "package_digest": identity["sdkPackageDigest"],
            "source_sha": identity["sdkPackageSourceSha"],
        },
        "operation_scope": {
            "complete": True,
            "owner_issue": TRACKING_ISSUE,
            "matrix_sha256": hashlib.sha256(_render(document).encode()).hexdigest(),
            "release_profile": document.get("releaseProfile", {}).get("id"),
            "denominator": document.get("releaseProfile", {}).get("denominator"),
            "excluded_operations": [
                exclusion["operationId"]
                for exclusion in document.get("releaseProfile", {}).get("excluded", [])
            ],
        },
        "observations": observations,
    }
    args.evidence.parent.mkdir(parents=True, exist_ok=True)
    args.evidence.write_text(_render(evidence), encoding="utf-8")
    if has_incomplete_execution:
        return 1
    return 1 if has_nonpass and not getattr(args, "allow_nonpass", False) else 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--tier", choices=("pr", "nightly", "release"))
    parser.add_argument("--trx", type=Path, action="append", default=[])
    parser.add_argument("--trx-exit-code", type=int, action="append", default=[])
    parser.add_argument("--evidence", type=Path)
    parser.add_argument("--requirements", type=Path, help="Retained immutable full governed requirements catalog.")
    parser.add_argument("--requirements-revision", help="Independently expected governed catalog revision.")
    parser.add_argument("--started-at")
    parser.add_argument("--sdk-commit")
    parser.add_argument("--sdk-version")
    parser.add_argument("--sdk-package-id")
    parser.add_argument("--sdk-package-digest")
    parser.add_argument("--sdk-package-source-sha")
    parser.add_argument(
        "--operations-source-root", type=Path,
        help="Evidence only: the installed package's own src tree (at --sdk-package-source-sha). "
             "Operations are declared from it so the certified set is the one the package ships.",
    )
    parser.add_argument("--server-source-sha")
    parser.add_argument("--image-source-revision")
    parser.add_argument("--server-image", default="")
    parser.add_argument("--release-cut")
    parser.add_argument("--candidate-cut")
    parser.add_argument("--fixture-revision")
    parser.add_argument("--seed-revision")
    parser.add_argument("--evidence-uri")
    parser.add_argument("--base-url")
    parser.add_argument("--grpc-base-url")
    parser.add_argument("--service-id")
    parser.add_argument("--layer-id")
    parser.add_argument("--collection-id")
    parser.add_argument(
        "--entitlement-observation", type=Path,
        help="Retained signed release entitlement observation; without it licensed passes stay skipped.",
    )
    parser.add_argument(
        "--allow-nonpass",
        action="store_true",
        help="Publish valid fail/skip observations without failing the producer job.",
    )
    args = parser.parse_args(argv)

    try:
        operations_root = args.operations_source_root
        if operations_root is not None:
            if args.check or not args.evidence:
                parser.error("--operations-source-root applies only to --evidence")
            if not operations_root.is_dir() or not any(operations_root.rglob("*.cs")):
                raise ValueError(f"operations source root has no C# sources: {operations_root}")
        document = build_document(operations_root or SRC_ROOT)
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
