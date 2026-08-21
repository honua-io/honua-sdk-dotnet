from __future__ import annotations

import importlib.util
from io import BytesIO
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock
import urllib.request
from zipfile import ZIP_DEFLATED, ZipFile


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "check-package-coordinates.py"
SPEC = importlib.util.spec_from_file_location("check_package_coordinates", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


SOURCE_SHA = "a" * 40


def package_bytes(
    package_id: str = "Honua.Sdk",
    version: str = "1.6.1",
    *,
    payload: bytes = b"assembly",
    signature: bytes | None = None,
    repository_commit: str = SOURCE_SHA,
) -> bytes:
    target = BytesIO()
    with ZipFile(target, "w", compression=ZIP_DEFLATED) as archive:
        archive.writestr(
            f"{package_id}.nuspec",
            "<package><metadata>"
            f"<id>{package_id}</id><version>{version}</version>"
            f'<repository type="git" url="https://github.com/honua-io/honua-sdk-dotnet" commit="{repository_commit}" />'
            "</metadata></package>",
        )
        archive.writestr("lib/net10.0/Honua.Sdk.dll", payload)
        content_types = (
            '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
            '<Default Extension="dll" ContentType="application/octet-stream" />'
        )
        relationships = (
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
        )
        archive.writestr("package/services/metadata/core-properties/build.psmdcp", b"metadata")
        if signature is not None:
            content_types += (
                '<Override PartName="/.signature.p7s" '
                'ContentType="application/vnd.openxmlformats-package.digital-signature-xmlsignature+xml" />'
            )
            relationships += (
                '<Relationship Id="signature" '
                'Target="/package/services/digital-signature/origin.psdor" '
                'Type="http://schemas.openxmlformats.org/package/2006/relationships/'
                'digital-signature/origin" />'
            )
            archive.writestr(".signature.p7s", signature)
            archive.writestr("package/services/digital-signature/origin.psdor", b"origin")
        archive.writestr("[Content_Types].xml", content_types + "</Types>")
        archive.writestr("_rels/.rels", relationships + "</Relationships>")
    return target.getvalue()


def symbol_package_bytes(
    package_id: str = "Honua.Sdk",
    version: str = "1.6.1",
    *,
    pdb: bytes = b"BSJBportable-pdb",
    signature: bytes | None = None,
    repository_commit: str = SOURCE_SHA,
) -> bytes:
    target = BytesIO()
    with ZipFile(target, "w", compression=ZIP_DEFLATED) as archive:
        archive.writestr(
            f"{package_id}.nuspec",
            "<package><metadata>"
            f"<id>{package_id}</id><version>{version}</version>"
            f'<repository type="git" url="https://github.com/honua-io/honua-sdk-dotnet" commit="{repository_commit}" />'
            "</metadata></package>",
        )
        archive.writestr("lib/net10.0/Honua.Sdk.pdb", pdb)
        if signature is not None:
            archive.writestr(".signature.p7s", signature)
    return target.getvalue()


class PackageArchiveTests(unittest.TestCase):
    def test_registry_authorization_is_not_forwarded_cross_host(self) -> None:
        handler = MODULE.RegistryRedirectHandler()
        original = urllib.request.Request(
            "https://registry.example/package",
            headers={"Authorization": "Basic redacted"},
        )
        redirected = handler.redirect_request(
            original,
            None,
            302,
            "Found",
            {},
            "https://storage.example/blob?signature=opaque",
        )
        self.assertIsNotNone(redirected)
        self.assertNotIn("Authorization", redirected.headers)

    def test_repository_signing_metadata_does_not_change_semantic_digest(self) -> None:
        unsigned = MODULE.inspect_package_bytes(package_bytes())
        signed = MODULE.inspect_package_bytes(package_bytes(signature=b"repository-signature"))
        self.assertNotEqual(unsigned.raw_sha256, signed.raw_sha256)
        self.assertEqual(unsigned.semantic_sha256, signed.semantic_sha256)

    def test_payload_change_changes_semantic_digest(self) -> None:
        left = MODULE.inspect_package_bytes(package_bytes(payload=b"left"))
        right = MODULE.inspect_package_bytes(package_bytes(payload=b"right"))
        self.assertNotEqual(left.semantic_sha256, right.semantic_sha256)

    def test_unsafe_or_duplicate_members_are_rejected(self) -> None:
        target = BytesIO()
        with ZipFile(target, "w") as archive:
            archive.writestr("../escape", b"no")
            archive.writestr("Honua.Sdk.nuspec", b"<package />")
        with self.assertRaises(MODULE.CoordinateError):
            MODULE.inspect_package_bytes(target.getvalue())

    def test_symbol_set_requires_real_portable_pdbs(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        package_dir = Path(directory.name)
        Path(package_dir, "Honua.Sdk.1.6.1.snupkg").write_bytes(symbol_package_bytes())
        symbols = MODULE.load_symbol_packages(
            package_dir,
            ["Honua.Sdk"],
            "1.6.1",
            SOURCE_SHA,
        )
        _, package, portable_pdbs = symbols["honua.sdk"]
        self.assertEqual("Honua.Sdk", package.package_id)
        self.assertEqual(1, len(portable_pdbs))

        Path(package_dir, "Honua.Sdk.1.6.1.snupkg").write_bytes(
            symbol_package_bytes(pdb=b"not-portable")
        )
        with self.assertRaises(MODULE.CoordinateError):
            MODULE.load_symbol_packages(package_dir, ["Honua.Sdk"], "1.6.1", SOURCE_SHA)


class CoordinateEvaluationTests(unittest.TestCase):
    def local(self) -> tuple[tempfile.TemporaryDirectory[str], dict]:
        directory = tempfile.TemporaryDirectory()
        path = Path(directory.name, "Honua.Sdk.1.6.1.nupkg")
        path.write_bytes(package_bytes())
        package = MODULE.inspect_package_file(path)
        return directory, {"honua.sdk": (path, package)}

    def test_absent_coordinate_is_publishable_during_preflight(self) -> None:
        directory, local = self.local()
        self.addCleanup(directory.cleanup)
        result, paths = MODULE.evaluate_coordinates(
            registry_name="nuget.org",
            package_base="https://example.invalid/flat/",
            package_version="1.6.1",
            source_revision=SOURCE_SHA,
            local_packages=local,
            fetch_package=lambda _: None,
            require_present=False,
        )
        self.assertEqual("pass", result["status"])
        self.assertEqual(1, result["summary"]["absent"])
        self.assertEqual(1, len(paths))

    def test_absent_coordinate_fails_required_public_proof(self) -> None:
        directory, local = self.local()
        self.addCleanup(directory.cleanup)
        result, _ = MODULE.evaluate_coordinates(
            registry_name="nuget.org",
            package_base="https://example.invalid/flat/",
            package_version="1.6.1",
            source_revision=SOURCE_SHA,
            local_packages=local,
            fetch_package=lambda _: None,
            require_present=True,
        )
        self.assertEqual("fail", result["status"])

    def test_identical_repository_signed_coordinate_is_accepted(self) -> None:
        directory, local = self.local()
        self.addCleanup(directory.cleanup)
        result, paths = MODULE.evaluate_coordinates(
            registry_name="nuget.org",
            package_base="https://example.invalid/flat/",
            package_version="1.6.1",
            source_revision=SOURCE_SHA,
            local_packages=local,
            fetch_package=lambda _: package_bytes(signature=b"repository-signature"),
            require_present=True,
        )
        self.assertEqual("pass", result["status"])
        self.assertEqual("present-identical", result["packages"][0]["state"])
        self.assertEqual([], paths)

    def test_divergent_occupied_coordinate_fails_closed(self) -> None:
        directory, local = self.local()
        self.addCleanup(directory.cleanup)
        result, _ = MODULE.evaluate_coordinates(
            registry_name="nuget.org",
            package_base="https://example.invalid/flat/",
            package_version="1.6.1",
            source_revision=SOURCE_SHA,
            local_packages=local,
            fetch_package=lambda _: package_bytes(payload=b"different"),
            require_present=False,
        )
        self.assertEqual("fail", result["status"])
        self.assertEqual("divergent", result["packages"][0]["state"])

    def test_local_package_set_binds_inventory_version_and_source(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        package_dir = Path(directory.name)
        Path(package_dir, "Honua.Sdk.1.6.1.nupkg").write_bytes(package_bytes())
        local = MODULE.load_local_packages(
            package_dir,
            ["Honua.Sdk"],
            "1.6.1",
            SOURCE_SHA,
        )
        self.assertEqual(["honua.sdk"], list(local))
        with self.assertRaises(MODULE.CoordinateError):
            MODULE.load_local_packages(package_dir, ["Honua.Sdk"], "1.6.2", SOURCE_SHA)
        with self.assertRaises(MODULE.CoordinateError):
            MODULE.load_local_packages(package_dir, ["Honua.Sdk"], "1.6.1", "b" * 40)


class SymbolCoordinateEvaluationTests(unittest.TestCase):
    def local(self) -> tuple[tempfile.TemporaryDirectory[str], dict]:
        directory = tempfile.TemporaryDirectory()
        path = Path(directory.name, "Honua.Sdk.1.6.1.snupkg")
        path.write_bytes(symbol_package_bytes())
        package, portable_pdbs = MODULE.inspect_symbol_package_bytes(path.read_bytes())
        return directory, {"honua.sdk": (path, package, portable_pdbs)}

    def test_symbol_url_is_derived_from_coordinate_not_local_path(self) -> None:
        self.assertEqual(
            "https://globalcdn.nuget.org/symbol-packages/honua.sdk.1.6.1.snupkg",
            MODULE.symbol_package_download_url(
                "https://globalcdn.nuget.org/symbol-packages/",
                "Honua.Sdk",
                "1.6.1",
            ),
        )

    def test_absent_symbol_coordinate_is_the_only_publish_candidate(self) -> None:
        directory, local = self.local()
        self.addCleanup(directory.cleanup)
        rows, paths, failures = MODULE.evaluate_symbol_coordinates(
            symbol_package_base="https://example.invalid/symbols/",
            package_version="1.6.1",
            local_symbols=local,
            fetch_package=lambda _: None,
            require_present=False,
        )
        self.assertEqual("absent", rows[0]["state"])
        self.assertEqual([local["honua.sdk"][0]], paths)
        self.assertEqual([], failures)

    def test_repository_signed_symbol_payload_is_semantically_identical(self) -> None:
        directory, local = self.local()
        self.addCleanup(directory.cleanup)
        rows, paths, failures = MODULE.evaluate_symbol_coordinates(
            symbol_package_base="https://example.invalid/symbols/",
            package_version="1.6.1",
            local_symbols=local,
            fetch_package=lambda _: symbol_package_bytes(signature=b"repository-signature"),
            require_present=True,
        )
        self.assertEqual("present-identical", rows[0]["state"])
        self.assertEqual([], paths)
        self.assertEqual([], failures)
        self.assertEqual(rows[0]["local"]["portablePdbs"], rows[0]["public"]["portablePdbs"])

    def test_divergent_public_symbol_payload_fails_closed(self) -> None:
        directory, local = self.local()
        self.addCleanup(directory.cleanup)
        rows, paths, failures = MODULE.evaluate_symbol_coordinates(
            symbol_package_base="https://example.invalid/symbols/",
            package_version="1.6.1",
            local_symbols=local,
            fetch_package=lambda _: symbol_package_bytes(pdb=b"BSJBdifferent"),
            require_present=False,
        )
        self.assertEqual("divergent", rows[0]["state"])
        self.assertEqual([], paths)
        self.assertEqual(1, len(failures))

    def test_cli_writes_remote_symbol_evidence_and_exact_publish_plan(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        root = Path(directory.name)
        package_dir = root / "packages"
        package_dir.mkdir()
        (package_dir / "Honua.Sdk.1.6.1.nupkg").write_bytes(package_bytes())
        (package_dir / "Honua.Sdk.1.6.1.snupkg").write_bytes(symbol_package_bytes())
        inventory = root / "inventory.txt"
        inventory.write_text("src/Honua.Sdk.csproj|Honua.Sdk\n", encoding="utf-8")
        evidence = root / "evidence.json"
        package_plan = root / "packages.txt"
        symbol_plan = root / "symbols.txt"

        with (
            mock.patch.object(
                MODULE,
                "resolve_package_base_address",
                return_value="https://example.invalid/flat/",
            ),
            mock.patch.object(MODULE, "fetch_registry_package", return_value=None),
        ):
            result = MODULE.main(
                [
                    "--registry-name",
                    "nuget.org",
                    "--service-index",
                    "https://example.invalid/index.json",
                    "--symbol-package-base-address",
                    "https://example.invalid/symbols/",
                    "--package-dir",
                    str(package_dir),
                    "--inventory",
                    str(inventory),
                    "--package-version",
                    "1.6.1",
                    "--source-revision",
                    SOURCE_SHA,
                    "--evidence-out",
                    str(evidence),
                    "--publish-list-out",
                    str(package_plan),
                    "--symbol-publish-list-out",
                    str(symbol_plan),
                ]
            )

        self.assertEqual(0, result)
        receipt = json.loads(evidence.read_text(encoding="utf-8"))
        self.assertEqual("pass", receipt["status"])
        self.assertEqual("absent", receipt["packages"][0]["state"])
        self.assertEqual("absent", receipt["symbolPackages"][0]["state"])
        self.assertTrue(package_plan.read_text(encoding="utf-8").strip().endswith(".nupkg"))
        self.assertTrue(symbol_plan.read_text(encoding="utf-8").strip().endswith(".snupkg"))


if __name__ == "__main__":
    unittest.main()
