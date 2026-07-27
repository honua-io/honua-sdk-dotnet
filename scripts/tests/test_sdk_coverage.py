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


class SourceTruthGateTests(unittest.TestCase):
    """The entrypoints-must-exist gate (rename/delete drift, issue #273)."""

    def setUp(self) -> None:
        self.module = _load_module()

    def _index_of(self, *sources: str):
        """Builds a source type index over throwaway .cs files."""
        import tempfile

        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        root = Path(self._tmp.name)
        for i, source in enumerate(sources):
            (root / f"File{i}.cs").write_text(source, encoding="utf-8")
        return self.module._build_source_type_index(root)

    def test_source_type_index_finds_declared_types(self) -> None:
        index = self.module._build_source_type_index(ROOT / "src")
        self.assertIn("Honua.Sdk.GeoServices.FeatureServer.HonuaFeatureServerClient", index)

    def test_type_entrypoint_resolves(self) -> None:
        index = {"Ns.Client": [Path("fake.cs")]}
        entries = [{"key": "a.one", "entrypoints": ["Ns.Client"]}]
        # Should not raise.
        self.module._validate_entrypoints_against_source(entries, index)

    def test_member_entrypoint_resolves_when_member_in_declaring_file(self) -> None:
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / "Client.cs"
            source.write_text("namespace Ns;\npublic interface Client { void DoThingAsync(); }\n", encoding="utf-8")
            index = {"Ns.Client": [source]}
            entries = [{"key": "a.one", "entrypoints": ["Ns.Client.DoThingAsync"]}]
            self.module._validate_entrypoints_against_source(entries, index)

    def test_deleted_type_fails(self) -> None:
        entries = [{"key": "a.one", "entrypoints": ["Ns.RemovedClient"]}]
        with self.assertRaisesRegex(ValueError, "does not resolve to any type"):
            self.module._validate_entrypoints_against_source(entries, {})

    def test_renamed_member_fails(self) -> None:
        import tempfile

        with tempfile.TemporaryDirectory() as tmp:
            source = Path(tmp) / "Client.cs"
            source.write_text("namespace Ns;\npublic interface Client { void NewNameAsync(); }\n", encoding="utf-8")
            index = {"Ns.Client": [source]}
            entries = [{"key": "a.one", "entrypoints": ["Ns.Client.OldNameAsync"]}]
            with self.assertRaisesRegex(ValueError, "was removed or renamed"):
                self.module._validate_entrypoints_against_source(entries, index)

    def test_internal_type_is_not_indexed(self) -> None:
        index = self._index_of("namespace Ns;\ninternal sealed class Client { }\n")
        self.assertNotIn("Ns.Client", index)
        entries = [{"key": "a.one", "entrypoints": ["Ns.Client"]}]
        with self.assertRaisesRegex(ValueError, "does not resolve to any type"):
            self.module._validate_entrypoints_against_source(entries, index)

    def test_default_accessibility_type_is_not_indexed(self) -> None:
        index = self._index_of("namespace Ns;\nclass Client { }\n")
        self.assertNotIn("Ns.Client", index)

    def test_type_declaration_only_in_comment_is_not_indexed(self) -> None:
        source = (
            "namespace Ns;\n"
            "// public class Client used to live here\n"
            "/* public interface Client { } */\n"
            "/// <summary>public record Client</summary>\n"
            'public class Other { public string S => "public class Client"; }\n'
        )
        index = self._index_of(source)
        self.assertNotIn("Ns.Client", index)
        self.assertIn("Ns.Other", index)

    def test_public_partial_and_nested_types_are_indexed(self) -> None:
        index = self._index_of(
            "namespace Ns;\npublic partial class Client { public void FirstAsync() { } }\n",
            "namespace Ns;\npublic partial class Client { public void SecondAsync() { } }\n",
            "namespace Ns;\npublic static class Outer { public sealed class Inner { } }\n",
        )
        self.assertEqual(2, len(index["Ns.Client"]))
        self.assertIn("Ns.Inner", index)
        entries = [
            {"key": "a.one", "entrypoints": ["Ns.Client.FirstAsync", "Ns.Client.SecondAsync"]},
            {"key": "a.two", "entrypoints": ["Ns.Outer.Inner"]},
        ]
        # Should not raise: partial spread across files and nested public types resolve.
        self.module._validate_entrypoints_against_source(entries, index)

    def test_record_struct_and_readonly_record_struct_are_indexed(self) -> None:
        index = self._index_of(
            "namespace Ns;\npublic readonly record struct Envelope(double X, double Y);\n"
            "namespace Ns;\npublic record class Payload(string Id);\n"
        )
        self.assertIn("Ns.Envelope", index)
        self.assertIn("Ns.Payload", index)

    def test_member_only_in_xml_doc_or_comment_fails(self) -> None:
        source = (
            "namespace Ns;\n"
            "/// <summary>Replaces OldNameAsync entirely; see OldNameAsync(CancellationToken).</summary>\n"
            "public interface Client\n{\n"
            "    // OldNameAsync() was removed in 2.0\n"
            "    Task NewNameAsync();\n"
            "}\n"
        )
        index = self._index_of(source)
        entries = [{"key": "a.one", "entrypoints": ["Ns.Client.OldNameAsync"]}]
        with self.assertRaisesRegex(ValueError, "was removed or renamed"):
            self.module._validate_entrypoints_against_source(entries, index)

    def test_member_only_in_string_literal_fails(self) -> None:
        source = (
            "namespace Ns;\n"
            "public class Client\n{\n"
            '    public string Hint => "call OldNameAsync() instead";\n'
            "}\n"
        )
        index = self._index_of(source)
        entries = [{"key": "a.one", "entrypoints": ["Ns.Client.OldNameAsync"]}]
        with self.assertRaisesRegex(ValueError, "was removed or renamed"):
            self.module._validate_entrypoints_against_source(entries, index)

    def test_private_member_fails(self) -> None:
        source = (
            "namespace Ns;\n"
            "public class Client\n{\n"
            "    private Task OldNameAsync() => Task.CompletedTask;\n"
            "}\n"
        )
        index = self._index_of(source)
        entries = [{"key": "a.one", "entrypoints": ["Ns.Client.OldNameAsync"]}]
        with self.assertRaisesRegex(ValueError, "was removed or renamed"):
            self.module._validate_entrypoints_against_source(entries, index)

    def test_genuine_public_member_shapes_resolve(self) -> None:
        source = (
            "namespace Ns;\n"
            "public class Client\n{\n"
            "    public async Task<int> QueryAsync(string q) => await Task.FromResult(1);\n"
            "    public int Count => 42;\n"
            "    public string Name { get; set; }\n"
            "    public event EventHandler Changed;\n"
            "    public Client(string seed) { }\n"
            "    public TResult Map<TResult>(Func<int, TResult> f) => f(1);\n"
            "}\n"
            "public interface Reader\n{\n"
            "    Task<string> ReadAsync(CancellationToken ct);\n"
            "}\n"
        )
        index = self._index_of(source)
        entries = [
            {
                "key": "a.one",
                "entrypoints": [
                    "Ns.Client.QueryAsync",
                    "Ns.Client.Count",
                    "Ns.Client.Name",
                    "Ns.Client.Changed",
                    "Ns.Client.Map",
                    "Ns.Reader.ReadAsync",
                ],
            }
        ]
        # Should not raise: methods, expression-bodied members, auto-properties,
        # events, generic methods, and implicit-public interface members resolve.
        self.module._validate_entrypoints_against_source(entries, index)

    def test_every_curated_entrypoint_resolves_against_real_source(self) -> None:
        # The gate CI actually relies on: the shipped inventory must resolve
        # against the shipped src/ tree, so a rename/deletion of a mapped
        # public surface fails this suite (and generate --check) immediately.
        index = self.module._build_source_type_index(ROOT / "src")
        self.module._validate_entrypoints_against_source(self.module.COVERAGE_ENTRIES, index)


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
