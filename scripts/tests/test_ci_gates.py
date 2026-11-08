from __future__ import annotations

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import textwrap
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


class ApiCompatibilityBaselineToolchainTests(unittest.TestCase):
    def setUp(self) -> None:
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.directory = Path(temporary.name)
        self.candidate = self.directory / "candidate"
        self.baseline = self.directory / "historical-source"
        for checkout, version in ((self.candidate, "10.0.403"), (self.baseline, "10.0.401")):
            (checkout / "src").mkdir(parents=True)
            (checkout / "global.json").write_text(json.dumps({"sdk": {
                "version": version, "workloadVersion": version, "rollForward": "disable"}}))
            (checkout / "src/Library.csproj").write_text(f"<Project><Version>{version}</Version></Project>")
            (checkout / "src/Library.cs").write_text(f'public class Library {{ public const string Version = "{version}"; }}')
            (checkout / "Directory.Packages.props").write_text(f'<Project><PackageVersion Include="Fixture" Version="{version}" /></Project>')
        for script in ("validate-api-compat.sh", "run-api-compat-check.sh"):
            destination = self.candidate / "scripts" / script
            destination.parent.mkdir(exist_ok=True)
            shutil.copy2(ROOT / "scripts" / script, destination)
        (self.candidate / "eng").mkdir()
        (self.candidate / "eng/shipped-packages.txt").write_text("src/Library.csproj|Honua.Sdk.Fixture\n")
        self.log = self.directory / "commands.jsonl"
        self.install_fake_commands()

    def install_fake_commands(self) -> None:
        commands = {
            "git": """
                import os, shutil, sys
                from pathlib import Path
                args = sys.argv[3:]
                if args[0] == 'rev-parse':
                    sys.exit(0)
                if args[:2] == ['worktree', 'add']:
                    shutil.copytree(os.environ['TEST_BASELINE'], args[3])
                elif args[:2] == ['worktree', 'remove']:
                    shutil.rmtree(args[3])
                else:
                    sys.exit('Unexpected git command: ' + repr(args))
            """,
            "dotnet": """
                import json, os, sys
                from pathlib import Path
                args = sys.argv[1:]
                call = {'args': args}
                if args[0] == 'pack':
                    checkout = Path(args[1]).parents[1]
                    call['sdk'] = json.loads((checkout / 'global.json').read_text())
                    call['source'] = {name: (checkout / name).read_text() for name in (
                        'src/Library.csproj', 'src/Library.cs', 'Directory.Packages.props')}
                with open(os.environ['TEST_COMMAND_LOG'], 'a') as output:
                    output.write(json.dumps(call) + '\\n')
                if args[0] == 'pack':
                    candidate = Path(os.environ['TEST_CANDIDATE'])
                    if call['sdk'] != json.loads((candidate / 'global.json').read_text()):
                        sys.exit('Historical workload is unavailable under candidate SDK')
                    output = Path(args[args.index('-o') + 1])
                    (output / 'Honua.Sdk.Fixture.1.0.0.nupkg').write_text('fixture')
                elif args[:3] == ['tool', 'run', 'apicompat']:
                    sys.exit(int(os.environ['TEST_COMPAT_EXIT']))
                elif args[:2] != ['tool', 'restore']:
                    sys.exit('Unexpected dotnet command: ' + repr(args))
            """,
        }
        self.bin_directory = self.directory / "bin"
        self.bin_directory.mkdir()
        for name, source in commands.items():
            executable = self.bin_directory / name
            executable.write_text("#!/usr/bin/env python3\n" + textwrap.dedent(source))
            executable.chmod(0o755)

    def run_validation(self, comparison_exit: int = 0) -> subprocess.CompletedProcess[str]:
        environment = os.environ.copy()
        for name in ("HONUA_API_COMPAT_ALLOW_BREAKING", "HONUA_API_COMPAT_BREAKING_APPROVAL"):
            environment.pop(name, None)
        environment.update({
            "PATH": str(self.bin_directory) + os.pathsep + environment["PATH"],
            "TEST_BASELINE": str(self.baseline), "TEST_CANDIDATE": str(self.candidate),
            "TEST_COMMAND_LOG": str(self.log), "TEST_COMPAT_EXIT": str(comparison_exit),
        })
        return subprocess.run(
            ["bash", str(self.candidate / "scripts/validate-api-compat.sh"), "historical"],
            env=environment, capture_output=True, text=True, check=False,
        )

    def test_baseline_uses_candidate_tuple_without_changing_api_or_dependency_source(self) -> None:
        result = self.run_validation()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        calls = [json.loads(line) for line in self.log.read_text().splitlines()]
        packs = [call for call in calls if call["args"][0] == "pack"]
        self.assertEqual(2, len(packs))
        candidate_pin = json.loads((self.candidate / "global.json").read_text())
        for call, checkout in zip(packs, (self.baseline, self.candidate)):
            self.assertEqual(candidate_pin, call["sdk"])
            for name, contents in call["source"].items():
                self.assertEqual((checkout / name).read_text(), contents)
            self.assertIn("/p:TreatWarningsAsErrors=true", call["args"])
            self.assertIn("/p:ContinuousIntegrationBuild=true", call["args"])
        self.assertIn("/p:NuGetAudit=false", packs[0]["args"])
        self.assertNotIn("/p:NuGetAudit=false", packs[1]["args"])
        comparison = calls[-1]["args"]
        self.assertEqual(["tool", "run", "apicompat", "--", "package"], comparison[:5])
        self.assertIn("--baseline-package", comparison)
        self.assertIn("--run-api-compat", comparison)
        self.assertIn("--enable-rule-cannot-change-parameter-name", comparison)
        self.assertNotEqual(comparison[5], comparison[comparison.index("--baseline-package") + 1])
        self.assertEqual("10.0.401", json.loads((self.baseline / "global.json").read_text())["sdk"]["version"])

    def test_comparison_failure_still_blocks_after_toolchain_alignment(self) -> None:
        result = self.run_validation(comparison_exit=17)
        self.assertEqual(17, result.returncode, result.stdout + result.stderr)
        self.assertIn("API compatibility failed", result.stdout)


if __name__ == "__main__":
    unittest.main()
