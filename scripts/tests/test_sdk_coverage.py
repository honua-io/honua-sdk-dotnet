from __future__ import annotations

import importlib.util
import subprocess
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT_PATH = ROOT / "scripts" / "generate-sdk-coverage.py"
OUTPUT_PATH = ROOT / "contracts" / "sdk-coverage.v1.json"
FIXTURE_PATH = ROOT / "contracts" / "fixtures" / "capability-keys.fixture.json"


def _load_module():
    spec = importlib.util.spec_from_file_location("generate_sdk_coverage", SCRIPT_PATH)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class FixtureTests(unittest.TestCase):
    def test_pinned_fixture_has_the_canonical_shape(self) -> None:
        module = _load_module()
        payload = module.json.loads(FIXTURE_PATH.read_text(encoding="utf-8"))
        self.assertIn("capabilities", payload)
        keys = module._extract_keys(payload)
        self.assertGreaterEqual(len(keys), 100)
        self.assertIn("serve.geoservices-featureserver", keys)
        self.assertIn("admin.control-plane", keys)


class ValidateEntriesTests(unittest.TestCase):
    def setUp(self) -> None:
        self.module = _load_module()
        self.canonical = frozenset({"a.one", "a.two", "a.three"})

    def test_valid_entries_pass(self) -> None:
        entries = [
            {"key": "a.one", "status": "covered", "entrypoints": ["X.Y"]},
            {"key": "a.two", "status": "partial", "entrypoints": ["X.Z"], "note": "stops here"},
        ]
        # Should not raise.
        self.module._validate_entries(entries, self.canonical)

    def test_unknown_key_fails_loudly(self) -> None:
        entries = [{"key": "not.a.real.key", "status": "covered", "entrypoints": ["X.Y"]}]
        with self.assertRaisesRegex(ValueError, "unknown capability key"):
            self.module._validate_entries(entries, self.canonical)

    def test_partial_without_note_fails(self) -> None:
        entries = [{"key": "a.one", "status": "partial", "entrypoints": ["X.Y"]}]
        with self.assertRaisesRegex(ValueError, "no non-empty 'note'"):
            self.module._validate_entries(entries, self.canonical)

    def test_partial_with_blank_note_fails(self) -> None:
        entries = [{"key": "a.one", "status": "partial", "entrypoints": ["X.Y"], "note": "   "}]
        with self.assertRaisesRegex(ValueError, "no non-empty 'note'"):
            self.module._validate_entries(entries, self.canonical)

    def test_covered_with_note_fails(self) -> None:
        entries = [{"key": "a.one", "status": "covered", "entrypoints": ["X.Y"], "note": "not allowed"}]
        with self.assertRaisesRegex(ValueError, "notes are reserved for 'partial'"):
            self.module._validate_entries(entries, self.canonical)

    def test_none_status_fails(self) -> None:
        entries = [{"key": "a.one", "status": "none", "entrypoints": ["X.Y"]}]
        with self.assertRaisesRegex(ValueError, "must be omitted entirely"):
            self.module._validate_entries(entries, self.canonical)

    def test_duplicate_key_fails(self) -> None:
        entries = [
            {"key": "a.one", "status": "covered", "entrypoints": ["X.Y"]},
            {"key": "a.one", "status": "covered", "entrypoints": ["X.Z"]},
        ]
        with self.assertRaisesRegex(ValueError, "duplicate coverage entry"):
            self.module._validate_entries(entries, self.canonical)

    def test_empty_entrypoints_fails(self) -> None:
        entries = [{"key": "a.one", "status": "covered", "entrypoints": []}]
        with self.assertRaisesRegex(ValueError, "non-empty list"):
            self.module._validate_entries(entries, self.canonical)


class RealInventoryTests(unittest.TestCase):
    """Exercises the actual curated COVERAGE_ENTRIES against the pinned fixture."""

    def test_real_entries_validate_against_pinned_fixture(self) -> None:
        module = _load_module()
        canonical_keys, source = module._load_canonical_keys()
        # Force fixture resolution regardless of any ambient KEY_LIST_URL so this
        # test is hermetic.
        document = module._build_document(canonical_keys, source)
        self.assertGreater(len(document["coverage"]), 0)

        for entry in document["coverage"]:
            self.assertIn(entry["status"], {"covered", "partial"})
            self.assertEqual(entry["sinceVersion"], "unreleased")
            self.assertTrue(entry["entrypoints"])
            if entry["status"] == "partial":
                self.assertTrue(entry.get("note", "").strip())
            else:
                self.assertNotIn("note", entry)

    def test_no_duplicate_keys_in_curated_inventory(self) -> None:
        module = _load_module()
        keys = [e["key"] for e in module.COVERAGE_ENTRIES]
        self.assertEqual(len(keys), len(set(keys)))


class EndToEndCheckModeTests(unittest.TestCase):
    def test_check_mode_passes_against_committed_snapshot(self) -> None:
        # This assumes contracts/sdk-coverage.v1.json is checked in and current;
        # CI's drift gate step is exactly this invocation.
        result = subprocess.run(
            [sys.executable, str(SCRIPT_PATH), "--check"],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertEqual(
            0,
            result.returncode,
            msg=f"stdout={result.stdout!r} stderr={result.stderr!r}",
        )

    def test_check_mode_fails_when_output_is_stale(self) -> None:
        original = OUTPUT_PATH.read_text(encoding="utf-8")
        try:
            OUTPUT_PATH.write_text(original + "\n", encoding="utf-8")
            result = subprocess.run(
                [sys.executable, str(SCRIPT_PATH), "--check"],
                cwd=ROOT,
                check=False,
                capture_output=True,
                text=True,
            )
            self.assertNotEqual(0, result.returncode)
            self.assertIn("stale", result.stderr)
        finally:
            OUTPUT_PATH.write_text(original, encoding="utf-8")


if __name__ == "__main__":
    unittest.main()
