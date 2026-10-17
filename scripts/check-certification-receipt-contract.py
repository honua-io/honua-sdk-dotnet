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
from pathlib import Path
import tempfile


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


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
                   "contract_revision": "sdk-dotnet-certification@" + "a" * 40,
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
                         sdk_commit="a" * 40, sdk_version="1.2.3", server_source_sha="b" * 40,
                         image_source_revision="b" * 40, server_image="ghcr.io/honua-io/honua-server@sha256:" + "c" * 64,
                         release_cut=None, candidate_cut="2026-01-01T00:00:00Z", fixture_revision="sha256:" + "d" * 64,
                         seed_revision="b" * 40, evidence_uri="https://example.test/fixture",
                         base_url="https://candidate.test", grpc_base_url="https://candidate.test:8443",
                         service_id="fixture", layer_id="0", collection_id="fixture")
        if producer.write_evidence(args, {"operations": [operation]}) != 0:
            raise ValueError("fixture producer unexpectedly refused complete unlicensed execution")
        cell = {**requirement, **json.loads(evidence.read_text())["observations"][0]}
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
    print("producer/real-consumer v2 contract passed; 1 positive + 4 refusal controls; not candidate qualification")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--governance-dir", type=Path, required=True)
    check(parser.parse_args().governance_dir)
