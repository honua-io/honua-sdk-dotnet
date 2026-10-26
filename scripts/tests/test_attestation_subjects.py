from __future__ import annotations

import hashlib
import importlib.util
import json
from pathlib import Path
import re
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "attestation-subjects.py"
WORKFLOW = ROOT / ".github" / "workflows" / "publish-dotnet-sdk.yml"
SPEC = importlib.util.spec_from_file_location("attestation_subjects", SCRIPT)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)

COMMIT = "a" * 40
VERSION = "1.13.2"


def _job(workflow: str, name: str) -> str:
    match = re.search(rf"(?ms)^  {re.escape(name)}:\n(.*?)(?=^  [a-z][a-z0-9-]*:\n|\Z)", workflow)
    assert match, name
    return match.group(0)


class AttestationSubjectTests(unittest.TestCase):
    def setUp(self) -> None:
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = Path(directory.name)
        self.packages = self.root / "nupkgs"
        self.packages.mkdir()
        self.built = {}
        for package in ("Honua.Sdk", "Honua.Sdk.Admin"):
            payload = f"unsigned {package}".encode()
            (self.packages / f"{package}.{VERSION}.nupkg").write_bytes(payload)
            self.built[package] = hashlib.sha256(payload).hexdigest()

    def proof(self, name: str, *, public=None, **overrides) -> Path:
        rows = []
        for package, digest in self.built.items():
            rows.append({
                "packageId": package,
                "state": "present-identical",
                "local": {"filename": f"{package}.{VERSION}.nupkg", "rawSha256": digest,
                          "repositoryCommit": COMMIT},
                "public": {"rawSha256": (public or {}).get(package, digest), "repositoryCommit": COMMIT},
            })
        document = {"mode": "verify", "status": "pass", "sourceRevision": COMMIT,
                    "packageVersion": VERSION, "packages": rows, "failures": []}
        document.update(overrides)
        path = self.root / name
        path.write_text(json.dumps(document), encoding="utf-8")
        return path

    def derive(self, nuget: Path | None, github: Path):
        return MODULE.derive(package_dir=self.packages, nuget_proof=nuget, github_proof=github,
                             source_revision=COMMIT, version=VERSION)

    def test_registry_signed_payload_is_the_sbom_subject(self) -> None:
        signed = {package: hashlib.sha256(f"signed {package}".encode()).hexdigest() for package in self.built}
        subjects, matrix = self.derive(self.proof("nuget.json", public=signed), self.proof("github.json"))
        names = dict(subjects)
        self.assertEqual(names[self.built["Honua.Sdk"]], f"unsigned/Honua.Sdk.{VERSION}.nupkg")
        self.assertEqual(names[signed["Honua.Sdk"]], f"nuget.org/Honua.Sdk.{VERSION}.nupkg")
        self.assertEqual(len(subjects), 4)
        self.assertEqual(matrix[0], {"package": "Honua.Sdk", "digest": f"sha256:{signed['Honua.Sdk']}",
                                     "name": f"nuget.org/Honua.Sdk.{VERSION}.nupkg"})

    def test_prerelease_uses_github_packages_only(self) -> None:
        subjects, matrix = self.derive(None, self.proof("github.json"))
        self.assertEqual(len(subjects), 2)
        self.assertEqual({row["digest"] for row in matrix}, {f"sha256:{d}" for d in self.built.values()})

    def test_refuses_unverified_or_unbound_proofs(self) -> None:
        cases = {
            "preflight": self.proof("p.json", mode="preflight"),
            "failing": self.proof("f.json", status="fail"),
            "other commit": self.proof("c.json", sourceRevision="b" * 40),
            "other version": self.proof("v.json", packageVersion="9.9.9"),
        }
        for label, path in cases.items():
            with self.subTest(label), self.assertRaises(MODULE.SubjectError):
                self.derive(path, self.proof("github.json"))

    def test_refuses_divergent_rows_and_bytes_not_on_disk(self) -> None:
        divergent = self.proof("d.json")
        document = json.loads(divergent.read_text())
        document["packages"][0]["state"] = "divergent"
        divergent.write_text(json.dumps(document))
        with self.assertRaises(MODULE.SubjectError):
            self.derive(divergent, self.proof("github.json"))
        (self.packages / f"Honua.Sdk.{VERSION}.nupkg").write_bytes(b"tampered")
        with self.assertRaises(MODULE.SubjectError):
            self.derive(self.proof("n.json"), self.proof("github.json"))


class AttestationWorkflowContractTests(unittest.TestCase):
    workflow = WORKFLOW.read_text(encoding="utf-8")

    def test_attestation_jobs_run_after_verified_publication_with_scoped_permissions(self) -> None:
        provenance = _job(self.workflow, "attest-dotnet-provenance")
        sbom = _job(self.workflow, "attest-dotnet-sbom")
        self.assertIn("- publish-dotnet-packages", provenance)
        self.assertIn("needs.publish-dotnet-packages.result == 'success'", provenance)
        self.assertIn("needs: attest-dotnet-provenance", sbom)
        for job in (provenance, sbom):
            self.assertIn("id-token: write", job)
            self.assertIn("attestations: write", job)
            self.assertIn("contents: read", job)
            self.assertNotIn("environment:", job)
            self.assertNotIn("uses: ./.github/workflows/", job)  # signer must stay this workflow
            for line in job.splitlines():
                if "uses:" in line:
                    self.assertRegex(line, r"@[0-9a-f]{40} # v")
        self.assertIn("actions/attest-build-provenance@4d101475d8b20a2381f78447822ac1eab6504dd8", provenance)
        self.assertIn('--source-revision "${GITHUB_SHA}"', provenance)
        self.assertIn("subject-checksums:", provenance)
        self.assertIn("actions/attest-sbom@c604332985a26aa8cf1bdc465b92731239ec6b9e", sbom)
        self.assertIn("subject-digest: ${{ matrix.digest }}", sbom)
        self.assertIn('--filename "${pkg_id}.cdx.json"', self.workflow)  # the SBOM the matrix selects


if __name__ == "__main__":
    unittest.main()
