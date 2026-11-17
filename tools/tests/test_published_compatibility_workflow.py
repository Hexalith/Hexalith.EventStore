"""Run the actual published CI pipeline with bounded successful and failed producers."""

import os
from pathlib import Path
import subprocess
import tempfile
import unittest


class PublishedCompatibilityWorkflowTests(unittest.TestCase):
    def preparation_command(self, block):
        source = block.split("run: ", 1)[1].strip()
        if source.startswith("|\n"):
            source = "\n".join(line[10:] for line in source.splitlines()[1:] if line.strip())
        return source

    def test_successful_preparation_exports_the_same_private_cache_to_later_steps(self):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / ".github/workflows/integration.yml").read_text()
        block = workflow.split("- name: Prepare the original pinned private runtime archives", 1)[1]
        block = block.split("- name: Pull the original qualified Redis profile", 1)[0]
        with tempfile.TemporaryDirectory(prefix="published-cache-export-") as scratch:
            path = Path(scratch)
            tool = path / "python3"
            tool.write_text('#!/bin/sh\nprintf "%s\\n" "$@" > "$P1R_CONTROL_PREPARATION_ARGUMENTS"\n')
            tool.chmod(0o700)
            cache = path / "private cache inputs"
            exports, arguments = path / "step-exports", path / "preparation-arguments"
            # This is a command-file fixture, not a positive GitHub/CI identity.
            environment = dict(os.environ, PATH=str(path) + os.pathsep + os.environ["PATH"],
                               P1R_DAPR_RUNTIME_ARCHIVE_CACHE=str(cache), GITHUB_ENV=str(exports),
                               P1R_CONTROL_PREPARATION_ARGUMENTS=str(arguments))
            result = subprocess.run(["bash", "--noprofile", "--norc", "-e", "-o", "pipefail", "-c",
                                     self.preparation_command(block)], cwd=root, env=environment,
                                    text=True, capture_output=True, timeout=10)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(arguments.read_text().splitlines(),
                             ["tools/published_runtime_archives.py", "prepare", "--destination", str(cache)])
            self.assertEqual(exports.read_text(), "P1R_DAPR_RUNTIME_ARCHIVE_CACHE=" + str(cache) + "\n")

    def test_failed_actual_preparation_blocks_following_provisioning_and_test_commands(self):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / ".github/workflows/integration.yml").read_text()
        before, block = workflow.split("- name: Prepare the original pinned private runtime archives", 1)
        prefix, following = block.split("- name: Pull the original qualified Redis profile", 1)
        self.assertNotIn("continue-on-error", prefix)
        self.assertIn("timeout-minutes: 90", before)
        self.assertIn("Run isolated published qualification and ownership regressions", following)
        command = self.preparation_command(prefix)
        with tempfile.TemporaryDirectory(prefix="published-ci-prerequisite-") as scratch:
            path = Path(scratch)
            # Exercise the actual CLI and committed pins through a failed transport,
            # never a real request, runtime or positive fake CI environment.
            tool = path / "curl"
            tool.write_text("#!/bin/sh\nexit 23\n")
            tool.chmod(0o700)
            environment = dict(os.environ, PATH=str(path) + os.pathsep + os.environ["PATH"],
                               P1R_DAPR_RUNTIME_ARCHIVE_CACHE=str(path / "inputs"),
                               GITHUB_ENV=str(path / "step-exports"))
            result = subprocess.run(["bash", "--noprofile", "--norc", "-e", "-o", "pipefail", "-c",
                                     command + "\nprintf provisioned > \"$P1R_DAPR_RUNTIME_ARCHIVE_CACHE\""],
                                    cwd=root, env=environment, text=True, capture_output=True, timeout=10)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("curl exit 23", result.stderr)
            self.assertFalse((path / "inputs").exists())
            self.assertFalse((path / "step-exports").exists())
            self.assertFalse(list(path.glob(".published-runtime-*")))

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
        self.assertIn("pull_request:\n    branches: [main, release/parties-sdk-3.130.2]", triggers)
        self.assertIn("push:\n    branches: [main, release/parties-sdk-3.130.2]", triggers)
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
