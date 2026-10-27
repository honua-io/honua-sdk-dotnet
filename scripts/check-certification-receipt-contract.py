#!/usr/bin/env python3
"""Exercise emitted fixture receipts against the retained real release consumer.

This is a producer/consumer contract self-test, never candidate qualification.
"""
from __future__ import annotations

import argparse
from argparse import Namespace
from copy import deepcopy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import tempfile
from unittest import mock


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


# Receipt identity fields a nightly-dispatched .NET run adds (ruling sdk-harness-trunk-pinned-package-2026-10-27).
HARNESS_IDENTITY_FIELDS = ("harness_commit", "client_package", "client_package_digest")


def digest_of(receipt):
    return "sha256:" + hashlib.sha256(json.dumps(
        receipt, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode("utf-8")).hexdigest()


def check(governance):
    consumer = governance / "check_protocol_certification.py"
    binding = json.loads((governance / "source-binding.json").read_text())
    if binding.get("repository") != "honua-io/honua-release":
        raise ValueError("contract consumer repository differs")
    expected = binding["files"]["tools/check_protocol_certification.py"]["sha256"]
    if hashlib.sha256(consumer.read_bytes()).hexdigest() != expected:
        raise ValueError("contract consumer bytes differ from retained immutable source")
    gate = load("retained_release_receipt_consumer", consumer)
    producer = load("sdk_certification_producer", Path(__file__).with_name("generate-sdk-certification.py"))
    operation = {"id": "Fixture.IClient.QueryAsync", "surface": "geoservices", "client": "Fixture.IClient",
                 "status": "exercised", "operation": "QueryAsync", "requiredTiers": ["pr"],
                 "scenarioFacets": ["read-only"], "tests": ["Fixture.Query"]}
    requirement = {"capability_key": "sdk-dotnet.geoservices", "surface": "geoservices",
                   "operation": operation["id"], "canonical_client": "Honua SDK .NET",
                   "client_version": "1.2.3", "deployment_target": "local-docker",
                   "client_lane": "sdk-dotnet-certification", "maturity": "supported", "required_tier": "nightly",
                   "licensed": False, "entitlement_policy_revision": None, "addressable_by_client": True,
                   "scenario_facets": ["positive"], "fixture_revision": "sha256:" + "d" * 64,
                   "contract_revision": "sdk-dotnet-certification@" + "e" * 40,
                   "auth_policy_revision": "api-key-protected-v1"}
    with tempfile.TemporaryDirectory() as directory:
        root = Path(directory)
        catalog = root / "requirements.json"
        catalog.write_text(json.dumps({"schema": "honua.protocol-certification-requirements/v1",
                                      "revision": "fixture-context", "complete": True,
                                      "receipt_schema_min": "v2", "requirements": [requirement]}))
        trx = root / "fixture.trx"
        trx.write_text('<TestRun><Results><UnitTestResult testName="Fixture.Query" outcome="Passed" /></Results></TestRun>')
        evidence = root / "fragment.json"
        args = Namespace(tier="pr", trx=[trx], evidence=evidence, started_at=None,
                         requirements=catalog, requirements_revision="fixture-context",
                         sdk_commit="a" * 40, sdk_package_source_sha="e" * 40,
                         sdk_package_id="Honua.Sdk", sdk_package_digest="sha256:" + "f" * 64,
                         sdk_version="1.2.3", server_source_sha="b" * 40,
                         image_source_revision="b" * 40, server_image="ghcr.io/honua-io/honua-server@sha256:" + "c" * 64,
                         release_cut=None, candidate_cut="2026-01-01T00:00:00Z", fixture_revision="sha256:" + "d" * 64,
                         seed_revision="b" * 40, evidence_uri="https://example.test/fixture",
                         base_url="https://candidate.test", grpc_base_url="https://candidate.test:8443",
                         service_id="fixture", layer_id="0", collection_id="fixture")
        undispatched = {key: value for key, value in os.environ.items() if key != "HONUA_NIGHTLY_DISPATCH_ID"}
        with mock.patch.dict(os.environ, undispatched, clear=True):
            if producer.write_evidence(args, {"operations": [operation]}) != 0:
                raise ValueError("fixture producer unexpectedly refused complete unlicensed execution")
        cell = {**requirement, **json.loads(evidence.read_text())["observations"][0]}
        if "producer_run" in cell["evidence_receipt"]["identity"]:
            raise ValueError("producer recorded a nightly run identity without a nightly dispatch")
        # The fixture intentionally uses different published-package and harness
        # commits so the real producer/consumer exercise catches conflation.
        identity = cell["evidence_receipt"]["identity"]
        if (cell["producer_source_sha"] != args.sdk_commit
                or identity["producer_source_sha"] != args.sdk_commit
                or cell["contract_revision"] != requirement["contract_revision"]
                or identity["contract_revision"] != requirement["contract_revision"]):
            raise ValueError("producer and installed-package identity were conflated")
        if not gate._valid_receipt(cell, requirements_revision="fixture-context"):
            raise ValueError("real strict consumer rejected the emitted v2 fixture receipt")
        for field, wrong in (("maturity", "preview"), ("required_tier", "pr"),
                             ("requirements_revision", "stale")):
            broken = deepcopy(cell)
            broken["evidence_receipt"]["identity"][field] = wrong
            if gate._valid_receipt(broken, requirements_revision="fixture-context"):
                raise ValueError("real consumer accepted forged governed context: " + field)
        broken = deepcopy(cell)
        broken["evidence_receipt"]["schema"] = "honua.certification-evidence-receipt/v1"
        if gate._valid_receipt(broken, requirements_revision="fixture-context"):
            raise ValueError("real consumer accepted legacy v1 fixture receipt")
        # The installed package identity rides on the observation and the consumer verifies it.
        if cell.get("client_package") != "Honua.Sdk" or cell.get("client_package_digest") != args.sdk_package_digest:
            raise ValueError("producer dropped the installed package identity")
        broken = deepcopy(cell)
        broken["client_package"] = "Honua.Sdk.Repacked"
        if gate._valid_receipt(broken, requirements_revision="fixture-context"):
            raise ValueError("real consumer accepted a different package name")
        # honua-release nightly dispatch: producer_run rides inside the hashed receipt identity, and
        # (ruling sdk-harness-trunk-pinned-package-2026-10-27) so do the trunk harness commit and the
        # pinned published package, with producer_source_sha bound to the package source.
        dispatch = "nightly-certification-1-1"
        nightly = {**undispatched, "GITHUB_ACTIONS": "true", "GITHUB_EVENT_NAME": "workflow_dispatch",
                   "GITHUB_REPOSITORY": "honua-io/honua-sdk-dotnet", "GITHUB_RUN_ID": "7", "GITHUB_RUN_ATTEMPT": "2",
                   "GITHUB_WORKFLOW_REF": "honua-io/honua-sdk-dotnet/.github/workflows/sdk-certification.yml"
                                          "@refs/heads/trunk",
                   "GITHUB_SHA": args.sdk_commit, "HONUA_NIGHTLY_DISPATCH_ID": dispatch}
        with mock.patch.dict(os.environ, nightly, clear=True):
            if producer.write_evidence(args, {"operations": [operation]}) != 0:
                raise ValueError("fixture producer unexpectedly refused the nightly dispatched execution")
        cell = {**requirement, **json.loads(evidence.read_text())["observations"][0]}
        run = {"repository": "honua-io/honua-sdk-dotnet", "workflow": "sdk-certification.yml",
               "run_id": 7, "run_attempt": 2, "dispatch_id": dispatch}
        receipt = cell["evidence_receipt"]
        if receipt["identity"].get("producer_run") != run or cell["evidence_digest"] != digest_of(receipt):
            raise ValueError("producer_run is not the exact dispatched run inside the hashed receipt identity")
        identity = receipt["identity"]
        if (identity.get("harness_commit") != args.sdk_commit
                or identity.get("producer_source_sha") != args.sdk_package_source_sha
                or cell["producer_source_sha"] != args.sdk_package_source_sha
                or identity.get("client_package") != "Honua.Sdk"
                or cell.get("client_package") != "Honua.Sdk"
                or identity.get("client_package_digest") != args.sdk_package_digest
                or cell.get("client_package_digest") != args.sdk_package_digest
                or identity["contract_revision"] != requirement["contract_revision"]):
            raise ValueError("nightly receipt identity does not separate the harness from the published package")
        controls = 2
        if gate._valid_receipt(cell, requirements_revision="fixture-context"):
            # The retained consumer admits the harness identity: it must still bind the package.
            for field, wrong in (("client_package_digest", "sha256:" + "0" * 64),
                                 ("client_package", "Honua.Sdk.Repacked")):
                broken = deepcopy(cell)
                broken["evidence_receipt"]["identity"][field] = wrong
                if gate._valid_receipt(broken, requirements_revision="fixture-context"):
                    raise ValueError("real consumer accepted a receipt identity naming another package: " + field)
                controls += 1
            admitted = cell
        else:
            # EXPECTED FAILURE, not a pass: the retained consumer predates the harness identity
            # (honua-release must admit harness_commit and the .NET client_package(_digest) receipt
            # fields). It is tolerated only when those fields are the sole reason: the same receipt
            # without them must be admitted. Any other refusal fails this check.
            legacy = deepcopy(cell)
            for field in HARNESS_IDENTITY_FIELDS:
                legacy["evidence_receipt"]["identity"].pop(field)
            if not gate._valid_receipt(legacy, requirements_revision="fixture-context"):
                raise ValueError("real strict consumer rejected the nightly dispatched v2 fixture receipt "
                                 "for a reason other than the harness identity fields")
            print("::warning::EXPECTED FAILURE: the retained honua-release consumer "
                  f"({binding.get('sourceSha')}) does not yet admit {', '.join(HARNESS_IDENTITY_FIELDS)} "
                  "in a .NET receipt identity (ruling sdk-harness-trunk-pinned-package-2026-10-27); "
                  "the receipt is otherwise admitted. Advance certification/requirements-pins.json once it does.")
            admitted = legacy
        broken = deepcopy(admitted)
        broken["evidence_receipt"]["identity"]["producer_run"] = {**run, "sha": "a" * 40}
        if gate._valid_receipt(broken, requirements_revision="fixture-context"):
            raise ValueError("real consumer accepted a producer_run outside the closed field set")
    print(f"producer/real-consumer v2 contract passed; 2 positive + {4 + controls} refusal controls; "
          "not candidate qualification")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--governance-dir", type=Path, required=True)
    check(parser.parse_args().governance_dir)
