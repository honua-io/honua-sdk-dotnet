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
        }

    def test_current_ledger_catalogs_every_operation_and_owns_every_gap(self):
        document = MODULE.build_document()
        self.assertGreater(document["summary"]["total"], 0)
        self.assertEqual(document["summary"]["total"], len(document["operations"]))
        self.assertEqual(len({cell["id"] for cell in document["operations"]}), len(document["operations"]))
        for cell in document["operations"]:
            if cell["status"] != "exercised":
                self.assertIn("ownerIssue", cell)
                self.assertIn("disposition", cell)

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
                candidate_cut="2026-01-01T00:00:00Z", evidence_uri="https://example.test/run/1",
                fixture_revision="sha256:" + "e" * 64,
                seed_revision="d" * 40,
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
            result = MODULE.write_evidence(Namespace(
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
                "sdk-dotnet-certification@" + "a" * 40,
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
            result = MODULE.write_evidence(Namespace(
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
            MODULE.write_evidence(Namespace(
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
        }
        cases = {
            ("Honua.Sdk.Abstractions.Features.IHonuaFeatureQueryClient", "QueryAsync"):
                "https://candidate.test/rest/services/places/FeatureServer/7/query",
            ("Honua.Sdk.Abstractions.Features.IHonuaFeatureEditClient", "ApplyEditsAsync"):
                "https://candidate.test/rest/services/places/FeatureServer/7/applyEdits",
            ("Honua.Sdk.Abstractions.Routing.IHonuaRoutingClient", "GetDirectionsAsync"):
                "https://candidate.test/rest/services/places/NAServer/Route",
            ("Honua.Sdk.Abstractions.Scenes.IHonuaSceneClient", "ListScenesAsync"):
                "https://candidate.test/api/scenes",
        }

        for (client, method), expected in cases.items():
            with self.subTest(client=client, method=method):
                self.assertEqual(expected, MODULE._certification_request_url({
                    "surface": "abstractions", "client": client, "operation": method,
                }, identity))

    def test_unproved_abstractions_families_have_no_request_url(self):
        identity = {
            "baseUrl": "https://candidate.test/root/",
            "grpcBaseUrl": "https://candidate.test:8443/root/",
            "serviceId": "places",
            "layerId": "7",
            "collectionId": "places",
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

        self.assertEqual(5, len(replica))
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
        self.assertEqual(4, len(apply_edits["missingImplementations"]))
        feature_server = next(
            implementation
            for implementation in apply_edits["implementations"]
            if implementation.endswith("HonuaFeatureServerClient")
        )
        self.assertFalse(apply_edits["implementationTests"][feature_server])
        self.assertEqual(MODULE.RC_FIXTURE_GAP_ISSUE, apply_edits["ownerIssue"])

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
            result = MODULE.write_evidence(Namespace(
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
            result = MODULE.write_evidence(Namespace(
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
