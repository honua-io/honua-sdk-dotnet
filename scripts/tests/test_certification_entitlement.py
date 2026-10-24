from __future__ import annotations

import copy
import importlib.util
import json
import tempfile
import unittest
from argparse import Namespace
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github/workflows/sdk-certification.yml"


def _load(name: str, file: str):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / file)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ENT = _load("certification_entitlement", "certification_entitlement.py")
GEN = _load("sdk_certification_generator", "generate-sdk-certification.py")
OBSERVE = _load("observe_certification_entitlement", "observe-certification-entitlement.py")
CHECKED = datetime(2026, 10, 24, 12, 0, tzinfo=timezone.utc)


def signed_status(**overrides):
    data = {"mode": "enabled", "isValid": True, "validationState": "Valid", "licenseId": "lic-release-1",
            "issuedAt": "2026-01-01T00:00:00Z", "expiresAt": "2027-01-01T00:00:00Z",
            "entitlements": [{"key": "editing.featureserver-edits", "isActive": True}]}
    data.update(overrides)
    return {"success": True, "data": data}


class EntitlementValidationTests(unittest.TestCase):
    def test_profile_requires_dispatch_and_signed_envelope_for_release(self):
        with self.assertRaises(ValueError):
            ENT.profile("release", "schedule", "Development", '{"a":1}')
        for content in ("", "not-json", "{}", "[]"):
            with self.assertRaises(ValueError):
                ENT.profile("release", "workflow_dispatch", "Development", content)
        self.assertEqual({"mode": "signed", "devGrantEdition": ""},
                         ENT.profile("release", "workflow_dispatch", "Development", '{"sig":"x"}'))
        self.assertEqual("development-grant", ENT.profile("pr", "pull_request", "Development", "")["mode"])
        with self.assertRaises(ValueError):
            ENT.profile("nightly", "schedule", "Production", "")

    def test_signed_status_refuses_dev_community_invalid_expired_and_unentitled(self):
        self.assertTrue(ENT.signed_status(signed_status(), now=CHECKED).startswith("sha256:"))
        refused = {
            "dev-grant": signed_status(licenseId="dev-grant"), "community": signed_status(licenseId="Community"),
            "disabled": signed_status(mode="disabled"), "invalid": signed_status(isValid=False),
            "state": signed_status(validationState="Expired"), "expired": signed_status(expiresAt="2026-02-01T00:00:00Z"),
            "future-issue": signed_status(issuedAt="2027-01-01T00:00:00Z"),
            "no-entitlement": signed_status(entitlements=[]),
            "inactive": signed_status(entitlements=[{"key": "editing.featureserver-edits", "isActive": False}]),
            "http-402": {"success": False, "error": "payment required"}, "none": None,
        }
        for name, status in refused.items():
            with self.subTest(name), self.assertRaises(AssertionError):
                ENT.signed_status(status, now=CHECKED)

    def test_rehearsal_observation_is_never_release_eligible(self):
        observation = ENT.build_observation("development-grant", signed_status(licenseId="dev-grant"), CHECKED)
        self.assertFalse(observation["release_eligible"])
        self.assertIsNone(observation["license_fingerprint"])
        with self.assertRaises(AssertionError):
            ENT.build_observation("signed", signed_status(licenseId="dev-grant"), CHECKED)

    def test_verified_entitlement_requires_matching_unmodified_signed_observation(self):
        good = ENT.build_observation("signed", signed_status(), CHECKED)
        window = {"capability_key": "sdk-dotnet.geoservices", "started_at": "2026-10-24T11:00:00Z",
                  "completed_at": "2026-10-24T13:00:00Z"}
        assertion = ENT.verified_entitlement(good, **window)
        self.assertEqual({"policy_revision", "capability_key", "deployment_target", "verification",
                          "status", "checked_at", "license_fingerprint"}, set(assertion))
        self.assertEqual("honua-pro-featureserver-edits-v1", assertion["policy_revision"])
        self.assertEqual("live-server-capability-probe-v1", assertion["verification"])
        tampered = []
        for mutate in (
            lambda o: o.update(policy_revision="other-policy"),
            lambda o: o.update(mode="development-grant"),
            lambda o: o.update(release_eligible=False),
            lambda o: o.update(license_fingerprint="sha256:" + "0" * 64),
            lambda o: o.update(checked_at="2026-10-24T10:00:00Z"),
            lambda o: o.update(checked_at="2026-10-24T14:00:00Z"),
            lambda o: o.update(deployment_target="windows-licensed"),
            lambda o: o["license_status"]["data"].update(licenseId="dev-grant"),
            lambda o: o.update(schema="invented/v1"),
        ):
            candidate = copy.deepcopy(good)
            mutate(candidate)
            tampered.append(candidate)
        for candidate in tampered:
            with self.subTest(candidate=candidate.get("mode")), self.assertRaises((ValueError, AssertionError)):
                ENT.verified_entitlement(candidate, **window)
        with self.assertRaises(ValueError):
            ENT.verified_entitlement(None, **window)

    def test_release_observe_fails_closed_without_signed_license(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "obs.json"
            code = OBSERVE.main(["observe", "--tier", "release", "--event", "schedule", "--mode", "signed",
                                 "--base-url", "http://127.0.0.1:9", "--output", str(output)])
            self.assertEqual(1, code)
            self.assertFalse(output.exists())


class GeneratorEntitlementTests(unittest.TestCase):
    DOCUMENT = {"operations": [{
        "id": "Fixture.AddFeaturesAsync", "surface": "geoservices", "operation": "AddFeaturesAsync",
        "client": "Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerEditClient",
        "status": "exercised", "requiredTiers": ["pr", "nightly", "release"], "scenarioFacets": ["mutation"], "tests": ["Fixture.Edit"],
    }]}

    def _run(self, directory: Path, tier: str, observation, outcome="Passed"):
        from test_generate_sdk_certification import SdkCertificationTests as Base  # type: ignore
        trx = directory / "t.trx"
        trx.write_text(f'<TestRun><Results><UnitTestResult testName="Fixture.Edit" outcome="{outcome}" /></Results></TestRun>')
        obs_path = None
        if observation is not None:
            obs_path = directory / "obs.json"
            obs_path.write_text(json.dumps(observation))
        args = Namespace(tier=tier, trx=[trx], evidence=directory / "e.json", started_at="2026-10-24T11:00:00Z",
                         sdk_commit="a" * 40, sdk_version="1.2.3", server_source_sha="b" * 40,
                         image_source_revision="b" * 40, server_image="ghcr.io/honua-io/honua-server@sha256:" + "c" * 64,
                         release_cut="2026-10-01T00:00:00Z", sdk_package_id="Honua.Sdk",
                         sdk_package_digest="sha256:" + "f" * 64, candidate_cut="2026-01-01T00:00:00Z", fixture_revision="sha256:" + "d" * 64,
                         seed_revision="b" * 40, evidence_uri="https://example.test/run/1", allow_nonpass=True,
                         entitlement_observation=obs_path, **Base._request_context())
        helper = Base()
        return args, Base._write_evidence(helper, args, copy.deepcopy(self.DOCUMENT))

    def _observation(self, directory, tier, observation):
        args, code = self._run(directory, tier, observation)
        return code, json.loads(args.evidence.read_text())["observations"][0]

    def _assert_refused(self, observed, fragment):
        self.assertEqual("skip", observed["result"])
        self.assertIn("missing retained live entitlement", observed["skip_reason"])
        self.assertIn(fragment, observed["skip_reason"])
        self.assertIsNone(observed["evidence_receipt"])

    def _signed(self):
        return ENT.build_observation("signed", signed_status(), datetime.now(timezone.utc))

    def test_refused_without_observation_or_with_unreadable_one(self):
        with tempfile.TemporaryDirectory() as directory:
            code, observed = self._observation(Path(directory), "release", None)
            self.assertEqual(1, code)
            self._assert_refused(observed, "no retained entitlement observation")

    def test_refused_for_release_dev_grant_community_invalid_or_expired(self):
        now = datetime.now(timezone.utc)
        cases = {
            "rehearsal": ENT.build_observation("development-grant", signed_status(licenseId="dev-grant"), now),
        }
        for name, status in (("dev", signed_status(licenseId="dev-grant")), ("community", signed_status(licenseId="community")),
                             ("invalid", signed_status(isValid=False)),
                             ("expired", signed_status(expiresAt="2026-01-02T00:00:00Z"))):
            forged = copy.deepcopy(self._signed())
            forged["license_status"] = status
            forged["license_status_sha256"] = ENT.canonical_sha256(status)
            cases[name] = forged
        for name, observation in cases.items():
            with self.subTest(name), tempfile.TemporaryDirectory() as directory:
                code, observed = self._observation(Path(directory), "release", observation)
                self.assertEqual(1, code)
                self._assert_refused(observed, "")

    def test_rehearsal_tiers_never_pass_licensed_rows_even_with_signed_observation(self):
        for tier in ("pr", "nightly"):
            with self.subTest(tier), tempfile.TemporaryDirectory() as directory:
                _, observed = self._observation(Path(directory), tier, self._signed())
                self._assert_refused(observed, "rehearsal")

    def test_release_pass_only_with_valid_signed_observation_matching_policy(self):
        with tempfile.TemporaryDirectory() as directory:
            code, observed = self._observation(Path(directory), "release", self._signed())
            self.assertEqual(0, code)
            self.assertEqual("pass", observed["result"])
            self.assertTrue(observed["licensed"])
            self.assertEqual(ENT.POLICY_REVISION, observed["entitlement_policy_revision"])
            assertion = observed["evidence_receipt"]["entitlement"]
            self.assertEqual("sdk-dotnet.geoservices", assertion["capability_key"])
            self.assertRegex(assertion["license_fingerprint"], r"^sha256:[0-9a-f]{64}$")
            self.assertLessEqual(observed["started_at"], assertion["checked_at"])
            self.assertLessEqual(assertion["checked_at"], observed["completed_at"])

    def test_failed_trx_is_never_rescued_by_entitlement(self):
        with tempfile.TemporaryDirectory() as directory:
            args, code = self._run(Path(directory), "release", self._signed(), outcome="Failed")
            observed = json.loads(args.evidence.read_text())["observations"][0]
            self.assertEqual(0, code)  # allow_nonpass
            self.assertEqual("fail", observed["result"])
            self.assertNotIn("entitlement", observed["evidence_receipt"])


class WorkflowWiringTests(unittest.TestCase):
    TEXT = WORKFLOW.read_text(encoding="utf-8")

    def test_license_secret_is_release_dispatch_only_and_never_disabled_mode(self):
        self.assertNotIn("Licensing__Mode=Disabled", self.TEXT)
        self.assertIn("-e Licensing__Mode=Enabled", self.TEXT)
        self.assertIn("-e Licensing__LicenseContent \\", self.TEXT)
        guard = "github.event_name == 'workflow_dispatch' && inputs.tier == 'release' && secrets.HONUA_CERTIFICATION_LICENSE_CONTENT || ''"
        self.assertEqual(2, self.TEXT.count(guard))
        self.assertEqual(self.TEXT.count("secrets.HONUA_CERTIFICATION_LICENSE_CONTENT"), self.TEXT.count(guard))
        self.assertNotIn("Licensing__LicenseContent=", self.TEXT)

    def test_observation_is_retained_before_tests_and_passed_to_generator(self):
        observe = self.TEXT.index("name: Retain candidate entitlement observation")
        self.assertLess(self.TEXT.index("name: Apply exact seed"), observe)
        self.assertLess(observe, self.TEXT.index("name: Run public SDK operations"))
        self.assertIn("artifacts/evidence/entitlement-observation.json", self.TEXT)
        self.assertIn('--entitlement-observation "${ENTITLEMENT_OBSERVATION}"', self.TEXT)
        self.assertIn("observe-certification-entitlement.py profile", self.TEXT)

    def test_workflow_triggers_on_entitlement_scripts(self):
        self.assertIn('"scripts/tests/**"', self.TEXT)
        self.assertIn('"scripts/generate-sdk-certification.py"', self.TEXT)


if __name__ == "__main__":
    unittest.main()
