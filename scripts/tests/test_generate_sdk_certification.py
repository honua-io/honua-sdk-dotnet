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
                image_source_revision="b" * 40,
                server_image="ghcr.io/honua-io/honua-server@sha256:" + "c" * 64,
                release_cut="2026-01-01T00:00:00Z",
                fixture_revision="sha256:" + "e" * 64,
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
                image_source_revision="b" * 40,
                server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                release_cut=None, fixture_revision="fixture", seed_revision="b" * 40,
            ), document)
            self.assertEqual(1, result)

    def test_interface_inheritance_is_resolved_transitively(self):
        document = MODULE.build_document()
        geocoding = [
            cell for cell in document["operations"]
            if cell["client"].endswith("IHonuaGeocodingClient")
        ]
        self.assertTrue(geocoding)
        self.assertTrue(all(cell["status"] != "non-addressable" for cell in geocoding))

    def test_overloads_have_distinct_ids_and_only_the_invoked_signature_is_exercised(self):
        document = MODULE.build_document()
        overloads = [
            cell for cell in document["operations"]
            if cell["client"].endswith("IHonuaWfsClient") and cell["operation"] == "GetFeaturesAsync"
        ]
        self.assertEqual(2, len(overloads))
        self.assertEqual(2, len({cell["id"] for cell in overloads}))
        ordinary = next(cell for cell in overloads if "IWfsOutputFormatHandler" not in cell["signature"])
        handler = next(cell for cell in overloads if "IWfsOutputFormatHandler" in cell["signature"])
        self.assertEqual("exercised", ordinary["status"])
        self.assertEqual("gap", handler["status"])

    def test_async_enumerable_and_local_service_operations_are_cataloged(self):
        document = MODULE.build_document()
        operations = document["operations"]
        self.assertTrue(any(cell["operation"] == "GetFeaturesAsyncEnumerable" for cell in operations))
        self.assertTrue(any(
            cell["client"].endswith("IHonuaFeatureServerClient")
            and cell["operation"] == "GetFeatureAsync"
            for cell in operations
        ))
        apply_edits = next(
            cell for cell in operations
            if cell["client"].endswith("IHonuaFeatureEditClient") and cell["operation"] == "ApplyEditsAsync"
        )
        self.assertEqual("exercised", apply_edits["status"])

    def test_known_state_changing_verbs_are_mutations(self):
        document = MODULE.build_document()
        names = {
            "RollbackDeployOperationAsync", "RotateEncryptionKeyAsync", "ImportRasterAsync",
            "PatchItemAsync", "RollbackAsync", "CommitEditSessionAsync", "StartEditSessionAsync",
            "UnsubscribeAsync", "ReopenVersionAsync",
        }
        matched = [cell for cell in document["operations"] if cell["operation"] in names]
        self.assertEqual(names, {cell["operation"] for cell in matched})
        self.assertTrue(all("mutation" in cell["scenarioFacets"] for cell in matched))

    def test_paginated_operations_are_classified_for_evidence(self):
        document = MODULE.build_document()
        names = {"QueryPagesAsync", "GetItemsPagesAsync", "GetFeaturesAsyncEnumerable"}
        matched = [cell for cell in document["operations"] if cell["operation"] in names]
        self.assertEqual(names, {cell["operation"] for cell in matched})
        self.assertTrue(all("pagination" in cell["scenarioFacets"] for cell in matched))

    def test_every_named_test_must_have_a_passing_result(self):
        document = {
            "operations": [{
                "id": "Honua.Example.IHonuaExampleClient.GetAsync",
                "surface": "example",
                "operation": "GetAsync",
                "status": "exercised",
                "requiredTiers": ["pr"],
                "scenarioFacets": ["read-only"],
                "tests": ["Example.Tests.First", "Example.Tests.Second"],
            }]
        }
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "partial.trx"
            trx.write_text(
                '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
                '<UnitTestResult testName="Example.Tests.First" outcome="Passed" />'
                '</Results></TestRun>',
                encoding="utf-8",
            )
            evidence = Path(directory) / "evidence.json"
            result = MODULE.write_evidence(Namespace(
                tier="pr", trx=[trx], evidence=evidence, started_at=None,
                sdk_commit="a" * 40, sdk_version="1.6.0",
                server_source_sha="b" * 40, image_source_revision="b" * 40,
                server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                release_cut=None, fixture_revision="sha256:" + "d" * 64, seed_revision="b" * 40,
            ), document)
            self.assertEqual(1, result)


if __name__ == "__main__":
    unittest.main()
