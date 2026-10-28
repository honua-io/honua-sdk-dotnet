"""The producer/real-consumer contract check tolerates only one named expected failure.

The retained honua-release consumer predates the nightly harness identity (ruling
sdk-harness-trunk-pinned-package-2026-10-27). The check may pass while that consumer refuses
the new fields only when they are the sole reason; a consumer that admits them must still bind
the package, and any other refusal fails. Fake consumers stand in for the retained one here.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "check-certification-receipt-contract.py"
SPEC = importlib.util.spec_from_file_location("receipt_contract_check", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

CONSUMER = '''
HARNESS = {"harness_commit", "client_package", "client_package_digest"}
RUN = {"repository", "workflow", "run_id", "run_attempt", "dispatch_id"}
MODE = %r


def _valid_receipt(cell, requirements_revision):
    receipt = cell.get("evidence_receipt") or {}
    identity = receipt.get("identity") or {}
    if receipt.get("schema") != "honua.certification-evidence-receipt/v2":
        return False
    if (identity.get("maturity") != "supported" or identity.get("required_tier") != "nightly"
            or identity.get("requirements_revision") != requirements_revision):
        return False
    if cell.get("client_package") != "Honua.Sdk":
        return False
    run = identity.get("producer_run")
    if run is not None and set(run) != RUN:
        return False
    carried = HARNESS & set(identity)
    if MODE == "legacy":
        return not carried
    if MODE == "refuse-nightly":
        return run is None
    if MODE == "admit":
        return all(identity[field] == cell[field] for field in carried - {"harness_commit"})
    return True
'''


def _check(mode):
    with tempfile.TemporaryDirectory() as directory:
        governance = Path(directory)
        consumer = governance / "check_protocol_certification.py"
        consumer.write_text(CONSUMER % mode, encoding="utf-8")
        (governance / "source-binding.json").write_text(json.dumps({
            "repository": "honua-io/honua-release", "sourceSha": "f" * 40,
            "files": {"tools/check_protocol_certification.py": {
                "sha256": hashlib.sha256(consumer.read_bytes()).hexdigest()}}}), encoding="utf-8")
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            MODULE.check(governance)
        return output.getvalue()


class ReceiptContractCheckTests(unittest.TestCase):
    def test_pre_harness_consumer_is_a_named_expected_failure(self):
        output = _check("legacy")
        self.assertIn("::warning::EXPECTED FAILURE", output)
        self.assertIn("harness_commit, client_package, client_package_digest", output)
        self.assertIn("2 positive + 6 refusal controls", output)

    def test_harness_aware_consumer_must_bind_the_package(self):
        output = _check("admit")
        self.assertNotIn("EXPECTED FAILURE", output)
        self.assertIn("2 positive + 8 refusal controls", output)

    def test_consumer_admitting_another_package_fails(self):
        with self.assertRaisesRegex(ValueError, "another package"):
            _check("lax")

    def test_any_other_nightly_refusal_fails(self):
        with self.assertRaisesRegex(ValueError, "other than the harness identity"):
            _check("refuse-nightly")


if __name__ == "__main__":
    unittest.main()
