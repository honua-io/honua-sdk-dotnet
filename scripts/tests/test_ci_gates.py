from __future__ import annotations

import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
COVERAGE_GATE = ROOT / "scripts" / "enforce-coverage.py"
API_COMPAT_GATE = ROOT / "scripts" / "run-api-compat-check.sh"


class CoverageGateTests(unittest.TestCase):
    def run_gate(
        self,
        line_rate: str,
        branch_rate: str,
        *,
        line_floor: str = "75",
        branch_floor: str = "60",
    ) -> subprocess.CompletedProcess[str]:
        with tempfile.TemporaryDirectory() as temp_dir:
            report = Path(temp_dir) / "Cobertura.xml"
            report.write_text(
                f'<coverage line-rate="{line_rate}" branch-rate="{branch_rate}" />',
                encoding="utf-8",
            )
            return subprocess.run(
                [
                    sys.executable,
                    str(COVERAGE_GATE),
                    "--report",
                    str(report),
                    "--line-floor",
                    line_floor,
                    "--branch-floor",
                    branch_floor,
                ],
                check=False,
                capture_output=True,
                text=True,
            )

    def test_line_floor_boundaries_are_not_rounded(self) -> None:
        self.assertNotEqual(0, self.run_gate("0.74999", "0.60").returncode)
        self.assertEqual(0, self.run_gate("0.75", "0.60").returncode)
        self.assertEqual(0, self.run_gate("0.75001", "0.60").returncode)

    def test_branch_floor_boundaries_are_not_rounded(self) -> None:
        self.assertNotEqual(0, self.run_gate("0.75", "0.59999").returncode)
        self.assertEqual(0, self.run_gate("0.75", "0.60").returncode)
        self.assertEqual(0, self.run_gate("0.75", "0.60001").returncode)

    def test_missing_report_fails_closed(self) -> None:
        result = subprocess.run(
            [
                sys.executable,
                str(COVERAGE_GATE),
                "--report",
                "/definitely/missing/Cobertura.xml",
                "--line-floor",
                "75",
                "--branch-floor",
                "60",
            ],
            check=False,
            capture_output=True,
            text=True,
        )
        self.assertNotEqual(0, result.returncode)
        self.assertIn("was not found", result.stdout)

    def test_unparsable_report_fails_closed(self) -> None:
        with tempfile.TemporaryDirectory() as temp_dir:
            report = Path(temp_dir) / "Cobertura.xml"
            report.write_text("not xml", encoding="utf-8")
            result = subprocess.run(
                [
                    sys.executable,
                    str(COVERAGE_GATE),
                    "--report",
                    str(report),
                    "--line-floor",
                    "75",
                    "--branch-floor",
                    "60",
                ],
                check=False,
                capture_output=True,
                text=True,
            )
        self.assertNotEqual(0, result.returncode)
        self.assertIn("::error::", result.stdout)

    def test_non_finite_report_rate_fails_closed(self) -> None:
        result = self.run_gate("NaN", "0.60")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("must be between 0 and 1", result.stdout)

    def test_non_finite_floor_fails_closed(self) -> None:
        result = self.run_gate("0.75", "0.60", line_floor="Infinity")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("must be between 0 and 100", result.stdout)

    def test_output_preserves_exact_values_and_floors(self) -> None:
        result = self.run_gate("0.75001", "0.60001")
        self.assertIn("Line coverage: 75.001% (floor 75%)", result.stdout)
        self.assertIn("Branch coverage: 60.001% (floor 60%)", result.stdout)


class ApiCompatibilityGateTests(unittest.TestCase):
    def run_gate(
        self,
        *,
        allow_breaking: bool = False,
        approval: str = "",
        github_event: str = "",
        github_workflow: str = "",
    ) -> subprocess.CompletedProcess[str]:
        environment = os.environ.copy()
        environment.pop("HONUA_API_COMPAT_ALLOW_BREAKING", None)
        environment.pop("HONUA_API_COMPAT_BREAKING_APPROVAL", None)
        environment.pop("GITHUB_ACTIONS", None)
        environment.pop("GITHUB_EVENT_NAME", None)
        environment.pop("GITHUB_WORKFLOW", None)
        if allow_breaking:
            environment["HONUA_API_COMPAT_ALLOW_BREAKING"] = "true"
        if approval:
            environment["HONUA_API_COMPAT_BREAKING_APPROVAL"] = approval
        if github_event:
            environment["GITHUB_ACTIONS"] = "true"
            environment["GITHUB_EVENT_NAME"] = github_event
        if github_workflow:
            environment["GITHUB_WORKFLOW"] = github_workflow

        return subprocess.run(
            [
                "bash",
                str(API_COMPAT_GATE),
                "Honua.Sdk.ControlledFixture",
                "--",
                "bash",
                "-c",
                "echo 'CP0001: CannotRemoveMember controlled fixture'; exit 17",
            ],
            check=False,
            capture_output=True,
            text=True,
            env=environment,
        )

    def test_breaking_change_fails_closed_by_default(self) -> None:
        result = self.run_gate()
        self.assertEqual(17, result.returncode)
        self.assertIn("CP0001: CannotRemoveMember", result.stdout)
        self.assertIn("API compatibility failed", result.stdout)

    def test_boolean_override_without_major_release_approval_is_rejected(self) -> None:
        result = self.run_gate(allow_breaking=True)
        self.assertEqual(2, result.returncode)
        self.assertIn("major-version-release", result.stdout)

    def test_explicit_major_release_override_is_honored(self) -> None:
        result = self.run_gate(
            allow_breaking=True, approval="major-version-release"
        )
        self.assertEqual(0, result.returncode)
        self.assertIn("explicitly approved", result.stdout)

    def test_approved_override_is_rejected_on_pull_request_workflow(self) -> None:
        result = self.run_gate(
            allow_breaking=True,
            approval="major-version-release",
            github_event="pull_request",
            github_workflow=".NET SDK CI",
        )
        self.assertEqual(2, result.returncode)
        self.assertIn("manually dispatched", result.stdout)

    def test_approved_override_is_honored_by_manual_major_release_workflow(self) -> None:
        result = self.run_gate(
            allow_breaking=True,
            approval="major-version-release",
            github_event="workflow_dispatch",
            github_workflow="API Compatibility Major Release Review",
        )
        self.assertEqual(0, result.returncode)
        self.assertIn("explicitly approved", result.stdout)


if __name__ == "__main__":
    unittest.main()
