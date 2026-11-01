"""Run the actual published CI pipeline with bounded successful and failed producers."""

import os
from pathlib import Path
import subprocess
import tempfile
import unittest


class PublishedCompatibilityWorkflowTests(unittest.TestCase):
    def test_exact_qualification_branch_can_run_normal_checks_without_merging_main(self):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / ".github/workflows/ci.yml").read_text()
        self.assertIn("workflow_dispatch:", workflow.split("concurrency:", 1)[0])
        self.assertIn("${{ github.ref }}", workflow.split("concurrency:", 1)[1].split("permissions:", 1)[0])

    def test_commitlint_preserves_full_automatic_ranges_without_a_last_commit_manual_gate(self):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / ".github/workflows/commitlint.yml").read_text()
        triggers = workflow.split("concurrency:", 1)[0]
        self.assertNotIn("workflow_dispatch:", triggers)
        self.assertIn("pull_request:\n    branches: [main]", triggers)
        self.assertIn("push:\n    branches: [main]", triggers)
        self.assertIn("types: [opened, synchronize, reopened, edited]", triggers)

    def run_pipeline(self, exit_code, remove_shell=False):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / ".github/workflows/integration.yml").read_text()
        block = workflow.split("- name: Run isolated published qualification and ownership regressions", 1)[1]
        block = block.split("- name: Upload measured outcomes and bounded command logs", 1)[0]
        prefix, _, source = block.partition("run: |\n")
        source = "\n".join(line[10:] for line in source.splitlines() if line.strip())
        explicit_bash = "shell: bash" in prefix and not remove_shell
        with tempfile.TemporaryDirectory(prefix="published-ci-exit-") as scratch:
            path = Path(scratch)
            tool = path / "dotnet"
            tool.write_text(f"#!/bin/sh\necho bounded-producer-diagnostic\nexit {exit_code}\n")
            tool.chmod(0o700)
            output = path / "output"
            environment = dict(os.environ, PATH=str(path) + os.pathsep + os.environ["PATH"],
                               P1R_QUALIFICATION_OUTPUT_ROOT=str(output))
            arguments = ["bash", "--noprofile", "--norc", "-e"]
            if explicit_bash:
                arguments += ["-o", "pipefail"]
            result = subprocess.run([*arguments, "-c", source], cwd=path, env=environment,
                                    text=True, capture_output=True, timeout=10)
            return result.returncode, (output / "runner.log").read_text(), result.stdout

    def test_actual_pipeline_propagates_failed_runner_and_preserves_diagnostics(self):
        code, log, output = self.run_pipeline(23)
        self.assertEqual(code, 23)
        self.assertIn("bounded-producer-diagnostic", log)
        self.assertIn("bounded-producer-diagnostic", output)

    def test_actual_pipeline_preserves_success_and_diagnostics(self):
        code, log, output = self.run_pipeline(0)
        self.assertEqual(code, 0)
        self.assertEqual(log, output)

    def test_removing_explicit_bash_reproduces_the_false_green_control(self):
        code, _, _ = self.run_pipeline(23, remove_shell=True)
        self.assertEqual(code, 0)


if __name__ == "__main__":
    unittest.main()
