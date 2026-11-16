"""SDK/workload graph refusal controls; synthetic data is never release evidence."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("dotnet_toolchain", ROOT / "scripts/ci/prepare-dotnet-toolchain.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)
SDK = "11.0.100"
EXPECTED = {"Microsoft.NET.Sdk.WebAssembly.Pack": "11.0.0", "Microsoft.NET.ILLink.Tasks": "11.0.0"}


def evaluation():
    return {"Properties": {"NETCoreSdkVersion": SDK}, "Items": {
        "KnownWebAssemblySdkPack": [{"TargetFramework": "net11.0", "WebAssemblySdkPackVersion": "11.0.0"}],
        "KnownILLinkPack": [{"TargetFramework": "net11.0", "ILLinkPackVersion": "11.0.0"}],
    }}


class DotnetToolchainTests(unittest.TestCase):
    def test_repository_projects_require_only_the_pinned_runtime(self):
        sdk = MODULE.pinned_sdk(json.loads((ROOT / "global.json").read_text()))
        self.assertEqual("11", sdk.split(".")[0])
        expected = "net" + ".".join(sdk.split(".")[:2])
        projects = list(ROOT.glob("src/**/*.csproj")) + list(ROOT.glob("tests/**/*.csproj")) + list(ROOT.glob("examples/**/*.csproj")) + list(ROOT.glob("certification/**/*.csproj"))
        self.assertTrue(projects)
        for project in projects:
            with self.subTest(project=str(project.relative_to(ROOT))):
                targets = [node.text for node in ET.parse(project).iter()
                           if node.tag in {"TargetFramework", "TargetFrameworks"}]
                self.assertEqual([expected], targets)

    def test_global_pin_refuses_missing_cross_version_or_roll_forward(self):
        pinned = {"version": SDK, "rollForward": "disable", "workloadVersion": SDK}
        self.assertEqual(SDK, MODULE.pinned_sdk({"sdk": pinned}))
        for field, value in (("version", "11.0.x"), ("workloadVersion", None),
                             ("workloadVersion", "11.0.101"), ("rollForward", "patch")):
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                MODULE.pinned_sdk({"sdk": {**pinned, field: value}})

    def test_ambient_manifest_cannot_change_implicit_graph(self):
        self.assertEqual(EXPECTED, MODULE.verify_evaluation(evaluation(), (SDK, "net11.0"), EXPECTED))
        for item, metadata in MODULE.PACK_METADATA.values():
            with self.subTest(item=item):
                mixed = evaluation()
                mixed["Items"][item][0][metadata] = "11.0.1"
                with self.assertRaisesRegex(ValueError, "committed lock"):
                    MODULE.verify_evaluation(mixed, (SDK, "net11.0"), EXPECTED)
        for mutation in (lambda x: x["Properties"].update(NETCoreSdkVersion="11.0.101"),
                         lambda x: x["Items"].pop("KnownWebAssemblySdkPack"),
                         lambda x: x["Items"]["KnownWebAssemblySdkPack"].append(x["Items"]["KnownWebAssemblySdkPack"][0])):
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                mixed = evaluation()
                mutation(mixed)
                MODULE.verify_evaluation(mixed, (SDK, "net11.0"), EXPECTED)

    def test_evaluation_uses_browser_framework_not_other_installed_packs(self):
        document = evaluation()
        document["Items"]["KnownILLinkPack"].append(
            {"TargetFramework": "net12.0", "ILLinkPackVersion": "12.0.0"})
        self.assertEqual(EXPECTED, MODULE.verify_evaluation(
            document, (SDK, "net11.0"), EXPECTED))
        with self.assertRaisesRegex(ValueError, "committed lock"):
            MODULE.verify_evaluation(document, (SDK, "net12.0"), EXPECTED)

    def test_bootstrap_uses_isolated_probe_and_refuses_unavailable_set(self):
        for unavailable in (False, True):
            with self.subTest(unavailable=unavailable), tempfile.TemporaryDirectory() as directory:
                root = Path(directory) / "repo"
                root.mkdir()
                (root / "global.json").write_text(json.dumps({"sdk": {
                    "version": SDK, "rollForward": "disable", "workloadVersion": SDK}}))
                lock = root / "tests/Honua.Sdk.BrowserSmoke/packages.lock.json"
                lock.parent.mkdir(parents=True)
                lock.write_text(json.dumps({"dependencies": {"net11.0": {
                    name: {"resolved": version} for name, version in EXPECTED.items()}}}))
                (lock.parent / "Honua.Sdk.BrowserSmoke.csproj").write_text(
                    '<Project><PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup></Project>')
                manifest = Path(directory) / "sdk/microsoft.net.workload.mono.toolchain.current/11.0.100/WorkloadManifest.targets"
                manifest.parent.mkdir(parents=True)
                manifest.write_text("<Project />")
                calls = []

                def fake(cwd, *args, required=True):
                    calls.append((cwd, args))
                    if args == ("--version",):
                        return subprocess.CompletedProcess(args, 0, SDK + "\n", "")
                    self.assertFalse(cwd.is_relative_to(root))
                    self.assertNotIn("Import", (cwd / "probe.csproj").read_text())
                    if args[:2] == ("workload", "restore") and unavailable:
                        raise ValueError("Workload set unavailable")
                    if args[:2] == ("workload", "--version"):
                        output = SDK
                    elif "-preprocess:imports.xml" in args:
                        (cwd / "imports.xml").write_text("<!--\n" + str(manifest) + "\n-->")
                        output = ""
                    elif args[0] == "msbuild":
                        output = json.dumps(evaluation())
                    else:
                        output = "recorded SDK/workload identity"
                    return subprocess.CompletedProcess(args, 0, output, "")

                output = io.StringIO()
                with patch.object(MODULE, "ROOT", root), patch.object(MODULE, "command", fake), contextlib.redirect_stdout(output), contextlib.redirect_stderr(output):
                    self.assertEqual(1 if unavailable else 0, MODULE.main())
                self.assertEqual(not unavailable, '"stage": "verified"' in output.getvalue())
                self.assertTrue(any(args[:2] == ("workload", "restore") for _, args in calls))

    def test_every_sdk_entrypoint_bootstraps_same_tuple(self):
        sources = list((ROOT / ".github/workflows").glob("*.yml"))
        sources.append(ROOT / ".github/actions/setup-dotnet-and-nuget/action.yml")
        for source in sources:
            text = source.read_text()
            installs = text.count("uses: actions/setup-dotnet@")
            self.assertEqual(installs, text.count("run: python3 scripts/ci/prepare-dotnet-toolchain.py"), str(source))
        workflow = (ROOT / ".github/workflows/release-please.yml").read_text()
        apply_job = workflow.split("  apply-release-pr-locks:\n", 1)[1].split("\n  publish:\n", 1)[0]
        self.assertNotIn("prepare-dotnet-toolchain", apply_job)
        self.assertIn("- name: Pin and verify SDK workload tuple\n        if: steps.release-pr.outputs.sha != ''", workflow)
