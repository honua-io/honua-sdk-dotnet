from __future__ import annotations

import importlib.util
import json
from pathlib import Path
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "check-nuget-org-prerequisites.py"
WORKFLOW = ROOT / ".github" / "workflows" / "publish-dotnet-sdk.yml"
SPEC = importlib.util.spec_from_file_location("nuget_org_prerequisites", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class NugetOrgPrerequisiteTests(unittest.TestCase):
    def manifest(self, version: str) -> tempfile.TemporaryDirectory[str]:
        directory = tempfile.TemporaryDirectory()
        Path(directory.name, "Directory.Packages.props").write_text(
            "<Project><ItemGroup>"
            f'<PackageVersion Include="Geospatial.Grpc" Version="{version}" />'
            "</ItemGroup></Project>",
            encoding="utf-8",
        )
        return directory

    def evaluate(self, version: str, *, key: bool, published: list[str]):
        directory = self.manifest(version)
        self.addCleanup(directory.cleanup)
        return MODULE.evaluate(
            manifest=Path(directory.name, "Directory.Packages.props"),
            package="Geospatial.Grpc",
            index_url="https://example.invalid/index.json",
            api_key_present=key,
            fetch_versions=lambda _: published,
        )

    def failed(self, result: dict) -> set[str]:
        return {check["check"] for check in result["checks"] if check["status"] == "fail"}

    def test_stable_public_dependency_and_credential_pass(self) -> None:
        result = self.evaluate("1.0.0", key=True, published=["1.0.0"])
        self.assertEqual("pass", result["status"])
        self.assertEqual(set(), self.failed(result))

    def test_missing_credential_fails_without_recording_its_value(self) -> None:
        result = self.evaluate("1.0.0", key=False, published=["1.0.0"])
        self.assertEqual("fail", result["status"])
        self.assertIn("nuget-api-key-present", self.failed(result))
        serialized = json.dumps(result).lower()
        self.assertNotIn("password", serialized)
        self.assertNotIn("ghp_", serialized)
        self.assertNotIn("github_pat_", serialized)

    def test_prerelease_dependency_is_refused_even_when_public(self) -> None:
        result = self.evaluate("1.0.0-alpha.1", key=True, published=["1.0.0-alpha.1"])
        self.assertEqual("fail", result["status"])
        self.assertIn("dependency-is-stable", self.failed(result))

    def test_missing_public_dependency_fails_closed(self) -> None:
        result = self.evaluate("1.0.0", key=True, published=[])
        self.assertEqual("fail", result["status"])
        self.assertIn("dependency-on-nuget-org", self.failed(result))

    def test_unreadable_registry_fails_closed(self) -> None:
        directory = self.manifest("1.0.0")
        self.addCleanup(directory.cleanup)

        def unreadable(_: str) -> list[str]:
            raise ValueError("registry unavailable")

        result = MODULE.evaluate(
            manifest=Path(directory.name, "Directory.Packages.props"),
            package="Geospatial.Grpc",
            index_url="https://example.invalid/index.json",
            api_key_present=True,
            fetch_versions=unreadable,
        )
        self.assertEqual("fail", result["status"])
        self.assertIn("dependency-on-nuget-org", self.failed(result))


class PublishWorkflowContractTests(unittest.TestCase):
    def test_stable_release_fails_before_build_when_public_prerequisites_are_missing(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        preflight = workflow.index("- name: Validate stable nuget.org prerequisites")
        build = workflow.index("- name: Build SDK packages")
        self.assertLess(preflight, build)
        self.assertIn("check-nuget-org-prerequisites.py", workflow)
        self.assertNotIn("nuget.org publish skipped", workflow)
        self.assertNotIn("nuget-org-ready", workflow)

    def test_public_verification_precedes_the_secondary_github_feed(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        public_push = workflow.index("- name: Publish stable packages to nuget.org")
        public_verify = workflow.index("- name: Verify stable packages from nuget.org")
        github_push = workflow.index("- name: Publish to GitHub Packages")
        self.assertLess(public_push, public_verify)
        self.assertLess(public_verify, github_push)
        self.assertIn("eng/shipped-packages.txt", workflow[public_verify:github_push])
        for package in ("Honua.Sdk", "Honua.Sdk.Admin", "Honua.Sdk.Grpc", "Honua.Sdk.Cli"):
            self.assertIn(package, workflow[public_verify:github_push])
        self.assertIn("<clear />", workflow[public_verify:github_push])
        self.assertNotIn("nuget.pkg.github.com", workflow[public_verify:github_push])

if __name__ == "__main__":
    unittest.main()
