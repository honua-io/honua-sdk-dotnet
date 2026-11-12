from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import json
import re
import unittest


ROOT = Path(__file__).resolve().parents[2]
POLICY_PATH = ROOT / "scripts" / "ci" / "vocabulary.v1.json"
MEMBER_SUFFIXES = (".get", ".set", ".init")
DOC_GLOBS = ("README.md", "INSTALL.md", "CONTRIBUTING.md", "docs/**/*.md", "src/**/README.md", "examples/**/*.md")
SOURCE_GLOB = "src/**/*.cs"
CUSTOMER_TEXT = re.compile(r"^\s*///|Exception\(")
OBSOLETE_LOOKBEHIND = 4


@dataclass(frozen=True)
class Finding:
    location: str
    name: str
    replacement: str

    def __str__(self) -> str:
        return f"{self.location}: {self.name} (use {self.replacement})"


def load_policy() -> dict:
    return json.loads(POLICY_PATH.read_text(encoding="utf-8"))


def declared_symbol(line: str) -> str | None:
    line = line.strip()
    if not line or line.startswith("#") or line.startswith("*REMOVED*"):
        return None
    words = line.lstrip("~").split("(", 1)[0].split(" -> ", 1)[0].split()
    symbol = next((word for word in words if "." in word), None)
    if symbol is None:
        return None
    for suffix in MEMBER_SUFFIXES:
        if symbol.endswith(suffix):
            return symbol[: -len(suffix)]
    return symbol


def retired_prefixes(symbol: str, tokens: dict[str, str]) -> list[tuple[str, str]]:
    segments = symbol.split(".")
    prefixes: list[tuple[str, str]] = []
    for index, segment in enumerate(segments):
        if index > 0 and segment == segments[index - 1]:
            continue
        replacement = next((tokens[token] for token in tokens if token in segment), None)
        if replacement is not None:
            prefixes.append((".".join(segments[: index + 1]), replacement))
    return prefixes


def public_api_symbols() -> dict[str, str]:
    symbols: dict[str, str] = {}
    for path in sorted(ROOT.glob("src/*/PublicAPI.*.txt")):
        for line in path.read_text(encoding="utf-8").splitlines():
            symbol = declared_symbol(line)
            if symbol is not None:
                symbols.setdefault(symbol, path.relative_to(ROOT).as_posix())
    return symbols


def public_api_findings(policy: dict, symbols: dict[str, str]) -> list[Finding]:
    tokens = {rule["token"]: rule["replacement"] for rule in policy["symbolTokens"]}
    allowed = set(policy["deprecatedAliases"]) | {entry["symbol"] for entry in policy["allowedSymbols"]}
    findings: set[Finding] = set()
    for symbol, location in symbols.items():
        for prefix, replacement in retired_prefixes(symbol, tokens):
            if prefix not in allowed:
                findings.add(Finding(location, prefix, replacement))
    return sorted(findings, key=str)


def text_paths() -> list[Path]:
    paths = {path for pattern in DOC_GLOBS + (SOURCE_GLOB,) for path in ROOT.glob(pattern)}
    return sorted(path for path in paths if "bin" not in path.parts and "obj" not in path.parts)


def customer_lines(path: Path) -> list[tuple[int, str]]:
    lines = enumerate(path.read_text(encoding="utf-8").splitlines(), start=1)
    if path.suffix != ".cs":
        return list(lines)
    return [(number, line) for number, line in lines if CUSTOMER_TEXT.search(line)]


def doc_findings(policy: dict) -> list[Finding]:
    rules = [(re.compile(rule["pattern"], re.IGNORECASE), rule["replacement"]) for rule in policy["docPhrases"]]
    allowed = {(entry["path"], entry["line"]) for entry in policy["allowedDocLines"]}
    findings: list[Finding] = []
    for path in text_paths():
        relative = path.relative_to(ROOT).as_posix()
        for number, line in customer_lines(path):
            if (relative, line.strip()) in allowed:
                continue
            for pattern, replacement in rules:
                match = pattern.search(line)
                if match:
                    findings.append(Finding(f"{relative}:{number}", match.group(0), replacement))
    return findings


def is_obsolete_in_source(symbol: str) -> bool:
    type_name, member = symbol.split(".")[-2:]
    declaration = re.compile(rf"\b{re.escape(member)}\b\s*[({{]")
    for path in ROOT.glob(SOURCE_GLOB):
        lines = path.read_text(encoding="utf-8").splitlines()
        if not any(re.search(rf"\b(class|record|interface)\s+{re.escape(type_name)}\b", line) for line in lines):
            continue
        for index, line in enumerate(lines):
            if declaration.search(line) and "=>" not in line.split(member, 1)[0]:
                window = lines[max(0, index - OBSOLETE_LOOKBEHIND):index]
                if any("[Obsolete(" in previous for previous in window):
                    return True
    return False


class VocabularyTests(unittest.TestCase):
    """Stops retired Honua vocabulary from returning to public SDK names and docs.

    The canonical glossary is honua-server docs/concepts/domain/README.md; the
    shared policy is honua-server scripts/ci/retired-terms.v1.json.
    """

    @classmethod
    def setUpClass(cls) -> None:
        cls.policy = load_policy()
        cls.symbols = public_api_symbols()

    def test_public_api_has_no_new_retired_names(self) -> None:
        findings = public_api_findings(self.policy, self.symbols)
        self.assertEqual([], [str(finding) for finding in findings])

    def test_docs_have_no_retired_phrases(self) -> None:
        findings = doc_findings(self.policy)
        self.assertEqual([], [str(finding) for finding in findings])

    def test_every_deprecated_alias_is_obsolete_and_ships_with_a_clean_replacement(self) -> None:
        tokens = {rule["token"]: rule["replacement"] for rule in self.policy["symbolTokens"]}
        for alias, replacement in self.policy["deprecatedAliases"].items():
            with self.subTest(alias=alias):
                self.assertIn(alias, self.symbols)
                self.assertIn(replacement, self.symbols)
                self.assertTrue(is_obsolete_in_source(alias), "alias must carry [Obsolete(...)]")
                self.assertFalse(retired_prefixes(replacement.rsplit(".", 1)[-1], tokens))

    def test_allowlists_name_existing_entries_with_reasons(self) -> None:
        for entry in self.policy["allowedSymbols"]:
            with self.subTest(symbol=entry["symbol"]):
                self.assertTrue(entry["reason"].strip())
                self.assertTrue(any(s == entry["symbol"] or s.startswith(entry["symbol"] + ".") for s in self.symbols))
        for entry in self.policy["allowedDocLines"]:
            with self.subTest(path=entry["path"], line=entry["line"]):
                self.assertTrue(entry["reason"].strip())
                lines = (ROOT / entry["path"]).read_text(encoding="utf-8").splitlines()
                self.assertIn(entry["line"], [line.strip() for line in lines])

    def test_a_new_retired_symbol_is_reported(self) -> None:
        symbols = {
            "Honua.Sdk.Admin.Models.HostedLayerInfo.Name": "src/Honua.Sdk.Admin/PublicAPI.Unshipped.txt",
            "Honua.Sdk.Studio.Packages.IHonuaStudioPackageClient.GetMapPackageAsync": "src/Honua.Sdk.Studio/PublicAPI.Unshipped.txt",
        }
        findings = public_api_findings(self.policy, symbols)
        self.assertEqual(
            ["Honua.Sdk.Admin.Models.HostedLayerInfo", "Honua.Sdk.Studio.Packages.IHonuaStudioPackageClient.GetMapPackageAsync"],
            sorted(finding.name for finding in findings))

    def test_constructors_of_allowlisted_types_are_not_reported(self) -> None:
        symbols = {"Honua.Sdk.Studio.Packages.StudioPackageDraft.StudioPackageDraft": "src/Honua.Sdk.Studio/PublicAPI.Shipped.txt"}
        self.assertEqual([], public_api_findings(self.policy, symbols))

    def test_retired_doc_phrases_are_recognised(self) -> None:
        patterns = [re.compile(rule["pattern"], re.IGNORECASE) for rule in self.policy["docPhrases"]]
        for text in ("a Hosted Layer", "the portal item", "a web map", "map packages", "the tenant"):
            with self.subTest(text=text):
                self.assertTrue(any(pattern.search(text) for pattern in patterns))
        self.assertFalse(any(pattern.search("web mapping and NuGet packages") for pattern in patterns))

    def test_declared_symbol_strips_modifiers_signatures_and_accessors(self) -> None:
        self.assertEqual("A.B.C", declared_symbol("override A.B.C.get -> string!"))
        self.assertEqual("A.B.M", declared_symbol("static A.B.M(int x) -> bool"))
        self.assertIsNone(declared_symbol("#nullable enable"))
        self.assertIsNone(declared_symbol("*REMOVED*A.B.C.get -> int"))


if __name__ == "__main__":
    unittest.main()
