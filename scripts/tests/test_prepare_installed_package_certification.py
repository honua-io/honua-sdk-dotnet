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
            "tier": "release", "package_id": "Honua.Sdk", "package_version": "1.6.0",
            "package_digest": "sha256:" + "a" * 64, "package_source_sha": "b" * 40,
            "publication_state": "published", "registry": "github-packages",
            "release_cut": "2026-08-27T03:54:36Z",
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
            {"package_digest": "sha256:" + "A" * 64},
            {"package_source_sha": "short"},
            {"registry": "local-feed"},
            {"release_cut": ""},
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


if __name__ == "__main__":
    unittest.main()
