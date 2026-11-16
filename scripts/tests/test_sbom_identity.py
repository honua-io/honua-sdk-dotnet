from __future__ import annotations

import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github/workflows/publish-dotnet-sdk.yml"
SCRIPT = ROOT / "scripts/check-sbom.py"


class SbomIdentityTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.sbom = self.root / "sboms/Honua.Sdk/Honua.Sdk.cdx.json"
        self.sbom.parent.mkdir(parents=True)
        self.document = {
            "bomFormat": "CycloneDX",
            "metadata": {"component": {"name": "Honua.Sdk", "version": "5.0.0",
                                        "bom-ref": "root"}},
            "components": [{"name": "Honua.Sdk.Abstractions", "version": "5.0.0",
                            "bom-ref": "dependency"}],
            "dependencies": [{"ref": "root", "dependsOn": ["dependency"]},
                             {"ref": "dependency", "dependsOn": []}],
        }

    def write_package(self, version="5.0.0"):
        package = self.root / f"nupkgs/Honua.Sdk.{version}.nupkg"
        package.parent.mkdir(exist_ok=True)
        with zipfile.ZipFile(package, "w") as archive:
            archive.writestr("Honua.Sdk.nuspec", f"""<package><metadata>
                <id>Honua.Sdk</id><version>{version}</version><dependencies>
                <group targetFramework="net11.0"><dependency
                id="Honua.Sdk.Abstractions" version="{version}" /></group>
                </dependencies></metadata></package>""")
        return package

    def run_check(self, version="5.0.0"):
        self.sbom.write_text(json.dumps(self.document))
        return subprocess.run(
            ["python3", str(SCRIPT), str(self.sbom), "--package-id", "Honua.Sdk",
             "--version", version, "--package", str(self.write_package(version))], capture_output=True, text=True)

    def test_valid_stable_and_prerelease(self):
        for version in ("5.0.0", "5.0.1-rc.2"):
            with self.subTest(version=version):
                self.document["metadata"]["component"]["version"] = version
                self.document["components"][0]["version"] = version
                result = self.run_check(version)
                self.assertEqual(result.returncode, 0, result.stderr)

    def test_invalid_identity_and_document(self):
        cases = [
            ("default version", {"version": "0.0.0"}),
            ("old release", {"version": "4.0.0"}),
            ("wrong package", {"name": "Honua.Sdk.Admin"}),
            ("missing name", {"name": None}),
            ("missing version", {"version": None}),
        ]
        for label, change in cases:
            with self.subTest(case=label):
                self.document["metadata"]["component"] = {
                    "name": "Honua.Sdk", "version": "5.0.0", **change}
                self.assertNotEqual(self.run_check().returncode, 0)

    def test_missing_metadata_and_empty_components(self):
        for field, value in (("metadata", None), ("metadata", []),
                             ("components", []), ("components", {}),
                             ("bomFormat", "SPDX")):
            with self.subTest(field=field, value=value):
                original = self.document[field]
                self.document[field] = value
                self.assertNotEqual(self.run_check().returncode, 0)
                self.document[field] = original

    def test_missing_project_dependency_is_rejected(self):
        self.document["components"] = [{"name": "Unrelated", "version": "1.0.0",
                                        "bom-ref": "dependency"}]
        self.assertNotEqual(self.run_check().returncode, 0)

    def test_wrong_project_dependency_version_is_rejected(self):
        self.document["components"][0]["version"] = "0.0.0"
        self.assertNotEqual(self.run_check().returncode, 0)

    def test_missing_root_edge_is_rejected(self):
        self.document["dependencies"][0]["dependsOn"] = []
        self.assertNotEqual(self.run_check().returncode, 0)

    def test_unresolved_dependency_edge_is_rejected(self):
        self.document["dependencies"][1]["dependsOn"] = ["missing"]
        self.assertNotEqual(self.run_check().returncode, 0)

    def test_missing_dependency_graph_is_rejected(self):
        del self.document["dependencies"]
        self.assertNotEqual(self.run_check().returncode, 0)

    def test_attestation_rejects_missing_project_before_export(self):
        self.document["components"][0]["name"] = "Unrelated"
        result, environment = self.run_attestation()
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertFalse(environment.exists())

    def test_malformed_graphs_are_rejected(self):
        for graph in ({}, [{"ref": "missing", "dependsOn": []}],
                      [{"ref": "root", "dependsOn": "dependency"}],
                      [{"ref": "root", "dependsOn": [None]}],
                      [{"ref": "root"}, {"ref": "root"}]):
            with self.subTest(graph=graph):
                self.document["dependencies"] = graph
                self.assertNotEqual(self.run_check().returncode, 0)

    def test_duplicate_or_missing_component_reference_is_rejected(self):
        for reference in (None, "root"):
            with self.subTest(reference=reference):
                self.document["components"][0]["bom-ref"] = reference
                self.assertNotEqual(self.run_check().returncode, 0)

    def test_packed_identity_must_match_sbom(self):
        self.document["metadata"]["component"]["name"] = "Honua.Sdk.Admin"
        self.sbom.write_text(json.dumps(self.document))
        result = subprocess.run(
            ["python3", str(SCRIPT), str(self.sbom), "--package-id", "Honua.Sdk.Admin",
             "--version", "5.0.0", "--package", str(self.write_package())],
            capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("nuspec", result.stderr)

    def run_attestation(self):
        self.write_package()
        workflow = WORKFLOW.read_text()
        step = workflow.split("- name: Select and check this package's SBOM", 1)[1]
        shell = step.split("        run: |\n", 1)[1].split("\n      - name:", 1)[0]
        shell = "\n".join(line[10:] for line in shell.splitlines())
        self.sbom.write_text(json.dumps(self.document))
        (self.root / "scripts").symlink_to(ROOT / "scripts", target_is_directory=True)
        environment = self.root / "github-env"
        result = subprocess.run(["bash", "-c", shell], cwd=self.root,
                                env={**os.environ, "PACKAGE_ID": "Honua.Sdk",
                                     "PACKAGE_VERSION": "5.0.0", "GITHUB_ENV": str(environment)},
                                capture_output=True, text=True)
        return result, environment

    def test_attestation_rejects_wrong_release_before_export(self):
        self.document["metadata"]["component"]["version"] = "0.0.0"
        result, environment = self.run_attestation()
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertFalse(environment.exists())

    def test_attestation_exports_validated_document(self):
        result, environment = self.run_attestation()
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(environment.read_text(), "SBOM_PATH=./sboms/Honua.Sdk/Honua.Sdk.cdx.json\n")

    def test_generator_binds_resolved_identity_and_checks_before_upload(self):
        workflow = WORKFLOW.read_text()
        generation = workflow.split("- name: Generate CycloneDX SBOM per package", 1)[1]
        generation = generation.split("- name: Upload SBOM artifacts", 1)[0]
        self.assertIn("${{ steps.package-version.outputs.value }}", generation)
        self.assertRegex(generation, r'--set-name "\$\{pkg_id\}"')
        self.assertRegex(generation, r'--set-version "\$\{PACKAGE_VERSION\}"')
        self.assertIn("python3 scripts/check-sbom.py", generation)
        self.assertIn("--include-project-references", generation)
        self.assertIn('--package "./nupkgs/${pkg_id}.${PACKAGE_VERSION}.nupkg"', generation)
        attestation = workflow.split("  attest-dotnet-sbom:", 1)[1]
        self.assertIn("${{ needs.release-smoke.outputs.package-version }}", attestation)
        self.assertIn("      - release-smoke", attestation)
        self.assertIn("-packages-input-unsigned", attestation)
        self.assertIn('--package "./nupkgs/${PACKAGE_ID}.${PACKAGE_VERSION}.nupkg"', attestation)
        self.assertLess(attestation.index("uses: actions/checkout@"),
                        attestation.index("python3 scripts/check-sbom.py"))


if __name__ == "__main__":
    unittest.main()
