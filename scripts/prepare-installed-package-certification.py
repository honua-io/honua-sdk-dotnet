#!/usr/bin/env python3
"""Prepare a clean test consumer from an immutable published Honua.Sdk package."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SHA256_RE = re.compile(r"sha256:[0-9a-f]{64}")
SHA_RE = re.compile(r"[0-9a-f]{40}")
VERSION_RE = re.compile(r"[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?")


def validate_identity(args: argparse.Namespace) -> None:
    if args.package_id != "Honua.Sdk":
        raise ValueError("release certification requires package ID Honua.Sdk")
    if not VERSION_RE.fullmatch(args.package_version) or "*" in args.package_version:
        raise ValueError("package version must be an exact semantic version")
    if not SHA256_RE.fullmatch(args.package_digest):
        raise ValueError("package digest must be a lowercase SHA-256")
    if not SHA_RE.fullmatch(args.package_source_sha):
        raise ValueError("package source SHA must be a full lowercase commit")
    if args.tier == "release":
        if args.publication_state != "published":
            raise ValueError("release certification requires a published package")
        if args.registry != "github-packages":
            raise ValueError("release certification requires the pinned remote registry")
        if not args.release_cut:
            raise ValueError("release certification requires an exact release cut")


def _write_consumer(directory: Path, args: argparse.Namespace) -> Path:
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
    <PackageReference Include="{args.package_id}" Version="[{args.package_version}]" />
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
    return project


def _verify_package(package: Path, args: argparse.Namespace) -> dict[str, str]:
    actual_digest = "sha256:" + hashlib.sha256(package.read_bytes()).hexdigest()
    if actual_digest != args.package_digest:
        raise ValueError(
            f"installed package integrity mismatch: expected {args.package_digest}, got {actual_digest}"
        )
    with zipfile.ZipFile(package) as archive:
        nuspec_name = next((name for name in archive.namelist() if name.endswith(".nuspec")), None)
        if nuspec_name is None:
            raise ValueError("installed package has no nuspec")
        nuspec = ET.fromstring(archive.read(nuspec_name))
    metadata = next(node for node in nuspec.iter() if node.tag.endswith("metadata"))
    repository = next((node for node in metadata if node.tag.endswith("repository")), None)
    commit = None if repository is None else repository.attrib.get("commit")
    if commit != args.package_source_sha:
        raise ValueError(
            f"installed package source mismatch: expected {args.package_source_sha}, got {commit or 'missing'}"
        )
    return {
        "packageId": args.package_id,
        "packageVersion": args.package_version,
        "packageDigest": actual_digest,
        "packageSourceSha": commit,
        "packagePath": str(package.resolve()),
        "consumerProject": str((args.output / "Honua.Sdk.InstalledCertification.csproj").resolve()),
    }


def prepare(args: argparse.Namespace) -> dict[str, str]:
    validate_identity(args)
    project = _write_consumer(args.output, args)
    packages = args.output / "packages"
    command = [
        "dotnet", "restore", str(project), "--packages", str(packages),
        "--configfile", str(args.nuget_config), "--no-cache", "--force-evaluate",
        f"-p:RestoreLockedMode=false",
    ]
    subprocess.run(command, check=True)
    package = packages / args.package_id.lower() / args.package_version.lower() / (
        f"{args.package_id.lower()}.{args.package_version.lower()}.nupkg"
    )
    if not package.is_file():
        raise ValueError(f"restore did not install the exact package archive: {package}")
    assets = json.loads((args.output / "obj" / "project.assets.json").read_text(encoding="utf-8"))
    if f"{args.package_id}/{args.package_version}" not in assets["libraries"]:
        raise ValueError("restored assets do not contain the exact package coordinate")
    if any(library.get("type") == "project" for library in assets["libraries"].values()):
        raise ValueError("clean consumer resolved a project reference")
    return _verify_package(package, args)


def parse_args(argv: list[str] | None = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tier", choices=("pr", "nightly", "release"), required=True)
    parser.add_argument("--package-id", required=True)
    parser.add_argument("--package-version", required=True)
    parser.add_argument("--package-digest", required=True)
    parser.add_argument("--package-source-sha", required=True)
    parser.add_argument("--publication-state", required=True)
    parser.add_argument("--registry", required=True)
    parser.add_argument("--release-cut")
    parser.add_argument("--nuget-config", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--identity-output", type=Path, required=True)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    args = parse_args(argv)
    try:
        identity = prepare(args)
        args.identity_output.parent.mkdir(parents=True, exist_ok=True)
        args.identity_output.write_text(json.dumps(identity, indent=2) + "\n", encoding="utf-8")
    except (OSError, ValueError, subprocess.CalledProcessError, ET.ParseError, zipfile.BadZipFile) as error:
        print(f"::error::{error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
