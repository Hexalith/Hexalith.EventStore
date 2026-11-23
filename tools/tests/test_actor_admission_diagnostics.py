"""CI diagnostics retain check locations and cleanup counts without raw child data."""

import json
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from actor_admission_diagnostics import summarize


class ActorAdmissionDiagnosticsTests(unittest.TestCase):
    def test_projects_only_runner_locations_and_cleanup_outcomes(self):
        with TemporaryDirectory() as directory:
            evidence = Path(directory)
            (evidence / "result.json").write_text(json.dumps({
                "qualification_passed": False, "cleanup_passed": False, "argv": ["private-command"]}))
            (evidence / "cleanup.json").write_text(json.dumps({
                "containers": [{"absent": False, "error": "private-error"}], "processes": [],
                "logs": [{"closed": True}], "runtime_absent": True}))
            (evidence / "failure.txt").write_text(
                'Traceback (most recent call last):\n'
                '  File "/private-dir/qualify-internal-actor-admission.py", line 104, in check\n'
                '    raise AssertionError(observed)\nAssertionError: private-response\n')
            report = summarize(evidence)
            self.assertEqual({"qualification": [{"line": 104, "function": "check"}]}, report["frames"])
            self.assertFalse(report["qualification_passed"])
            self.assertFalse(report["cleanup_passed"])
            self.assertEqual({"processes": 0, "containers": 1, "logs": 0}, report["remaining"])
            self.assertNotIn("private", json.dumps(report))

    def test_early_startup_failure_uses_the_actor_tests_trx_message(self):
        with TemporaryDirectory() as directory:
            evidence = Path(directory)
            trx = evidence / "live-sidecar-results.trx"
            trx.write_text('<TestRun xmlns="urn:test"><Results><UnitTestResult testName="Suite.ActorCallbackAdmissionProcessTests.Test">'
                           '<Output><ErrorInfo><Message>Traceback (most recent call last):\n'
                           '  File "/private/qualify-internal-actor-admission.py", line 70, in __init__\n'
                           'OSError: private-error</Message></ErrorInfo></Output></UnitTestResult></Results></TestRun>')
            report = summarize(evidence, trx)
            self.assertEqual([{"line": 70, "function": "__init__"}], report["frames"]["startup"])
            self.assertIsNone(report["cleanup_passed"])
            self.assertNotIn("private", json.dumps(report))

    def test_incomplete_or_absent_results_are_unknown(self):
        with TemporaryDirectory() as directory:
            evidence = Path(directory)
            self.assertIsNone(summarize(evidence)["qualification_passed"])
            (evidence / "result.json").write_text("{")
            self.assertIsNone(summarize(evidence)["cleanup_passed"])
