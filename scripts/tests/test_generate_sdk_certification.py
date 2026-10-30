import importlib.util
import json
import tempfile
import unittest
from argparse import Namespace
from pathlib import Path
from urllib.parse import urlsplit


SCRIPT = Path(__file__).resolve().parents[1] / "generate-sdk-certification.py"
SPEC = importlib.util.spec_from_file_location("sdk_certification", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class SdkCertificationTests(unittest.TestCase):
    @staticmethod
    def _request_context():
        return {
            "base_url": "https://candidate.test/root/",
            "grpc_base_url": "https://candidate.test:8443/root/",
            "service_id": "places",
            "layer_id": "0",
            "collection_id": "places",
            "route_service_id": "Routing", "route_layer": "Route",
            "service_area_layer": "ServiceArea", "closest_facility_layer": "ClosestFacility",
            "scene_id": "fixture-scene",
        }

    def _write_evidence(self, args, document, licensing=None):
        # Synthetic governed input for harness tests only, never a live receipt. `licensing`
        # maps an operation id to the catalog's (licensed, entitlement_policy_revision).
        if not hasattr(args, "sdk_package_source_sha"):
            args.sdk_package_source_sha = "e" * 40
        args.requirements = args.evidence.with_suffix(".requirements.json")
        args.requirements_revision = "fixture-governed-revision"
        rows = []
        for operation in document["operations"]:
            licensed = "FeatureServer.IHonuaFeatureServerEditClient" in operation["client"]
            licensed, policy = (licensing or {}).get(operation["id"], (
                licensed, MODULE.FEATURESERVER_EDIT_ENTITLEMENT_POLICY if licensed else None))
            rows.append({
                "capability_key": "sdk-dotnet." + operation["surface"], "surface": operation["surface"],
                "operation": operation["id"], "canonical_client": "Honua SDK .NET",
                "client_version": args.sdk_version, "deployment_target": "local-docker",
                "client_lane": "sdk-dotnet-certification", "maturity": "supported", "required_tier": "nightly",
                "addressable_by_client": True, "licensed": licensed,
                "entitlement_policy_revision": policy,
                "contract_revision": "sdk-dotnet-certification@" + args.sdk_package_source_sha,
                "auth_policy_revision": "api-key-protected-v1", "fixture_revision": args.fixture_revision,
                "scenario_facets": list(dict.fromkeys("positive" if x == "read-only" else x for x in operation["scenarioFacets"])),
            })
        args.requirements.write_text(json.dumps({"schema": "honua.protocol-certification-requirements/v1",
                                                "revision": args.requirements_revision,
                                                "receipt_schema_min": "v2", "complete": True, "requirements": rows}))
        return MODULE.write_evidence(args, document)

    def test_governed_context_refuses_missing_stale_duplicate_or_invalid_catalog(self):
        row = {"capability_key": "sdk-dotnet.geoservices", "surface": "geoservices", "operation": "Fixture.Query",
               "canonical_client": "Honua SDK .NET", "client_version": "1.2.3", "deployment_target": "local-docker",
               "client_lane": "sdk-dotnet-certification", "maturity": "supported", "required_tier": "nightly"}
        catalog = {"schema": "honua.protocol-certification-requirements/v1", "revision": "expected",
                   "complete": True, "receipt_schema_min": "v2", "requirements": [row]}
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "requirements.json"
            with self.assertRaises(ValueError):
                MODULE._requirements(Namespace())
            for field, value in (("revision", "stale"), ("complete", False), ("receipt_schema_min", "v1"),
                                 ("requirements", [row, row]),
                                 ("requirements", [{**row, "maturity": "claimed-supported"}]),
                                 ("requirements", [{**row, "required_tier": "optional"}])):
                with self.subTest(field=field, value=value):
                    path.write_text(json.dumps({**catalog, field: value}))
                    with self.assertRaises(ValueError):
                        MODULE._requirements(Namespace(requirements=path, requirements_revision="expected"))
            path.write_text('{"schema":"first","schema":"second"}')
            with self.assertRaisesRegex(ValueError, "duplicate"):
                MODULE._requirements(Namespace(requirements=path, requirements_revision="expected"))

    def test_passing_edit_trx_cannot_invent_live_entitlement_proof(self):
        document = {"operations": [{
            "id": "Fixture.AddFeaturesAsync", "surface": "geoservices", "operation": "AddFeaturesAsync",
            "client": "Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerEditClient",
            "status": "exercised", "requiredTiers": ["pr"], "scenarioFacets": ["mutation"],
            "tests": ["Fixture.Edit"],
        }]}
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            trx = root / "passed.trx"
            trx.write_text('<TestRun><Results><UnitTestResult testName="Fixture.Edit" outcome="Passed" /></Results></TestRun>')
            args = Namespace(tier="pr", trx=[trx], evidence=root / "evidence.json", started_at=None,
                             sdk_commit="a" * 40, sdk_version="1.2.3", server_source_sha="b" * 40,
                             image_source_revision="b" * 40, server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                             release_cut=None, candidate_cut="2026-01-01T00:00:00Z", fixture_revision="sha256:" + "d" * 64,
                             seed_revision="b" * 40, evidence_uri="https://example.test/run/1", allow_nonpass=True,
                             **self._request_context())
            self.assertEqual(1, self._write_evidence(args, document))
            observed = json.loads(args.evidence.read_text())["observations"][0]
            self.assertEqual("skip", observed["result"])
            self.assertIn("missing retained live entitlement", observed["skip_reason"])
            self.assertIsNone(observed["evidence_receipt"])

    def _edit_args(self, root, trx_outcome="Passed"):
        trx = root / "edit.trx"
        trx.write_text('<TestRun><Results><UnitTestResult testName="Fixture.Edit" outcome="'
                       + trx_outcome + '" /></Results></TestRun>')
        return Namespace(tier="pr", trx=[trx], evidence=root / "evidence.json", started_at=None,
                         sdk_commit="a" * 40, sdk_version="1.2.3", server_source_sha="b" * 40,
                         image_source_revision="b" * 40, server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                         release_cut=None, candidate_cut="2026-01-01T00:00:00Z", fixture_revision="sha256:" + "d" * 64,
                         seed_revision="b" * 40, evidence_uri="https://example.test/run/1", allow_nonpass=True,
                         **self._request_context())

    EDIT_DOCUMENT = {"operations": [{
        "id": "Fixture.AddFeaturesAsync", "surface": "geoservices", "operation": "AddFeaturesAsync",
        "client": "Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerEditClient",
        "status": "exercised", "requiredTiers": ["pr"], "scenarioFacets": ["mutation"],
        "tests": ["Fixture.Edit"],
    }]}

    def test_catalog_unlicensed_edit_row_certifies_without_entitlement(self):
        # honua-release is the single source of licensing truth: 2026.1 runs with licensing
        # disabled, so its FeatureServer edit rows are unlicensed and need no entitlement proof.
        with tempfile.TemporaryDirectory() as directory:
            args = self._edit_args(Path(directory))
            code = self._write_evidence(args, self.EDIT_DOCUMENT,
                                        licensing={"Fixture.AddFeaturesAsync": (False, None)})
            observed = json.loads(args.evidence.read_text())["observations"][0]
            self.assertEqual(0, code)
            self.assertEqual("pass", observed["result"])
            self.assertIs(False, observed["licensed"])
            self.assertIsNone(observed["entitlement_policy_revision"])
            self.assertNotIn("entitlement", observed["evidence_receipt"])

    def test_catalog_licensed_row_needs_entitlement_whatever_the_client_type(self):
        document = {"operations": [{**self.EDIT_DOCUMENT["operations"][0],
                                    "id": "Fixture.QueryAsync", "operation": "QueryAsync",
                                    "client": "Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerClient"}]}
        with tempfile.TemporaryDirectory() as directory:
            args = self._edit_args(Path(directory))
            self.assertEqual(1, self._write_evidence(args, document, licensing={
                "Fixture.QueryAsync": (True, MODULE.FEATURESERVER_EDIT_ENTITLEMENT_POLICY)}))
            observed = json.loads(args.evidence.read_text())["observations"][0]
            self.assertIs(True, observed["licensed"])
            self.assertEqual("skip", observed["result"])
            self.assertIn("missing retained live entitlement", observed["skip_reason"])

    def test_malformed_catalog_licensing_is_refused(self):
        for licensing in ((True, None), (False, MODULE.FEATURESERVER_EDIT_ENTITLEMENT_POLICY),
                          ("yes", None), (True, "honua-unproven-policy-v1"), (None, None)):
            with self.subTest(licensing=licensing), tempfile.TemporaryDirectory() as directory:
                args = self._edit_args(Path(directory))
                with self.assertRaisesRegex(ValueError, "licens|entitlement policy"):
                    self._write_evidence(args, self.EDIT_DOCUMENT,
                                         licensing={"Fixture.AddFeaturesAsync": licensing})

    def test_failed_edit_trx_retains_failure_and_receipt_without_entitlement(self):
        document = {"operations": [{
            "id": "Fixture.AddFeaturesAsync", "surface": "geoservices", "operation": "AddFeaturesAsync",
            "client": "Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerEditClient",
            "status": "exercised", "requiredTiers": ["pr"], "scenarioFacets": ["mutation"],
            "tests": ["Fixture.Edit"],
        }]}
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            trx = root / "failed.trx"
            trx.write_text('<TestRun><Results><UnitTestResult testName="Fixture.Edit" outcome="Failed" /></Results></TestRun>')
            args = Namespace(tier="pr", trx=[trx], evidence=root / "evidence.json", started_at=None,
                             sdk_commit="a" * 40, sdk_version="1.2.3", server_source_sha="b" * 40,
                             image_source_revision="b" * 40, server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                             release_cut=None, candidate_cut="2026-01-01T00:00:00Z", fixture_revision="sha256:" + "d" * 64,
                             seed_revision="b" * 40, evidence_uri="https://example.test/run/1", allow_nonpass=False,
                             **self._request_context())
            self.assertEqual(1, self._write_evidence(args, document))
            observed = json.loads(args.evidence.read_text())["observations"][0]
            self.assertEqual("fail", observed["result"])
            self.assertIsNone(observed["skip_reason"])
            self.assertEqual("fail", observed["evidence_receipt"]["result"])
            self.assertEqual({"mutation": "fail"}, observed["evidence_receipt"]["facets"])

    def test_exact_operation_context_rejects_version_or_contract_drift(self):
        identity = {"sdkVersion": "1.2.3", "sdkCommit": "a" * 40, "sdkPackageSourceSha": "c" * 40,
                    "fixtureRevision": "sha256:" + "d" * 64}
        operation = {"surface": "geoservices", "id": "Fixture.Query", "client": "Fixture.IClient"}
        key = ("sdk-dotnet.geoservices", "geoservices", "Fixture.Query", "Honua SDK .NET", "1.2.3", "local-docker")
        row = {"client_lane": "sdk-dotnet-certification", "addressable_by_client": True,
               "licensed": False, "entitlement_policy_revision": None,
               "contract_revision": "sdk-dotnet-certification@" + "c" * 40,
               "auth_policy_revision": "api-key-protected-v1", "fixture_revision": identity["fixtureRevision"],
               "scenario_facets": ["positive"], "maturity": "supported", "required_tier": "nightly"}
        self.assertEqual(row, MODULE._governed_context({key: row}, operation, identity, ["positive"]))
        with self.assertRaises(ValueError):
            MODULE._governed_context({key: row}, operation, {**identity, "sdkVersion": "1.2.4"}, ["positive"])
        for field, value in (("contract_revision", "unreviewed"),
                             ("contract_revision", "sdk-dotnet-certification@" + identity["sdkCommit"]),
                             ("fixture_revision", "wrong"),
                             ("scenario_facets", ["negative"]), ("addressable_by_client", False),
                             ("licensed", True), ("auth_policy_revision", "anonymous-public-v1")):
            with self.subTest(field=field), self.assertRaises(ValueError):
                MODULE._governed_context({key: {**row, field: value}}, operation, identity, ["positive"])

    def test_contract_identity_cannot_fall_back_to_producer_checkout(self):
        for source in (None, "", "unreleased", "A" * 40, "a" * 39):
            with self.subTest(source=source), self.assertRaisesRegex(ValueError, "installed package source"):
                MODULE._contract_revision({"sdkCommit": "a" * 40, "sdkPackageSourceSha": source})

    def test_untested_rows_cannot_pass_or_complete_the_scope(self):
        document = MODULE.build_document()
        self.assertTrue(document["trackingIssue"].endswith("/issues/473"))
        self.assertNotIn("/issues/31", document["trackingIssue"])
        self.assertFalse(document["complete"])
        gaps = [
            cell for cell in document["operations"]
            if cell["releaseDenominator"] == "included" and not cell["tests"]
        ]
        self.assertTrue(gaps)
        self.assertTrue(all(cell["status"] == "gap" for cell in gaps))
        self.assertTrue(all(
            cell["ownerIssue"].startswith("https://github.com/honua-io/honua-sdk-dotnet/issues/")
            and "/issues/31" not in cell["ownerIssue"]
            for cell in document["operations"]
            if cell["status"] != "exercised"
        ))
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            trx = root / "lookalike.trx"
            trx.write_text(
                '<TestRun><Results><UnitTestResult testName="Fixture.Lookalike" outcome="Passed" />'
                '</Results></TestRun>'
            )
            gap_document = {
                "operations": [{
                    "id": "Gap.One", "surface": "admin",
                    "client": "Honua.Sdk.Admin.IHonuaAdminClient",
                    "operation": "ListAsync", "status": "gap",
                    "requiredTiers": ["nightly"], "scenarioFacets": ["read-only"],
                    "tests": ["Fixture.Lookalike"], "ownerIssue": MODULE.TRACKING_ISSUE,
                    "releaseDenominator": "included",
                }],
                "releaseProfile": {"id": "test", "denominator": 1, "excluded": []},
            }
            args = Namespace(
                tier="nightly", trx=[trx], evidence=root / "evidence.json", started_at=None,
                sdk_commit="a" * 40, sdk_version="1.2.3", server_source_sha="b" * 40,
                image_source_revision="b" * 40,
                server_image="ghcr.io/honua-io/honua-server@sha256:" + "c" * 64,
                sdk_package_id="Honua.Sdk", sdk_package_digest="sha256:" + "e" * 64,
                sdk_package_source_sha="a" * 40,
                release_cut=None, candidate_cut="2026-01-01T00:00:00Z",
                fixture_revision="sha256:" + "d" * 64, seed_revision="b" * 40,
                evidence_uri="https://example.test/run/1", allow_nonpass=True,
                **self._request_context(),
            )
            self.assertEqual(0, self._write_evidence(args, gap_document))
            fragment = json.loads(args.evidence.read_text())
            self.assertFalse(fragment["operation_scope"]["complete"])
            self.assertTrue(fragment["operation_scope"]["owner_issue"].endswith("/issues/473"))
            observed = fragment["observations"][0]
            self.assertEqual("skip", observed["result"])
            self.assertNotEqual("pass", observed["result"])
            self.assertIsNone(observed["evidence_receipt"])
            self.assertIn("/issues/473", observed["skip_reason"])

    def test_current_ledger_catalogs_every_operation_and_owns_every_gap(self):
        document = MODULE.build_document()
        self.assertGreater(document["summary"]["total"], 0)
        self.assertEqual(document["summary"]["total"], len(document["operations"]))
        self.assertEqual(len({cell["id"] for cell in document["operations"]}), len(document["operations"]))
        for cell in document["operations"]:
            if cell["status"] != "exercised":
                self.assertIn("ownerIssue", cell)
                self.assertIn("disposition", cell)

    def test_fixture_dependent_operations_remain_individual_release_obligations(self):
        document = MODULE.build_document()
        names = {
            "GeocodingForward", "GeocodingReverse", "GeocodingSuggest", "GeocodingBatch",
            "SpecValidate", "SpecPlan", "SpecApply", "SceneList", "SceneGet", "SceneResolve",
            "RoutingMetadata", "RoutingDirections", "RoutingServiceArea", "RoutingClosestFacility",
        }
        operations = [cell for cell in document["operations"] if any(
            test.rsplit(".", 1)[-1] == name + "_IsReachable"
            for test in cell["tests"] for name in names
        )]
        self.assertEqual(14, len(operations))
        self.assertEqual(14, len({test for cell in operations for test in cell["tests"]}))
        for cell in operations:
            self.assertEqual("included", cell["releaseDenominator"], cell["id"])
            self.assertIn("release", cell["requiredTiers"], cell["id"])
            self.assertEqual(1, len(cell["tests"]), cell["id"])

    def test_fixture_skips_and_missing_results_cannot_qualify_release(self):
        # Harness-only TRX; these are refusal controls, never candidate evidence.
        document = {"operations": [{
            "id": "Fixture.Geocode", "surface": "admin",
            "client": "Honua.Sdk.Admin.Geocoding.IHonuaGeocodingClient",
            "operation": "ForwardGeocodeAsync", "status": "exercised",
            "requiredTiers": ["release"], "scenarioFacets": ["read-only"],
            "tests": ["Fixture.Geocode"],
        }]}
        for result in ("", '<UnitTestResult testName="Fixture.Geocode" outcome="NotExecuted" />'):
            with self.subTest(result=result), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                trx = root / "fixture.trx"
                trx.write_text("<TestRun><Results>" + result + "</Results></TestRun>")
                args = Namespace(
                    tier="release", trx=[trx], evidence=root / "evidence.json", started_at=None,
                    sdk_commit="a" * 40, sdk_version="1.2.3", server_source_sha="b" * 40,
                    image_source_revision="b" * 40,
                    server_image="ghcr.io/honua-io/honua-server@sha256:" + "c" * 64,
                    sdk_package_id="Honua.Sdk", sdk_package_digest="sha256:" + "e" * 64,
                    sdk_package_source_sha="a" * 40,
                    release_cut="2026-01-01T00:00:00Z", candidate_cut="2026-01-01T00:00:00Z",
                    fixture_revision="sha256:" + "d" * 64, seed_revision="b" * 40,
                    evidence_uri="https://example.test/run/1", allow_nonpass=True,
                    **self._request_context(),
                )
                self.assertEqual(1, self._write_evidence(args, document))
                observation = json.loads(args.evidence.read_text())["observations"][0]
                self.assertEqual("skip", observation["result"])
                self.assertIsNone(observation["evidence_receipt"])

    def test_skipped_routing_rows_without_fixture_targets_make_no_request_claim(self):
        # Harness-only TRX: an unprovisioned routing fixture skips its tests, so the row never
        # sent a request. It must be retained as a skip with no request URL, not crash the run.
        document = {"operations": [{
            "id": "Fixture.Route", "surface": "abstractions",
            "client": "Honua.Sdk.Abstractions.Routing.IHonuaRoutingClient",
            "operation": "GetDirectionsAsync", "status": "exercised",
            "requiredTiers": ["nightly"], "scenarioFacets": ["read-only"],
            "tests": ["Fixture.Route"],
        }]}
        context = {key: value for key, value in self._request_context().items()
                   if not key.startswith("route_") and not key.endswith("_layer")}
        for outcome, expected in (("NotExecuted", "skip"), ("Passed", None)):
            with self.subTest(outcome=outcome), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                trx = root / "fixture.trx"
                trx.write_text('<TestRun><Results><UnitTestResult testName="Fixture.Route" outcome="'
                               + outcome + '" /></Results></TestRun>')
                args = Namespace(
                    tier="nightly", trx=[trx], evidence=root / "evidence.json", started_at=None,
                    sdk_commit="a" * 40, sdk_version="1.2.3", server_source_sha="b" * 40,
                    image_source_revision="b" * 40,
                    server_image="ghcr.io/honua-io/honua-server@sha256:" + "c" * 64,
                    release_cut=None, candidate_cut="2026-01-01T00:00:00Z",
                    fixture_revision="sha256:" + "d" * 64, seed_revision="b" * 40,
                    evidence_uri="https://example.test/run/1", allow_nonpass=True, **context,
                )
                if expected is None:
                    # An executed row still has to name the live fixture it ran against.
                    with self.assertRaisesRegex(ValueError, "routing request evidence requires"):
                        self._write_evidence(args, document)
                    continue
                # Incomplete execution still fails the producer; only the crash is gone.
                self.assertEqual(1, self._write_evidence(args, document))
                observation = json.loads(args.evidence.read_text())["observations"][0]
                self.assertEqual("skip", observation["result"])
                self.assertIsNone(observation["request_url"])
                self.assertIsNone(observation["evidence_receipt"])
                self.assertIsNone(observation["evidence_uri"])
                self.assertEqual([], observation["exercised_capabilities"])

    def test_operations_are_declared_from_the_supplied_package_source(self):
        # The installed package's source, not this checkout, defines the certified operations.
        with tempfile.TemporaryDirectory() as directory:
            src = Path(directory) / "src"
            package = src / "Honua.Sdk.Abstractions"
            package.mkdir(parents=True)
            (package / "FixturePackageOnly.cs").write_text(
                "namespace Honua.Sdk.Abstractions.Fixture;\n"
                "public interface IFixturePackageOnlyClient\n{\n"
                "    Task<int> PackageOnlyAsync(string id, CancellationToken cancellationToken = default);\n}\n"
                "public sealed class FixturePackageOnlyClient : IFixturePackageOnlyClient\n{\n"
                "    public Task<int> PackageOnlyAsync(string id, CancellationToken cancellationToken = default)"
                " => Task.FromResult(0);\n}\n",
                encoding="utf-8",
            )
            operations = MODULE.build_document(src)["operations"]
            self.assertEqual(
                ["Honua.Sdk.Abstractions.Fixture.IFixturePackageOnlyClient.PackageOnlyAsync(string,CancellationToken)"],
                [operation["id"] for operation in operations],
            )
            self.assertEqual("abstractions", operations[0]["surface"])
            trunk_ids = {operation["id"] for operation in MODULE.build_document()["operations"]}
            self.assertNotIn(operations[0]["id"], trunk_ids)

    def test_operations_source_root_is_evidence_only_and_must_hold_sources(self):
        with tempfile.TemporaryDirectory() as directory:
            empty = Path(directory)
            with self.assertRaises(SystemExit):
                MODULE.main(["--check", "--operations-source-root", str(empty)])
            trx = empty / "fixture.trx"
            trx.write_text("<TestRun><Results /></TestRun>")
            self.assertEqual(1, MODULE.main([
                "--tier", "nightly", "--trx", str(trx), "--evidence", str(empty / "out.json"),
                "--operations-source-root", str(empty),
            ]))
            self.assertFalse((empty / "out.json").exists())

    def test_rc_unavailable_surfaces_are_owned_by_release_gate(self):
        document = MODULE.build_document()
        unavailable = [
            cell for cell in document["operations"]
            if cell.get("unavailableTests") or cell["id"] in MODULE.RC_UNAVAILABLE_OPERATIONS
        ]

        self.assertTrue(unavailable)
        self.assertTrue(all(cell["status"] in {"gap", "non-addressable"} for cell in unavailable))
        self.assertTrue(all(cell["ownerIssue"] == MODULE.RC_FIXTURE_GAP_ISSUE for cell in unavailable))
        self.assertTrue(all(not cell["tests"] for cell in unavailable))

    def test_2026_1_release_denominator_explicitly_partitions_every_operation(self):
        document = MODULE.build_document()
        operations = document["operations"]
        profile = document["releaseProfile"]
        exclusions = {item["operationId"]: item for item in profile["excluded"]}

        self.assertEqual(MODULE.RELEASE_PROFILE_ID, profile["id"])
        self.assertEqual(len(operations), sum(
            cell["releaseDenominator"] in {"included", "excluded", "non-addressable"}
            for cell in operations
        ))
        self.assertEqual(
            profile["denominator"],
            sum(cell["releaseDenominator"] == "included" for cell in operations),
        )
        self.assertEqual(
            exclusions,
            {
                cell["id"]: {
                    "operationId": cell["id"],
                    "ownerIssue": cell["ownerIssue"],
                    "reason": cell["disposition"],
                }
                for cell in operations
                if cell["releaseDenominator"] == "excluded"
            },
        )
        self.assertEqual({}, exclusions)
        self.assertTrue(all("release" not in cell["requiredTiers"] for cell in operations if cell["id"] in exclusions))
        self.assertTrue(all(item["ownerIssue"] == MODULE.RC_FIXTURE_GAP_ISSUE for item in exclusions.values()))
        self.assertTrue(all(item["reason"] for item in exclusions.values()))

    def test_release_identity_requires_exact_image_source_seed_and_cut(self):
        with self.assertRaisesRegex(ValueError, "seed revision"):
            MODULE._identity(Namespace(
                tier="release",
                sdk_commit="a" * 40,
                sdk_version="1.0.0",
                sdk_package_id="Honua.Sdk", sdk_package_digest="sha256:" + "f" * 64,
                sdk_package_source_sha="a" * 40,
                server_source_sha="b" * 40,
                image_source_revision="b" * 40,
                server_image="ghcr.io/honua-io/honua-server@sha256:" + "c" * 64,
                release_cut="2026-01-01T00:00:00Z",
                candidate_cut="2026-01-01T00:00:00Z", evidence_uri="https://example.test/run/1",
                fixture_revision="sha256:" + "e" * 64,
                seed_revision="d" * 40,
                **self._request_context(),
            ))

    def test_release_identity_rejects_floating_nightly_tags(self):
        sha = "b" * 40
        for image in (
            "ghcr.io/honua-io/honua-server:nightly",
            "ghcr.io/honua-io/honua-server:nightly-aot",
            "ghcr.io/honua-io/honua-server:nightly-87966c3",
            "ghcr.io/honua-io/honua-server:nightly@sha256:" + "c" * 64,
        ):
            with self.subTest(image=image), self.assertRaisesRegex(ValueError, "floating nightly tag"):
                MODULE._identity(Namespace(
                    tier="release",
                    sdk_commit="a" * 40,
                    sdk_version="1.6.0",
                    sdk_package_id="Honua.Sdk", sdk_package_digest="sha256:" + "f" * 64,
                    sdk_package_source_sha="a" * 40,
                    server_source_sha=sha,
                    image_source_revision=sha,
                    server_image=image,
                    release_cut="2026-01-01T00:00:00Z",
                    candidate_cut="2026-01-01T00:00:00Z", evidence_uri="https://example.test/run/1",
                    fixture_revision="sha256:" + "e" * 64,
                    seed_revision=sha,
                    **self._request_context(),
                ))

    def test_missing_required_result_fails_closed(self):
        document = {
            "operations": [{
                "id": "Honua.Example.IHonuaExampleClient.GetAsync",
                "surface": "geoservices",
                "client": "Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerClient",
                "operation": "GetAsync",
                "status": "exercised",
                "requiredTiers": ["pr"],
                "scenarioFacets": ["mutation"],
                "tests": ["Example.Tests.Get"],
            }]
        }
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "empty.trx"
            trx.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results /></TestRun>', encoding="utf-8")
            evidence = Path(directory) / "evidence.json"
            result = self._write_evidence(Namespace(
                tier="pr", trx=[trx], evidence=evidence, started_at=None,
                sdk_commit="a" * 40, sdk_version="unreleased",
                server_source_sha="b" * 40,
                image_source_revision="b" * 40,
                server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                release_cut=None, candidate_cut="2026-01-01T00:00:00Z",
                fixture_revision="fixture", seed_revision="b" * 40,
                evidence_uri="https://example.test/run/1",
                allow_nonpass=True,
                **self._request_context(),
            ), document)
            self.assertEqual(1, result)
            fragment = json.loads(evidence.read_text(encoding="utf-8"))
            self.assertEqual("honua.protocol-certification-fragment/v1", fragment["schema"])
            self.assertEqual("honua-sdk-dotnet", fragment["producer"])
            self.assertEqual("skip", fragment["observations"][0]["result"])
            self.assertEqual("b" * 40, fragment["candidate"]["source_sha"])
            self.assertEqual("a" * 40, fragment["observations"][0]["producer_source_sha"])
            self.assertEqual(["mutation"], fragment["observations"][0]["scenario_facets"])
            self.assertEqual("Honua SDK .NET", fragment["observations"][0]["canonical_client"])
            self.assertEqual("Honua SDK .NET", fragment["observations"][0]["client_id"])
            self.assertEqual("sdk-dotnet-certification", fragment["observations"][0]["runner_lane"])
            self.assertEqual("11.0", fragment["observations"][0]["protocol_version"])
            self.assertEqual("GeoServices REST", fragment["observations"][0]["protocol_profile"])
            self.assertEqual("Honua SDK .NET", fragment["observations"][0]["performed_by"])
            self.assertEqual(
                "https://candidate.test/rest/services/places/FeatureServer/0/query",
                fragment["observations"][0]["request_url"],
            )
            self.assertEqual([], fragment["observations"][0]["exercised_capabilities"])
            self.assertEqual(
                "sdk-dotnet-certification@" + "e" * 40,
                fragment["observations"][0]["contract_revision"],
            )
            self.assertEqual(
                "api-key-protected-v1",
                fragment["observations"][0]["auth_policy_revision"],
            )
            self.assertIsNone(fragment["observations"][0]["evidence_digest"])
            self.assertIsNone(fragment["observations"][0]["facet_results"])

    def test_pass_records_all_claimed_facets_as_exercised(self):
        document = {
            "operations": [{
                "id": "Honua.Sdk.GeoServices.IHonuaFeatureServerClient.QueryAsync",
                "surface": "geoservices",
                "client": "Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerClient",
                "operation": "QueryAsync",
                "status": "exercised",
                "requiredTiers": ["pr"],
                "scenarioFacets": ["read-only", "pagination"],
                "tests": ["Example.Tests.Query"],
            }]
        }
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "passed.trx"
            trx.write_text(
                '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
                '<UnitTestResult testName="Example.Tests.Query" outcome="Passed" />'
                '</Results></TestRun>',
                encoding="utf-8",
            )
            evidence = Path(directory) / "evidence.json"
            result = self._write_evidence(Namespace(
                tier="pr", trx=[trx], evidence=evidence, started_at=None,
                sdk_commit="a" * 40, sdk_version="1.6.0",
                server_source_sha="b" * 40, image_source_revision="b" * 40,
                server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                release_cut=None, candidate_cut="2026-01-01T00:00:00Z",
                fixture_revision="sha256:" + "d" * 64, seed_revision="b" * 40,
                evidence_uri="https://example.test/run/1", allow_nonpass=False,
                **self._request_context(),
            ), document)

            observation = json.loads(evidence.read_text(encoding="utf-8"))["observations"][0]
            self.assertEqual(0, result)
            self.assertEqual("pass", observation["result"])
            self.assertEqual(
                observation["scenario_facets"], observation["exercised_capabilities"]
            )

    def test_every_observation_satisfies_truthful_identity_ingest_rules(self):
        document = MODULE.build_document()
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "empty.trx"
            trx.write_text(
                '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results /></TestRun>',
                encoding="utf-8",
            )
            evidence = Path(directory) / "evidence.json"
            self._write_evidence(Namespace(
                tier="nightly", trx=[trx], evidence=evidence, started_at=None,
                sdk_commit="a" * 40, sdk_version="1.6.0",
                server_source_sha="b" * 40, image_source_revision="b" * 40,
                server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                release_cut=None, candidate_cut="2026-01-01T00:00:00Z",
                fixture_revision="sha256:" + "d" * 64, seed_revision="b" * 40,
                evidence_uri="https://example.test/run/1", allow_nonpass=True,
                **self._request_context(),
            ), document)

            fragment = json.loads(evidence.read_text(encoding="utf-8"))
            self.assertGreater(len(fragment["observations"]), 0)
            feature_server_edit_observations = [
                observation for observation in fragment["observations"]
                if "IHonuaFeatureServerEditClient" in observation["operation"]
            ]
            self.assertTrue(feature_server_edit_observations)
            self.assertTrue(all(
                observation["licensed"] is True
                and observation["entitlement_policy_revision"]
                == MODULE.FEATURESERVER_EDIT_ENTITLEMENT_POLICY
                for observation in feature_server_edit_observations
            ))
            for observation in fragment["observations"]:
                self.assertEqual(observation["canonical_client"], observation["client_id"])
                self.assertEqual("sdk-dotnet-certification", observation["runner_lane"])
                self.assertEqual(observation["client_id"], observation["performed_by"])
                self.assertTrue(observation["protocol_version"])
                self.assertTrue(observation["protocol_profile"])
                request_url = observation["request_url"]
                if request_url is not None:
                    parsed = urlsplit(request_url)
                    self.assertIn(parsed.scheme, {"http", "https"})
                    self.assertTrue(parsed.netloc)
                    self.assertNotEqual("/api/v1", parsed.path)
                self.assertIsInstance(observation["exercised_capabilities"], list)
                if observation["result"] == "pass":
                    self.assertLessEqual(
                        set(observation["scenario_facets"]),
                        set(observation["exercised_capabilities"]),
                    )

    def test_abstractions_request_urls_are_proved_representative_endpoints(self):
        identity = {
            "baseUrl": "https://candidate.test/root/",
            "grpcBaseUrl": "https://candidate.test:8443/root/",
            "serviceId": "places",
            "layerId": "7",
            "collectionId": "places",
            "operationTargets": {"routeServiceId": "Routing", "routeLayer": "Route",
                                 "serviceAreaLayer": "ServiceArea", "closestFacilityLayer": "ClosestFacility",
                                 "sceneId": "fixture-scene"},
        }
        cases = {
            ("Honua.Sdk.Abstractions.Features.IHonuaFeatureQueryClient", "QueryAsync"):
                "https://candidate.test/rest/services/places/FeatureServer/7/query",
            ("Honua.Sdk.Abstractions.Features.IHonuaFeatureEditClient", "ApplyEditsAsync"):
                "https://candidate.test/rest/services/places/FeatureServer/7/applyEdits",
            ("Honua.Sdk.Abstractions.Routing.IHonuaRoutingClient", "GetDirectionsAsync"):
                "https://candidate.test/rest/services/Routing/NAServer/Route/solve",
            ("Honua.Sdk.Abstractions.Scenes.IHonuaSceneClient", "ListScenesAsync"):
                "https://candidate.test/api/scenes",
        }

        for (client, method), expected in cases.items():
            with self.subTest(client=client, method=method):
                self.assertEqual(expected, MODULE._certification_request_url({
                    "surface": "abstractions", "client": client, "operation": method,
                }, identity))

    def test_spec_request_urls_match_each_exercised_operation(self):
        identity = {"baseUrl": "https://candidate.test/", "grpcBaseUrl": "https://candidate.test:8443/",
                    "serviceId": "places", "layerId": "7", "collectionId": "places"}
        for surface, client in [("spec", "Honua.Sdk.Spec.HonuaSpecClient"),
                                ("abstractions", "Honua.Sdk.Abstractions.Spec.IHonuaSpecClient")]:
            for method, action in [("ValidateAsync", "validate"), ("PlanAsync", "plan"), ("ApplyAsync", "apply")]:
                with self.subTest(surface=surface, method=method):
                    self.assertEqual(f"https://candidate.test/v1/spec/{action}",
                                     MODULE._certification_request_url(
                                         {"surface": surface, "client": client, "operation": method}, identity))
            self.assertIsNone(MODULE._certification_request_url(
                {"surface": surface, "client": client, "operation": "UnknownAsync"}, identity))

    def test_restored_fixture_urls_require_exact_operation_targets(self):
        identity = {"baseUrl": "https://candidate.test/", "grpcBaseUrl": "https://candidate.test:8443/",
                    "serviceId": "WRONG-feature-service", "layerId": "0", "collectionId": "places",
                    "operationTargets": {"routeServiceId": "routing fixture", "routeLayer": "Route",
                                         "serviceAreaLayer": "ServiceArea", "closestFacilityLayer": "ClosestFacility",
                                         "sceneId": "scene/id"}}
        cases = [("Routing.IHonuaRoutingClient", "GetServiceMetadataAsync", "/rest/services/routing%20fixture/NAServer/Route"),
                 ("Routing.IHonuaRoutingClient", "GetDirectionsAsync", "/rest/services/routing%20fixture/NAServer/Route/solve"),
                 ("Routing.IHonuaRoutingClient", "GetServiceAreaAsync", "/rest/services/routing%20fixture/NAServer/ServiceArea/solveServiceArea"),
                 ("Routing.IHonuaRoutingClient", "FindClosestFacilityAsync", "/rest/services/routing%20fixture/NAServer/ClosestFacility/solveClosestFacility"),
                 ("Scenes.IHonuaSceneClient", "ListScenesAsync", "/api/scenes"),
                 ("Scenes.IHonuaSceneClient", "GetSceneAsync", "/api/scenes/scene%2Fid"),
                 ("Scenes.IHonuaSceneClient", "ResolveSceneAsync", "/api/scenes/scene%2Fid/resolve")]
        for client, method, path in cases:
            operation = {"surface": "abstractions", "client": "Honua.Sdk.Abstractions." + client, "operation": method}
            self.assertEqual("https://candidate.test" + path, MODULE._certification_request_url(operation, identity))
            if method != "ListScenesAsync":
                with self.assertRaises(ValueError):
                    MODULE._certification_request_url(operation, {**identity, "operationTargets": {}})
        for method, action in [("ForwardGeocodeAsync", "findAddressCandidates"), ("ReverseGeocodeAsync", "reverseGeocode"),
                               ("SuggestAsync", "suggest"), ("BatchGeocodeAsync", "geocodeAddresses")]:
            self.assertEqual("https://candidate.test/rest/services/World/GeocodeServer/" + action,
                             MODULE._certification_request_url({"surface": "admin", "client": "Honua.Sdk.Admin.Geocoding.IHonuaGeocodingClient",
                                                                "operation": method}, identity))

    def test_unproved_abstractions_families_have_no_request_url(self):
        identity = {
            "baseUrl": "https://candidate.test/root/",
            "grpcBaseUrl": "https://candidate.test:8443/root/",
            "serviceId": "places",
            "layerId": "7",
            "collectionId": "places",
            "operationTargets": {"routeServiceId": "Routing", "routeLayer": "Route",
                                 "serviceAreaLayer": "ServiceArea", "closestFacilityLayer": "ClosestFacility",
                                 "sceneId": "fixture-scene"},
        }
        clients_and_methods = [
            ("Honua.Sdk.Abstractions.Features.IHonuaFeatureAttachmentClient", "ListAttachmentsAsync"),
            ("Honua.Sdk.Abstractions.Features.IHonuaFeatureDescriptorClient", "GetDescriptorAsync"),
            ("Honua.Sdk.Abstractions.Raster.IHonuaRasterDataClient", "ReadWindowAsync"),
            ("Honua.Sdk.Abstractions.Offline.IReplicaSyncClient", "CreateReplicaAsync"),
            ("Honua.Sdk.Abstractions.Routing.IHonuaRoutingClient", "OptimizeRouteAsync"),
        ]

        for client, method in clients_and_methods:
            with self.subTest(client=client, method=method):
                self.assertIsNone(MODULE._certification_request_url({
                    "surface": "abstractions", "client": client, "operation": method,
                }, identity))

    def test_interface_inheritance_is_resolved_transitively(self):
        document = MODULE.build_document()
        geocoding = [
            cell for cell in document["operations"]
            if cell["client"].endswith("IHonuaGeocodingClient")
        ]
        self.assertTrue(geocoding)
        self.assertTrue(all(cell["status"] != "non-addressable" for cell in geocoding))

    def test_concrete_only_typed_clients_are_cataloged(self):
        document = MODULE.build_document()
        operations = document["operations"]
        image = [cell for cell in operations if cell["client"].endswith("HonuaImageServerClient")]
        geometry = [cell for cell in operations if cell["client"].endswith("HonuaGeometryServerClient")]

        self.assertEqual(
            {"GetServiceMetadataAsync", "ExportImageAsync", "ExportImageMetadataAsync", "ComputeStatisticsHistogramsAsync", "IdentifyAsync"},
            {cell["operation"] for cell in image},
        )
        self.assertEqual(
            {"ProjectAsync", "BufferAsync", "LengthsAsync", "AreasAndLengthsAsync"},
            {cell["operation"] for cell in geometry},
        )

    def test_interface_backed_concrete_only_methods_are_cataloged(self):
        document = MODULE.build_document()
        operations = document["operations"]
        expected = {
            ("HonuaFeatureServerClient", "QueryVectorAsync"),
            ("HonuaOgcFeaturesClient", "GetItemsVectorAsync"),
            ("HonuaWfsClient", "GetFeaturesVectorAsync"),
        }
        actual = {
            (cell["client"].rsplit(".", 1)[-1], cell["operation"])
            for cell in operations
        }

        self.assertTrue(expected <= actual, expected - actual)

    def test_replica_sync_client_contract_is_cataloged(self):
        operations = MODULE.build_document()["operations"]
        replica = [
            cell for cell in operations
            if cell["client"].endswith("IReplicaSyncClient")
        ]

        self.assertEqual(6, len(replica))
        self.assertEqual(
            {
                "CreateReplicaAsync(string,string,IReadOnlyList<int>?,CancellationToken)",
                "ExtractChangesAsync(string,string,CancellationToken)",
                "ExtractChangesAsync(string,string,string?,CancellationToken)",
                "SynchronizeReplicaAsync(string,string,string,CancellationToken)",
                "SynchronizeReplicaAsync(string,string,long,string,CancellationToken)",
                "UnRegisterReplicaAsync(string,string,CancellationToken)",
            },
            {cell["signature"] for cell in replica},
        )
        self.assertEqual(
            {
                "CreateReplicaAsync",
                "ExtractChangesAsync",
                "SynchronizeReplicaAsync",
                "UnRegisterReplicaAsync",
            },
            {cell["operation"] for cell in replica},
        )
        self.assertTrue(all(cell["status"] != "non-addressable" for cell in replica))
        unregister = next(cell for cell in replica if cell["operation"] == "UnRegisterReplicaAsync")
        self.assertIn("mutation", unregister["scenarioFacets"])

    def test_semantic_signature_ignores_qualified_parameter_spelling(self):
        interface = {
            "signature": "QueryFeaturesAsync(QueryFeaturesRequest,CancellationToken)",
            "parameters": [{"type": "QueryFeaturesRequest"}, {"type": "CancellationToken"}],
        }
        implementation = {
            "signature": "QueryFeaturesAsync(Models.QueryFeaturesRequest,CancellationToken)",
            "parameters": [
                {"type": "Models.QueryFeaturesRequest"},
                {"type": "System.Threading.CancellationToken"},
            ],
        }

        self.assertEqual(
            MODULE._semantic_signature(interface),
            MODULE._semantic_signature(implementation),
        )

    def test_non_raster_non_addressable_operations_use_general_owner(self):
        document = MODULE.build_document()
        non_addressable = [
            cell for cell in document["operations"] if cell["status"] == "non-addressable"
        ]

        self.assertTrue(non_addressable)
        self.assertTrue(all(
            not cell["client"].endswith("IHonuaRasterDataClient")
            for cell in non_addressable
        ))
        self.assertTrue(all(
            cell["ownerIssue"] == (
                MODULE.RC_FIXTURE_GAP_ISSUE
                if cell["id"] in MODULE.RC_UNAVAILABLE_OPERATIONS
                else MODULE.TRACKING_ISSUE
            )
            for cell in non_addressable
        ))

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
        self.assertEqual("gap", apply_edits["status"])
        self.assertEqual(4, len(apply_edits["implementations"]))
        self.assertEqual(3, len(apply_edits["missingImplementations"]))
        feature_server = next(
            implementation
            for implementation in apply_edits["implementations"]
            if implementation.endswith("HonuaFeatureServerClient")
        )
        self.assertTrue(apply_edits["implementationTests"][feature_server])
        self.assertEqual(MODULE.TRACKING_ISSUE, apply_edits["ownerIssue"])
        self.assertEqual("included", apply_edits["releaseDenominator"])
        self.assertNotEqual(MODULE.RC_FIXTURE_GAP_ISSUE, apply_edits["ownerIssue"])

    def test_featureserver_edit_operations_are_exercised_by_governed_round_trip(self):
        document = MODULE.build_document()
        edit_operations = [
            cell for cell in document["operations"]
            if cell["client"].endswith("IHonuaFeatureServerEditClient")
            and cell["operation"] in {"AddFeaturesAsync", "DeleteFeaturesAsync"}
        ]

        self.assertEqual(2, len(edit_operations))
        self.assertTrue(all(cell["status"] == "exercised" for cell in edit_operations))
        self.assertTrue(all(cell["releaseDenominator"] == "included" for cell in edit_operations))
        self.assertTrue(all(
            "Honua.Sdk.ProtocolIntegration.Tests.DestructiveProtocolIntegrationTests."
            "FeatureServerApplyEdits_AddUpdateDelete_RoundTrips" in cell["tests"]
            for cell in edit_operations
        ))

    def test_explicit_source_facade_delegation_maps_shared_query_operation(self):
        document = MODULE.build_document()
        query = next(
            cell for cell in document["operations"]
            if cell["client"].endswith("IHonuaFeatureQueryClient")
            and cell["operation"] == "QueryAsync"
        )

        self.assertEqual("exercised", query["status"])
        self.assertFalse(query.get("missingImplementations"))
        self.assertTrue(all(query["implementationTests"].values()))
        self.assertIn(
            "Honua.Sdk.ProtocolIntegration.Tests.FeatureProtocolIntegrationTests."
            "SourceFacade_QueriesConfiguredFeatureProtocols",
            query["tests"],
        )

    def test_known_state_changing_verbs_are_mutations(self):
        document = MODULE.build_document()
        names = {
            "RollbackDeployOperationAsync", "RotateEncryptionKeyAsync", "ImportRasterAsync",
            "PatchItemAsync", "RollbackAsync", "CommitEditSessionAsync", "StartEditSessionAsync",
            "ConnectAsync", "ReconnectAsync", "SubscribeAsync", "UnsubscribeAsync", "ReopenVersionAsync",
        }
        matched = [cell for cell in document["operations"] if cell["operation"] in names]
        self.assertEqual(names, {cell["operation"] for cell in matched})
        self.assertTrue(all("mutation" in cell["scenarioFacets"] for cell in matched))

    def test_paginated_operations_are_classified_for_evidence(self):
        document = MODULE.build_document()
        names = {
            "GetFeaturesAsyncEnumerable",
            "GetItemsPagesAsync",
            "GetRecordsPagesAsync",
            "PostSearchPagesAsync",
            "QueryPagesAsync",
            "SearchPagesAsync",
        }
        non_paginated_names = {
            "GetLandingPageAsync",
            "GetPageAsync",
            "ListCollectionsAsync",
            "QueryCountAsync",
            "QueryExtentAsync",
            "UpdatePageAsync",
        }
        matched = [cell for cell in document["operations"] if cell["operation"] in names]
        non_paginated = [
            cell for cell in document["operations"] if cell["operation"] in non_paginated_names
        ]
        self.assertEqual(names, {cell["operation"] for cell in matched})
        self.assertEqual(non_paginated_names, {cell["operation"] for cell in non_paginated})
        self.assertTrue(all("pagination" in cell["scenarioFacets"] for cell in matched))
        self.assertTrue(all("pagination" not in cell["scenarioFacets"] for cell in non_paginated))

    def test_every_named_test_must_have_a_passing_result(self):
        document = {
            "operations": [{
                "id": "Honua.Example.IHonuaExampleClient.GetAsync",
                "surface": "geoservices",
                "client": "Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerClient",
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
            result = self._write_evidence(Namespace(
                tier="pr", trx=[trx], evidence=evidence, started_at=None,
                sdk_commit="a" * 40, sdk_version="1.6.0",
                server_source_sha="b" * 40, image_source_revision="b" * 40,
                server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                release_cut=None, candidate_cut="2026-01-01T00:00:00Z",
                fixture_revision="sha256:" + "d" * 64, seed_revision="b" * 40,
                evidence_uri="https://example.test/run/1",
                **self._request_context(),
            ), document)
            self.assertEqual(1, result)

    def test_every_parameterized_result_must_pass(self):
        document = {
            "operations": [{
                "id": "Honua.Example.IHonuaExampleClient.GetAsync",
                "surface": "geoservices",
                "client": "Honua.Sdk.GeoServices.FeatureServer.IHonuaFeatureServerClient",
                "operation": "GetAsync",
                "status": "exercised",
                "requiredTiers": ["pr"],
                "scenarioFacets": ["read-only"],
                "tests": ["Example.Tests.Get"],
            }]
        }
        with tempfile.TemporaryDirectory() as directory:
            trx = Path(directory) / "mixed.trx"
            trx.write_text(
                '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
                '<UnitTestResult testName="Example.Tests.Get(first)" outcome="Passed" />'
                '<UnitTestResult testName="Example.Tests.Get(second)" outcome="NotExecuted" />'
                '</Results></TestRun>',
                encoding="utf-8",
            )
            evidence = Path(directory) / "evidence.json"
            result = self._write_evidence(Namespace(
                tier="pr", trx=[trx], evidence=evidence, started_at=None,
                sdk_commit="a" * 40, sdk_version="1.6.0",
                server_source_sha="b" * 40, image_source_revision="b" * 40,
                server_image="ghcr.io/honua/server@sha256:" + "c" * 64,
                release_cut=None, candidate_cut="2026-01-01T00:00:00Z",
                fixture_revision="sha256:" + "d" * 64, seed_revision="b" * 40,
                evidence_uri="https://example.test/run/1",
                **self._request_context(),
            ), document)
            self.assertEqual(1, result)
            self.assertEqual("skip", json.loads(evidence.read_text(encoding="utf-8"))["observations"][0]["result"])


if __name__ == "__main__":
    unittest.main()
