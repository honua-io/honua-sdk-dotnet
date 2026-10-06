#!/usr/bin/env python3
"""Prepare a clean test consumer from an immutable published Honua.Sdk package.

certification/candidate-pins.json is the only place the certified SDK package and
server candidate are written down. The workflow reads it through --emit-env, the
restore reads it directly, and --check verifies it against nuget.org and the
honua-release platform manifest. --follow-manifest copies a published manifest
package coordinate into the pin file after the nuget.org bytes match it.
"""

from __future__ import annotations

import argparse
import base64
import binascii
import gzip
import hashlib
import io
import json
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path
from typing import Any, Protocol


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_PINS = ROOT / "certification" / "candidate-pins.json"
SHA256_RE = re.compile(r"sha256:[0-9a-f]{64}")
SHA_RE = re.compile(r"[0-9a-f]{40}")
VERSION_RE = re.compile(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?")
DIGEST_IMAGE_RE = re.compile(r"ghcr\.io/honua-io/honua-server@sha256:[0-9a-f]{64}")
SERVER_REPOSITORY = "ghcr.io/honua-io/honua-server"
NUGET_ORG = "https://api.nuget.org/v3/index.json"
RELEASE_MANIFEST = "repos/honua-io/honua-release/contents/platform-manifest.yaml?ref=trunk"
FIXTURE_PATH = "tests/seed/base-schema.sql"
PIN_FIELDS = (
    "sdkPackageId",
    "sdkPackageVersion",
    "sdkPackageRegistry",
    "sdkPackageSha512",
    "sdkPackageDigest",
    "sdkSourceSha",
    "serverSourceSha",
    "serverImageDigest",
    "fixtureRevision",
)


def validate_pins(pins: dict[str, Any]) -> None:
    missing = [field for field in PIN_FIELDS if not isinstance(pins.get(field), str) or not pins[field]]
    if missing:
        raise ValueError(f"candidate pin file is missing: {', '.join(missing)}")
    unknown = sorted(set(pins) - set(PIN_FIELDS))
    if unknown:
        raise ValueError(f"candidate pin file has unknown fields: {', '.join(unknown)}")
    if pins["sdkPackageId"] != "Honua.Sdk":
        raise ValueError("release certification requires package ID Honua.Sdk")
    if pins["sdkPackageRegistry"] != "nuget.org":
        raise ValueError("certification installs the bytes published on nuget.org; sdkPackageRegistry must be nuget.org")
    if not VERSION_RE.fullmatch(pins["sdkPackageVersion"]):
        raise ValueError("package version must be an exact semantic version")
    try:
        sha512 = base64.b64decode(pins["sdkPackageSha512"], validate=True)
    except binascii.Error as error:
        raise ValueError("sdkPackageSha512 must be the base64 SHA-512 nuget.org records") from error
    if len(sha512) != 64:
        raise ValueError("sdkPackageSha512 must be the base64 SHA-512 nuget.org records")
    for field in ("sdkPackageDigest", "serverImageDigest", "fixtureRevision"):
        if not SHA256_RE.fullmatch(pins[field]):
            raise ValueError(f"{field} must be a lowercase sha256:<hex> digest")
    for field in ("sdkSourceSha", "serverSourceSha"):
        if not SHA_RE.fullmatch(pins[field]):
            raise ValueError(f"{field} must be a full lowercase commit")


def load_pins(path: Path) -> dict[str, Any]:
    pins = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(pins, dict):
        raise ValueError(f"{path} must hold a JSON object")
    validate_pins(pins)
    return pins


def server_image(pins: dict[str, Any]) -> str:
    return f"{SERVER_REPOSITORY}@{pins['serverImageDigest']}"


def emit_env(pins: dict[str, Any]) -> str:
    values = {
        "PINNED_SERVER_IMAGE": server_image(pins),
        "PINNED_SERVER_SOURCE_SHA": pins["serverSourceSha"],
        "PINNED_SERVER_FIXTURE_REVISION": pins["fixtureRevision"],
        "PINNED_SDK_PACKAGE_ID": pins["sdkPackageId"],
        "PINNED_SDK_PACKAGE_VERSION": pins["sdkPackageVersion"],
        "PINNED_SDK_PACKAGE_DIGEST": pins["sdkPackageDigest"],
        "PINNED_SDK_PACKAGE_SOURCE_SHA": pins["sdkSourceSha"],
    }
    return "".join(f"{name}={value}\n" for name, value in values.items())


def validate_identity(args: argparse.Namespace, pins: dict[str, Any]) -> None:
    image = getattr(args, "server_image", None)
    if args.tier == "release" or image:
        if not image or not DIGEST_IMAGE_RE.fullmatch(image):
            raise ValueError("server image must be digest-addressed; refusing a floating nightly tag")
    if args.tier == "release":
        if not args.release_cut:
            raise ValueError("release certification requires an exact release cut")
        if image != server_image(pins):
            raise ValueError("release certification requires the exact candidate image digest")
        if getattr(args, "server_source_sha", None) != pins["serverSourceSha"]:
            raise ValueError(f"release certification requires server pin {pins['serverSourceSha']}")
        if getattr(args, "fixture_revision", None) != pins["fixtureRevision"]:
            raise ValueError("release fixture revision does not match the candidate pin")


def _write_consumer(directory: Path, pins: dict[str, Any]) -> Path:
    if directory.exists() and any(directory.iterdir()):
        raise ValueError(f"consumer directory must be clean: {directory}")
    directory.mkdir(parents=True, exist_ok=True)
    project = directory / "Honua.Sdk.InstalledCertification.csproj"
    sources = [
        ROOT / "tests" / "Honua.Sdk.ProtocolIntegration.Tests",
        ROOT / "tests" / "Honua.Sdk.Conformance.Tests",
    ]
    compile_items = "\n".join(
        f'    <Compile Include="{source}/*.cs" Link="{source.name}/%(Filename)%(Extension)" />'
        for source in sources
    )
    project.write_text(f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <NoWarn>CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="{pins['sdkPackageId']}" Version="[{pins['sdkPackageVersion']}]" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="Testcontainers" Version="4.14.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
  <ItemGroup>
{compile_items}
  </ItemGroup>
</Project>
""", encoding="utf-8")
    # nuget.org only: the repository NuGet.config maps Honua.Sdk to GitHub Packages,
    # whose bytes are not the published, repository-signed nupkg.
    (directory / "nuget.config").write_text(f"""<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="{NUGET_ORG}" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
""", encoding="utf-8")
    return project


def _sha512(data: bytes) -> str:
    return base64.b64encode(hashlib.sha512(data).digest()).decode("ascii")


def _nuspec_commit(data: bytes) -> str | None:
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        nuspec_name = next((name for name in archive.namelist() if name.endswith(".nuspec")), None)
        if nuspec_name is None:
            raise ValueError("installed package has no nuspec")
        nuspec = ET.fromstring(archive.read(nuspec_name))
    metadata = next(node for node in nuspec.iter() if node.tag.endswith("metadata"))
    repository = next((node for node in metadata if node.tag.endswith("repository")), None)
    return None if repository is None else repository.attrib.get("commit")


def _verify_package(package: Path, pins: dict[str, Any], output: Path) -> dict[str, str]:
    data = package.read_bytes()
    actual_sha512 = _sha512(data)
    if actual_sha512 != pins["sdkPackageSha512"]:
        raise ValueError(
            f"installed package integrity mismatch: expected SHA-512 {pins['sdkPackageSha512']}, got {actual_sha512}"
        )
    actual_digest = "sha256:" + hashlib.sha256(data).hexdigest()
    if actual_digest != pins["sdkPackageDigest"]:
        raise ValueError(
            f"installed package integrity mismatch: expected {pins['sdkPackageDigest']}, got {actual_digest}"
        )
    commit = _nuspec_commit(data)
    if commit != pins["sdkSourceSha"]:
        raise ValueError(
            f"installed package source mismatch: expected {pins['sdkSourceSha']}, got {commit or 'missing'}"
        )
    return {
        "packageId": pins["sdkPackageId"],
        "packageVersion": pins["sdkPackageVersion"],
        "packageRegistry": pins["sdkPackageRegistry"],
        "packageDigest": actual_digest,
        "packageSha512": actual_sha512,
        "packageSourceSha": commit,
        "packagePath": str(package.resolve()),
        "consumerProject": str((output / "Honua.Sdk.InstalledCertification.csproj").resolve()),
    }


def package_archive(packages: Path, pins: dict[str, Any]) -> Path:
    package_id = pins["sdkPackageId"].lower()
    version = pins["sdkPackageVersion"].lower()
    return packages / package_id / version / f"{package_id}.{version}.nupkg"


def restore_failure_message(output: str, package_present: bool, server_sha: str) -> str:
    if "NU3018" in output or "NU3042" in output:
        return (
            "published package bytes cannot be installed: the author signing certificate "
            "O=Honua, CN=Honua SDK Signing is not trusted by the .NET trust provider "
            "(NU3042/NU3018). Certification fails closed and does not suppress signature "
            "warnings, pack from source, or substitute a floating nightly tag."
        )
    if not package_present:
        return f"published package bytes for server pin {server_sha} do not exist"
    return "installed package restore failed closed"


def prepare(args: argparse.Namespace, pins: dict[str, Any]) -> dict[str, str]:
    validate_identity(args, pins)
    project = _write_consumer(args.output, pins)
    packages = args.output / "packages"
    command = [
        "dotnet", "restore", str(project), "--packages", str(packages),
        "--configfile", str(args.output / "nuget.config"), "--no-cache", "--force-evaluate",
        "-p:RestoreLockedMode=false",
    ]
    completed = subprocess.run(command, capture_output=True, text=True)
    package = package_archive(packages, pins)
    server_sha = getattr(args, "server_source_sha", None) or pins["serverSourceSha"]
    if completed.returncode != 0:
        print(completed.stdout, completed.stderr, sep="\n", file=sys.stderr)
        raise ValueError(
            restore_failure_message(f"{completed.stdout}\n{completed.stderr}", package.is_file(), server_sha)
        )
    if not package.is_file():
        raise ValueError(f"published package bytes for server pin {server_sha} do not exist")
    assets = json.loads((args.output / "obj" / "project.assets.json").read_text(encoding="utf-8"))
    if f"{pins['sdkPackageId']}/{pins['sdkPackageVersion']}" not in assets["libraries"]:
        raise ValueError("restored assets do not contain the exact package coordinate")
    if any(library.get("type") == "project" for library in assets["libraries"].values()):
        raise ValueError("clean consumer resolved a project reference")
    return _verify_package(package, pins, args.output)


class Sources(Protocol):
    def nuget_catalog_entry(self, package_id: str, version: str) -> dict[str, Any] | None: ...
    def nuget_package(self, package_id: str, version: str) -> bytes | None: ...
    def release_manifest(self) -> dict[str, Any]: ...
    def server_compare(self, base: str, head: str) -> str: ...
    def server_image_digest(self, tag: str) -> str | None: ...
    def server_fixture(self, sha: str) -> bytes: ...


def _retry(action):
    delays = (10, 30, 60)
    for attempt in range(len(delays) + 1):
        try:
            return action()
        except urllib.error.HTTPError as error:
            if error.code < 500 or attempt == len(delays):
                raise
        except (urllib.error.URLError, subprocess.CalledProcessError, TimeoutError):
            if attempt == len(delays):
                raise
        time.sleep(delays[attempt])
    raise AssertionError("unreachable")


class LiveSources:
    """nuget.org and GHCR anonymously; GitHub through gh so GH_TOKEN applies."""

    def _http(self, url: str, headers: dict[str, str] | None = None, method: str = "GET"):
        request = urllib.request.Request(url, headers=headers or {}, method=method)

        def fetch():
            with urllib.request.urlopen(request, timeout=60) as response:
                body = response.read()
                if response.headers.get("Content-Encoding") == "gzip":
                    body = gzip.decompress(body)
                return body, response.headers

        try:
            return _retry(fetch)
        except urllib.error.HTTPError as error:
            if error.code == 404:
                return None, None
            raise

    def _gh(self, *args: str) -> bytes:
        return _retry(lambda: subprocess.run(["gh", "api", *args], capture_output=True, check=True).stdout)

    def nuget_catalog_entry(self, package_id: str, version: str) -> dict[str, Any] | None:
        leaf, _ = self._http(
            f"https://api.nuget.org/v3/registration5-gz-semver2/{package_id.lower()}/{version.lower()}.json"
        )
        if leaf is None:
            return None
        entry, _ = self._http(json.loads(leaf)["catalogEntry"])
        return None if entry is None else json.loads(entry)

    def nuget_package(self, package_id: str, version: str) -> bytes | None:
        package_id, version = package_id.lower(), version.lower()
        data, _ = self._http(
            f"https://api.nuget.org/v3-flatcontainer/{package_id}/{version}/{package_id}.{version}.nupkg"
        )
        return data

    def release_manifest(self) -> dict[str, Any]:
        try:
            import yaml
        except ImportError as error:
            raise ValueError("--check needs PyYAML to read the honua-release platform manifest") from error
        return yaml.safe_load(self._gh(RELEASE_MANIFEST, "-H", "Accept: application/vnd.github.raw"))

    def server_compare(self, base: str, head: str) -> str:
        return self._gh(f"repos/honua-io/honua-server/compare/{base}...{head}", "--jq", ".status").decode().strip()

    def server_image_digest(self, tag: str) -> str | None:
        repository = SERVER_REPOSITORY.split("/", 1)[1]
        token, _ = self._http(f"https://ghcr.io/token?scope=repository:{repository}:pull")
        if token is None:
            return None
        accept = ", ".join((
            "application/vnd.oci.image.index.v1+json",
            "application/vnd.docker.distribution.manifest.list.v2+json",
            "application/vnd.oci.image.manifest.v1+json",
            "application/vnd.docker.distribution.manifest.v2+json",
        ))
        _, headers = self._http(
            f"https://ghcr.io/v2/{repository}/manifests/{tag}",
            {"Authorization": f"Bearer {json.loads(token)['token']}", "Accept": accept},
            method="HEAD",
        )
        return None if headers is None else headers.get("Docker-Content-Digest")

    def server_fixture(self, sha: str) -> bytes:
        return self._gh(
            f"repos/honua-io/honua-server/contents/{FIXTURE_PATH}?ref={sha}",
            "-H", "Accept: application/vnd.github.raw",
        )


def _check_published_package(pins: dict[str, Any], sources: Sources) -> list[str]:
    coordinate = f"{pins['sdkPackageId']} {pins['sdkPackageVersion']}"
    missing = (
        f"published package bytes for server pin {pins['serverSourceSha']} do not exist: "
        f"{coordinate} is not on nuget.org"
    )
    entry = sources.nuget_catalog_entry(pins["sdkPackageId"], pins["sdkPackageVersion"])
    if entry is None:
        return [missing]
    problems = []
    if entry.get("listed") is False:
        problems.append(f"{coordinate} is unlisted on nuget.org")
    if entry.get("packageHashAlgorithm") != "SHA512":
        problems.append(f"nuget.org catalog records {entry.get('packageHashAlgorithm')}, not SHA512, for {coordinate}")
    if entry.get("packageHash") != pins["sdkPackageSha512"]:
        problems.append(
            f"installed package integrity mismatch: expected SHA-512 {pins['sdkPackageSha512']}, "
            f"got {entry.get('packageHash')} from the nuget.org catalog for {coordinate}"
        )
    catalog_commit = (entry.get("repository") or {}).get("commit")
    if catalog_commit != pins["sdkSourceSha"]:
        problems.append(
            f"installed package source mismatch: expected {pins['sdkSourceSha']}, "
            f"nuget.org catalog records {catalog_commit or 'missing'} for {coordinate}"
        )
    data = sources.nuget_package(pins["sdkPackageId"], pins["sdkPackageVersion"])
    if data is None:
        return problems + [missing]
    if _sha512(data) != entry.get("packageHash"):
        problems.append(
            f"nuget.org served {coordinate} bytes with SHA-512 {_sha512(data)}, "
            f"which is not its catalog SHA-512 {entry.get('packageHash')}"
        )
    digest = "sha256:" + hashlib.sha256(data).hexdigest()
    if digest != pins["sdkPackageDigest"]:
        problems.append(
            f"installed package integrity mismatch: expected {pins['sdkPackageDigest']}, "
            f"got {digest} from the nuget.org download of {coordinate}"
        )
    commit = _nuspec_commit(data)
    if commit != pins["sdkSourceSha"]:
        problems.append(
            f"installed package source mismatch: expected {pins['sdkSourceSha']}, "
            f"nuspec of the nuget.org download records {commit or 'missing'}"
        )
    return problems


def _check_release_manifest(pins: dict[str, Any], sources: Sources) -> list[str]:
    manifest = sources.release_manifest()
    problems = []
    prefix = "release certification requires the manifest-pinned Honua.Sdk package"
    artifact = (manifest.get("clientArtifacts") or {}).get("honua-sdk-dotnet") or {}
    component = (manifest.get("components") or {}).get("honua-sdk-dotnet") or {}
    expectations = (
        ("clientArtifacts.honua-sdk-dotnet", artifact, "package", "sdkPackageId"),
        ("clientArtifacts.honua-sdk-dotnet", artifact, "version", "sdkPackageVersion"),
        ("clientArtifacts.honua-sdk-dotnet", artifact, "digest", "sdkPackageDigest"),
        ("clientArtifacts.honua-sdk-dotnet", artifact, "sourceSha", "sdkSourceSha"),
        ("clientArtifacts.honua-sdk-dotnet", artifact, "registry", "sdkPackageRegistry"),
        ("components.honua-sdk-dotnet", component, "version", "sdkPackageVersion"),
        ("components.honua-sdk-dotnet", component, "sha", "sdkSourceSha"),
    )
    for location, entry, field, pin in expectations:
        if entry.get(field) != pins[pin]:
            problems.append(
                f"{prefix}: {pin} ({location}.{field}): pin file has {pins[pin]}, "
                f"honua-release manifest has {entry.get(field)}"
            )
    if artifact.get("publicationState") != "published":
        problems.append(
            f"{prefix}: clientArtifacts.honua-sdk-dotnet.publicationState is "
            f"{artifact.get('publicationState')}, not published"
        )
    server = (manifest.get("components") or {}).get("honua-server") or {}
    base = server.get("sha")
    if not base:
        return problems + ["honua-release manifest has no components.honua-server.sha"]
    status = sources.server_compare(base, pins["serverSourceSha"])
    if status == "identical":
        if server.get("digest") != pins["serverImageDigest"]:
            problems.append(
                f"serverImageDigest: pin file has {pins['serverImageDigest']}, honua-release manifest "
                f"server pin {base} has {server.get('digest')}"
            )
    elif status != "ahead":
        problems.append(
            f"server pin {pins['serverSourceSha']} is {status} relative to the honua-release manifest "
            f"server pin {base}; advance serverSourceSha to that pin or a newer imaged trunk commit"
        )
    return problems


def _check_server(pins: dict[str, Any], sources: Sources) -> list[str]:
    problems = []
    tag = f"nightly-{pins['serverSourceSha'][:7]}"
    digest = sources.server_image_digest(tag)
    if digest is None:
        problems.append(f"{SERVER_REPOSITORY}:{tag} does not exist; serverSourceSha has no published image")
    elif digest != pins["serverImageDigest"]:
        problems.append(
            f"server image drifted: {SERVER_REPOSITORY}:{tag} resolves to {digest}, "
            f"pin file has serverImageDigest {pins['serverImageDigest']}"
        )
    fixture = "sha256:" + hashlib.sha256(sources.server_fixture(pins["serverSourceSha"])).hexdigest()
    if fixture != pins["fixtureRevision"]:
        problems.append(
            f"fixtureRevision drifted: {FIXTURE_PATH} at honua-server {pins['serverSourceSha']} is "
            f"{fixture}, pin file has {pins['fixtureRevision']}"
        )
    return problems


def check_pins(pins: dict[str, Any], sources: Sources) -> list[str]:
    return (
        _check_published_package(pins, sources)
        + _check_release_manifest(pins, sources)
        + _check_server(pins, sources)
    )


def _published_manifest_artifact(manifest: dict[str, Any]) -> dict[str, Any]:
    artifact = (manifest.get("clientArtifacts") or {}).get("honua-sdk-dotnet") or {}
    component = (manifest.get("components") or {}).get("honua-sdk-dotnet") or {}
    state = artifact.get("publicationState")
    if state != "published":
        raise ValueError(
            "refusing to pin an unpublished Honua.Sdk package: "
            f"clientArtifacts.honua-sdk-dotnet.publicationState is {state}, not published"
        )
    required = ("package", "version", "digest", "sourceSha", "registry")
    missing = [field for field in required if not isinstance(artifact.get(field), str) or not artifact[field]]
    if missing:
        raise ValueError(
            "honua-release manifest clientArtifacts.honua-sdk-dotnet is missing " + ", ".join(missing)
        )
    if component.get("version") != artifact["version"] or component.get("sha") != artifact["sourceSha"]:
        raise ValueError(
            "honua-release manifest components.honua-sdk-dotnet does not match "
            "clientArtifacts.honua-sdk-dotnet"
        )
    if artifact["package"] != "Honua.Sdk" or artifact["registry"] != "nuget.org":
        raise ValueError("honua-release manifest does not pin Honua.Sdk on nuget.org")
    if not VERSION_RE.fullmatch(artifact["version"]):
        raise ValueError("manifest package version must be an exact semantic version")
    if not SHA256_RE.fullmatch(artifact["digest"]):
        raise ValueError("manifest package digest must be a lowercase sha256:<hex> digest")
    if not SHA_RE.fullmatch(artifact["sourceSha"]):
        raise ValueError("manifest package sourceSha must be a full lowercase commit")
    return artifact


def _nuget_package_identity(package_id: str, version: str, sources: Sources) -> dict[str, str]:
    """SHA-512, sha256, and nuspec commit of a package that nuget.org actually serves."""
    coordinate = f"{package_id} {version}"
    entry = sources.nuget_catalog_entry(package_id, version)
    if entry is None:
        raise ValueError(f"refusing to pin an unpublished version: {coordinate} is not on nuget.org")
    if entry.get("listed") is False:
        raise ValueError(f"refusing to pin an unlisted package: {coordinate} is unlisted on nuget.org")
    catalog_hash = entry.get("packageHash")
    if entry.get("packageHashAlgorithm") != "SHA512" or not isinstance(catalog_hash, str) or not catalog_hash:
        raise ValueError(
            f"nuget.org catalog records {entry.get('packageHashAlgorithm')}, not SHA512, for {coordinate}"
        )
    data = sources.nuget_package(package_id, version)
    if data is None:
        raise ValueError(f"refusing to pin an unpublished version: {coordinate} bytes are not on nuget.org")
    sha512 = _sha512(data)
    if sha512 != catalog_hash:
        raise ValueError(
            f"nuget.org served {coordinate} bytes with SHA-512 {sha512}, "
            f"which is not its catalog SHA-512 {catalog_hash}"
        )
    commit = _nuspec_commit(data)
    catalog_commit = (entry.get("repository") or {}).get("commit")
    if not commit or commit != catalog_commit:
        raise ValueError(
            f"installed package source mismatch: nuget.org catalog records {catalog_commit or 'missing'} "
            f"and the nuspec records {commit or 'missing'} for {coordinate}"
        )
    return {
        "sha512": sha512,
        "digest": "sha256:" + hashlib.sha256(data).hexdigest(),
        "commit": commit,
    }


def follow_release_manifest(pins: dict[str, Any], sources: Sources) -> tuple[dict[str, Any], bool]:
    """Return pin fields aligned to the published manifest package, and whether they moved.

    Server pins are copied through. An unpublished manifest coordinate or a
    nuget.org package whose bytes disagree with the manifest raises ValueError
    and must not be written.
    """
    artifact = _published_manifest_artifact(sources.release_manifest())
    identity = _nuget_package_identity(artifact["package"], artifact["version"], sources)
    coordinate = f"{artifact['package']} {artifact['version']}"
    if identity["digest"] != artifact["digest"]:
        raise ValueError(
            f"nuget.org download of {coordinate} is {identity['digest']}, "
            f"honua-release manifest digest is {artifact['digest']}"
        )
    if identity["commit"] != artifact["sourceSha"]:
        raise ValueError(
            f"installed package source mismatch: nuget.org records {identity['commit']}, "
            f"honua-release manifest sourceSha is {artifact['sourceSha']}"
        )
    updated = dict(pins)
    updated["sdkPackageId"] = artifact["package"]
    updated["sdkPackageVersion"] = artifact["version"]
    updated["sdkPackageRegistry"] = artifact["registry"]
    updated["sdkPackageSha512"] = identity["sha512"]
    updated["sdkPackageDigest"] = identity["digest"]
    updated["sdkSourceSha"] = identity["commit"]
    validate_pins(updated)
    package_fields = (
        "sdkPackageId",
        "sdkPackageVersion",
        "sdkPackageRegistry",
        "sdkPackageSha512",
        "sdkPackageDigest",
        "sdkSourceSha",
    )
    changed = any(updated[field] != pins[field] for field in package_fields)
    return updated, changed


def write_pins(path: Path, pins: dict[str, Any]) -> None:
    ordered = {field: pins[field] for field in PIN_FIELDS}
    path.write_text(json.dumps(ordered, indent=2) + "\n", encoding="utf-8")


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pins", type=Path, default=DEFAULT_PINS)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check", action="store_true", help="verify the pin file against nuget.org and honua-release")
    mode.add_argument("--emit-env", action="store_true", help="print the pins as GITHUB_ENV lines")
    mode.add_argument(
        "--follow-manifest",
        action="store_true",
        help="write the published honua-release package coordinate when nuget.org bytes match it",
    )
    parser.add_argument("--tier", choices=("pr", "nightly", "release"))
    parser.add_argument("--server-image")
    parser.add_argument("--server-source-sha")
    parser.add_argument("--fixture-revision")
    parser.add_argument("--release-cut")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--identity-output", type=Path)
    args = parser.parse_args(argv)
    if not (args.check or args.emit_env or args.follow_manifest):
        missing = [flag for flag, value in (
            ("--tier", args.tier), ("--output", args.output), ("--identity-output", args.identity_output),
        ) if value is None]
        if missing:
            parser.error(f"restore requires {', '.join(missing)}")
    return args


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        pins = load_pins(args.pins)
        if args.emit_env:
            sys.stdout.write(emit_env(pins))
            return 0
        if args.follow_manifest:
            updated, changed = follow_release_manifest(pins, LiveSources())
            if changed:
                write_pins(args.pins, updated)
                print(
                    f"updated {args.pins} to {updated['sdkPackageId']} {updated['sdkPackageVersion']} "
                    f"{updated['sdkPackageDigest']} {updated['sdkSourceSha']}"
                )
            else:
                print(f"{args.pins} already matches the published honua-release manifest")
            print(f"changed={'true' if changed else 'false'}")
            return 0
        if args.check:
            problems = check_pins(pins, LiveSources())
            for problem in problems:
                print(f"::error::{problem}", file=sys.stderr)
            if problems:
                print(f"{args.pins} drifted; see certification/README.md to advance it.", file=sys.stderr)
                return 1
            print(
                f"{args.pins} matches nuget.org {pins['sdkPackageId']} {pins['sdkPackageVersion']} and "
                f"honua-release; server {pins['serverSourceSha']} {pins['serverImageDigest']}"
            )
            return 0
        identity = prepare(args, pins)
        args.identity_output.parent.mkdir(parents=True, exist_ok=True)
        args.identity_output.write_text(json.dumps(identity, indent=2) + "\n", encoding="utf-8")
    except (
        OSError, ValueError, KeyError, subprocess.CalledProcessError, ET.ParseError, zipfile.BadZipFile,
        urllib.error.URLError,
    ) as error:
        print(f"::error::{error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
