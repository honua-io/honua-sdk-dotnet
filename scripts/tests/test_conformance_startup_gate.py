from __future__ import annotations

import importlib.util
from pathlib import Path
import re
import unittest


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github" / "workflows" / "conformance.yml"


def conformance_steps(workflow_text: str) -> list[dict[str, str]]:
    """Return name, id, and body for steps in the conformance job."""
    lines = workflow_text.splitlines()
    try:
        start = lines.index("  conformance:")
    except ValueError as error:
        raise AssertionError("conformance job is missing") from error
    end = len(lines)
    for index in range(start + 1, len(lines)):
        if re.match(r"^  [A-Za-z0-9_-]+:\s*$", lines[index]):
            end = index
            break

    steps: list[dict[str, str]] = []
    current: dict[str, str] | None = None
    for line in lines[start:end]:
        name_match = re.match(r"^      - name: (.*)$", line)
        if name_match:
            if current is not None:
                steps.append(current)
            current = {"name": name_match.group(1).strip(), "id": "", "body": ""}
            continue
        if current is None:
            continue
        id_match = re.match(r"^        id: (\S+)\s*$", line)
        if id_match and not current["id"]:
            current["id"] = id_match.group(1)
        current["body"] += line + "\n"
    if current is not None:
        steps.append(current)
    return steps


LEGACY_DIGEST = "d7a45c871bf318b4882ec8e1c32004803e6d0210246be30120751f05dee1a14d"
LEGACY_IMAGE = f"ghcr.io/honua-io/honua-server@sha256:{LEGACY_DIGEST}"
SEED_LOAD = re.compile(r"-f\s+\.conformance/base-schema\.sql")
ORDER_COMMAND = (
    '          order="$(python3 scripts/conformance_seed_order.py '
    '--image "${HONUA_SERVER_IMAGE}" --legacy "${LEGACY_SEED_BEFORE_START_IMAGE}")"'
)
PRE_START_GUARD = re.compile(
    re.escape(ORDER_COMMAND)
    + r"\n"
    + r'          if \[\[ "\$\{order\}" != "before" \]\]; then\n'
    + r'            echo "modern image; seed waits until the server is ready"\n'
    + r"            exit 0\n"
    + r"          fi\n"
    + r"          psql -h localhost -U honua -d honua_test -v ON_ERROR_STOP=1 "
    + r"-f \.conformance/base-schema\.sql"
)
POST_START_GUARD = re.compile(
    re.escape(ORDER_COMMAND)
    + r"\n"
    + r'          if \[\[ "\$\{order\}" == "before" \]\]; then\n'
    + r'            echo "legacy pinned image was seeded before startup"\n'
    + r"            exit 0\n"
    + r"          fi\n"
    + r"          psql -h localhost -U honua -d honua_test -v ON_ERROR_STOP=1 "
    + r"-f \.conformance/base-schema\.sql"
)


def _seed_order_module():
    path = ROOT / "scripts" / "conformance_seed_order.py"
    spec = importlib.util.spec_from_file_location("conformance_seed_order", path)
    if spec is None or spec.loader is None:
        raise AssertionError("conformance_seed_order.py is not importable")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def legacy_guarded_pre_start_seed(body: str) -> bool:
    """True when a pre-start load exits before psql for every non-legacy image."""
    if not SEED_LOAD.search(body):
        return True
    return PRE_START_GUARD.search(body) is not None


def startup_gate_errors(workflow_text: str) -> list[str]:
    """Modern images must migrate before tests/seed/base-schema.sql is loaded."""
    steps = conformance_steps(workflow_text)
    by_id = {step["id"]: step for step in steps if step["id"]}
    errors: list[str] = []
    required = ("fetch-seed", "legacy-seed", "start-server", "apply-seed", "live")
    missing = [step_id for step_id in required if step_id not in by_id]
    if missing:
        return [f"missing conformance step: {', '.join(missing)}"]

    order = [step["id"] for step in steps if step["id"] in required]
    if order != list(required):
        errors.append(
            "conformance startup order must be fetch-seed, legacy-seed, "
            f"start-server, apply-seed, live; got {', '.join(order)}"
        )

    legacy_image = (
        'LEGACY_SEED_BEFORE_START_IMAGE: "ghcr.io/honua-io/honua-server@sha256:'
        f'{LEGACY_DIGEST}"'
    )
    if legacy_image not in workflow_text:
        errors.append("legacy seed-before-start image must stay the e3ab87ce digest")

    fetch = by_id["fetch-seed"]["body"]
    legacy = by_id["legacy-seed"]["body"]
    start = by_id["start-server"]["body"]
    apply = by_id["apply-seed"]["body"]

    if SEED_LOAD.search(fetch):
        errors.append("fetch-seed loads base-schema.sql before the server starts")
    if "CREATE EXTENSION IF NOT EXISTS postgis" not in fetch or "postgis_raster" not in fetch:
        errors.append("fetch-seed must create postgis and postgis_raster only")
    if not legacy_guarded_pre_start_seed(legacy):
        errors.append(
            "legacy-seed must exit 0 for a non-legacy image before loading base-schema.sql"
        )
    if SEED_LOAD.search(start):
        errors.append("start-server loads base-schema.sql before readiness")
    if "/healthz/ready" not in start or '!= "200"' not in start:
        errors.append("start-server must fail closed unless /healthz/ready returns 200")
    if POST_START_GUARD.search(apply) is None:
        errors.append(
            "apply-seed must exit 0 for the legacy image before loading base-schema.sql"
        )
    if "conformance_seed_order.py --canonicalize" not in workflow_text:
        errors.append("selected image must be canonicalized to a repository digest")
    for marker in (
        "legacy-seed-outcome: ${{ steps.legacy-seed.outcome }}",
        "legacy-seed:${LEGACY_SEED_OUTCOME}",
        '"legacySeed":',
        '("legacy seed", outcome("LEGACY_SEED_OUTCOME"))',
    ):
        if marker not in workflow_text:
            errors.append(f"legacy seed outcome is not reported: {marker}")

    start_at = next(index for index, step in enumerate(steps) if step["id"] == "start-server")
    for step in steps[: start_at + 1]:
        if not legacy_guarded_pre_start_seed(step["body"]):
            errors.append(
                f"{step['id'] or step['name']} loads base-schema.sql before the server is ready"
            )
    return errors


class ConformanceStartupGateTests(unittest.TestCase):
    def test_committed_workflow_boots_before_seed(self) -> None:
        errors = startup_gate_errors(WORKFLOW.read_text(encoding="utf-8"))
        self.assertEqual([], errors)

    def test_seed_before_start_is_rejected(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8")
        swapped = workflow.replace(
            "      - name: Start honua-server\n        id: start-server\n",
            "      - name: Start honua-server\n        id: start-server-moved\n",
            1,
        ).replace(
            "      - name: Apply seed to PostGIS\n        id: apply-seed\n",
            "      - name: Apply seed to PostGIS\n        id: start-server\n",
            1,
        ).replace(
            "        id: start-server-moved\n",
            "        id: apply-seed\n",
            1,
        )
        errors = startup_gate_errors(swapped)
        self.assertTrue(any("startup order" in error for error in errors), errors)
        self.assertTrue(
            any("before the server is ready" in error or "before the server starts" in error for error in errors),
            errors,
        )

    def test_unguarded_pre_start_seed_is_rejected(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8").replace(
            'if [[ "${order}" != "before" ]]; then\n'
            '            echo "modern image; seed waits until the server is ready"\n'
            "            exit 0\n"
            "          fi\n",
            "",
            1,
        )
        errors = startup_gate_errors(workflow)
        self.assertTrue(any("exit 0" in error for error in errors), errors)

    def test_missing_early_exit_is_rejected(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8").replace(
            'echo "modern image; seed waits until the server is ready"\n'
            "            exit 0\n",
            'echo "modern image; seed waits until the server is ready"\n',
            1,
        )
        errors = startup_gate_errors(workflow)
        self.assertTrue(any("exit 0" in error for error in errors), errors)

    def test_tag_for_the_legacy_digest_seeds_before_start(self) -> None:
        module = _seed_order_module()
        nightly = "ghcr.io/honua-io/honua-server@sha256:" + ("ab" * 32)
        other = "ghcr.io/honua-io/honua-server@sha256:" + ("c" * 64)

        def resolve_digest(image: str) -> str:
            return {
                "ghcr.io/honua-io/honua-server:rc-cert-e3ab87c-e3ab87c": LEGACY_IMAGE,
                LEGACY_IMAGE: LEGACY_IMAGE,
                "ghcr.io/honua-io/honua-server:nightly": nightly,
                other: other,
            }[image]

        self.assertTrue(
            module.seed_before_start(
                "ghcr.io/honua-io/honua-server:rc-cert-e3ab87c-e3ab87c",
                LEGACY_IMAGE,
                resolve_digest,
            )
        )
        self.assertTrue(module.seed_before_start(LEGACY_IMAGE, LEGACY_IMAGE, resolve_digest))
        self.assertFalse(
            module.seed_before_start(
                "ghcr.io/honua-io/honua-server:nightly",
                LEGACY_IMAGE,
                resolve_digest,
            )
        )
        self.assertFalse(module.seed_before_start(other, LEGACY_IMAGE, resolve_digest))

    def test_digest_reference_does_not_call_the_registry(self) -> None:
        module = _seed_order_module()
        mixed = "ghcr.io/honua-io/honua-server@sha256:" + LEGACY_DIGEST.upper()
        self.assertEqual(LEGACY_IMAGE, module.docker_resolve_digest(mixed))

    def test_missing_ready_check_is_rejected(self) -> None:
        workflow = WORKFLOW.read_text(encoding="utf-8").replace("/healthz/ready", "/healthz/live")
        errors = startup_gate_errors(workflow)
        self.assertTrue(any("fail closed" in error for error in errors), errors)


if __name__ == "__main__":
    unittest.main()
