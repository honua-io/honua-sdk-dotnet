import importlib.util
import tempfile
import unittest
from argparse import Namespace
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "prepare-installed-package-certification.py"
SPEC = importlib.util.spec_from_file_location("installed_package_certification", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class InstalledPackageCertificationTests(unittest.TestCase):
    @staticmethod
    def _identity(**overrides):
        values = {
            "tier": "release", "package_id": "Honua.Sdk",
            "package_version": MODULE.CANDIDATE_PACKAGE_VERSION,
            "package_digest": MODULE.CANDIDATE_PACKAGE_DIGEST,
            "package_source_sha": MODULE.CANDIDATE_PACKAGE_SOURCE_SHA,
            "publication_state": "published", "registry": "github-packages",
            "release_cut": "2026-09-16T08:23:18.979734311Z",
            "server_image": MODULE.CANDIDATE_SERVER_IMAGE,
            "server_source_sha": MODULE.CANDIDATE_SERVER_SHA,
            "fixture_revision": MODULE.CANDIDATE_FIXTURE_REVISION,
        }
        values.update(overrides)
        return Namespace(**values)

    def test_release_identity_accepts_exact_published_package(self):
        MODULE.validate_identity(self._identity())

    def test_release_refuses_unpublished_or_placeholder_package(self):
        for state in ("staged", "placeholder", "source-built"):
            with self.subTest(state=state), self.assertRaisesRegex(ValueError, "published"):
                MODULE.validate_identity(self._identity(publication_state=state))

    def test_release_refuses_floating_or_integrity_mismatched_identity(self):
        invalid = (
            {"package_version": "1.*"},
            {"package_version": "1.10.0"},
            {"package_digest": "sha256:" + "A" * 64},
            {"package_digest": "sha256:" + "a" * 64},
            {"package_source_sha": "short"},
            {"registry": "local-feed"},
            {"release_cut": ""},
            {"server_image": "ghcr.io/honua-io/honua-server:nightly"},
            {"server_image": "ghcr.io/honua-io/honua-server:nightly-aot"},
            {"server_image": "ghcr.io/honua-io/honua-server:nightly-87966c3"},
            {"server_image": "ghcr.io/honua-io/honua-server:nightly@sha256:" + "a" * 64},
            {"server_image": "ghcr.io/honua-io/honua-server@sha256:" + "a" * 64},
            {"server_source_sha": "c" * 40},
            {"fixture_revision": "sha256:" + "d" * 64},
        )
        for values in invalid:
            with self.subTest(values=values), self.assertRaises(ValueError):
                MODULE.validate_identity(self._identity(**values))

    def test_consumer_has_exact_package_and_no_project_reference(self):
        with tempfile.TemporaryDirectory() as directory:
            args = self._identity(output=Path(directory))
            project = MODULE._write_consumer(Path(directory), args).read_text(encoding="utf-8")
            self.assertIn('PackageReference Include="Honua.Sdk" Version="[1.6.0]"', project)
            self.assertNotIn("ProjectReference", project)

    def test_missing_package_bytes_fail_closed(self):
        message = MODULE.restore_failure_message("", package_present=False, server_sha=MODULE.CANDIDATE_SERVER_SHA)
        self.assertIn("published package bytes for server pin", message)
        self.assertIn(MODULE.CANDIDATE_SERVER_SHA, message)
        self.assertIn("do not exist", message)

    def test_untrusted_signature_fails_closed_without_claiming_missing_bytes(self):
        message = MODULE.restore_failure_message(
            "error NU3042: untrusted\nerror NU3018: not trusted",
            package_present=False,
            server_sha=MODULE.CANDIDATE_SERVER_SHA,
        )
        self.assertIn("NU3042/NU3018", message)
        self.assertNotIn("do not exist", message)
        self.assertIn("does not suppress", message)

    def test_workflow_pins_the_exact_candidate_digest(self):
        workflow = (MODULE.ROOT / ".github/workflows/sdk-certification.yml").read_text(encoding="utf-8")
        script = (MODULE.ROOT / "scripts/prepare-installed-package-certification.py").read_text(encoding="utf-8")
        self.assertIn(f'PINNED_SERVER_IMAGE: "{MODULE.CANDIDATE_SERVER_IMAGE}"', workflow)
        self.assertIn(f'PINNED_SERVER_SOURCE_SHA: "{MODULE.CANDIDATE_SERVER_SHA}"', workflow)
        self.assertIn(f'PINNED_SERVER_FIXTURE_REVISION: "{MODULE.CANDIDATE_FIXTURE_REVISION}"', workflow)
        self.assertNotRegex(workflow, r'PINNED_SERVER_IMAGE: "[^"]*:(nightly|latest)')
        self.assertNotIn("dotnet pack", script)
        self.assertNotIn("NuGetSignatureValidationMode", script)
        self.assertNotIn("WarningsNotAsErrors", script)


if __name__ == "__main__":
    unittest.main()
