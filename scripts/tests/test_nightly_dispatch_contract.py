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
import subprocess
import tempfile
import textwrap
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
    # The harness commit the nightly dispatched (trunk head with the contract); the fixture's
    # sdk_commit, so it differs from the published package source "e" * 40.
    "GITHUB_SHA": "a" * 40,
}
HARNESS_COMMIT = "a" * 40
PACKAGE_SOURCE_SHA = "e" * 40
PACKAGE_DIGEST = "sha256:" + "f" * 64
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
    def _emit(self, directory, **overrides):
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
        for key, value in overrides.items():
            setattr(args, key, value)
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
        self.assertEqual("skip", skipped["result"])
        self.assertIsNone(skipped["evidence_receipt"])
        self.assertNotIn("producer_run", skipped)

    def test_dispatched_receipt_identity_names_harness_and_published_package(self):
        # Ruling sdk-harness-trunk-pinned-package-2026-10-27: the trunk harness runs against the
        # pinned published package, so the hashed identity separates the two and binds the package.
        with _clean_env(**ACTIONS_ENV), tempfile.TemporaryDirectory() as directory:
            executed, _ = self._emit(directory)
        identity = executed["evidence_receipt"]["identity"]
        self.assertEqual(HARNESS_COMMIT, identity["harness_commit"])
        self.assertEqual(PACKAGE_SOURCE_SHA, identity["producer_source_sha"])
        self.assertEqual(PACKAGE_SOURCE_SHA, executed["producer_source_sha"])
        self.assertEqual("Honua.Sdk", identity["client_package"])
        self.assertEqual(PACKAGE_DIGEST, identity["client_package_digest"])
        self.assertEqual(identity["client_package"], executed["client_package"])
        self.assertEqual(identity["client_package_digest"], executed["client_package_digest"])
        self.assertEqual("1.2.3", identity["client_version"])
        self.assertEqual("sdk-dotnet-certification@" + PACKAGE_SOURCE_SHA, identity["contract_revision"])
        self.assertEqual(executed["evidence_digest"], _digest(executed["evidence_receipt"]))

    def test_dispatched_receipt_refuses_an_unbound_harness_or_package(self):
        refusals = {
            "missing harness": ({"GITHUB_SHA": ""}, {}),
            "short harness": ({"GITHUB_SHA": "a" * 7}, {}),
            "harness is not the checkout": ({"GITHUB_SHA": "9" * 40}, {}),
            "repacked package": ({}, {"sdk_package_id": "Honua.Sdk.Repacked"}),
            "missing digest": ({}, {"sdk_package_digest": None}),
            "malformed digest": ({}, {"sdk_package_digest": "sha256:" + "F" * 64}),
        }
        for name, (env, args) in refusals.items():
            with self.subTest(name), _clean_env(**{**ACTIONS_ENV, **env}), \
                    tempfile.TemporaryDirectory() as directory:
                with self.assertRaisesRegex(ValueError, "nightly"):
                    self._emit(directory, **args)
                self.assertFalse((Path(directory) / "sdk-certification.json").exists())

    def test_undispatched_run_emits_no_producer_run(self):
        for env in ({}, {**ACTIONS_ENV, "HONUA_NIGHTLY_DISPATCH_ID": ""},
                    {key: value for key, value in ACTIONS_ENV.items() if key != "HONUA_NIGHTLY_DISPATCH_ID"}):
            with self.subTest(env=env), _clean_env(**env), tempfile.TemporaryDirectory() as directory:
                executed, _ = self._emit(directory)
                identity = executed["evidence_receipt"]["identity"]
                self.assertNotIn("producer_run", identity)
                # Unchanged outside the nightly: the checkout is producer_source_sha, and the .NET
                # package identity stays on the observation only.
                for field in ("harness_commit", "client_package", "client_package_digest"):
                    self.assertNotIn(field, identity)
                self.assertEqual(HARNESS_COMMIT, identity["producer_source_sha"])
                self.assertEqual(HARNESS_COMMIT, executed["producer_source_sha"])
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


DISPATCH_INPUTS = ("sdk_package", "sdk_package_version", "sdk_package_digest", "sdk_source_sha")
REQUIRED_WHEN_DISPATCHED = ("requirements_ref", "requirements_revision", *DISPATCH_INPUTS,
                            "server_image", "server_source_sha", "release_cut")


def _step(text, name):
    return text.split(f"      - name: {name}\n", 1)[1].split("\n      - ", 1)[0]


def _run_step(text, name, env, cwd):
    script = textwrap.dedent(_step(text, name).split("        run: |\n", 1)[1])
    return subprocess.run(["bash", "-c", script], cwd=cwd, env={**os.environ, **env},
                          capture_output=True, text=True)


class WorkflowDispatchContractTests(unittest.TestCase):
    TEXT = WORKFLOW.read_text(encoding="utf-8")

    def test_package_inputs_are_declared_optional_strings(self):
        dispatch = self.TEXT.index("  workflow_dispatch:")
        for name in DISPATCH_INPUTS:
            with self.subTest(name):
                block = re.search(rf"\n      {name}:\n((?:        .*\n)+)", self.TEXT)
                self.assertIsNotNone(block, f"workflow_dispatch must accept {name} (HTTP 422 otherwise)")
                self.assertIn("required: false", block.group(1))
                self.assertIn("type: string", block.group(1))
                self.assertGreater(block.start(), dispatch)

    def _contract(self, **inputs):
        env = {name.upper(): "" for name in REQUIRED_WHEN_DISPATCHED}
        env.update({"NIGHTLY_DISPATCH_ID": "", **inputs})
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "output"
            completed = _run_step(self.TEXT, "Require the nightly dispatch contract",
                                  {**env, "GITHUB_OUTPUT": str(output)}, directory)
            values = dict(line.split("=", 1) for line in output.read_text().splitlines()) if output.exists() else {}
        return completed, values

    def test_dispatched_run_requires_every_governed_input(self):
        complete = {name.upper(): "x" for name in REQUIRED_WHEN_DISPATCHED}
        completed, values = self._contract(NIGHTLY_DISPATCH_ID=DISPATCH, **complete)
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual("true", values["dispatched"])
        for name in REQUIRED_WHEN_DISPATCHED:
            with self.subTest(missing=name):
                completed, _ = self._contract(NIGHTLY_DISPATCH_ID=DISPATCH, **{**complete, name.upper(): ""})
                self.assertNotEqual(0, completed.returncode)
                self.assertIn(name, completed.stdout)

    def test_undispatched_run_requires_nothing_new(self):
        completed, values = self._contract()
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual("false", values["dispatched"])

    def test_dispatched_run_pins_the_dispatched_package_not_the_committed_pins(self):
        pin = _step(self.TEXT, "Pin the dispatched package and server")
        self.assertIn("if: ${{ inputs.nightly_dispatch_id != '' }}", pin)
        self.assertIn("--dispatched-pins-output artifacts/package/dispatched-pins.json", pin)
        for flag, name in (("--sdk-package", "sdk_package"), ("--sdk-package-version", "sdk_package_version"),
                           ("--sdk-package-digest", "sdk_package_digest"), ("--sdk-source-sha", "sdk_source_sha"),
                           ("--server-image", "server_image"), ("--server-source-sha", "server_source_sha"),
                           ("--release-cut", "release_cut")):
            with self.subTest(flag):
                self.assertIn(f"{name.upper()}: ${{{{ inputs.{name} }}}}", pin)
                self.assertIn(f'{flag} "${{{name.upper()}}}"', pin)
        load = _step(self.TEXT, "Load candidate pins")
        self.assertIn("pins=artifacts/package/dispatched-pins.json", load)
        self.assertIn('--pins "${pins}" --emit-env', load)
        self.assertIn('echo "CERT_PINS=${pins}"', load)
        verify = _step(self.TEXT, "Verify candidate pins against nuget.org and honua-release")
        self.assertIn("if: ${{ inputs.nightly_dispatch_id == '' }}", verify)
        restore = _step(self.TEXT, "Restore and verify exact installed SDK package")
        self.assertIn('--pins "${CERT_PINS}"', restore)
        self.assertNotIn("certification/candidate-pins.json", restore)
        order = [self.TEXT.index(f"- name: {name}") for name in (
            "Resolve certification tier", "Require the nightly dispatch contract",
            "Retain immutable governed requirements and strict consumer",
            "Pin the dispatched package and server", "Load candidate pins",
            "Resolve tier and immutable identity", "Restore and verify exact installed SDK package")]
        self.assertEqual(sorted(order), order)

    def _identity(self, pins_env, label, **inputs):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            image = pins_env["PINNED_SERVER_IMAGE"]
            metadata = json.dumps({"manifest": {"digest": image.rsplit("@", 1)[1]}})
            docker = root / "docker"
            docker.write_text("#!/bin/bash\nset -eu\ncase \"$1\" in\n"
                              "buildx) printf '%s' '" + metadata + "';;\n"
                              "login) cat >/dev/null;;\npull) ;;\n"
                              "image) printf '%s\\n' '" + label + "';;\n*) exit 91;;\nesac\n")
            gh = root / "gh"
            gh.write_text("#!/bin/bash\nset -eu\n[[ \"$1 $2\" == 'attestation verify' ]]\nprintf '[]'\n")
            docker.chmod(0o700)
            gh.chmod(0o700)
            output = root / "output"
            env = {"PATH": str(root) + os.pathsep + os.environ["PATH"], **pins_env,
                   "CERT_TIER": "release", "INPUT_IMAGE": image,
                   "INPUT_SOURCE_SHA": pins_env["PINNED_SERVER_SOURCE_SHA"], "INPUT_SEED_REF": "",
                   "INPUT_FIXTURE_REVISION": "", "INPUT_RELEASE_CUT": pins_env["PINNED_CANDIDATE_CUT"],
                   "NIGHTLY_DISPATCH_ID": DISPATCH, "GH_TOKEN": "synthetic-test-token",
                   "GITHUB_ACTOR": "test", "GITHUB_OUTPUT": str(output), **inputs}
            completed = _run_step(self.TEXT, "Resolve tier and immutable identity", env, root)
            values = dict(line.split("=", 1) for line in output.read_text().splitlines()) if output.exists() else {}
        return completed, values

    DISPATCHED_PINS = {
        # A dispatched candidate that is not certification/candidate-pins.json.
        "PINNED_SERVER_IMAGE": "ghcr.io/honua-io/honua-server@sha256:" + "1" * 64,
        "PINNED_SERVER_SOURCE_SHA": "2" * 40,
        "PINNED_SERVER_FIXTURE_REVISION": "sha256:" + "3" * 64,
        "PINNED_SDK_PACKAGE_ID": "Honua.Sdk", "PINNED_SDK_PACKAGE_VERSION": "9.9.9",
        "PINNED_SDK_PACKAGE_DIGEST": "sha256:" + "4" * 64, "PINNED_SDK_PACKAGE_SOURCE_SHA": "5" * 40,
        "PINNED_CANDIDATE_CUT": "2026-10-27T06:00:00Z",
    }

    def test_dispatched_identity_takes_the_dispatched_server_and_derives_the_fixture(self):
        completed, values = self._identity(self.DISPATCHED_PINS, "2" * 40)
        self.assertEqual(0, completed.returncode, completed.stdout + completed.stderr)
        self.assertEqual(self.DISPATCHED_PINS["PINNED_SERVER_IMAGE"], values["image"])
        self.assertEqual("2" * 40, values["source-sha"])
        self.assertEqual("sha256:" + "3" * 64, values["expected-fixture-revision"])
        self.assertEqual("2026-10-27T06:00:00Z", values["candidate-cut"])

    def test_dispatched_identity_refuses_a_mislabelled_image_or_disagreeing_fixture(self):
        completed, _ = self._identity(self.DISPATCHED_PINS, "6" * 40)
        self.assertNotEqual(0, completed.returncode)
        self.assertIn("revision label", completed.stdout)
        completed, _ = self._identity(self.DISPATCHED_PINS, "2" * 40,
                                      INPUT_FIXTURE_REVISION="sha256:" + "7" * 64)
        self.assertNotEqual(0, completed.returncode)
        self.assertIn("fixture", completed.stdout)

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
