"""Real GitHub expression/context regression, requiring an installed actionlint.

Run with ACTIONLINT=/path/to/actionlint and unittest discovery for this file.
There is no skip or string-only substitute when the parser is unavailable.
The hosted workflow's own pre-job validation remains the actual CI authority.
"""

import os
from pathlib import Path
import shutil
import subprocess
import unittest


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ".github/workflows/integration.yml"


class PublishedWorkflowContextTests(unittest.TestCase):
    def parse(self, body):
        parser = os.environ.get("ACTIONLINT") or shutil.which("actionlint")
        if not parser:
            self.fail("Install actionlint or supply ACTIONLINT to run the real context regression")
        return subprocess.run([parser, "-shellcheck=", "-pyflakes=", "-no-color", "-oneline",
                               "-stdin-filename", WORKFLOW, "-"], input=body, cwd=ROOT,
                              capture_output=True, timeout=30)

    def test_actual_workflow_accepts_the_runner_temporary_selector_at_step_scope(self):
        result = self.parse((ROOT / WORKFLOW).read_bytes())
        self.assertEqual(result.returncode, 0, (result.stdout + result.stderr).decode())

    def test_original_committed_job_env_selector_is_rejected_by_the_real_parser(self):
        # Reconstruct the rejected context without depending on temporary PR
        # history, so normal squash checkouts and source exports can run it.
        invalid = b"""name: Invalid private runtime cache context
on: push
jobs:
  published-compatibility:
    runs-on: ubuntu-latest
    env:
      P1R_DAPR_RUNTIME_ARCHIVE_CACHE: ${{ runner.temp }}/published-runtime-1.18.4-${{ github.run_id }}-${{ github.run_attempt }}
    steps:
      - run: ':'
"""
        result = self.parse(invalid)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b'context "runner" is not allowed here', result.stdout + result.stderr)
        self.assertIn(b"[expression]", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
