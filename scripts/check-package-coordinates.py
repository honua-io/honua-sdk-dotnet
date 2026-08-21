#!/usr/bin/env python3
"""Fail-closed NuGet coordinate preflight and public payload verifier."""
from __future__ import annotations

import argparse
import base64
import concurrent.futures
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from collections.abc import Callable, Iterable
from dataclasses import dataclass
from io import BytesIO
from pathlib import Path, PurePosixPath
from typing import Any


SCHEMA = "honua.nuget-coordinate-audit/v1"
MAX_HTTP_BYTES = 256 * 1024 * 1024
MAX_ARCHIVE_MEMBERS = 20_000
MAX_ARCHIVE_UNCOMPRESSED_BYTES = 512 * 1024 * 1024
IGNORED_EXACT = {
    ".signature.p7s",
}
IGNORED_PREFIXES = (
    "package/services/metadata/core-properties/",
    "package/services/digital-signature/",
)


class CoordinateError(ValueError):
    """Raised when registry state or a package archive is not trustworthy."""


class RegistryRedirectHandler(urllib.request.HTTPRedirectHandler):
    """Allow HTTPS redirects without forwarding registry credentials cross-host."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):  # type: ignore[no-untyped-def]
        redirected = super().redirect_request(req, fp, code, msg, headers, newurl)
        if redirected is None:
            return None
        old_url = urllib.parse.urlsplit(req.full_url)
        new_url = urllib.parse.urlsplit(newurl)
        if new_url.scheme != "https":
            raise CoordinateError("registry attempted a non-HTTPS redirect")
        if old_url.netloc.casefold() != new_url.netloc.casefold():
            redirected.remove_header("Authorization")
        return redirected


@dataclass(frozen=True)
class PackageArchive:
    package_id: str
    version: str
    raw_sha256: str
    semantic_sha256: str
    repository_commit: str | None


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _is_ignored_member(name: str) -> bool:
    lowered = name.lower()
    return lowered in IGNORED_EXACT or any(
        lowered.startswith(prefix) for prefix in IGNORED_PREFIXES
    )


def _canonical_container_xml(name: str, payload: bytes) -> bytes:
    try:
        root = ET.fromstring(payload)
    except ET.ParseError as exc:
        raise CoordinateError(f"package container metadata {name} is invalid XML: {exc}") from exc
    rows: list[dict[str, Any]] = []
    for child in root:
        attributes = {key.rsplit("}", 1)[-1]: value for key, value in child.attrib.items()}
        if (
            name.lower() == "[content_types].xml"
            and attributes.get("PartName", "").lower() == "/.signature.p7s"
        ):
            continue
        if (
            name.lower() == "_rels/.rels"
            and "digital-signature" in attributes.get("Type", "").lower()
        ):
            continue
        rows.append(
            {
                "tag": child.tag.rsplit("}", 1)[-1],
                "attributes": dict(sorted(attributes.items())),
                "text": (child.text or "").strip(),
            }
        )
    canonical = {
        "root": root.tag.rsplit("}", 1)[-1],
        "attributes": dict(
            sorted((key.rsplit("}", 1)[-1], value) for key, value in root.attrib.items())
        ),
        "children": sorted(rows, key=lambda row: json.dumps(row, sort_keys=True)),
    }
    return json.dumps(canonical, separators=(",", ":"), sort_keys=True).encode("utf-8")


def _semantic_member_payload(name: str, payload: bytes) -> bytes:
    if name.lower() in {"[content_types].xml", "_rels/.rels"}:
        return _canonical_container_xml(name, payload)
    return payload


def _safe_member_name(name: str) -> str:
    if not name or "\\" in name:
        raise CoordinateError(f"archive contains an invalid member name: {name!r}")
    path = PurePosixPath(name)
    if path.is_absolute() or ".." in path.parts:
        raise CoordinateError(f"archive contains an unsafe member name: {name!r}")
    return path.as_posix()


def _metadata_element(root: ET.Element, local_name: str) -> ET.Element | None:
    for element in root.iter():
        if element.tag.rsplit("}", 1)[-1] == local_name:
            return element
    return None


def inspect_package_bytes(value: bytes) -> PackageArchive:
    if len(value) > MAX_HTTP_BYTES:
        raise CoordinateError("package archive exceeds the 256 MiB validation limit")

    semantic_records: list[bytes] = []
    nuspecs: list[tuple[str, bytes]] = []
    seen_names: set[str] = set()
    total_uncompressed = 0

    try:
        archive = zipfile.ZipFile(BytesIO(value))
    except (OSError, zipfile.BadZipFile) as exc:
        raise CoordinateError(f"package is not a readable ZIP archive: {exc}") from exc

    with archive:
        members = archive.infolist()
        if len(members) > MAX_ARCHIVE_MEMBERS:
            raise CoordinateError("package archive has too many members")
        for member in members:
            name = _safe_member_name(member.filename)
            if member.is_dir():
                continue
            folded = name.casefold()
            if folded in seen_names:
                raise CoordinateError(f"package archive contains duplicate member {name!r}")
            seen_names.add(folded)
            if member.flag_bits & 0x1:
                raise CoordinateError(f"package archive contains encrypted member {name!r}")
            total_uncompressed += member.file_size
            if total_uncompressed > MAX_ARCHIVE_UNCOMPRESSED_BYTES:
                raise CoordinateError("package archive exceeds the uncompressed-size limit")
            payload = archive.read(member)
            if name.lower().endswith(".nuspec") and "/" not in name:
                nuspecs.append((name, payload))
            if _is_ignored_member(name):
                continue
            semantic_payload = _semantic_member_payload(name, payload)
            member_hash = sha256_bytes(semantic_payload)
            semantic_records.append(
                f"{len(name.encode('utf-8'))}:{name}\0{len(semantic_payload)}\0{member_hash}\n".encode(
                    "utf-8"
                )
            )

    if len(nuspecs) != 1:
        raise CoordinateError(f"expected exactly one root .nuspec, found {len(nuspecs)}")
    try:
        nuspec_root = ET.fromstring(nuspecs[0][1])
    except ET.ParseError as exc:
        raise CoordinateError(f"package .nuspec is invalid XML: {exc}") from exc
    package_id_element = _metadata_element(nuspec_root, "id")
    version_element = _metadata_element(nuspec_root, "version")
    if package_id_element is None or not (package_id_element.text or "").strip():
        raise CoordinateError("package .nuspec has no id")
    if version_element is None or not (version_element.text or "").strip():
        raise CoordinateError("package .nuspec has no version")
    repository_element = _metadata_element(nuspec_root, "repository")
    repository_commit = None
    if repository_element is not None:
        repository_commit = (repository_element.get("commit") or "").strip() or None

    return PackageArchive(
        package_id=(package_id_element.text or "").strip(),
        version=(version_element.text or "").strip(),
        raw_sha256=sha256_bytes(value),
        semantic_sha256=sha256_bytes(b"".join(sorted(semantic_records))),
        repository_commit=repository_commit,
    )


def inspect_package_file(path: Path) -> PackageArchive:
    try:
        return inspect_package_bytes(path.read_bytes())
    except OSError as exc:
        raise CoordinateError(f"cannot read package {path}: {exc}") from exc


def read_inventory(path: Path) -> list[str]:
    try:
        lines = path.read_text(encoding="utf-8").splitlines()
    except OSError as exc:
        raise CoordinateError(f"cannot read package inventory {path}: {exc}") from exc
    package_ids: list[str] = []
    seen: set[str] = set()
    for line_number, raw_line in enumerate(lines, start=1):
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue
        fields = [field.strip() for field in line.split("|")]
        if len(fields) != 2 or not fields[1]:
            raise CoordinateError(f"invalid inventory row at {path}:{line_number}")
        folded = fields[1].casefold()
        if folded in seen:
            raise CoordinateError(f"duplicate package id in inventory: {fields[1]}")
        seen.add(folded)
        package_ids.append(fields[1])
    if not package_ids:
        raise CoordinateError("package inventory is empty")
    return package_ids


def load_local_packages(
    package_dir: Path,
    inventory: Iterable[str],
    expected_version: str,
    expected_source_revision: str,
) -> dict[str, tuple[Path, PackageArchive]]:
    expected_ids = {package_id.casefold(): package_id for package_id in inventory}
    local: dict[str, tuple[Path, PackageArchive]] = {}
    for path in sorted(package_dir.glob("*.nupkg")):
        package = inspect_package_file(path)
        folded = package.package_id.casefold()
        if folded not in expected_ids:
            raise CoordinateError(f"unexpected package archive: {package.package_id}")
        if folded in local:
            raise CoordinateError(f"duplicate local package archive: {package.package_id}")
        if package.version != expected_version:
            raise CoordinateError(
                f"{package.package_id} has version {package.version}; expected {expected_version}"
            )
        if package.repository_commit != expected_source_revision:
            raise CoordinateError(
                f"{package.package_id} repository commit is "
                f"{package.repository_commit or '<missing>'}; expected {expected_source_revision}"
            )
        local[folded] = (path, package)
    missing = [expected_ids[key] for key in expected_ids.keys() - local.keys()]
    if missing:
        raise CoordinateError(f"local package set is incomplete: {', '.join(sorted(missing))}")
    return local


def inspect_symbol_package_bytes(value: bytes) -> tuple[PackageArchive, tuple[dict[str, Any], ...]]:
    package = inspect_package_bytes(value)
    portable_pdbs: list[dict[str, Any]] = []
    try:
        with zipfile.ZipFile(BytesIO(value)) as archive:
            for member in archive.infolist():
                name = _safe_member_name(member.filename)
                if member.is_dir() or not name.lower().endswith(".pdb"):
                    continue
                payload = archive.read(member)
                if not payload.startswith(b"BSJB"):
                    raise CoordinateError(
                        f"{package.package_id} contains non-portable PDB {name}"
                    )
                portable_pdbs.append(
                    {"path": name, "sha256": sha256_bytes(payload), "bytes": len(payload)}
                )
    except (OSError, zipfile.BadZipFile) as exc:
        raise CoordinateError(f"cannot inspect symbol package for {package.package_id}: {exc}") from exc
    if not portable_pdbs:
        raise CoordinateError(f"{package.package_id} symbol package has no PDB")
    return package, tuple(sorted(portable_pdbs, key=lambda row: row["path"]))


def load_symbol_packages(
    package_dir: Path,
    inventory: Iterable[str],
    expected_version: str,
    expected_source_revision: str,
) -> dict[str, tuple[Path, PackageArchive, tuple[dict[str, Any], ...]]]:
    expected_ids = {package_id.casefold(): package_id for package_id in inventory}
    symbols: dict[str, tuple[Path, PackageArchive, tuple[dict[str, Any], ...]]] = {}
    for path in sorted(package_dir.glob("*.snupkg")):
        try:
            package, portable_pdbs = inspect_symbol_package_bytes(path.read_bytes())
        except OSError as exc:
            raise CoordinateError(f"cannot read symbol package {path}: {exc}") from exc
        folded = package.package_id.casefold()
        if folded not in expected_ids:
            raise CoordinateError(f"unexpected symbol package archive: {package.package_id}")
        if folded in symbols:
            raise CoordinateError(f"duplicate local symbol package: {package.package_id}")
        if package.version != expected_version:
            raise CoordinateError(
                f"{package.package_id} symbol package has version {package.version}; "
                f"expected {expected_version}"
            )
        if package.repository_commit != expected_source_revision:
            raise CoordinateError(
                f"{package.package_id} symbol repository commit is "
                f"{package.repository_commit or '<missing>'}; expected {expected_source_revision}"
            )
        symbols[folded] = (path, package, portable_pdbs)
    missing = [expected_ids[key] for key in expected_ids.keys() - symbols.keys()]
    if missing:
        raise CoordinateError(f"local symbol package set is incomplete: {', '.join(sorted(missing))}")
    return symbols


def _read_bounded(response: Any, *, limit: int = MAX_HTTP_BYTES) -> bytes:
    value = response.read(limit + 1)
    if len(value) > limit:
        raise CoordinateError("registry response exceeds the validation limit")
    return value


def _request_bytes(
    url: str,
    *,
    headers: dict[str, str],
    timeout: int,
    limit: int,
    missing_is_none: bool,
) -> bytes | None:
    retryable_http = {408, 425, 429, 500, 502, 503, 504}
    last_error: BaseException | None = None
    for attempt in range(1, 4):
        request = urllib.request.Request(url, headers=headers)
        try:
            opener = urllib.request.build_opener(RegistryRedirectHandler())
            with opener.open(request, timeout=timeout) as response:  # noqa: S310
                return _read_bounded(response, limit=limit)
        except urllib.error.HTTPError as exc:
            if exc.code == 404 and missing_is_none:
                return None
            last_error = exc
            if exc.code not in retryable_http:
                raise CoordinateError(f"registry request failed with HTTP {exc.code}") from exc
        except (OSError, urllib.error.URLError) as exc:
            last_error = exc
        if attempt < 3:
            time.sleep(attempt * 2)
    if isinstance(last_error, urllib.error.HTTPError):
        raise CoordinateError(f"registry request failed with HTTP {last_error.code}") from last_error
    raise CoordinateError(f"registry request failed after three attempts: {last_error}") from last_error


def registry_headers(username: str | None, token: str | None) -> dict[str, str]:
    headers = {
        "User-Agent": "honua-sdk-coordinate-audit",
        "Cache-Control": "no-cache",
        "Pragma": "no-cache",
    }
    if token:
        if not username:
            raise CoordinateError("registry username is required when an access token is configured")
        encoded = base64.b64encode(f"{username}:{token}".encode()).decode("ascii")
        headers["Authorization"] = f"Basic {encoded}"
    return headers


def resolve_package_base_address(
    service_index: str,
    *,
    headers: dict[str, str],
) -> str:
    try:
        response_bytes = _request_bytes(
            service_index,
            headers=headers,
            timeout=60,
            limit=5 * 1024 * 1024,
            missing_is_none=False,
        )
        assert response_bytes is not None
        payload = json.loads(response_bytes)
    except (CoordinateError, json.JSONDecodeError) as exc:
        raise CoordinateError(f"registry service index is unreadable: {exc}") from exc
    resources = payload.get("resources") if isinstance(payload, dict) else None
    if not isinstance(resources, list):
        raise CoordinateError("registry service index has no resources array")
    candidates: list[str] = []
    for resource in resources:
        if not isinstance(resource, dict):
            continue
        resource_type = resource.get("@type")
        types = resource_type if isinstance(resource_type, list) else [resource_type]
        if any(isinstance(value, str) and value.startswith("PackageBaseAddress") for value in types):
            resource_id = resource.get("@id")
            if isinstance(resource_id, str) and resource_id.startswith("https://"):
                candidates.append(resource_id.rstrip("/") + "/")
    if len(candidates) != 1:
        raise CoordinateError(
            f"expected exactly one HTTPS PackageBaseAddress resource, found {len(candidates)}"
        )
    return candidates[0]


def package_download_url(package_base: str, package_id: str, version: str) -> str:
    safe_id = urllib.parse.quote(package_id.lower(), safe=".-_")
    safe_version = urllib.parse.quote(version.lower(), safe=".-_")
    return f"{package_base}{safe_id}/{safe_version}/{safe_id}.{safe_version}.nupkg"


def symbol_package_download_url(
    symbol_package_base: str,
    package_id: str,
    version: str,
) -> str:
    filename = f"{package_id}.{version}.snupkg"
    safe_filename = urllib.parse.quote(filename.lower(), safe=".-_")
    return f"{symbol_package_base.rstrip('/')}/{safe_filename}"


def fetch_registry_package(url: str, *, headers: dict[str, str]) -> bytes | None:
    return _request_bytes(
        url,
        headers=headers,
        timeout=120,
        limit=MAX_HTTP_BYTES,
        missing_is_none=True,
    )


def evaluate_coordinates(
    *,
    registry_name: str,
    package_base: str,
    package_version: str,
    source_revision: str,
    local_packages: dict[str, tuple[Path, PackageArchive]],
    fetch_package: Callable[[str], bytes | None],
    require_present: bool,
) -> tuple[dict[str, Any], list[Path]]:
    rows: list[dict[str, Any]] = []
    publish_paths: list[Path] = []
    failures: list[str] = []
    public_by_id: dict[str, bytes | None] = {}
    with concurrent.futures.ThreadPoolExecutor(
        max_workers=min(4, max(1, len(local_packages)))
    ) as executor:
        futures = {
            folded_id: executor.submit(
                fetch_package,
                package_download_url(
                    package_base,
                    package.package_id,
                    package_version,
                ),
            )
            for folded_id, (_, package) in local_packages.items()
        }
        for folded_id, future in futures.items():
            public_by_id[folded_id] = future.result()
    for folded_id in sorted(local_packages):
        path, local = local_packages[folded_id]
        public_bytes = public_by_id[folded_id]
        row: dict[str, Any] = {
            "packageId": local.package_id,
            "version": package_version,
            "local": {
                "filename": path.name,
                "rawSha256": local.raw_sha256,
                "semanticSha256": local.semantic_sha256,
                "repositoryCommit": local.repository_commit,
            },
        }
        if public_bytes is None:
            row["state"] = "absent"
            publish_paths.append(path)
            if require_present:
                failures.append(f"{local.package_id} {package_version} is absent")
        else:
            public = inspect_package_bytes(public_bytes)
            row["public"] = {
                "rawSha256": public.raw_sha256,
                "semanticSha256": public.semantic_sha256,
                "repositoryCommit": public.repository_commit,
            }
            if public.package_id.casefold() != local.package_id.casefold() or public.version != package_version:
                row["state"] = "divergent"
                failures.append(f"{local.package_id} returned mismatched package identity")
            elif public.semantic_sha256 != local.semantic_sha256:
                row["state"] = "divergent"
                failures.append(f"{local.package_id} {package_version} has divergent public payload")
            else:
                row["state"] = "present-identical"
        rows.append(row)

    result = {
        "schema": SCHEMA,
        "registry": registry_name,
        "packageBaseAddress": package_base,
        "packageVersion": package_version,
        "sourceRevision": source_revision,
        "mode": "verify" if require_present else "preflight",
        "status": "fail" if failures else "pass",
        "summary": {
            "total": len(rows),
            "absent": sum(row["state"] == "absent" for row in rows),
            "presentIdentical": sum(row["state"] == "present-identical" for row in rows),
            "divergent": sum(row["state"] == "divergent" for row in rows),
        },
        "packages": rows,
        "failures": failures,
    }
    return result, publish_paths


def evaluate_symbol_coordinates(
    *,
    symbol_package_base: str,
    package_version: str,
    local_symbols: dict[str, tuple[Path, PackageArchive, tuple[dict[str, Any], ...]]],
    fetch_package: Callable[[str], bytes | None],
    require_present: bool,
) -> tuple[list[dict[str, Any]], list[Path], list[str]]:
    rows: list[dict[str, Any]] = []
    publish_paths: list[Path] = []
    failures: list[str] = []
    public_by_id: dict[str, bytes | None] = {}
    with concurrent.futures.ThreadPoolExecutor(
        max_workers=min(4, max(1, len(local_symbols)))
    ) as executor:
        futures = {
            folded_id: executor.submit(
                fetch_package,
                symbol_package_download_url(
                    symbol_package_base,
                    package.package_id,
                    package_version,
                ),
            )
            for folded_id, (_, package, _) in local_symbols.items()
        }
        for folded_id, future in futures.items():
            public_by_id[folded_id] = future.result()

    for folded_id in sorted(local_symbols):
        path, local, local_pdbs = local_symbols[folded_id]
        public_bytes = public_by_id[folded_id]
        row: dict[str, Any] = {
            "packageId": local.package_id,
            "version": package_version,
            "local": {
                "filename": path.name,
                "rawSha256": local.raw_sha256,
                "semanticSha256": local.semantic_sha256,
                "repositoryCommit": local.repository_commit,
                "portablePdbs": list(local_pdbs),
            },
        }
        if public_bytes is None:
            row["state"] = "absent"
            publish_paths.append(path)
            if require_present:
                failures.append(f"{local.package_id} {package_version} symbols are absent")
        else:
            public, public_pdbs = inspect_symbol_package_bytes(public_bytes)
            row["public"] = {
                "rawSha256": public.raw_sha256,
                "semanticSha256": public.semantic_sha256,
                "repositoryCommit": public.repository_commit,
                "portablePdbs": list(public_pdbs),
            }
            if public.package_id.casefold() != local.package_id.casefold() or public.version != package_version:
                row["state"] = "divergent"
                failures.append(f"{local.package_id} returned mismatched symbol package identity")
            elif public.semantic_sha256 != local.semantic_sha256:
                row["state"] = "divergent"
                failures.append(
                    f"{local.package_id} {package_version} has divergent public symbol payload"
                )
            else:
                row["state"] = "present-identical"
        rows.append(row)
    return rows, publish_paths, failures


def local_symbol_evidence(
    local_symbols: dict[str, tuple[Path, PackageArchive, tuple[dict[str, Any], ...]]],
) -> list[dict[str, Any]]:
    return [
        {
            "packageId": package.package_id,
            "version": package.version,
            "state": "local-only",
            "local": {
                "filename": path.name,
                "rawSha256": package.raw_sha256,
                "semanticSha256": package.semantic_sha256,
                "repositoryCommit": package.repository_commit,
                "portablePdbs": list(portable_pdbs),
            },
        }
        for _, (path, package, portable_pdbs) in sorted(local_symbols.items())
    ]


def write_json(path: Path, value: dict[str, Any]) -> None:
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--registry-name", required=True)
    parser.add_argument("--service-index", required=True)
    parser.add_argument("--username")
    parser.add_argument("--token-env")
    parser.add_argument("--package-dir", required=True, type=Path)
    parser.add_argument("--inventory", required=True, type=Path)
    parser.add_argument("--package-version", required=True)
    parser.add_argument("--source-revision", required=True)
    parser.add_argument("--symbol-package-base-address")
    parser.add_argument("--evidence-out", required=True, type=Path)
    parser.add_argument("--publish-list-out", type=Path)
    parser.add_argument("--symbol-publish-list-out", type=Path)
    parser.add_argument("--require-present", action="store_true")
    parser.add_argument("--attempts", type=int, default=1)
    parser.add_argument("--delay-seconds", type=float, default=0)
    args = parser.parse_args(argv)

    if not args.source_revision or len(args.source_revision) != 40 or any(
        char not in "0123456789abcdef" for char in args.source_revision
    ):
        parser.error("--source-revision must be a full lowercase 40-character Git SHA")
    if args.attempts < 1 or args.attempts > 60:
        parser.error("--attempts must be between 1 and 60")
    if args.delay_seconds < 0 or args.delay_seconds > 300:
        parser.error("--delay-seconds must be between 0 and 300")
    if args.symbol_publish_list_out and not args.symbol_package_base_address:
        parser.error("--symbol-publish-list-out requires --symbol-package-base-address")
    if args.symbol_package_base_address and not args.symbol_package_base_address.startswith("https://"):
        parser.error("--symbol-package-base-address must use HTTPS")
    token = None
    if args.token_env:
        token = os.environ.get(args.token_env, "").strip()
        if not token:
            parser.error(f"environment variable {args.token_env} is required")

    try:
        headers = registry_headers(args.username, token)
        package_base = resolve_package_base_address(args.service_index, headers=headers)
        inventory = read_inventory(args.inventory)
        local = load_local_packages(
            args.package_dir,
            inventory,
            args.package_version,
            args.source_revision,
        )
        symbols = load_symbol_packages(
            args.package_dir,
            inventory,
            args.package_version,
            args.source_revision,
        )
        result: dict[str, Any] | None = None
        publish_paths: list[Path] = []
        symbol_publish_paths: list[Path] = []
        for attempt in range(1, args.attempts + 1):
            result, publish_paths = evaluate_coordinates(
                registry_name=args.registry_name,
                package_base=package_base,
                package_version=args.package_version,
                source_revision=args.source_revision,
                local_packages=local,
                fetch_package=lambda url: fetch_registry_package(url, headers=headers),
                require_present=args.require_present,
            )
            result["attempt"] = attempt
            if args.symbol_package_base_address:
                symbol_rows, symbol_publish_paths, symbol_failures = evaluate_symbol_coordinates(
                    symbol_package_base=args.symbol_package_base_address,
                    package_version=args.package_version,
                    local_symbols=symbols,
                    fetch_package=lambda url: fetch_registry_package(
                        url,
                        headers=registry_headers(None, None),
                    ),
                    require_present=args.require_present,
                )
                result["symbolPackageBaseAddress"] = args.symbol_package_base_address
                result["symbolPackages"] = symbol_rows
                result["failures"].extend(symbol_failures)
                result["summary"]["symbols"] = {
                    "total": len(symbol_rows),
                    "absent": sum(row["state"] == "absent" for row in symbol_rows),
                    "presentIdentical": sum(
                        row["state"] == "present-identical" for row in symbol_rows
                    ),
                    "divergent": sum(row["state"] == "divergent" for row in symbol_rows),
                }
                result["status"] = "fail" if result["failures"] else "pass"
            else:
                result["symbolPackages"] = local_symbol_evidence(symbols)
            if result["status"] == "pass":
                break
            if any(row["state"] == "divergent" for row in result["packages"]) or any(
                row["state"] == "divergent" for row in result["symbolPackages"]
            ):
                break
            if attempt < args.attempts:
                time.sleep(args.delay_seconds)
        assert result is not None
        write_json(args.evidence_out, result)
        if args.publish_list_out:
            args.publish_list_out.write_text(
                "".join(f"{path.as_posix()}\n" for path in publish_paths), encoding="utf-8"
            )
        if args.symbol_publish_list_out:
            args.symbol_publish_list_out.write_text(
                "".join(f"{path.as_posix()}\n" for path in symbol_publish_paths),
                encoding="utf-8",
            )
        for failure in result["failures"]:
            print(f"::error::{failure}")
        print(
            f"::{ 'notice' if result['status'] == 'pass' else 'error' }::"
            f"{args.registry_name}: {result['summary']}"
        )
        return 0 if result["status"] == "pass" else 1
    except CoordinateError as exc:
        failure = {
            "schema": SCHEMA,
            "registry": args.registry_name,
            "packageVersion": args.package_version,
            "sourceRevision": args.source_revision,
            "mode": "verify" if args.require_present else "preflight",
            "status": "fail",
            "failures": [str(exc)],
        }
        write_json(args.evidence_out, failure)
        print(f"::error::{exc}")
        return 1


if __name__ == "__main__":
    sys.exit(main())
