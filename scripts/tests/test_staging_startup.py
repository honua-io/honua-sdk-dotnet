from __future__ import annotations

import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import textwrap
import unittest


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github/workflows/staging-integration.yml"
SEED = b"-- deterministic staging fixture\nSELECT 1;\n"
STUB = '''#!/usr/bin/env python3
import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
with open(os.environ["STUB_TRACE"], "a") as output:
    output.write(json.dumps([name, *args]) + "\\n")
if name == "gh":
    if args[:2] == ["attestation", "verify"]:
        print("[]")
        sys.exit(int(os.environ.get("STUB_ATTESTATION_EXIT", "0")))
    if args[:1] == ["api"]:
        print("-- deterministic staging fixture\\nSELECT 1;")
    else:
        sys.exit(2)
elif name == "curl":
    if any("healthz/ready" in arg for arg in args):
        print(os.environ.get("STUB_READY", "200"))
    else:
        print("<Name>honua:test_layer</Name>")
elif name == "psql":
    if "-f" in args:
        sys.exit(int(os.environ.get("STUB_SEED_EXIT", "0")))
elif name == "docker":
    if args[:1] == ["run"]:
        print("fixture-container-id")
elif name != "sleep":
    sys.exit(2)
'''


def startup_shell() -> str:
    body = WORKFLOW.read_text().split(
        "      - name: Seed and boot an ephemeral Honua server\n", 1
    )[1].split("\n      - name:", 1)[0]
    return textwrap.dedent(body.split("        run: |\n", 1)[1])


class StagingStartupTests(unittest.TestCase):
    def run_startup(self, **overrides: str):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        root = Path(directory.name)
        binaries = root / "bin"
        binaries.mkdir()
        for name in ("gh", "docker", "psql", "curl", "sleep"):
            path = binaries / name
            path.write_text(STUB)
            path.chmod(0o755)
        pins = json.loads((ROOT / "certification/candidate-pins.json").read_text())
        (root / "certification").mkdir()
        (root / "certification/candidate-pins.json").write_text(json.dumps(pins))
        trace = root / "trace.jsonl"
        environment_file = root / "github.env"
        env = {
            **os.environ,
            "PATH": str(binaries) + os.pathsep + os.environ["PATH"],
            "STUB_TRACE": str(trace),
            "PINNED_SERVER_IMAGE": "ghcr.io/honua-io/honua-server@" + pins["serverImageDigest"],
            "PINNED_SERVER_SOURCE_SHA": pins["serverSourceSha"],
            "PINNED_SERVER_FIXTURE_REVISION": "sha256:" + hashlib.sha256(SEED).hexdigest(),
            "EPHEMERAL_SERVICE_NAME": "test_service",
            "EPHEMERAL_LAYER_ID": "0",
            "EPHEMERAL_OGC_COLLECTION_ID": "0",
            "GITHUB_ENV": str(environment_file),
            **overrides,
        }
        result = subprocess.run(
            ["bash", "-euo", "pipefail", "-c", startup_shell()],
            cwd=root, env=env, text=True, capture_output=True, timeout=30,
        )
        events = [json.loads(line) for line in trace.read_text().splitlines()]
        return result, events, environment_file, root

    def test_actual_workflow_attests_then_migrates_before_loading_seed(self):
        result, events, environment_file, root = self.run_startup()
        self.assertEqual(0, result.returncode, result.stderr + result.stdout)
        boot = next(i for i, event in enumerate(events) if event[:2] == ["docker", "run"])
        ready = next(i for i, event in enumerate(events) if event[0] == "curl")
        seed = next(i for i, event in enumerate(events) if event[0] == "psql" and "-f" in event)
        extensions = next(i for i, event in enumerate(events) if event[0] == "psql" and "-c" in event)
        self.assertLess(extensions, boot)
        self.assertLess(boot, ready)
        self.assertLess(ready, seed)
        pins = json.loads((ROOT / "certification/candidate-pins.json").read_text())
        self.assertEqual(["gh", "attestation", "verify"], events[0][:3])
        self.assertIn("--deny-self-hosted-runners", events[0])
        self.assertEqual(pins["serverSourceSha"], events[0][events[0].index("--source-digest") + 1])
        self.assertEqual("ghcr.io/honua-io/honua-server@" + pins["serverImageDigest"], events[boot][-1])
        emitted = environment_file.read_text()
        self.assertIn("HONUA_STAGING_SERVER_COMMIT=" + pins["serverSourceSha"], emitted)
        self.assertIn("sha256:" + hashlib.sha256(SEED).hexdigest(), emitted)
        retained = root / "artifacts/evidence/staging-inputs"
        self.assertEqual(SEED, (retained / "base-schema.sql").read_bytes())
        self.assertEqual(pins, json.loads((retained / "candidate-pins.json").read_text()))
        self.assertTrue((retained / "server-attestation.json").is_file())

    def test_wrong_seed_bytes_refuse_before_database_or_container_mutation(self):
        result, events, environment_file, _ = self.run_startup(
            PINNED_SERVER_FIXTURE_REVISION="sha256:" + "0" * 64
        )
        self.assertNotEqual(0, result.returncode)
        self.assertIn("seed digest does not match", result.stdout)
        self.assertFalse(any(event[0] in {"docker", "psql"} for event in events))
        self.assertFalse(environment_file.exists())

    def test_unverified_image_refuses_before_seed_download_or_database(self):
        result, events, environment_file, _ = self.run_startup(STUB_ATTESTATION_EXIT="1")
        self.assertNotEqual(0, result.returncode)
        self.assertEqual(1, len(events))
        self.assertFalse(environment_file.exists())

    def test_unready_server_does_not_load_seed_or_emit_success(self):
        result, events, environment_file, _ = self.run_startup(STUB_READY="503")
        self.assertNotEqual(0, result.returncode)
        self.assertFalse(any(event[0] == "psql" and "-f" in event for event in events))
        self.assertEqual(60, sum(event[0] == "curl" for event in events))
        self.assertFalse(environment_file.exists())

    def test_seed_application_failure_does_not_emit_success(self):
        result, events, environment_file, _ = self.run_startup(STUB_SEED_EXIT="1")
        self.assertNotEqual(0, result.returncode)
        self.assertEqual(1, sum(event[0] == "curl" for event in events))
        self.assertFalse(environment_file.exists())


if __name__ == "__main__":
    unittest.main()
