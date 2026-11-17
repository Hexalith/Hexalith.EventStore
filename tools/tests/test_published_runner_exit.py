"""Run the actual published-profile CI step with a bounded, owned producer stub."""

import os
from pathlib import Path
import subprocess
import tempfile
import unittest


class PublishedRunnerExitTests(unittest.TestCase):
    def run_step(self, producer_exit):
        root = Path(__file__).resolve().parents[2]
        workflow = (root / ".github/workflows/integration.yml").read_text()
        block = workflow.split("- name: Run isolated published qualification and ownership regressions\n", 1)[1].split("      - name:", 1)[0]
        self.assertIn("        shell: bash\n", block)
        self.assertNotIn("continue-on-error:", block)
        source = block.split("        run: |\n", 1)[1]
        source = "\n".join(line[10:] for line in source.splitlines())
        with tempfile.TemporaryDirectory(prefix="published-runner-exit-") as scratch:
            directory = Path(scratch)
            tool = directory / "dotnet"
            tool.write_text("#!/bin/sh\nprintf 'retained producer output\\n'\nexit \"$FAKE_PRODUCER_EXIT\"\n")
            tool.chmod(0o700)
            environment = {**os.environ, "PATH": str(directory) + os.pathsep + os.environ["PATH"],
                           "P1R_QUALIFICATION_OUTPUT_ROOT": str(directory / "outcomes"),
                           "FAKE_PRODUCER_EXIT": str(producer_exit)}
            # This is GitHub's explicit `shell: bash` contract. The actual workflow
            # body still runs unchanged, including its real pipe into native tee.
            result = subprocess.run(["bash", "--noprofile", "--norc", "-e", "-o", "pipefail", "-c", source],
                                    cwd=root, env=environment, capture_output=True, text=True, timeout=5)
            self.assertEqual((directory / "outcomes/runner.log").read_text(), "retained producer output\n")
            self.assertIn("retained producer output", result.stdout)
            return result.returncode

    def test_successful_producer_and_tee_succeed(self):
        self.assertEqual(self.run_step(0), 0)

    def test_failing_producer_survives_successful_tee(self):
        self.assertEqual(self.run_step(37), 37)


if __name__ == "__main__":
    unittest.main()
