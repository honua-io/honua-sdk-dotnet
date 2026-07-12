from __future__ import annotations

from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
TRIM_GATE = ROOT / "scripts" / "validate-browser-trim-warnings.py"


class BrowserTrimWarningGateTests(unittest.TestCase):
    def run_gate(self, log_contents: str) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory() as temp_dir:
            log_path = Path(temp_dir) / "browser-trim.log"
            log_path.write_text(log_contents, encoding="utf-8")
            return subprocess.run(
                [sys.executable, str(TRIM_GATE), str(log_path)],
                check=False,
                capture_output=True,
                text=True,
            )

    def test_reviewed_upstream_warning_is_accepted(self) -> None:
        result = self.run_gate(
            "ILLink : Trim analysis warning IL2026: "
            "NetTopologySuite.Features.JsonElementAttributesTable.TryDeserializeElement<T>"
            "(JsonElement, JsonSerializerOptions, T&): upstream reflection path"
        )

        self.assertEqual(0, result.returncode)
        self.assertIn("1 reviewed upstream warning(s)", result.stdout)

    def test_sdk_owned_warning_is_rejected(self) -> None:
        result = self.run_gate(
            "ILLink : Trim analysis warning IL2026: "
            "Honua.Sdk.Geometry.GeoJsonGeometryConverter.ReadGeometry(String): "
            "SDK reflection path"
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("Unexpected browser trim warnings", result.stderr)
        self.assertIn("Honua.Sdk.Geometry", result.stderr)

    def test_new_upstream_warning_site_is_rejected(self) -> None:
        result = self.run_gate(
            "ILLink : Trim analysis warning IL2026: "
            "NetTopologySuite.IO.Converters.NewReflectionConverter.Write(Object): "
            "unreviewed upstream reflection path"
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("Unexpected browser trim warnings", result.stderr)
        self.assertIn("NewReflectionConverter", result.stderr)

    def test_missing_log_is_rejected(self) -> None:
        result = subprocess.run(
            [sys.executable, str(TRIM_GATE), "/definitely/missing/browser-trim.log"],
            check=False,
            capture_output=True,
            text=True,
        )

        self.assertEqual(2, result.returncode)
        self.assertIn("trim publish log not found", result.stderr)


if __name__ == "__main__":
    unittest.main()
