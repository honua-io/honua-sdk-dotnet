#!/usr/bin/env python3
"""Provision and attest the SDK/workload tuple without executing repository MSBuild source."""
from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[2]
PACK_METADATA = {
    "Microsoft.NET.Sdk.WebAssembly.Pack": ("KnownWebAssemblySdkPack", "WebAssemblySdkPackVersion"),
    "Microsoft.NET.ILLink.Tasks": ("KnownILLinkPack", "ILLinkPackVersion"),
}


def pinned_sdk(document: dict) -> str:
    sdk = document.get("sdk", {})
    version = sdk.get("version", "")
    if (not re.fullmatch(r"\d+\.\d+\.\d+", version)
            or sdk.get("rollForward") != "disable"
            or sdk.get("workloadVersion") != version):
        raise ValueError("global.json must pin matching exact SDK/workload versions and disable roll-forward")
    return version


def locked_packs(document: dict) -> dict[str, str]:
    result = {}
    for package in PACK_METADATA:
        versions = {dependencies[package]["resolved"]
                    for dependencies in document["dependencies"].values() if package in dependencies}
        if len(versions) != 1:
            raise ValueError(f"Lock must contain one resolved {package} version")
        result[package] = versions.pop()
    return result


def verify_evaluation(document: dict, sdk: str, expected: dict[str, str]) -> dict[str, str]:
    if document.get("Properties", {}).get("NETCoreSdkVersion") != sdk:
        raise ValueError("MSBuild selected a different SDK")
    framework = "net" + ".".join(sdk.split(".")[:2])
    selected = {}
    for package, (item, metadata) in PACK_METADATA.items():
        matches = [entry[metadata] for entry in document.get("Items", {}).get(item, [])
                   if entry.get("TargetFramework") == framework]
        if matches != [expected[package]]:
            raise ValueError(f"Implicit {package} does not match committed lock: {matches}")
        selected[package] = matches[0]
    return selected


def command(cwd: Path, *args: str, required: bool = True) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(["dotnet", *args], cwd=cwd, text=True, capture_output=True)
    if required and result.returncode:
        raise ValueError(f"dotnet {' '.join(args)} failed:\n{result.stdout}{result.stderr}")
    return result


def evaluate(cwd: Path, required: bool = True) -> tuple[subprocess.CompletedProcess[str], dict | None]:
    result = command(cwd, "msbuild", "probe.csproj",
                     "-getProperty:NETCoreSdkVersion,_RuntimePackInWorkloadVersionCurrent",
                     "-getItem:KnownWebAssemblySdkPack,KnownILLinkPack", required=required)
    if result.returncode:
        return result, None
    return result, json.loads(result.stdout[result.stdout.index("{"):])


def main() -> int:
    try:
        global_json = json.loads((ROOT / "global.json").read_text())
        sdk = pinned_sdk(global_json)
        lock = ROOT / "tests/Honua.Sdk.BrowserSmoke/packages.lock.json"
        expected = locked_packs(json.loads(lock.read_text()))
        if command(ROOT, "--version").stdout.strip() != sdk:
            raise ValueError("Selected dotnet CLI is not the pinned SDK")
        # The probe lives outside the checkout, so workload provisioning cannot
        # evaluate Directory.Build.* or arbitrary targets from release-PR source.
        with tempfile.TemporaryDirectory(prefix="honua-sdk-workload-", dir=os.environ.get("RUNNER_TEMP")) as directory:
            probe = Path(directory)
            if probe.is_relative_to(ROOT):
                raise ValueError("Workload probe must be outside the repository checkout")
            project = '<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly"><PropertyGroup><TargetFramework>net' + ".".join(sdk.split(".")[:2]) + '</TargetFramework><WasmBuildNative>false</WasmBuildNative></PropertyGroup></Project>'
            (probe / "probe.csproj").write_text(project)
            (probe / "global.json").write_text(json.dumps({"sdk": {"version": sdk, "rollForward": "disable"}}))
            before = command(probe, "workload", "--info", required=False)
            baseline, baseline_document = evaluate(probe, required=False)
            if baseline_document is not None:
                framework = "net" + ".".join(sdk.split(".")[:2])
                baseline_document = {
                    "Properties": baseline_document.get("Properties", {}),
                    "Items": {item: [{key: value for key, value in entry.items()
                                      if key in {"TargetFramework", "WebAssemblySdkPackVersion", "ILLinkPackVersion", "DefiningProjectFullPath"}}
                                     for entry in entries if entry.get("TargetFramework") == framework]
                              for item, entries in baseline_document.get("Items", {}).items()},
                }
            print(json.dumps({"stage": "ambient", "sdkInfo": command(probe, "--info").stdout,
                              "workloadInfo": before.stdout + before.stderr,
                              "evaluation": baseline_document,
                              "evaluationError": baseline.stderr if baseline.returncode else None}), flush=True)
            (probe / "global.json").write_text(json.dumps(global_json))
            restored = command(probe, "workload", "restore", "probe.csproj", "--source", "https://api.nuget.org/v3/index.json")
            print(restored.stdout, flush=True)
            if command(probe, "workload", "--version").stdout.strip() != sdk:
                raise ValueError("Resolved workload set differs from global.json")
            _, document = evaluate(probe)
            assert document is not None
            selected = verify_evaluation(document, sdk, expected)
            # The preprocessor records the resolver's actual imported manifest
            # path, including when several versions coexist under one SDK root.
            command(probe, "msbuild", "probe.csproj", "-preprocess:imports.xml")
            paths = sorted({line.strip() for line in (probe / "imports.xml").read_text().splitlines()
                            if "microsoft.net.workload.mono.toolchain.current" in line.lower()
                            and Path(line.strip()).is_file()})
            receipt = {"stage": "verified", "sdkVersion": sdk, "workloadSetVersion": sdk,
                       "implicitPackages": selected, "lockSha256": hashlib.sha256(lock.read_bytes()).hexdigest(),
                       "manifestImports": [{"path": name, "sha256": hashlib.sha256(Path(name).read_bytes()).hexdigest()} for name in paths],
                       "workloadInfo": command(probe, "workload", "--info").stdout}
            if not paths:
                raise ValueError("No selected Mono workload manifest import was retained")
            print(json.dumps(receipt), flush=True)
        return 0
    except (OSError, ValueError, KeyError, subprocess.SubprocessError) as error:
        print(f"::error::{error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
