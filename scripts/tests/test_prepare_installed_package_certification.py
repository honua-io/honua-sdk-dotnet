import base64
import copy
import hashlib
import importlib.util
import io
import json
import os
import re
import subprocess
import tempfile
import textwrap
import unittest
import zipfile
from argparse import Namespace
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "prepare-installed-package-certification.py"
SPEC = importlib.util.spec_from_file_location("installed_package_certification", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

PINS = MODULE.load_pins(MODULE.DEFAULT_PINS)
SERVER_IMAGE = MODULE.server_image(PINS)


def _nupkg(commit: str, version: str = PINS["sdkPackageVersion"]) -> bytes:
    buffer = io.BytesIO()
    with zipfile.ZipFile(buffer, "w") as archive:
        archive.writestr("Honua.Sdk.nuspec", f"""<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>Honua.Sdk</id>
    <version>{version}</version>
    <repository type="git" url="https://github.com/honua-io/honua-sdk-dotnet" commit="{commit}" />
  </metadata>
</package>
""")
    return buffer.getvalue()


def _sha512(data: bytes) -> str:
    return base64.b64encode(hashlib.sha512(data).digest()).decode("ascii")


def _sha256(data: bytes) -> str:
    return "sha256:" + hashlib.sha256(data).hexdigest()


class FakeSources:
    """Published state that agrees with a pin file until a test drifts one field."""

    def __init__(self, pins: dict) -> None:
        self.package = _nupkg(pins["sdkSourceSha"], pins["sdkPackageVersion"])
        self.fixture = b"-- seed\n"
        self.catalog = {
            pins["sdkPackageVersion"]: {
                "id": "Honua.Sdk",
                "version": pins["sdkPackageVersion"],
                "listed": True,
                "packageHash": _sha512(self.package),
                "packageHashAlgorithm": "SHA512",
                "repository": {"commit": pins["sdkSourceSha"]},
            }
        }
        self.packages = {pins["sdkPackageVersion"]: self.package}
        self.manifest = {
            "components": {
                "honua-server": {"sha": "b" * 40, "digest": "sha256:" + "0" * 64},
                "honua-sdk-dotnet": {"version": pins["sdkPackageVersion"], "sha": pins["sdkSourceSha"]},
            },
            "protocolCertification": {"candidateCutAt": "2026-10-12T09:45:00Z"},
            "clientArtifacts": {
                "honua-sdk-dotnet": {
                    "ecosystem": "nuget",
                    "package": "Honua.Sdk",
                    "version": pins["sdkPackageVersion"],
                    "digest": _sha256(self.package),
                    "sourceSha": pins["sdkSourceSha"],
                    "registry": "nuget.org",
                    "publicationState": "published",
                }
            },
        }
        self.image_digests = {f"nightly-{pins['serverSourceSha'][:7]}": pins["serverImageDigest"]}
        self.compare_status = "ahead"

    def nuget_catalog_entry(self, package_id, version):
        return self.catalog.get(version)

    def nuget_package(self, package_id, version):
        return self.packages.get(version)

    def release_manifest(self):
        return self.manifest

    def server_compare(self, base, head):
        return "identical" if base == head else self.compare_status

    def server_image_digest(self, tag):
        return self.image_digests.get(tag)

    def server_fixture(self, sha):
        return self.fixture


class InstalledPackageCertificationTests(unittest.TestCase):
    @staticmethod
    def _pins(**overrides):
        pins = copy.deepcopy(PINS)
        pins.update(overrides)
        return pins

    @classmethod
    def _consistent(cls, **overrides):
        pins = cls._pins(**overrides)
        sources = FakeSources(pins)
        pins["sdkPackageSha512"] = _sha512(sources.package)
        pins["sdkPackageDigest"] = _sha256(sources.package)
        pins["fixtureRevision"] = _sha256(sources.fixture)
        return pins, sources

    @staticmethod
    def _identity(**overrides):
        values = {
            "tier": "release",
            "release_cut": "2026-09-16T08:23:18.979734311Z",
            "server_image": SERVER_IMAGE,
            "server_source_sha": PINS["serverSourceSha"],
            "fixture_revision": PINS["fixtureRevision"],
        }
        values.update(overrides)
        return Namespace(**values)

    def test_committed_pin_file_is_well_formed(self):
        self.assertEqual(PINS["sdkPackageId"], "Honua.Sdk")
        self.assertEqual(PINS["sdkPackageRegistry"], "nuget.org")
        self.assertRegex(PINS["sdkPackageVersion"], r"^[0-9]+\.[0-9]+\.[0-9]+$")
        self.assertEqual(len(base64.b64decode(PINS["sdkPackageSha512"], validate=True)), 64)
        self.assertRegex(PINS["sdkPackageDigest"], r"^sha256:[0-9a-f]{64}$")
        self.assertRegex(PINS["sdkSourceSha"], r"^[0-9a-f]{40}$")
        self.assertRegex(PINS["serverSourceSha"], r"^[0-9a-f]{40}$")
        self.assertRegex(PINS["serverImageDigest"], r"^sha256:[0-9a-f]{64}$")
        self.assertRegex(PINS["fixtureRevision"], r"^sha256:[0-9a-f]{64}$")

    def test_pin_file_refuses_missing_floating_or_malformed_values(self):
        invalid = (
            {"sdkPackageVersion": "1.*"},
            {"sdkPackageVersion": ""},
            {"sdkPackageSha512": "not-base64"},
            {"sdkPackageSha512": base64.b64encode(b"short").decode()},
            {"sdkPackageDigest": "sha256:" + "A" * 64},
            {"sdkSourceSha": "short"},
            {"serverSourceSha": "ff5f567"},
            {"serverImageDigest": "nightly-ff5f567"},
            {"fixtureRevision": "sha256:"},
            {"sdkPackageRegistry": "github-packages"},
            {"sdkPackageId": "Honua.Sdk.Grpc"},
        )
        for values in invalid:
            with self.subTest(values=values), self.assertRaises(ValueError):
                MODULE.validate_pins(self._pins(**values))
        incomplete = self._pins()
        del incomplete["serverImageDigest"]
        with self.assertRaisesRegex(ValueError, "serverImageDigest"):
            MODULE.validate_pins(incomplete)

    def test_release_identity_accepts_exact_pinned_candidate(self):
        MODULE.validate_identity(self._identity(), PINS)

    def test_release_refuses_floating_or_unpinned_candidate(self):
        invalid = (
            {"release_cut": ""},
            {"server_image": "ghcr.io/honua-io/honua-server:nightly"},
            {"server_image": "ghcr.io/honua-io/honua-server:nightly-aot"},
            {"server_image": f"ghcr.io/honua-io/honua-server:nightly-{PINS['serverSourceSha'][:7]}"},
            {"server_image": "ghcr.io/honua-io/honua-server:nightly@sha256:" + "a" * 64},
            {"server_image": "ghcr.io/honua-io/honua-server@sha256:" + "a" * 64},
            {"server_source_sha": "c" * 40},
            {"fixture_revision": "sha256:" + "d" * 64},
        )
        for values in invalid:
            with self.subTest(values=values), self.assertRaises(ValueError):
                MODULE.validate_identity(self._identity(**values), PINS)

    def test_nightly_refuses_a_floating_server_image(self):
        with self.assertRaisesRegex(ValueError, "digest-addressed"):
            MODULE.validate_identity(
                self._identity(tier="nightly", server_image="ghcr.io/honua-io/honua-server:nightly"), PINS
            )

    def test_consumer_has_exact_package_from_nuget_org_and_no_project_reference(self):
        with tempfile.TemporaryDirectory() as directory:
            project = MODULE._write_consumer(Path(directory), PINS).read_text(encoding="utf-8")
            config = (Path(directory) / "nuget.config").read_text(encoding="utf-8")
        self.assertIn(
            f'PackageReference Include="Honua.Sdk" Version="[{PINS["sdkPackageVersion"]}]"', project
        )
        self.assertNotIn("ProjectReference", project)
        self.assertIn("<clear />", config)
        self.assertIn('value="https://api.nuget.org/v3/index.json"', config)
        self.assertEqual(config.count("<add key="), 1)
        self.assertNotIn("nuget.pkg.github.com", config)

    def test_installed_package_bytes_must_match_the_pinned_sha512(self):
        pins, sources = self._consistent()
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory) / "honua.sdk.nupkg"
            package.write_bytes(sources.package)
            identity = MODULE._verify_package(package, pins, Path(directory))
            self.assertEqual(identity["packageSha512"], pins["sdkPackageSha512"])
            self.assertEqual(identity["packageDigest"], pins["sdkPackageDigest"])
            self.assertEqual(identity["packageSourceSha"], pins["sdkSourceSha"])
            package.write_bytes(_nupkg(pins["sdkSourceSha"], "9.9.9"))
            with self.assertRaisesRegex(ValueError, "installed package integrity mismatch: expected SHA-512 "):
                MODULE._verify_package(package, pins, Path(directory))

    def test_installed_package_must_carry_the_pinned_source_commit(self):
        pins, sources = self._consistent()
        sources.package = _nupkg("e" * 40, pins["sdkPackageVersion"])
        pins["sdkPackageSha512"] = _sha512(sources.package)
        pins["sdkPackageDigest"] = _sha256(sources.package)
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory) / "honua.sdk.nupkg"
            package.write_bytes(sources.package)
            with self.assertRaisesRegex(ValueError, "installed package source mismatch"):
                MODULE._verify_package(package, pins, Path(directory))

    def test_check_accepts_pins_that_match_nuget_org_and_the_release_manifest(self):
        pins, sources = self._consistent()
        self.assertEqual(MODULE.check_pins(pins, sources), [])

    def test_check_reports_drifted_version_against_the_release_manifest(self):
        pins, sources = self._consistent()
        sources.manifest["clientArtifacts"]["honua-sdk-dotnet"]["version"] = "9.9.9"
        sources.manifest["components"]["honua-sdk-dotnet"]["version"] = "9.9.9"
        problems = MODULE.check_pins(pins, sources)
        self.assertTrue(problems)
        self.assertIn("release certification requires the manifest-pinned Honua.Sdk package", problems[0])
        self.assertIn(f"pin file has {pins['sdkPackageVersion']}, honua-release manifest has 9.9.9", problems[0])

    def test_check_reports_drifted_source_against_the_release_manifest(self):
        pins, sources = self._consistent()
        sources.manifest["clientArtifacts"]["honua-sdk-dotnet"]["sourceSha"] = "f" * 40
        problems = MODULE.check_pins(pins, sources)
        self.assertTrue(any("manifest-pinned Honua.Sdk package" in p and "sourceSha" in p for p in problems))

    def test_check_reports_drifted_digest_against_nuget_org(self):
        pins, sources = self._consistent()
        pins["sdkPackageSha512"] = _sha512(b"other bytes")
        problems = MODULE.check_pins(pins, sources)
        self.assertTrue(problems)
        self.assertIn("installed package integrity mismatch: expected SHA-512 " + pins["sdkPackageSha512"], problems[0])
        self.assertIn("nuget.org", problems[0])

    def test_check_reports_drifted_sha256_against_the_release_manifest(self):
        pins, sources = self._consistent()
        sources.manifest["clientArtifacts"]["honua-sdk-dotnet"]["digest"] = "sha256:" + "9" * 64
        problems = MODULE.check_pins(pins, sources)
        self.assertTrue(any("digest" in p and "manifest-pinned Honua.Sdk package" in p for p in problems))

    def test_check_reports_missing_package_with_the_server_pin(self):
        pins, sources = self._consistent()
        sources.catalog.clear()
        sources.packages.clear()
        problems = MODULE.check_pins(pins, sources)
        self.assertTrue(problems)
        self.assertIn(f"published package bytes for server pin {pins['serverSourceSha']} do not exist", problems[0])
        self.assertIn(f"Honua.Sdk {pins['sdkPackageVersion']}", problems[0])

    def test_check_refuses_an_unpublished_manifest_artifact(self):
        pins, sources = self._consistent()
        for state in ("staged", "placeholder", "source-built"):
            sources.manifest["clientArtifacts"]["honua-sdk-dotnet"]["publicationState"] = state
            with self.subTest(state=state):
                problems = MODULE.check_pins(pins, sources)
                self.assertEqual(len(problems), 1)
                self.assertIn(f"publicationState is {state}, not published", problems[0])

    def test_check_reports_drifted_server_image(self):
        pins, sources = self._consistent()
        tag = f"nightly-{pins['serverSourceSha'][:7]}"
        sources.image_digests[tag] = "sha256:" + "1" * 64
        problems = MODULE.check_pins(pins, sources)
        self.assertEqual(len(problems), 1)
        self.assertIn(f"ghcr.io/honua-io/honua-server:{tag} resolves to sha256:{'1' * 64}", problems[0])

    def test_check_reports_drifted_fixture(self):
        pins, sources = self._consistent()
        sources.fixture = b"-- changed seed\n"
        problems = MODULE.check_pins(pins, sources)
        self.assertEqual(len(problems), 1)
        self.assertIn("tests/seed/base-schema.sql", problems[0])

    def test_check_refuses_a_server_pin_behind_the_release_manifest(self):
        pins, sources = self._consistent()
        for status in ("behind", "diverged"):
            sources.compare_status = status
            with self.subTest(status=status):
                problems = MODULE.check_pins(pins, sources)
                self.assertEqual(len(problems), 1)
                self.assertIn(f"is {status}", problems[0])
                self.assertIn("honua-release manifest server pin " + "b" * 40, problems[0])

    def test_check_requires_the_manifest_digest_when_the_server_pin_is_identical(self):
        pins, sources = self._consistent()
        sources.manifest["components"]["honua-server"]["sha"] = pins["serverSourceSha"]
        sources.manifest["protocolCertification"]["candidateCutAt"] = pins["candidateCut"]
        problems = MODULE.check_pins(pins, sources)
        self.assertEqual(len(problems), 1)
        self.assertIn("serverImageDigest", problems[0])
        sources.manifest["components"]["honua-server"]["digest"] = pins["serverImageDigest"]
        self.assertEqual(MODULE.check_pins(pins, sources), [])

    def test_check_requires_the_manifest_cut_when_the_server_pin_is_identical(self):
        pins, sources = self._consistent()
        server = sources.manifest["components"]["honua-server"]
        server["sha"] = pins["serverSourceSha"]
        server["digest"] = pins["serverImageDigest"]
        sources.manifest["protocolCertification"]["candidateCutAt"] = "2026-09-16T02:20:00Z"
        pins["candidateCut"] = "2026-10-12T09:45:00Z"
        problems = MODULE.check_pins(pins, sources)
        self.assertEqual(len(problems), 1)
        self.assertIn("candidateCut", problems[0])
        self.assertIn("2026-09-16T02:20:00Z", problems[0])
        del sources.manifest["protocolCertification"]
        problems = MODULE.check_pins(pins, sources)
        self.assertEqual(len(problems), 1)
        self.assertIn("is None", problems[0])
        sources.manifest["protocolCertification"] = {"candidateCutAt": pins["candidateCut"]}
        self.assertEqual(MODULE.check_pins(pins, sources), [])

    def test_check_leaves_the_cut_of_a_server_pin_ahead_of_the_manifest(self):
        pins, sources = self._consistent()
        sources.manifest["protocolCertification"]["candidateCutAt"] = "2026-09-16T02:20:00Z"
        self.assertEqual(MODULE.check_pins(pins, sources), [])

    def test_pin_file_refuses_a_malformed_candidate_cut(self):
        for cut in ("", "2026-10-12", "2026-10-12T09:45:00+00:00", "2026-10-12 09:45:00Z", "latest",
                    "2026-02-31T25:61:61Z", "2026-02-29T09:45:00Z", "2026-10-12T24:00:00Z", "0000-01-01T00:00:00Z"):
            with self.subTest(cut=cut), self.assertRaises(ValueError):
                MODULE.validate_pins(self._pins(candidateCut=cut))

    def test_candidate_cut_checks_calendar_dates_in_pins_and_manifest(self):
        for cut in ("2024-02-29T23:59:59Z", "2026-10-12T09:45:00.123456Z"):
            with self.subTest(cut=cut):
                MODULE.validate_pins(self._pins(candidateCut=cut))
                self.assertEqual(cut, MODULE._manifest_candidate_cut({"protocolCertification": {"candidateCutAt": cut}}))
        for cut in ("2026-02-31T09:45:00Z", "2026-10-12T25:61:61Z"):
            with self.subTest(cut=cut), self.assertRaisesRegex(ValueError, "candidateCutAt"):
                MODULE._manifest_candidate_cut({"protocolCertification": {"candidateCutAt": cut}})

    def test_missing_package_bytes_fail_closed(self):
        message = MODULE.restore_failure_message("", package_present=False, server_sha=PINS["serverSourceSha"])
        self.assertIn("published package bytes for server pin", message)
        self.assertIn(PINS["serverSourceSha"], message)
        self.assertIn("do not exist", message)

    def test_untrusted_signature_fails_closed_without_claiming_missing_bytes(self):
        message = MODULE.restore_failure_message(
            "error NU3042: untrusted\nerror NU3018: not trusted",
            package_present=False,
            server_sha=PINS["serverSourceSha"],
        )
        self.assertIn("NU3042/NU3018", message)
        self.assertNotIn("do not exist", message)
        self.assertIn("does not suppress", message)

    def test_emit_env_exposes_the_pin_file_to_the_workflow(self):
        lines = dict(line.split("=", 1) for line in MODULE.emit_env(PINS).splitlines())
        self.assertEqual(lines["PINNED_SERVER_IMAGE"], SERVER_IMAGE)
        self.assertEqual(lines["PINNED_SERVER_SOURCE_SHA"], PINS["serverSourceSha"])
        self.assertEqual(lines["PINNED_SERVER_FIXTURE_REVISION"], PINS["fixtureRevision"])
        self.assertEqual(lines["PINNED_SDK_PACKAGE_ID"], "Honua.Sdk")
        self.assertEqual(lines["PINNED_SDK_PACKAGE_VERSION"], PINS["sdkPackageVersion"])
        self.assertEqual(lines["PINNED_SDK_PACKAGE_DIGEST"], PINS["sdkPackageDigest"])
        self.assertEqual(lines["PINNED_SDK_PACKAGE_SOURCE_SHA"], PINS["sdkSourceSha"])
        self.assertEqual(lines["PINNED_CANDIDATE_CUT"], PINS["candidateCut"])

    def test_workflow_and_script_read_the_one_pin_file(self):
        workflow = (MODULE.ROOT / ".github/workflows/sdk-certification.yml").read_text(encoding="utf-8")
        script = (MODULE.ROOT / "scripts/prepare-installed-package-certification.py").read_text(encoding="utf-8")
        self.assertIn("certification/candidate-pins.json", workflow)
        self.assertIn("--emit-env", workflow)
        self.assertIn("--check", workflow)
        for value in (
            PINS["sdkPackageVersion"], PINS["sdkPackageSha512"], PINS["sdkPackageDigest"],
            PINS["sdkSourceSha"], PINS["serverSourceSha"], PINS["serverImageDigest"], PINS["fixtureRevision"],
        ):
            with self.subTest(value=value):
                self.assertNotIn(value, workflow)
                self.assertNotIn(value, script)
        self.assertNotRegex(workflow, r"PINNED_(SERVER|SDK|CANDIDATE)_[A-Z_]+: ")
        self.assertNotIn(PINS["candidateCut"], workflow)
        self.assertIsNone(re.search(r"ghcr\.io/honua-io/honua-server:(nightly|latest)", workflow))
        self.assertNotIn("dotnet pack", script)
        self.assertNotIn("NuGetSignatureValidationMode", script)
        self.assertNotIn("WarningsNotAsErrors", script)
        self.assertNotIn("signatureValidationMode", script)
        self.assertLess(
            workflow.index("Resolve certification tier"),
            workflow.index("Verify candidate pins against nuget.org and honua-release"),
        )
        self.assertIn("schedule) tier=nightly ;;", workflow)
        self.assertIn("TIER: ${{ steps.tier.outputs.tier }}", workflow)
        emit = workflow.split("Emit and enforce normalized certification evidence", 1)[1]
        self.assertIn("schedule) tier=nightly ;;", emit)
        self.assertIn('--tier "${tier}"', emit)
        follow = (MODULE.ROOT / ".github/workflows/certification-pin-follow.yml").read_text(encoding="utf-8")
        self.assertIn("schedule:", follow)
        self.assertIn("workflow_dispatch:", follow)
        self.assertIn("--follow-manifest", follow)
        self.assertIn("--check", follow)
        self.assertIn("chore/certification-pin-follow", follow)
        self.assertNotIn("gh pr merge", follow)
        for value in (
            PINS["sdkPackageVersion"], PINS["sdkPackageSha512"], PINS["sdkPackageDigest"],
            PINS["sdkSourceSha"], PINS["serverSourceSha"], PINS["serverImageDigest"], PINS["fixtureRevision"],
        ):
            with self.subTest(follow=value):
                self.assertNotIn(value, follow)

    def test_actual_identity_step_keeps_release_and_candidate_cuts_independent(self):
        workflow = (MODULE.ROOT / ".github/workflows/sdk-certification.yml").read_text()
        step = workflow.split("      - name: Resolve tier and immutable identity\n", 1)[1].split("\n      - ", 1)[0]
        script = textwrap.dedent(step.split("        run: |\n", 1)[1])
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            metadata = json.dumps({"manifest": {"digest": PINS["serverImageDigest"]},
                                   "image": {"created": "2026-01-01T00:00:00Z"}})
            docker = root / "docker"
            docker.write_text("#!/bin/bash\nset -eu\ncase \"$1\" in\n"
                              "buildx) printf '%s' '" + metadata + "';;\n"
                              "login) cat >/dev/null;;\n*) exit 91;;\nesac\n")
            gh = root / "gh"
            gh.write_text("#!/bin/bash\nset -eu\n[[ \"$1 $2\" == 'attestation verify' ]]\nprintf '[]'\n")
            docker.chmod(0o700); gh.chmod(0o700)
            output = root / "output"
            release_cut = "2026-10-29T12:00:00Z"
            env = {**os.environ, "PATH": str(root) + os.pathsep + os.environ["PATH"],
                   **dict(line.split("=", 1) for line in MODULE.emit_env(PINS).splitlines()),
                   "CERT_TIER": "release", "INPUT_IMAGE": SERVER_IMAGE,
                   "INPUT_SOURCE_SHA": PINS["serverSourceSha"], "INPUT_SEED_REF": "",
                   "INPUT_FIXTURE_REVISION": "", "INPUT_RELEASE_CUT": release_cut,
                   "GH_TOKEN": "synthetic-test-token", "GITHUB_ACTOR": "test", "GITHUB_OUTPUT": str(output)}
            subprocess.run(["bash", "-c", script], cwd=root, env=env, check=True, capture_output=True, text=True)
            values = dict(line.split("=", 1) for line in output.read_text().splitlines())
            self.assertEqual(values["candidate-cut"], PINS["candidateCut"])
            self.assertEqual(values["release-cut"], release_cut)
            self.assertNotEqual(values["candidate-cut"], values["release-cut"])

    def test_follow_is_a_no_op_when_the_manifest_matches_nuget_org(self):
        pins, sources = self._consistent()
        updated, changed = MODULE.follow_release_manifest(pins, sources)
        self.assertFalse(changed)
        self.assertEqual(updated, pins)

    def test_follow_updates_package_fields_and_keeps_the_server_pin(self):
        pins, sources = self._consistent()
        version = "1.10.3"
        source = "d" * 40
        package = _nupkg(source, version)
        sources.packages[version] = package
        sources.catalog[version] = {
            "id": "Honua.Sdk",
            "version": version,
            "listed": True,
            "packageHash": _sha512(package),
            "packageHashAlgorithm": "SHA512",
            "repository": {"commit": source},
        }
        artifact = sources.manifest["clientArtifacts"]["honua-sdk-dotnet"]
        artifact["version"] = version
        artifact["sourceSha"] = source
        artifact["digest"] = _sha256(package)
        sources.manifest["components"]["honua-sdk-dotnet"]["version"] = version
        sources.manifest["components"]["honua-sdk-dotnet"]["sha"] = source
        updated, changed = MODULE.follow_release_manifest(pins, sources)
        self.assertTrue(changed)
        self.assertEqual(updated["sdkPackageVersion"], version)
        self.assertEqual(updated["sdkPackageDigest"], _sha256(package))
        self.assertEqual(updated["sdkPackageSha512"], _sha512(package))
        self.assertEqual(updated["sdkSourceSha"], source)
        self.assertEqual(updated["serverSourceSha"], pins["serverSourceSha"])
        self.assertEqual(updated["serverImageDigest"], pins["serverImageDigest"])
        self.assertEqual(updated["fixtureRevision"], pins["fixtureRevision"])
        self.assertEqual(pins["sdkPackageVersion"], PINS["sdkPackageVersion"])

    def test_follow_advances_a_server_pin_behind_the_manifest(self):
        pins, sources = self._consistent()
        sources.compare_status = "behind"
        manifest_sha = sources.manifest["components"]["honua-server"]["sha"]
        manifest_digest = sources.manifest["components"]["honua-server"]["digest"]
        sources.image_digests[f"nightly-{manifest_sha[:7]}"] = manifest_digest
        sources.fixture = b"-- manifest seed\n"
        updated, changed = MODULE.follow_release_manifest(pins, sources)
        self.assertTrue(changed)
        self.assertEqual(updated["sdkPackageVersion"], pins["sdkPackageVersion"])
        self.assertEqual(updated["serverSourceSha"], manifest_sha)
        self.assertEqual(updated["serverImageDigest"], manifest_digest)
        self.assertEqual(updated["fixtureRevision"], _sha256(sources.fixture))
        self.assertEqual(updated["candidateCut"], "2026-10-12T09:45:00Z")
        self.assertEqual(pins["serverSourceSha"], PINS["serverSourceSha"])

    def test_follow_refuses_to_advance_without_a_manifest_cut(self):
        pins, sources = self._consistent()
        sources.compare_status = "behind"
        manifest_sha = sources.manifest["components"]["honua-server"]["sha"]
        sources.image_digests[f"nightly-{manifest_sha[:7]}"] = sources.manifest["components"]["honua-server"]["digest"]
        sources.manifest["protocolCertification"]["candidateCutAt"] = "2026-10-12"
        with self.assertRaisesRegex(ValueError, "candidateCutAt"):
            MODULE.follow_release_manifest(pins, sources)

    def test_follow_corrects_a_stale_cut_on_the_manifest_snapshot(self):
        pins, sources = self._consistent(candidateCut="2026-09-16T02:20:00Z")
        sources.manifest["components"]["honua-server"]["sha"] = pins["serverSourceSha"]
        sources.manifest["components"]["honua-server"]["digest"] = pins["serverImageDigest"]
        updated, changed = MODULE.follow_release_manifest(pins, sources)
        self.assertTrue(changed)
        self.assertEqual(updated["candidateCut"], "2026-10-12T09:45:00Z")
        self.assertEqual(MODULE.check_pins(updated, sources), [])

    def test_follow_keeps_the_cut_of_a_server_pin_ahead_of_the_manifest(self):
        pins, sources = self._consistent(candidateCut="2026-10-20T00:00:00Z")
        updated, changed = MODULE.follow_release_manifest(pins, sources)
        self.assertFalse(changed)
        self.assertEqual(updated["candidateCut"], "2026-10-20T00:00:00Z")

    def test_follow_refuses_a_diverged_server_pin(self):
        pins, sources = self._consistent()
        sources.compare_status = "diverged"
        with self.assertRaisesRegex(ValueError, "diverged"):
            MODULE.follow_release_manifest(pins, sources)
        self.assertEqual(pins["serverSourceSha"], PINS["serverSourceSha"])

    def test_follow_refuses_a_behind_server_image_ghcr_does_not_serve(self):
        pins, sources = self._consistent()
        sources.compare_status = "behind"
        with self.assertRaisesRegex(ValueError, "is not served"):
            MODULE.follow_release_manifest(pins, sources)
        self.assertEqual(pins["serverSourceSha"], PINS["serverSourceSha"])

    def test_follow_keeps_the_manifest_digest_when_the_tag_was_rebuilt(self):
        pins, sources = self._consistent()
        sources.compare_status = "behind"
        manifest_sha = sources.manifest["components"]["honua-server"]["sha"]
        manifest_digest = sources.manifest["components"]["honua-server"]["digest"]
        sources.image_digests[f"nightly-{manifest_sha[:7]}"] = "sha256:" + "e" * 64
        sources.image_digests[manifest_digest] = manifest_digest
        updated, changed = MODULE.follow_release_manifest(pins, sources)
        self.assertTrue(changed)
        self.assertEqual(updated["serverSourceSha"], manifest_sha)
        self.assertEqual(updated["serverImageDigest"], manifest_digest)

    def test_check_accepts_the_manifest_digest_when_its_tag_was_rebuilt(self):
        pins, sources = self._consistent()
        sources.manifest["components"]["honua-server"]["sha"] = pins["serverSourceSha"]
        sources.manifest["protocolCertification"]["candidateCutAt"] = pins["candidateCut"]
        sources.manifest["components"]["honua-server"]["digest"] = pins["serverImageDigest"]
        sources.image_digests[f"nightly-{pins['serverSourceSha'][:7]}"] = "sha256:" + "e" * 64
        sources.image_digests[pins["serverImageDigest"]] = pins["serverImageDigest"]
        self.assertEqual(MODULE.check_pins(pins, sources), [])

    def test_check_refuses_a_rebuilt_tag_when_the_pinned_digest_is_gone(self):
        pins, sources = self._consistent()
        sources.manifest["components"]["honua-server"]["sha"] = pins["serverSourceSha"]
        sources.manifest["protocolCertification"]["candidateCutAt"] = pins["candidateCut"]
        sources.manifest["components"]["honua-server"]["digest"] = pins["serverImageDigest"]
        sources.image_digests[f"nightly-{pins['serverSourceSha'][:7]}"] = "sha256:" + "e" * 64
        problems = MODULE.check_pins(pins, sources)
        self.assertEqual(len(problems), 1)
        self.assertIn("server image drifted", problems[0])

    def test_follow_leaves_an_identical_server_pin_when_the_digest_already_matches(self):
        pins, sources = self._consistent()
        sources.manifest["components"]["honua-server"]["sha"] = pins["serverSourceSha"]
        sources.manifest["protocolCertification"]["candidateCutAt"] = pins["candidateCut"]
        sources.manifest["components"]["honua-server"]["digest"] = pins["serverImageDigest"]
        updated, changed = MODULE.follow_release_manifest(pins, sources)
        self.assertFalse(changed)
        self.assertEqual(updated["serverSourceSha"], pins["serverSourceSha"])
        self.assertEqual(updated["serverImageDigest"], pins["serverImageDigest"])

    def test_follow_refuses_an_unpublished_manifest_artifact(self):
        pins, sources = self._consistent()
        for state in ("staged", "placeholder", "source-built", None):
            sources.manifest["clientArtifacts"]["honua-sdk-dotnet"]["publicationState"] = state
            with self.subTest(state=state), self.assertRaisesRegex(ValueError, "unpublished"):
                MODULE.follow_release_manifest(pins, sources)

    def test_follow_refuses_a_version_missing_from_nuget_org(self):
        pins, sources = self._consistent()
        sources.manifest["clientArtifacts"]["honua-sdk-dotnet"]["version"] = "9.9.9"
        sources.manifest["components"]["honua-sdk-dotnet"]["version"] = "9.9.9"
        with self.assertRaisesRegex(ValueError, "9.9.9 is not on nuget.org"):
            MODULE.follow_release_manifest(pins, sources)

    def test_follow_refuses_when_nuget_bytes_disagree_with_the_manifest_digest(self):
        pins, sources = self._consistent()
        sources.manifest["clientArtifacts"]["honua-sdk-dotnet"]["digest"] = "sha256:" + "9" * 64
        with self.assertRaisesRegex(ValueError, "manifest digest"):
            MODULE.follow_release_manifest(pins, sources)

    def test_follow_refuses_when_the_component_sha_disagrees(self):
        pins, sources = self._consistent()
        sources.manifest["components"]["honua-sdk-dotnet"]["sha"] = "a" * 40
        with self.assertRaisesRegex(ValueError, "does not match"):
            MODULE.follow_release_manifest(pins, sources)


class DispatchedPinTests(unittest.TestCase):
    """Ruling sdk-harness-trunk-pinned-package-2026-10-27: the nightly-dispatched package and
    server are the governed identity, not certification/candidate-pins.json."""

    SOURCE = "5" * 40
    SERVER = "2" * 40
    SEED = "8" * 40

    def _dispatch(self, **overrides):
        package = _nupkg(self.SOURCE, "9.9.9")
        sources = FakeSources({**PINS, "sdkPackageVersion": "9.9.9", "sdkSourceSha": self.SOURCE})
        sources.fixture = b"-- dispatched seed\n"
        fetched = []
        original = sources.server_fixture
        sources.server_fixture = lambda sha: (fetched.append(sha), original(sha))[1]
        values = {
            "sdk_package": "Honua.Sdk", "sdk_package_version": "9.9.9",
            "sdk_package_digest": _sha256(package), "sdk_source_sha": self.SOURCE,
            "server_image": "ghcr.io/honua-io/honua-server@sha256:" + "1" * 64,
            "server_source_sha": self.SERVER, "server_seed_ref": self.SEED,
            "release_cut": "2026-10-27T06:00:00Z",
        }
        values.update(overrides)
        return Namespace(**values), sources, package, fetched

    def test_dispatched_pins_are_the_dispatched_inputs_and_the_published_bytes(self):
        args, sources, package, fetched = self._dispatch()
        pins = MODULE.dispatched_pins(args, sources)
        MODULE.validate_pins(pins)
        self.assertEqual({
            "sdkPackageId": "Honua.Sdk", "sdkPackageVersion": "9.9.9", "sdkPackageRegistry": "nuget.org",
            "sdkPackageSha512": _sha512(package), "sdkPackageDigest": _sha256(package),
            "sdkSourceSha": self.SOURCE, "serverSourceSha": self.SERVER,
            "serverImageDigest": "sha256:" + "1" * 64,
            # Measured exactly as the workflow measures the applied seed: sha256 of the seed file.
            "fixtureRevision": _sha256(b"-- dispatched seed\n"),
            "candidateCut": "2026-10-27T06:00:00Z",
        }, pins)
        self.assertEqual([self.SEED], fetched)
        for field in ("sdkPackageVersion", "sdkSourceSha", "serverSourceSha", "serverImageDigest"):
            self.assertNotEqual(PINS[field], pins[field])
        # The restore then accepts the dispatched release candidate against these pins only.
        MODULE.validate_identity(Namespace(tier="release", release_cut=args.release_cut,
                                           server_image=args.server_image, server_source_sha=self.SERVER,
                                           fixture_revision=pins["fixtureRevision"]), pins)
        with tempfile.TemporaryDirectory() as directory:
            nupkg = Path(directory) / "honua.sdk.9.9.9.nupkg"
            nupkg.write_bytes(package)
            identity = MODULE._verify_package(nupkg, pins, Path(directory))
        self.assertEqual(_sha256(package), identity["packageDigest"])

    def test_seed_ref_defaults_to_the_server_source(self):
        args, sources, _, fetched = self._dispatch(server_seed_ref=None)
        self.assertEqual(self.SERVER, MODULE.dispatched_pins(args, sources)["serverSourceSha"])
        self.assertEqual([self.SERVER], fetched)

    def test_dispatched_pins_refuse_a_digest_the_nuget_org_bytes_do_not_have(self):
        args, sources, _, _ = self._dispatch(sdk_package_digest="sha256:" + "a" * 64)
        with self.assertRaisesRegex(ValueError, "sdk_package_digest"):
            MODULE.dispatched_pins(args, sources)

    def test_dispatched_pins_refuse_a_package_built_from_another_source(self):
        args, sources, _, _ = self._dispatch(sdk_source_sha="6" * 40)
        with self.assertRaisesRegex(ValueError, "sdk_source_sha"):
            MODULE.dispatched_pins(args, sources)

    def test_dispatched_pins_refuse_a_version_nuget_org_does_not_serve(self):
        args, sources, _, _ = self._dispatch(sdk_package_version="9.9.10")
        with self.assertRaisesRegex(ValueError, "not on nuget.org"):
            MODULE.dispatched_pins(args, sources)

    def test_dispatched_pins_refuse_missing_or_malformed_inputs(self):
        refusals = {
            "sdk_package": ("Honua.Sdk.Repacked", None, ""),
            "sdk_package_version": ("latest", "1.12", None),
            "sdk_package_digest": ("sha256:" + "A" * 64, "sha512:" + "a" * 64, None),
            "sdk_source_sha": ("a" * 7, "A" * 40, None),
            "server_image": ("ghcr.io/honua-io/honua-server:nightly", None),
            "server_source_sha": ("b" * 39, None),
            "server_seed_ref": ("main",),
            "release_cut": ("2026-10-27", None),
        }
        for field, values in refusals.items():
            for value in values:
                with self.subTest(field=field, value=value):
                    args, sources, _, _ = self._dispatch(**{field: value})
                    with self.assertRaisesRegex(ValueError, field):
                        MODULE.dispatched_pins(args, sources)

    def test_dispatched_pins_cli_writes_a_canonical_pin_file(self):
        args, sources, package, _ = self._dispatch()
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "package" / "dispatched-pins.json"
            argv = ["--dispatched-pins-output", str(output)]
            for name, value in vars(args).items():
                argv += [f"--{name.replace('_', '-')}", value]
            original = MODULE.LiveSources
            MODULE.LiveSources = lambda: sources
            try:
                self.assertEqual(0, MODULE.main(argv))
            finally:
                MODULE.LiveSources = original
            text = output.read_text(encoding="utf-8")
            self.assertEqual(text, json.dumps(json.loads(text), indent=2) + "\n")
            self.assertEqual(_sha256(package), MODULE.load_pins(output)["sdkPackageDigest"])


class CommittedPinFileTests(unittest.TestCase):
    def test_pin_file_is_canonical_json(self):
        text = MODULE.DEFAULT_PINS.read_text(encoding="utf-8")
        self.assertEqual(text, json.dumps(json.loads(text), indent=2) + "\n")


if __name__ == "__main__":
    unittest.main()
