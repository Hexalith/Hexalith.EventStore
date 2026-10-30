"""Exercise real /proc mappings and failure retention without Dapr or synthetic DLL mappings."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

SCRIPT = Path(__file__).resolve().parents[1] / "bounded_worker_observer.py"
SPEC = importlib.util.spec_from_file_location("bounded_worker_observer", SCRIPT)
OBSERVER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(OBSERVER)


@unittest.skipUnless(sys.platform == "linux", "Linux /proc is the observation authority.")
class BoundedWorkerObserverTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.directory = Path(self.temporary.name)
        self.pe = Path(os.readlink("/proc/self/exe"))
        self.pdb = self.directory / "native-symbol-fixture.pdb"
        self.pdb.write_bytes(b"test symbol bytes")
        self.rows = OBSERVER.mapping_rows(Path("/proc/self/maps").read_text())
        self.expected = {"peSha256": hashlib.sha256(self.pe.read_bytes()).hexdigest(),
                         "pdbSha256": hashlib.sha256(self.pdb.read_bytes()).hexdigest(),
                         "loadedMvid": "fixture-native-image"}

    def tearDown(self):
        self.temporary.cleanup()

    def test_real_existing_native_mapping_is_authenticated_without_creating_a_mapping(self):
        result = OBSERVER.authenticate_image(self.pe, self.pdb, self.rows, self.expected)
        self.assertTrue(result["mapping_rows"])
        self.assertEqual(os.stat(self.pe).st_ino, result["inode"])
        self.assertEqual(self.expected["peSha256"], result["dll_sha256"])

    def test_identical_bytes_at_different_inode_do_not_establish_actual_mapping(self):
        copy = self.directory / self.pe.name
        shutil.copyfile(self.pe, copy)
        with self.assertRaisesRegex(ValueError, "no actual device/inode mapping"):
            OBSERVER.authenticate_image(copy, self.pdb, self.rows, self.expected)

    def test_pe_hash_mismatch_rejects_real_mapping(self):
        with self.assertRaisesRegex(ValueError, "PE hash mismatch"):
            OBSERVER.authenticate_image(self.pe, self.pdb, self.rows, {**self.expected, "peSha256": "0" * 64})

    def test_pdb_hash_mismatch_rejects_real_mapping(self):
        with self.assertRaisesRegex(ValueError, "PDB hash mismatch"):
            OBSERVER.authenticate_image(self.pe, self.pdb, self.rows, {**self.expected, "pdbSha256": "0" * 64})

    def test_final_component_symlink_is_rejected(self):
        link = self.directory / "linked-image"
        link.symlink_to(self.pe)
        with self.assertRaises(OSError):
            OBSERVER.authenticate_image(link, self.pdb, self.rows, self.expected)

    def test_fifo_module_paths_reject_without_a_writer_and_retain_failure_receipts(self):
        wrapper = self.directory / "fifo-observer.py"
        wrapper.write_text(
            "import functools,importlib.util,sys\n"
            f"s=importlib.util.spec_from_file_location('observer',{str(SCRIPT)!r})\n"
            "m=importlib.util.module_from_spec(s);s.loader.exec_module(m)\n"
            f"m.observe=functools.partial(m.observe,required={{{self.pe.stem!r}}})\n"
            "raise SystemExit(m.main())\n")
        for key, fifo in (("pe", self.directory / self.pe.name),
                          ("pdb", self.directory / "fifo-symbols.pdb")):
            with self.subTest(path=key):
                os.mkfifo(fifo)
                module = {"name": self.pe.stem, "pe": str(self.pe), "pdb": str(self.pdb), **self.expected}
                module[key] = str(fifo)
                request = {"pid": os.getpid(), "start_ticks": OBSERVER.start_ticks(os.getpid()),
                           "modules": [module]}
                output = self.directory / ("fifo-" + key + "-receipts")
                with subprocess.Popen([sys.executable, str(wrapper), "--output", str(output)],
                                      stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                      stderr=subprocess.PIPE, text=True) as child:
                    try:
                        stdout, stderr = child.communicate(json.dumps(request), timeout=2)
                    except subprocess.TimeoutExpired:
                        self.fail("Observer waited for a FIFO writer: " + str(fifo))
                    finally:
                        if child.poll() is None:
                            child.kill()
                        child.wait(timeout=2)
                        self.assertIsNotNone(child.poll())
                    self.assertEqual(1, child.returncode, stdout + stderr)
                    receipts = list(output.glob("*.json"))
                    self.assertEqual(1, len(receipts))
                    receipt = json.loads(receipts[0].read_text())
                    self.assertFalse(receipt["passed"])
                    self.assertEqual(["Not a regular file: " + str(fifo)], receipt["errors"])
                    self.assertEqual(child.pid, receipt["observer_pid"])
                    self.assertEqual(str(receipts[0]), json.loads(stdout)["receipt"])
                    self.assertIn(str(self.pe), receipt["raw_maps"])
                    self.assertEqual(request["start_ticks"], receipt["start_ticks_after"])

    def test_stale_worker_start_identity_fails_closed(self):
        request = {"pid": os.getpid(), "start_ticks": "stale", "modules": []}
        with patch.object(OBSERVER.os, "getppid", return_value=os.getpid()):
            result = OBSERVER.observe(request, os.getpid())
        self.assertFalse(result["passed"])
        self.assertIn("PID/start identity", result["errors"][0])

    def test_observer_retains_missing_module_failure_and_raw_maps(self):
        modules = []
        for name in sorted(OBSERVER.REQUIRED):
            pe = self.directory / (name + ".dll")
            pe.write_bytes(b"unmapped fixture bytes")
            modules.append({"name": name, "pe": str(pe), "pdb": str(self.pdb),
                            "peSha256": hashlib.sha256(pe.read_bytes()).hexdigest(),
                            "pdbSha256": self.expected["pdbSha256"], "loadedMvid": "fixture"})
        request = {"pid": os.getpid(), "start_ticks": OBSERVER.start_ticks(os.getpid()), "modules": modules}
        output = self.directory / "receipts"
        result = subprocess.run([sys.executable, str(SCRIPT), "--output", str(output)],
                                input=json.dumps(request), text=True, capture_output=True, check=False)
        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        receipt = json.loads(next(output.glob("*.json")).read_text())
        self.assertFalse(receipt["passed"])
        self.assertEqual(4, len(receipt["errors"]))
        self.assertTrue(all("no actual device/inode mapping" in error for error in receipt["errors"]))
        self.assertIn(str(self.pe), receipt["raw_maps"])
        self.assertEqual(request["start_ticks"], receipt["start_ticks_after"])
        self.assertTrue(receipt["exe_sha256"])
        self.assertEqual(os.getpid(), receipt["pid"])

    def test_invalid_expected_hashes_retain_rejection_receipts_for_real_mapped_image(self):
        # Change only the test inventory to the already mapped native executable;
        # the observer still reads the real living parent and writes its own receipt.
        wrapper = self.directory / "native-observer.py"
        wrapper.write_text(
            "import functools,importlib.util,sys\n"
            f"s=importlib.util.spec_from_file_location('observer',{str(SCRIPT)!r})\n"
            "m=importlib.util.module_from_spec(s);s.loader.exec_module(m)\n"
            f"m.observe=functools.partial(m.observe,required={{{self.pe.stem!r}}})\n"
            "raise SystemExit(m.main())\n")
        module = {"name": self.pe.stem, "pe": str(self.pe), "pdb": str(self.pdb), **self.expected}
        for key in ("peSha256", "pdbSha256"):
            for index, invalid in enumerate((None, 7, "a" * 63, "a" * 65, "z" * 64)):
                with self.subTest(key=key, value=invalid):
                    request = {"pid": os.getpid(), "start_ticks": OBSERVER.start_ticks(os.getpid()),
                               "modules": [{**module, key: invalid}]}
                    output = self.directory / (key + "-" + str(index))
                    result = subprocess.run([sys.executable, str(wrapper), "--output", str(output)],
                                            input=json.dumps(request), text=True, capture_output=True, check=False)
                    self.assertEqual(1, result.returncode, result.stdout + result.stderr)
                    receipt = json.loads(next(output.glob("*.json")).read_text())
                    self.assertFalse(receipt["passed"])
                    self.assertEqual(["Expected " + key + " must be a string of exactly 64 hexadecimal characters."], receipt["errors"])
                    self.assertIn(str(self.pe), receipt["raw_maps"])
                    self.assertEqual(request["start_ticks"], receipt["start_ticks_after"])
                    self.assertEqual(invalid, receipt["request"]["modules"][0][key])

    def test_deleted_extra_sdk_mapping_fails_closed_and_retains_raw_mapping_row(self):
        raw = "1000-2000 r--p 00000000 00:01 42 /retired/Hexalith.Unexpected.dll (deleted)"
        deleted = OBSERVER.mapping_rows(raw)[0]
        module = {"name": self.pe.stem, "pe": str(self.pe), "pdb": str(self.pdb), **self.expected}
        request = {"pid": os.getpid(), "start_ticks": OBSERVER.start_ticks(os.getpid()), "modules": [module]}
        with patch.object(OBSERVER.os, "getppid", return_value=os.getpid()), \
                patch.object(OBSERVER, "mapping_rows", return_value=self.rows + [deleted]):
            receipt = OBSERVER.observe(request, os.getpid(), required={self.pe.stem})
        self.assertFalse(receipt["passed"])
        self.assertEqual([deleted], receipt["deleted_sdk_mapping_rows"])
        self.assertEqual(raw, receipt["deleted_sdk_mapping_rows"][0]["raw"])
        self.assertIn("Deleted first-party image mapping", receipt["errors"][0])
        self.assertEqual(1, len(receipt["images"]))
        self.assertEqual(self.expected["peSha256"], receipt["images"][0]["dll_sha256"])

    def test_malformed_request_retains_a_rejection_receipt(self):
        output = self.directory / "malformed"
        result = subprocess.run([sys.executable, str(SCRIPT), "--output", str(output)],
                                input="{", text=True, capture_output=True, check=False)
        self.assertEqual(1, result.returncode)
        self.assertFalse(json.loads(next(output.glob("*.json")).read_text())["passed"])


if __name__ == "__main__":
    unittest.main()
