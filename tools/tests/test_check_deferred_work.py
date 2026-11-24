"""Exercise the deferred-work checker's public CLI against both ledger formats."""

import json
from pathlib import Path
import subprocess
import sys
from tempfile import TemporaryDirectory
import unittest


CHECKER = Path(__file__).resolve().parents[2] / "scripts" / "check-deferred-work.py"


class DeferredWorkCheckerTests(unittest.TestCase):
    def check(self, markdown):
        with TemporaryDirectory() as directory:
            ledger = Path(directory) / "deferred-work.md"
            ledger.write_text(markdown, encoding="utf-8")
            result = subprocess.run(
                [sys.executable, "-B", str(CHECKER), "--json", str(ledger)],
                check=False,
                capture_output=True,
                text=True,
                timeout=10,
            )
        return result.returncode, json.loads(result.stdout)

    def test_numbered_status_counts_coexist_with_legacy_dispositions(self):
        code, result = self.check("""# Deferred Work
### DW-1: [RESOLVED] in historical title
status: open
```markdown
### DW-9: Example only
status: done
```
### DW-2: Archived
status: done 2026-11-24
archived: 2026-11-24
### DW-3: Retained
status: accepted (2026-11-24)
- source_spec: none
  summary: Legacy follow-up
## Separate legacy heading
- [RESOLVED] Legacy completed item
""")

        self.assertEqual(0, code)
        self.assertEqual(1, result["counts"]["OPEN"])
        self.assertEqual(2, result["counts"]["RESOLVED"])
        self.assertEqual(1, result["counts"]["ACCEPTED-DEBT"])
        self.assertEqual(1, result["counts"]["unclassified"])

    def test_duplicate_ids_and_absent_repeated_or_unknown_status_fail(self):
        code, result = self.check("""### DW-1: No status
- source_spec: none
status: open
### DW-1: Repeated status
status: open
status: done
### DW-2: Unknown status
status: pending
""")

        self.assertEqual(1, code)
        self.assertEqual(4, result["counts"]["unclassified"])
        self.assertCountEqual(
            [("dw6-invalid-numbered-status", line) for line in (1, 4, 7)]
            + [("dw6-duplicate-numbered-id", 4)],
            [(item["rule"], item["line"]) for item in result["diagnostics"] if item["heading"].startswith("DW-")
             and item["rule"] != "dw6-unclassified-legacy-advisory"],
        )


    def test_malformed_numbered_heading_cannot_hide_an_entry(self):
        code, result = self.check("### DW-01 Missing colon\nstatus: open\n")

        self.assertEqual(1, code)
        self.assertEqual(1, result["counts"]["unclassified"])
        self.assertEqual("dw6-invalid-numbered-heading", result["diagnostics"][0]["rule"])


if __name__ == "__main__":
    unittest.main()
