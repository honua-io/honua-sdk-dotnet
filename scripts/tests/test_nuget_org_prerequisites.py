from __future__ import annotations

import importlib.util
from pathlib import Path
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "check-nuget-org-prerequisites.py"
WORKFLOW = ROOT / ".github" / "workflows" / "publish-dotnet-sdk.yml"
STAGING_WORKFLOW = ROOT / ".github" / "workflows" / "staging-integration.yml"
NUGET_CONFIG = ROOT / "NuGet.config"
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

    def evaluate(self, version: str, *, published: list[str]):
        directory = self.manifest(version)
        self.addCleanup(directory.cleanup)
        return MODULE.evaluate(
            manifest=Path(directory.name, "Directory.Packages.props"),
            package="Geospatial.Grpc",
            index_url="https://example.invalid/index.json",
            fetch_versions=lambda _: published,
        )

    def failed(self, result: dict) -> set[str]:
        return {check["check"] for check in result["checks"] if check["status"] == "fail"}

    def test_stable_public_dependency_passes(self) -> None:
        result = self.evaluate("1.0.0", published=["1.0.0"])
        self.assertEqual("pass", result["status"])
        self.assertEqual(set(), self.failed(result))

    def test_prerelease_dependency_is_refused_even_when_public(self) -> None:
        result = self.evaluate("1.0.0-alpha.1", published=["1.0.0-alpha.1"])
        self.assertEqual("fail", result["status"])
        self.assertIn("dependency-is-stable", self.failed(result))

    def test_missing_public_dependency_fails_closed(self) -> None:
        result = self.evaluate("1.0.0", published=[])
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
            fetch_versions=unreadable,
        )
        self.assertEqual("fail", result["status"])
        self.assertIn("dependency-on-nuget-org", self.failed(result))


class PublishWorkflowContractTests(unittest.TestCase):
    def test_public_protocol_dependency_is_not_mapped_to_github_packages(self) -> None:
        config = NUGET_CONFIG.read_text(encoding="utf-8")
        github_mapping = config.split('<packageSource key="github-honua">', 1)[1].split(
            "</packageSource>", 1
        )[0]
        self.assertNotIn("Geospatial.Grpc", github_mapping)

    def test_package_smoke_consumes_protocol_dependency_from_nuget_org_only(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        smoke = workflow.index("- name: Package install smoke")
        upload = workflow.index("- name: Record immutable package checksums")
        smoke_block = workflow[smoke:upload]
        self.assertIn("https://api.nuget.org/v3/index.json", smoke_block)
        self.assertNotIn("nuget.pkg.github.com", smoke_block)

    def test_stable_release_fails_before_build_when_public_prerequisites_are_missing(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        preflight = workflow.index("- name: Validate stable nuget.org prerequisites")
        build = workflow.index("- name: Build SDK packages")
        self.assertLess(preflight, build)
        self.assertIn("check-nuget-org-prerequisites.py", workflow)
        self.assertNotIn("nuget.org publish skipped", workflow)
        self.assertNotIn("nuget-org-ready", workflow)
        self.assertIn("environment: public-nuget", workflow)
        self.assertIn(
            "Trusted Publishing exchange produced no key; refusing a partial release",
            workflow,
        )
        self.assertIn("uses: NuGet/login@", workflow)
        self.assertIn("id-token: write", workflow)
        self.assertNotIn("secrets.NUGET_API_KEY", workflow)

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

    def test_coordinates_are_preflighted_before_any_registry_mutation(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        preflight = workflow.index("- name: Preflight every immutable registry coordinate")
        nuget_push = workflow.index("- name: Publish stable packages to nuget.org")
        nuget_verify = workflow.index("- name: Verify stable packages from nuget.org")
        github_push = workflow.index("- name: Publish to GitHub Packages")
        self.assertLess(preflight, nuget_push)
        self.assertLess(preflight, github_push)
        self.assertIn("check-package-coordinates.py", workflow[preflight:nuget_push])
        self.assertNotIn("--skip-duplicate", workflow[nuget_push:nuget_verify])
        self.assertNotIn("--skip-duplicate", workflow[github_push:])

    def test_non_dry_publish_requires_staging_and_trunk_bound_tag(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        staging = workflow.index("release-staging-integration:")
        publish = workflow.index("publish-dotnet-packages:")
        self.assertIn("github.event.inputs.dry_run != 'true'", workflow[staging:publish])
        self.assertIn(
            "needs.release-staging-integration.result == 'success'",
            workflow[publish:],
        )
        self.assertNotIn(
            "needs.release-staging-integration.result == 'skipped'",
            workflow[publish:],
        )
        self.assertIn(
            'git merge-base --is-ancestor "${checked_out_sha}" refs/remotes/origin/trunk',
            workflow[:staging],
        )

    def test_trusted_publishing_replaces_author_signing(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        protected_environment = workflow.index("environment: public-nuget")
        trusted_publishing = workflow.index("uses: NuGet/login@")
        self.assertLess(protected_environment, trusted_publishing)
        self.assertIn("packages-input-unsigned", workflow)
        self.assertNotIn("NUGET_SIGNING_", workflow)
        self.assertNotIn("dotnet nuget sign", workflow)
        self.assertNotIn("packages-signed", workflow)
        self.assertNotIn("secrets.NUGET_API_KEY", workflow)
        self.assertNotIn("secrets: inherit", workflow)

    def test_prerelease_skips_trusted_publishing_exchange(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        login = workflow.index("- name: Exchange OIDC for a short-lived NuGet key")
        preflight = workflow.index("- name: Preflight every immutable registry coordinate")
        login_block = workflow[login:preflight]
        stable_condition = "if: ${{ !contains(needs.release-smoke.outputs.package-version, '-') }}"
        self.assertEqual(2, login_block.count(stable_condition))

    def test_symbol_coordinates_are_preflighted_and_proven_without_duplicate_acceptance(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        preflight = workflow.index("- name: Preflight every immutable registry coordinate")
        symbol_push = workflow.index("- name: Submit portable symbol packages to nuget.org")
        symbol_proof = workflow.index("- name: Verify portable symbol packages from nuget.org")
        github_push = workflow.index("- name: Publish to GitHub Packages")
        self.assertLess(preflight, symbol_push)
        self.assertLess(symbol_push, symbol_proof)
        self.assertLess(symbol_proof, github_push)
        self.assertIn("--symbol-package-base-address", workflow[preflight:symbol_push])
        self.assertIn("--symbol-publish-list-out", workflow[preflight:symbol_push])
        self.assertIn("--require-present", workflow[symbol_proof:github_push])
        self.assertNotIn("--skip-duplicate", workflow)

    def test_registry_evidence_survives_partial_publication_failure(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        upload = workflow.index("- name: Upload registry publication evidence")
        self.assertIn("if: ${{ always() }}", workflow[upload:])
        self.assertIn("if-no-files-found: warn", workflow[upload:])

    def test_release_tools_and_artifacts_are_rerun_safe(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        self.assertIn('DOTNET_VERSION: "10.0.100"', workflow)
        self.assertNotIn('DOTNET_VERSION: "10.0.x"', workflow)
        staging_workflow = STAGING_WORKFLOW.read_text(encoding="utf-8")
        self.assertIn('DOTNET_VERSION: "10.0.100"', staging_workflow)
        self.assertIn("Verify pinned .NET SDK", staging_workflow)
        self.assertIn("CycloneDX --version 4.2.0", workflow)
        self.assertNotIn("CycloneDX --version 4.*", workflow)
        self.assertIn("overwrite: true", workflow)
        self.assertIn("sha256sum --check SHA256SUMS", workflow)
        self.assertIn("git rev-parse \"refs/tags/${GITHUB_REF_NAME}^{commit}\"", workflow)

if __name__ == "__main__":
    unittest.main()
