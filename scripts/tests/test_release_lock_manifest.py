"""Execute the release workflow's privileged lock validator against local Git data."""

import base64
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import textwrap
import unittest


ROOT = Path(os.environ.get(
    "HONUA_SDK_DOTNET_REPO", Path(__file__).resolve().parents[2]
))
WORKFLOW = ROOT / ".github/workflows/release-please.yml"
BRANCH = "release-please--branches--trunk--components--dotnet-sdk"
SOURCE = "a" * 40


def workflow_step() -> str:
    source = WORKFLOW.read_text(encoding="utf-8")
    start = source.index("      - name: Verify PR identity and apply only tracked locks\n")
    end = source.index("      # GITHUB_TOKEN branch pushes", start)
    block = source[start:end]
    return textwrap.dedent(block.split("        run: |\n", 1)[1])


def validator_source() -> str:
    return workflow_step().split("python3 - <<'PY'\n", 1)[1].split("\nPY\n", 1)[0]


class ReleaseLockManifestTests(unittest.TestCase):
    def test_privileged_job_does_not_restore_or_execute_pr_source(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        generation = workflow.split("  generate-release-pr-locks:\n", 1)[1].split(
            "  apply-release-pr-locks:\n", 1
        )[0]
        application = workflow.split("  apply-release-pr-locks:\n", 1)[1].split(
            "\n  publish:\n", 1
        )[0]
        self.assertIn("      contents: read\n", generation)
        self.assertIn("      packages: read\n", generation)
        self.assertNotIn("      contents: write\n", generation)
        self.assertNotIn("      actions: write\n", generation)
        self.assertIn("dotnet restore Honua.Sdk.sln", generation)
        self.assertIn("      contents: write\n", application)
        self.assertIn("      actions: write\n", application)
        self.assertNotIn("dotnet restore", application)
        self.assertNotIn("dotnet build", application)
        self.assertNotIn("./scripts/", application)

    def setUp(self) -> None:
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        self.relative = "tests/Honua.Sdk.BrowserSmoke/packages.lock.json"
        self.lock = self.root / self.relative
        self.lock.parent.mkdir(parents=True)
        self.lock.write_bytes(b'{"version":1}')
        subprocess.run(["git", "add", "."], cwd=self.root, check=True)
        subprocess.run(
            ["git", "-c", "user.name=Test", "-c", "user.email=test@example.com",
             "commit", "-qm", "base"],
            cwd=self.root, check=True,
        )
        self.manifest = self.root / "manifest.json"
        self.environment = {
            **os.environ,
            "LOCK_MANIFEST": str(self.manifest),
            "RELEASE_BRANCH": BRANCH,
            "RELEASE_HEAD": SOURCE,
        }

    @staticmethod
    def entry(path: str, data: bytes = b'{"version":2}', digest: str | None = None) -> dict:
        return {
            "path": path,
            "sha256": digest or hashlib.sha256(data).hexdigest(),
            "base64": base64.b64encode(data).decode("ascii"),
        }

    def run_validator(self, files: list[dict], source: str = SOURCE) -> subprocess.CompletedProcess[str]:
        self.manifest.write_text(json.dumps({
            "branch": BRANCH, "sourceSha": source, "files": files,
        }))
        return subprocess.run(
            ["python3", "-c", validator_source()],
            cwd=self.root, env=self.environment, text=True, capture_output=True,
        )

    def staged_paths(self) -> str:
        return subprocess.check_output(
            ["git", "diff", "--cached", "--name-only"], cwd=self.root, text=True,
        ).strip()

    def test_valid_lock_and_empty_manifest(self) -> None:
        result = self.run_validator([self.entry(self.relative)])
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.staged_paths(), self.relative)
        staged = subprocess.check_output(
            ["git", "show", f":{self.relative}"], cwd=self.root,
        )
        self.assertEqual(staged, b'{"version":2}')
        subprocess.run(["git", "reset", "-q", "--hard", "HEAD"], cwd=self.root, check=True)
        result = self.run_validator([])
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(self.staged_paths(), "")

    def test_malformed_or_untracked_payload_is_refused(self) -> None:
        invalid = {
            "traversal": [self.entry("../packages.lock.json")],
            "untracked": [self.entry("tests/Other/packages.lock.json")],
            "other-root": [self.entry(".github/Other/packages.lock.json")],
            "bad-digest": [self.entry(self.relative, digest="0" * 64)],
            "oversize": [self.entry(self.relative, data=b"x" * 4_000_001)],
            "duplicate": [self.entry(self.relative), self.entry(self.relative)],
        }
        for name, files in invalid.items():
            with self.subTest(name=name):
                subprocess.run(["git", "reset", "-q", "--hard", "HEAD"],
                               cwd=self.root, check=True)
                result = self.run_validator(files)
                self.assertNotEqual(result.returncode, 0, name)
                self.assertEqual(self.staged_paths(), "", name)

    def test_source_mismatch_is_refused(self) -> None:
        result = self.run_validator([], source="b" * 40)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("does not match current PR", result.stderr)
        self.assertEqual(self.staged_paths(), "")

    def test_verification_dispatches_complete_set_and_stops_if_branch_moves(self) -> None:
        remote = self.root / "remote.git"
        subprocess.run(["git", "init", "--bare", "-q", str(remote)], check=True)
        subprocess.run(["git", "remote", "add", "origin", str(remote)], cwd=self.root, check=True)
        head = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=self.root, text=True).strip()
        subprocess.run(["git", "push", "-q", "origin", f"HEAD:refs/heads/{BRANCH}"],
                       cwd=self.root, check=True)
        subprocess.run(["git", "-c", "user.name=Test", "-c", "user.email=test@example.com",
                        "commit", "--allow-empty", "-qm", "advanced"], cwd=self.root, check=True)
        moved = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=self.root, text=True).strip()
        subprocess.run(["git", "push", "-q", "origin", "HEAD:refs/heads/other"],
                       cwd=self.root, check=True)
        subprocess.run(["git", "checkout", "-q", "--detach", head], cwd=self.root, check=True)
        bin_dir = self.root / "bin"
        bin_dir.mkdir()
        fake_gh = bin_dir / "gh"
        fake_gh.write_text(
            "#!/usr/bin/env python3\nimport json, os, pathlib, subprocess, sys\n"
            "log = pathlib.Path(os.environ['DISPATCH_LOG'])\n"
            "with log.open('a') as output: output.write(json.dumps(sys.argv[1:]) + '\\n')\n"
            "if os.environ.get('MOVE_BRANCH'):\n"
            "    subprocess.run(['git', '--git-dir', os.environ['TEST_REMOTE'], 'update-ref', "
            "'refs/heads/' + os.environ['RELEASE_BRANCH'], os.environ['MOVE_BRANCH']], check=True)\n"
        )
        fake_gh.chmod(0o755)
        source = WORKFLOW.read_text()
        start = source.index("      - name: Verify the refreshed release PR head\n")
        end = source.index("\n  publish:\n", start)
        script = textwrap.dedent(source[start:end].split("        run: |\n", 1)[1])
        log = self.root / "dispatch.jsonl"
        environment = {**self.environment, "GITHUB_REPOSITORY": "honua-io/honua-sdk-dotnet",
                       "PATH": f"{bin_dir}:{os.environ['PATH']}", "DISPATCH_LOG": str(log),
                       "TEST_REMOTE": str(remote)}
        result = subprocess.run(["bash", "-c", script], cwd=self.root, env=environment,
                                text=True, capture_output=True)
        self.assertEqual(0, result.returncode, result.stderr)
        calls = [json.loads(line) for line in log.read_text().splitlines()]
        self.assertEqual(["ci.yml", "browser-trim.yml", "docs.yml", "codeql.yml"],
                         [call[2] for call in calls])
        self.assertTrue(all(call[-2:] == ["--ref", BRANCH] for call in calls))
        log.unlink()
        result = subprocess.run(["bash", "-c", script], cwd=self.root,
                                env={**environment, "MOVE_BRANCH": moved}, text=True, capture_output=True)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("moved before verification dispatch", result.stderr)
        self.assertEqual(1, len(log.read_text().splitlines()))

    def test_branch_move_is_refused_before_checkout(self) -> None:
        remote = self.root / "remote.git"
        subprocess.run(["git", "init", "--bare", "-q", str(remote)], check=True)
        subprocess.run(["git", "remote", "add", "origin", str(remote)],
                       cwd=self.root, check=True)
        subprocess.run(["git", "push", "-q", "origin", f"HEAD:refs/heads/{BRANCH}"],
                       cwd=self.root, check=True)
        bin_dir = self.root / "bin"
        bin_dir.mkdir()
        fake_gh = bin_dir / "gh"
        fake_gh.write_text(
            "#!/usr/bin/env python3\nimport json, os\n"
            "print(os.environ['FAKE_GH_JSON'])\n"
        )
        fake_gh.chmod(0o755)
        self.manifest.write_text("{}")
        result = subprocess.run(
            ["bash", "-c", workflow_step()], cwd=self.root,
            env={
                **self.environment,
                "PATH": f"{bin_dir}:{os.environ['PATH']}",
                "GITHUB_REPOSITORY": "honua-io/honua-sdk-dotnet",
                "GH_TOKEN": "test",
                "FAKE_GH_JSON": json.dumps([{
                    "headRefName": BRANCH, "baseRefName": "trunk",
                    "headRefOid": SOURCE, "isCrossRepository": False,
                }]),
            },
            text=True, capture_output=True,
        )
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Release branch advanced", result.stderr)
        self.assertEqual(self.staged_paths(), "")


if __name__ == "__main__":
    unittest.main()
