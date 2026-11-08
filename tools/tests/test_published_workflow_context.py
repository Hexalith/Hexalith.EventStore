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
ORIGINAL = "debdaaa596661c26a765ce5c1ae702385f38b852"
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
        original = subprocess.run(["git", "--no-optional-locks", "cat-file", "blob", ORIGINAL + ":" + WORKFLOW],
                                  cwd=ROOT, capture_output=True, check=True, timeout=10).stdout
        result = self.parse(original)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(b'context "runner" is not allowed here', result.stdout + result.stderr)
        self.assertIn(b"[expression]", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
