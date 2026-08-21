import importlib.util
import tempfile
import unittest
from argparse import Namespace
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "generate-sdk-certification.py"
SPEC = importlib.util.spec_from_file_location("sdk_certification", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class SdkCertificationTests(unittest.TestCase):
    def test_current_ledger_catalogs_every_operation_and_owns_every_gap(self):
        document = MODULE.build_document()
        self.assertGreater(document["summary"]["total"], 0)
        self.assertEqual(document["summary"]["total"], len(document["operations"]))
        self.assertEqual(len({cell["id"] for cell in document["operations"]}), len(document["operations"]))
        for cell in document["operations"]:
            if cell["status"] != "exercised":
                self.assertIn("ownerIssue", cell)
                self.assertIn("disposition", cell)

    def test_release_identity_requires_exact_image_source_seed_and_cut(self):
        with self.assertRaisesRegex(ValueError, "seed revision"):
            MODULE._identity(Namespace(
                tier="release",
                sdk_commit="a" * 40,
                sdk_version="1.0.0",
                server_source_sha="b" * 40,
                server_image="ghcr.io/honua-io/honua-server@sha256:" + "c" * 64,
                release_cut="2026-01-01T00:00:00Z",
                fixture_revision="fixture-1",
                seed_revision="d" * 40,
            ))

    def test_missing_required_result_fails_closed(self):
        document = {
            "operations": [{
                "id": "Honua.Example.IHonuaExampleClient.GetAsync",
                "surface": "example",
                "operation": "GetAsync",
                "status": "exercised",
                "requiredTiers": ["pr"],
                "scenarioFacets": ["read-only"],
                "tests": ["Example.Tests.Get"],
            }]
        }
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "empty.trx"
            trx.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results /></TestRun>', encoding="utf-8")
            evidence = Path(directory) / "evidence.json"
            result = MODULE.write_evidence(Namespace(
                tier="pr", trx=[trx], evidence=evidence, started_at=None,
                sdk_commit="a" * 40, sdk_version="unreleased",
                server_source_sha="b" * 40,
                server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                release_cut=None, fixture_revision="fixture", seed_revision="b" * 40,
            ), document)
            self.assertEqual(1, result)


if __name__ == "__main__":
    unittest.main()
