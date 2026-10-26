"""honua-release nightly dispatch contract (certification/RECEIPT-V2.md "Nightly producer run identity").

The nightly dispatches sdk-certification.yml with `nightly_dispatch_id`; every executed receipt must
name the run that observed it inside the identity hashed into evidence_digest, and the run title
must carry the dispatch id so honua-evidence can bind the receipt to the GitHub run.
"""
import hashlib
import importlib.util
import json
import os
import re
import tempfile
import unittest
from argparse import Namespace
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github/workflows/sdk-certification.yml"
SPEC = importlib.util.spec_from_file_location("sdk_certification_dispatch", ROOT / "scripts/generate-sdk-certification.py")
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

DISPATCH = "nightly-certification-40367649673-2"
ACTIONS_ENV = {
    "GITHUB_ACTIONS": "true",
    "GITHUB_EVENT_NAME": "workflow_dispatch",
    "GITHUB_REPOSITORY": "honua-io/honua-sdk-dotnet",
    "GITHUB_WORKFLOW_REF": "honua-io/honua-sdk-dotnet/.github/workflows/sdk-certification.yml"
                           "@refs/tags/nightly-candidate/" + "a" * 40,
    "GITHUB_RUN_ID": "40367650001",
    "GITHUB_RUN_ATTEMPT": "3",
    "HONUA_NIGHTLY_DISPATCH_ID": DISPATCH,
}
EXPECTED_RUN = {"repository": "honua-io/honua-sdk-dotnet", "workflow": "sdk-certification.yml",
                "run_id": 40367650001, "run_attempt": 3, "dispatch_id": DISPATCH}
TITLE = ".NET SDK Protocol Certification"
DOCUMENT = {"operations": [
    {"id": "Fixture.IClient.QueryAsync", "surface": "geoservices", "client": "Fixture.IClient",
     "status": "exercised", "operation": "QueryAsync", "requiredTiers": ["pr"],
     "scenarioFacets": ["read-only"], "tests": ["Fixture.Query"]},
    {"id": "Fixture.IClient.SkippedAsync", "surface": "geoservices", "client": "Fixture.IClient",
     "status": "exercised", "operation": "SkippedAsync", "requiredTiers": ["pr"],
     "scenarioFacets": ["read-only"], "tests": ["Fixture.Skipped"]},
]}


def _clean_env(**overrides):
    env = {key: value for key, value in os.environ.items()
           if not key.startswith("GITHUB_") and key != "HONUA_NIGHTLY_DISPATCH_ID"}
    env.update(overrides)
    return mock.patch.dict(os.environ, env, clear=True)


def _digest(receipt):
    return "sha256:" + hashlib.sha256(json.dumps(
        receipt, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")).hexdigest()


class GeneratorProducerRunTests(unittest.TestCase):
    def _emit(self, directory):
        root = Path(directory)
        trx = root / "fixture.trx"
        trx.write_text('<TestRun><Results><UnitTestResult testName="Fixture.Query" outcome="Passed" />'
                       '<UnitTestResult testName="Fixture.Skipped" outcome="NotExecuted" /></Results></TestRun>')
        requirements = root / "requirements.json"
        requirements.write_text(json.dumps({
            "schema": "honua.protocol-certification-requirements/v1", "revision": "fixture-governed-revision",
            "receipt_schema_min": "v2", "complete": True, "requirements": [
                {"capability_key": "sdk-dotnet.geoservices", "surface": "geoservices", "operation": op["id"],
                 "canonical_client": "Honua SDK .NET", "client_version": "1.2.3",
                 "deployment_target": "local-docker", "client_lane": "sdk-dotnet-certification",
                 "maturity": "supported", "required_tier": "nightly", "addressable_by_client": True,
                 "licensed": False, "entitlement_policy_revision": None,
                 "contract_revision": "sdk-dotnet-certification@" + "e" * 40,
                 "auth_policy_revision": "api-key-protected-v1", "fixture_revision": "sha256:" + "d" * 64,
                 "scenario_facets": ["positive"]}
                for op in DOCUMENT["operations"]]}))
        args = Namespace(
            tier="pr", trx=[trx], evidence=root / "sdk-certification.json", started_at=None,
            requirements=requirements, requirements_revision="fixture-governed-revision",
            sdk_commit="a" * 40, sdk_package_source_sha="e" * 40, sdk_package_id="Honua.Sdk",
            sdk_package_digest="sha256:" + "f" * 64, sdk_version="1.2.3", server_source_sha="b" * 40,
            image_source_revision="b" * 40, server_image="ghcr.io/honua-io/honua-server@sha256:" + "c" * 64,
            release_cut=None, candidate_cut="2026-01-01T00:00:00Z", fixture_revision="sha256:" + "d" * 64,
            seed_revision="b" * 40, evidence_uri="https://example.test/run/1", allow_nonpass=True,
            base_url="https://candidate.test/", grpc_base_url="https://candidate.test:8443/",
            service_id="places", layer_id="0", collection_id="places")
        MODULE.write_evidence(args, DOCUMENT)
        return json.loads(args.evidence.read_text())["observations"]

    def test_dispatched_run_binds_exact_producer_run_inside_the_digest(self):
        with _clean_env(**ACTIONS_ENV), tempfile.TemporaryDirectory() as directory:
            executed, skipped = self._emit(directory)
        self.assertEqual("pass", executed["result"])
        receipt = executed["evidence_receipt"]
        self.assertEqual(EXPECTED_RUN, receipt["identity"]["producer_run"])
        for key in ("run_id", "run_attempt"):
            self.assertIs(int, type(receipt["identity"]["producer_run"][key]))
        self.assertEqual(executed["evidence_digest"], _digest(receipt))
        self.assertEqual("https://evidence.honua.io/data/sha256/" + executed["evidence_digest"][7:],
                         executed["evidence_uri"])
        self.assertEqual({"positive": {"result": "pass", "evidence_digest": executed["evidence_digest"]}},
                         executed["facet_results"])
        # The .NET lane keeps package identity on the observation only (RECEIPT-V2.md).
        self.assertNotIn("client_package", receipt["identity"])
        self.assertNotIn("client_package_digest", receipt["identity"])
        self.assertEqual("skip", skipped["result"])
        self.assertIsNone(skipped["evidence_receipt"])
        self.assertNotIn("producer_run", skipped)

    def test_undispatched_run_emits_no_producer_run(self):
        for env in ({}, {**ACTIONS_ENV, "HONUA_NIGHTLY_DISPATCH_ID": ""},
                    {key: value for key, value in ACTIONS_ENV.items() if key != "HONUA_NIGHTLY_DISPATCH_ID"}):
            with self.subTest(env=env), _clean_env(**env), tempfile.TemporaryDirectory() as directory:
                executed, _ = self._emit(directory)
                self.assertNotIn("producer_run", executed["evidence_receipt"]["identity"])
                self.assertEqual(executed["evidence_digest"], _digest(executed["evidence_receipt"]))

    def test_dispatch_outside_the_canonical_actions_run_is_refused(self):
        refusals = {
            "malformed id": {"HONUA_NIGHTLY_DISPATCH_ID": "nightly-certification-0-1"},
            "free-form id": {"HONUA_NIGHTLY_DISPATCH_ID": "manual"},
            "trailing id": {"HONUA_NIGHTLY_DISPATCH_ID": DISPATCH + "\n"},
            "not actions": {"GITHUB_ACTIONS": "false"},
            "wrong event": {"GITHUB_EVENT_NAME": "schedule"},
            "pull request": {"GITHUB_EVENT_NAME": "pull_request"},
            "wrong repository": {"GITHUB_REPOSITORY": "someone/honua-sdk-dotnet"},
            "wrong workflow": {"GITHUB_WORKFLOW_REF": "honua-io/honua-sdk-dotnet/.github/workflows/ci.yml@refs/heads/trunk"},
            "fork workflow": {"GITHUB_WORKFLOW_REF": "someone/honua-sdk-dotnet/.github/workflows/sdk-certification.yml@refs/heads/trunk"},
            "pull ref": {"GITHUB_WORKFLOW_REF": "honua-io/honua-sdk-dotnet/.github/workflows/sdk-certification.yml@refs/pull/1/merge"},
            "zero run": {"GITHUB_RUN_ID": "0"},
            "missing attempt": {"GITHUB_RUN_ATTEMPT": ""},
            "non-numeric attempt": {"GITHUB_RUN_ATTEMPT": "1a"},
        }
        for name, override in refusals.items():
            with self.subTest(name), _clean_env(**{**ACTIONS_ENV, **override}), \
                    tempfile.TemporaryDirectory() as directory:
                with self.assertRaisesRegex(ValueError, "nightly"):
                    self._emit(directory)
                self.assertFalse((Path(directory) / "sdk-certification.json").exists())

    def test_trunk_head_dispatch_is_accepted(self):
        env = {**ACTIONS_ENV, "GITHUB_WORKFLOW_REF":
               "honua-io/honua-sdk-dotnet/.github/workflows/sdk-certification.yml@refs/heads/trunk"}
        with _clean_env(**env), tempfile.TemporaryDirectory() as directory:
            executed, _ = self._emit(directory)
        self.assertEqual(EXPECTED_RUN, executed["evidence_receipt"]["identity"]["producer_run"])


class WorkflowDispatchContractTests(unittest.TestCase):
    TEXT = WORKFLOW.read_text(encoding="utf-8")

    def test_dispatch_input_is_declared_optional_string(self):
        block = re.search(r"\n      nightly_dispatch_id:\n((?:        .*\n)+)", self.TEXT)
        self.assertIsNotNone(block, "workflow_dispatch must accept nightly_dispatch_id (HTTP 422 otherwise)")
        self.assertIn("required: false", block.group(1))
        self.assertIn("type: string", block.group(1))
        dispatch = self.TEXT.index("  workflow_dispatch:")
        self.assertGreater(block.start(), dispatch)

    def test_run_name_carries_the_dispatch_id(self):
        self.assertEqual(f"name: {TITLE}", self.TEXT.splitlines()[0])
        self.assertIn(
            "run-name: ${{ inputs.nightly_dispatch_id && format('" + TITLE
            + " [{0}]', inputs.nightly_dispatch_id) || '" + TITLE + "' }}\n", self.TEXT)

    def test_dispatch_id_reaches_the_receipt_generator(self):
        step = self.TEXT[self.TEXT.index("- name: Emit and enforce normalized certification evidence"):
                         self.TEXT.index("- name: Upload certification evidence")]
        self.assertIn("HONUA_NIGHTLY_DISPATCH_ID: ${{ inputs.nightly_dispatch_id || '' }}", step)
        self.assertIn("scripts/generate-sdk-certification.py", step)

    def test_artifact_name_is_unique_per_run_attempt_and_keeps_evidence_prefix(self):
        self.assertIn(
            "name: dotnet-sdk-${{ steps.identity.outputs.tier }}-certification-"
            "${{ github.run_id }}-${{ github.run_attempt }}\n", self.TEXT)
        self.assertIn("--evidence artifacts/evidence/sdk-certification.json", self.TEXT)


if __name__ == "__main__":
    unittest.main()
