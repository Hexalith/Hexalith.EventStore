"""Focused failure-injection controls for the owned process qualifier; no shared infrastructure is used."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / "qualify-internal-actor-admission.py"
SPEC = importlib.util.spec_from_file_location("admission_runner", SCRIPT)
RUNNER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RUNNER)


class ActorAdmissionRunnerTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.root = Path(self.directory.name)
        self.run = RUNNER.Qualification(RUNNER.parse_args(["--evidence", str(self.root / "evidence")]))

    def tearDown(self):
        shutil.rmtree(self.run.runtime, ignore_errors=True)
        self.directory.cleanup()

    def test_default_configuration_remains_debug(self):
        self.assertEqual("Debug", self.run.args.configuration)
        self.assertEqual(self.run.root / "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Debug/net10.0",
                         self.run.test_output)

    def test_selected_configuration_applies_to_every_binary_source(self):
        for configuration in ("Debug", "Release"):
            with self.subTest(configuration=configuration):
                args = RUNNER.parse_args(["--evidence", str(self.root / configuration),
                                          "--configuration", configuration])
                run = RUNNER.Qualification(args)
                try:
                    for actual, project in (
                            (run.test_output, "tests/Hexalith.EventStore.Server.LiveSidecar.Tests"),
                            (run.sample_output, "samples/Hexalith.EventStore.Sample"),
                            (run.eventstore_output, "src/Hexalith.EventStore")):
                        self.assertEqual(run.root / project / "bin" / configuration / "net10.0", actual)
                finally:
                    shutil.rmtree(run.runtime)

    def test_frozen_runtime_keeps_selected_test_configuration(self):
        frozen = self.root / "frozen"
        args = RUNNER.parse_args(["--evidence", str(self.root / "frozen-evidence"),
                                  "--runtime", str(frozen), "--configuration", "Release"])
        run = RUNNER.Qualification(args)
        try:
            self.assertEqual(frozen / "sample", run.sample_output)
            self.assertEqual(frozen / "eventstore", run.eventstore_output)
            self.assertEqual(run.root / "tests/Hexalith.EventStore.Server.LiveSidecar.Tests/bin/Release/net10.0",
                             run.test_output)
        finally:
            shutil.rmtree(run.runtime)

    def test_unsupported_configuration_fails_before_creating_evidence(self):
        evidence = self.root / "unsupported"
        result = subprocess.run([sys.executable, str(SCRIPT), "--evidence", str(evidence),
                                 "--configuration", "Profile"], text=True, capture_output=True)
        self.assertEqual(2, result.returncode)
        self.assertIn("invalid choice", result.stderr)
        self.assertFalse(evidence.exists())

    def fake_docker(self, *, absent=None, failed_remove=None):
        calls = []
        ids = {name: identity for name, identity in self.run.containers}

        def invoke(*argv, **kwargs):
            calls.append(argv)
            if argv[1:3] == ("container", "inspect"):
                name = argv[3]
                if name == absent:
                    return subprocess.CompletedProcess(argv, 1, "", "No such object")
                value = [{"Id": ids[name], "Config": {"Labels": {"hexalith.qualification.run-id": self.run.run_id}}}]
                return subprocess.CompletedProcess(argv, 0, json.dumps(value), "")
            if argv[1] == "rm":
                return subprocess.CompletedProcess(argv, int(argv[-1] == failed_remove), "", "")
            if argv[1:3] == ("container", "ls"):
                name = argv[6].removeprefix("name=^/").removesuffix("$")
                return subprocess.CompletedProcess(argv, 0, ids[name] if ids[name] == failed_remove else "", "")
            raise AssertionError(argv)

        self.run.run = invoke
        return calls

    def test_verified_already_absent_container_does_not_skip_other_cleanup(self):
        first, second = self.run.run_id + "-first", self.run.run_id + "-second"
        self.run.containers = [(first, "a" * 64), (second, "b" * 64)]
        calls = self.fake_docker(absent=second)
        self.assertTrue(self.run.cleanup())
        self.assertIn(("docker", "rm", "-f", "a" * 64), calls)
        receipt = json.loads((self.run.out / "cleanup.json").read_text())
        self.assertEqual(2, len(receipt["containers"]))
        self.assertTrue(all(value["absent"] for value in receipt["containers"]))

    def test_failed_removal_does_not_skip_other_resources_or_pass_cleanup(self):
        first, second = self.run.run_id + "-first", self.run.run_id + "-second"
        self.run.containers = [(first, "a" * 64), (second, "b" * 64)]
        calls = self.fake_docker(failed_remove="b" * 64)
        self.assertFalse(self.run.cleanup())
        self.assertIn(("docker", "rm", "-f", "a" * 64), calls)
        self.assertIn(("docker", "rm", "-f", "b" * 64), calls)
        receipt = json.loads((self.run.out / "cleanup.json").read_text())
        self.assertFalse(receipt["passed"])
        self.assertTrue(receipt["runtime_absent"])

    def test_qualification_cannot_report_success_when_cleanup_fails(self):
        self.run.setup = lambda: None
        self.run.qualify = lambda: None
        self.run.cleanup = lambda: False
        with patch.object(RUNNER, "Qualification", return_value=self.run), \
                patch.object(sys, "argv", [str(SCRIPT), "--evidence", "unused"]), \
                patch.object(RUNNER.signal, "signal"), patch.object(RUNNER.threading, "Thread"):
            self.assertEqual(1, RUNNER.main())
        result = json.loads((self.run.out / "result.json").read_text())
        self.assertTrue(result["qualification_passed"])
        self.assertFalse(result["cleanup_passed"])
        self.assertFalse(result["passed"])

    def test_sigterm_and_owner_loss_cleanup_owned_child_processes(self):
        # Exercise real signals/processes without launching Redis/Dapr: the unchanged cleanup path
        # must reap an independently launched child on both cancellation and test-runner loss.
        harness = self.root / "harness.py"
        harness.write_text(
            "import importlib.util,os,sys,time\n"
            f"s=importlib.util.spec_from_file_location('runner',{str(SCRIPT)!r})\n"
            "m=importlib.util.module_from_spec(s);s.loader.exec_module(m)\n"
            "def setup(self):\n"
            " self.launch('owned-child',[sys.executable,'-c','import time;time.sleep(60)'],os.environ.copy(),self.runtime)\n"
            "m.Qualification.setup=setup\n"
            "m.Qualification.qualify=lambda self:time.sleep(60)\n"
            "raise SystemExit(m.main())\n")
        for mode in ["signal", "owner-loss"]:
            owner = subprocess.Popen([sys.executable, "-c", "import time;time.sleep(60)"])
            evidence = self.root / mode
            process = subprocess.Popen([sys.executable, str(harness), "--evidence", str(evidence), "--owner-pid", str(owner.pid)])
            try:
                deadline = time.monotonic() + 10
                while not (evidence / "processes.json").exists() and time.monotonic() < deadline:
                    time.sleep(0.05)
                child = json.loads((evidence / "processes.json").read_text())[0]["pid"]
                if mode == "signal":
                    process.terminate()
                else:
                    owner.terminate()
                    owner.wait(timeout=5)
                self.assertEqual(1, process.wait(timeout=20))
                result = json.loads((evidence / "result.json").read_text())
                self.assertFalse(result["qualification_passed"])
                self.assertTrue(result["cleanup_passed"])
                self.assertFalse(result["passed"])
                with self.assertRaises(ProcessLookupError):
                    os.kill(child, 0)
            finally:
                if process.poll() is None:
                    process.terminate()
                    process.wait(timeout=20)
                if owner.poll() is None:
                    owner.terminate()
                owner.wait(timeout=5)


if __name__ == "__main__":
    unittest.main()
