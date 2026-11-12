from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import json
import re
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
POLICY_PATH = ROOT / "scripts" / "ci" / "vocabulary.v1.json"
SHARED_ALLOWLIST_PATH = ROOT / "scripts" / "ci" / "retired-terms-allowlist.v1.json"
REMOVED_PREFIX = "*REMOVED*"
MEMBER_SUFFIXES = (".get", ".set", ".init")
DOC_GLOBS = ("README.md", "INSTALL.md", "CONTRIBUTING.md", "docs/**/*.md", "src/**/README.md", "examples/**/*.md")
SOURCE_GLOB = "src/**/*.cs"
XML_DOC = re.compile(r"^\s*///")
EXCEPTION_START = "Exception("


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
    if not line or line.startswith("#") or line.startswith(REMOVED_PREFIX):
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


def live_declarations(package: Path) -> dict[str, str]:
    declarations: dict[str, str] = {}
    removed: set[str] = set()
    for name in ("PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt"):
        path = package / name
        if not path.exists():
            continue
        location = path.relative_to(ROOT).as_posix()
        for line in (raw.strip() for raw in path.read_text(encoding="utf-8").splitlines()):
            if line.startswith(REMOVED_PREFIX):
                removed.add(line[len(REMOVED_PREFIX):])
            elif line:
                declarations.setdefault(line, location)
    return {line: location for line, location in declarations.items() if line not in removed}


def symbols_from(declarations: dict[str, str]) -> dict[str, str]:
    symbols: dict[str, str] = {}
    for line, location in sorted(declarations.items()):
        symbol = declared_symbol(line)
        if symbol is not None:
            symbols.setdefault(symbol, location)
    return symbols


def public_api_symbols() -> dict[str, str]:
    declarations: dict[str, str] = {}
    for package in sorted({path.parent for path in ROOT.glob("src/*/PublicAPI.*.txt")}):
        declarations.update(live_declarations(package))
    return symbols_from(declarations)


def public_api_findings(policy: dict, symbols: dict[str, str]) -> list[Finding]:
    tokens = {rule["token"]: rule["replacement"] for rule in policy["symbolTokens"]}
    allowed = {entry["symbol"] for entry in policy["allowedSymbols"]}
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
    lines = list(enumerate(path.read_text(encoding="utf-8").splitlines(), start=1))
    if path.suffix != ".cs":
        return lines
    return customer_code_lines(lines)


def customer_code_lines(lines: list[tuple[int, str]]) -> list[tuple[int, str]]:
    """XML doc lines, plus every line of an exception construction until its parentheses close."""
    selected: list[tuple[int, str]] = []
    depth = 0
    for number, line in lines:
        if depth == 0 and EXCEPTION_START in line:
            depth = line.count("(", line.index(EXCEPTION_START)) - line.count(")", line.index(EXCEPTION_START))
            selected.append((number, line))
        elif depth > 0:
            depth += line.count("(") - line.count(")")
            selected.append((number, line))
        elif XML_DOC.search(line):
            selected.append((number, line))
        depth = max(depth, 0)
    return selected


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


def source_mentions(member: str) -> list[str]:
    pattern = re.compile(rf"\b{re.escape(member)}\b")
    paths = {path for glob in (SOURCE_GLOB, "tests/**/*.cs", "examples/**/*.cs") for path in ROOT.glob(glob)}
    return sorted(
        f"{path.relative_to(ROOT).as_posix()}:{number}"
        for path in paths
        if "bin" not in path.parts and "obj" not in path.parts
        for number, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1)
        if pattern.search(line))


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

    def test_renamed_symbols_are_gone_and_their_replacements_ship(self) -> None:
        tokens = {rule["token"]: rule["replacement"] for rule in self.policy["symbolTokens"]}
        for old, replacement in self.policy["renamedSymbols"].items():
            with self.subTest(old=old):
                self.assertNotIn(old, self.symbols)
                self.assertEqual([], source_mentions(old.rsplit(".", 1)[-1]))
                self.assertIn(replacement, self.symbols)
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

    def test_shared_retired_terms_allowlist_names_existing_lines(self) -> None:
        entries = json.loads(SHARED_ALLOWLIST_PATH.read_text(encoding="utf-8"))["entries"]
        for entry in entries:
            with self.subTest(path=entry["path"], line=entry["line"]):
                self.assertTrue(entry["reason"].strip())
                path = ROOT / entry["path"]
                self.assertTrue(path.is_file())
                self.assertIn(entry["line"], path.read_text(encoding="utf-8").replace("\r\n", "\n").split("\n"))

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
        for text in ("a Hosted Layer", "the portal item", "a web map", "map packages", "the tenant",
                     "package-family capabilities", "a content-item", "map-package bodies"):
            with self.subTest(text=text):
                self.assertTrue(any(pattern.search(text) for pattern in patterns))
        self.assertFalse(any(pattern.search("web mapping and NuGet packages") for pattern in patterns))

    def test_declared_symbol_strips_modifiers_signatures_and_accessors(self) -> None:
        self.assertEqual("A.B.C", declared_symbol("override A.B.C.get -> string!"))
        self.assertEqual("A.B.M", declared_symbol("static A.B.M(int x) -> bool"))
        self.assertIsNone(declared_symbol("#nullable enable"))
        self.assertIsNone(declared_symbol("*REMOVED*A.B.C.get -> int"))

    def test_removal_hides_only_the_exact_declaration(self) -> None:
        with tempfile.TemporaryDirectory(dir=ROOT / "src") as directory:
            package = Path(directory)
            (package / "PublicAPI.Shipped.txt").write_text("A.B.M(int x) -> void\nA.B.P.get -> int\n", encoding="utf-8")
            (package / "PublicAPI.Unshipped.txt").write_text(
                "*REMOVED*A.B.M(int x) -> void\nA.B.M(long x) -> void\n*REMOVED*A.B.P.get -> int\n", encoding="utf-8")
            self.assertEqual({"A.B.M"}, set(symbols_from(live_declarations(package))))

    def test_multi_line_exception_messages_are_customer_text(self) -> None:
        lines = list(enumerate([
            "throw new ArgumentException(",
            "    \"The content item is missing.\",",
            "    nameof(itemId));",
            "var other = \"content item\";",
        ], start=1))
        self.assertEqual([1, 2, 3], [number for number, _ in customer_code_lines(lines)])


if __name__ == "__main__":
    unittest.main()
