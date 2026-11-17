"""Exercise the real temporary-job wrapper without running application builds."""

import json
import os
import shutil
import signal
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WRAPPER = ROOT / "scripts" / "run-with-temp.sh"


@unittest.skipUnless(os.name == "posix", "The wrapper requires a POSIX shell")
class RunWithTempTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="job-wrapper-test-")
        self.addCleanup(self.temporary.cleanup)
        self.parent = Path(self.temporary.name) / "scratch parent"
        self.parent.mkdir()
        self.env = dict(os.environ, TMPDIR=str(self.parent))

    def run_job(self, program, *arguments):
        return subprocess.run(
            ["bash", str(WRAPPER), sys.executable, "-c", program, *arguments],
            env=self.env, capture_output=True, text=True, timeout=15,
        )

    def test_success_isolates_children_preserves_arguments_and_removes_workspace(self):
        result = self.run_job(
            "import json, os, pathlib, sys, tempfile; "
            "scratch = pathlib.Path(os.environ['TMPDIR']); "
            "assert os.environ['TMP'] == os.environ['TEMP'] == str(scratch); "
            "assert pathlib.Path(tempfile.mkdtemp()).parent == scratch; "
            "print(json.dumps({'scratch': str(scratch), 'args': sys.argv[1:]}))",
            "an argument with spaces", "$(not-a-command)", "",
        )
        self.assertEqual(0, result.returncode, result.stderr)
        output = json.loads(result.stdout)
        self.assertEqual(
            ["an argument with spaces", "$(not-a-command)", ""], output["args"],
        )
        self.assertEqual(self.parent, Path(output["scratch"]).parent)
        self.assertFalse(Path(output["scratch"]).exists())
        self.assertEqual([], list(self.parent.iterdir()))

    def test_failure_preserves_status_and_removes_partial_outputs(self):
        result = self.run_job(
            "import os, pathlib, sys; "
            "(pathlib.Path(os.environ['TMPDIR']) / 'partial').write_text('partial'); "
            "sys.exit(23)"
        )
        self.assertEqual(23, result.returncode, result.stderr)
        self.assertEqual([], list(self.parent.iterdir()))

    def test_read_only_directories_are_removed_without_changing_job_status(self):
        for status in (0, 23):
            with self.subTest(status=status):
                result = self.run_job(
                    "import os, pathlib, sys; "
                    "root = pathlib.Path(os.environ['TMPDIR']) / 'readonly'; "
                    "root.mkdir(); (root / 'file').write_text('output'); "
                    f"root.chmod(0o500); sys.exit({status})"
                )
                self.assertEqual(status, result.returncode, result.stderr)
                self.assertEqual([], list(self.parent.iterdir()))

    @unittest.skipUnless(shutil.which("flock"), "Directory locking requires flock")
    def test_active_workspace_is_locked_against_systemd_cleanup(self):
        result = self.run_job(
            "import os, subprocess; "
            "result = subprocess.run(['flock', '--nonblock', '--exclusive', "
            "os.environ['TMPDIR'], 'true']); "
            "assert result.returncode == 1"
        )
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual([], list(self.parent.iterdir()))

    @unittest.skipUnless(
        shutil.which("flock") and shutil.which("systemd-tmpfiles"),
        "Integration check requires flock and systemd-tmpfiles",
    )
    def test_systemd_removes_abandoned_sibling_and_preserves_running_job(self):
        abandoned = self.parent / "abandoned"
        abandoned.mkdir()
        (abandoned / "discard").write_text("disposable")
        rules = Path(self.temporary.name) / "cleanup.conf"
        rules.write_text(f"e {json.dumps(str(self.parent))} - - - 0\n")
        process = subprocess.Popen(
            ["bash", str(WRAPPER), sys.executable, "-u", "-c",
             "import os, pathlib, sys; root = pathlib.Path(os.environ['TMPDIR']); "
             "(root / 'keep').write_text('active'); print(root, flush=True); "
             "sys.stdin.readline()"],
            env=self.env, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.PIPE, text=True, start_new_session=True,
        )
        try:
            active = Path(process.stdout.readline().strip())
            self.assertTrue((active / "keep").is_file())
            result = subprocess.run(
                ["systemd-tmpfiles", "--clean", str(rules)],
                capture_output=True, text=True, timeout=10,
            )
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertTrue((active / "keep").is_file())
            self.assertFalse(abandoned.exists())
            _, error = process.communicate("done\n", timeout=10)
            self.assertEqual(0, process.returncode, error)
            self.assertFalse(active.exists())
        finally:
            if process.poll() is None:
                os.killpg(process.pid, signal.SIGKILL)
                process.communicate(timeout=10)

    def test_process_group_cancellation_removes_workspace(self):
        process = subprocess.Popen(
            ["bash", str(WRAPPER), sys.executable, "-u", "-c",
             "import os, time; print(os.environ['TMPDIR'], flush=True); time.sleep(60)"],
            env=self.env, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
            text=True, start_new_session=True,
        )
        try:
            scratch = process.stdout.readline().strip()
            self.assertTrue(Path(scratch).is_dir())
            os.killpg(process.pid, signal.SIGTERM)
            _, error = process.communicate(timeout=10)
            self.assertEqual(143, process.returncode, error)
            self.assertFalse(Path(scratch).exists())
            self.assertEqual([], list(self.parent.iterdir()))
        finally:
            if process.poll() is None:
                os.killpg(process.pid, signal.SIGKILL)
                process.communicate(timeout=10)

    def test_wrapper_pid_cancellation_stops_child_and_removes_workspace(self):
        for event, status in ((signal.SIGTERM, 143), (signal.SIGINT, 130)):
            with self.subTest(signal=event):
                process = subprocess.Popen(
                    ["bash", str(WRAPPER), sys.executable, "-u", "-c",
                     "import json, os, time; "
                     "print(json.dumps({'scratch': os.environ['TMPDIR'], "
                     "'pid': os.getpid()}), flush=True); time.sleep(60)"],
                    env=self.env, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                    text=True, start_new_session=True,
                )
                child = None
                try:
                    child = json.loads(process.stdout.readline())
                    process.send_signal(event)
                    _, error = process.communicate(timeout=3)
                    self.assertEqual(status, process.returncode, error)
                    self.assertFalse(Path(child["scratch"]).exists())
                    self.assertEqual([], list(self.parent.iterdir()))
                    with self.assertRaises(ProcessLookupError):
                        os.kill(child["pid"], 0)
                finally:
                    if process.poll() is None:
                        for pid in (child["pid"] if child else None, process.pid):
                            if pid is not None:
                                try:
                                    os.kill(pid, signal.SIGKILL)
                                except ProcessLookupError:
                                    pass
                        process.communicate(timeout=10)

    def test_missing_command_reports_usage_without_creating_workspace(self):
        result = subprocess.run(
            ["bash", str(WRAPPER)], env=self.env,
            capture_output=True, text=True, timeout=10,
        )
        self.assertEqual(2, result.returncode)
        self.assertIn("Usage:", result.stderr)
        self.assertEqual([], list(self.parent.iterdir()))
