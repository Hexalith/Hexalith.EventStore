"""Pure transport/filesystem controls; no public archive execution or listeners."""

import hashlib
import importlib.util
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("runtime_inputs", ROOT / "tools/published_runtime_archives.py")
runtime = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runtime)


class PublishedRuntimeArchiveTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory(prefix="runtime-input-control-")
        self.addCleanup(self.scratch.cleanup)
        self.root = Path(self.scratch.name)
        self.cache = self.root / "prepared"
        self.bodies = {}
        pins = []
        # Deliberately synthetic data accepted only by the in-process test seam.
        # The actual CLI has no manifest/hash/URL override.
        for name, *_ in runtime.PINS:
            body = ("resource-free control " + name).encode()
            digest = hashlib.sha256(body).hexdigest()
            checksum = (digest + " *" + name + "\n").encode()
            self.bodies[name] = body
            self.bodies[name + ".sha256"] = checksum
            pins.append((name, len(body), digest, len(checksum), hashlib.sha256(checksum).hexdigest()))
        self.pins = tuple(pins)
        self.requests = []

    def fetch(self, url, size):
        self.requests.append((url, size))
        self.assertTrue(url.startswith(runtime.RELEASE))
        body = self.bodies[url.removeprefix(runtime.RELEASE)]
        self.assertEqual(size, len(body))
        return body

    def prepare(self):
        runtime.prepare(str(self.cache), transport=self.fetch, pins=self.pins)

    def assert_semantic_refusal_with_rebound_record_pin(self, record):
        # Rebind only this synthetic record's whole-body pin, so refusal must
        # come from the semantic guard after the ordinary size/hash checks pass.
        name, size, digest, _, _ = self.pins[0]
        bodies = {**self.bodies, name + ".sha256": record}
        pins = ((name, size, digest, len(record), hashlib.sha256(record).hexdigest()), *self.pins[1:])
        with self.assertRaisesRegex(runtime.InputError, "Upstream archive checksum disagreement"):
            runtime.verify_bodies(bodies, pins)

    def test_actual_three_published_binary_records_match_pins_and_semantic_guard(self):
        # Exact small original public records, with no private paths or archives.
        records = {
            "daprd_linux_amd64.tar.gz": b"d679a47789cddacc8cd38d96d0da7a75514475146ca3da3e5b02bff1ad259dfb *daprd_linux_amd64.tar.gz\n",
            "placement_linux_amd64.tar.gz": b"81e515f5ef14ee8534696edc614593d3cf500f5b02d054c44339b297ff4277b5 *placement_linux_amd64.tar.gz\n",
            "scheduler_linux_amd64.tar.gz": b"7a206972e0066da1508def6f18fbe92324a89b9cb50cc5c0b0c44f5397da7eff *scheduler_linux_amd64.tar.gz\n",
        }
        self.assertEqual(set(records), {pin[0] for pin in runtime.PINS})
        for name, _, digest, size, record_digest in runtime.PINS:
            with self.subTest(name=name):
                record = records[name]
                self.assertEqual(len(record), size)
                self.assertEqual(hashlib.sha256(record).hexdigest(), record_digest)
                runtime.verify_checksum_record(name, digest, record)

    def test_semantic_guard_refuses_wrong_filename_even_with_rebound_record_pin(self):
        name, _, digest, _, _ = self.pins[0]
        for filename in ("foreign.tar.gz", "./" + name, "directory/" + name, name + " extra"):
            with self.subTest(filename=filename):
                self.assert_semantic_refusal_with_rebound_record_pin((digest + " *" + filename + "\n").encode())

    def test_semantic_guard_refuses_wrong_digest_even_with_rebound_record_pin(self):
        name, _, digest, _, _ = self.pins[0]
        wrong = ("0" if digest[0] != "0" else "1") + digest[1:]
        self.assert_semantic_refusal_with_rebound_record_pin((wrong + " *" + name + "\n").encode())

    def test_semantic_guard_refuses_text_or_unknown_marker_even_with_rebound_record_pin(self):
        name, _, digest, _, _ = self.pins[0]
        for marker in ("  ", " ?", " **", "*"):
            with self.subTest(marker=marker):
                self.assert_semantic_refusal_with_rebound_record_pin((digest + marker + name + "\n").encode())

    def refuse_cache(self):
        destination = self.root / "private"
        with patch.object(runtime, "fetch", side_effect=AssertionError("materialization must never download")):
            with self.assertRaises((runtime.InputError, OSError)):
                runtime.materialize(str(self.cache), str(destination), pins=self.pins)
        self.assertFalse(destination.exists())
        self.assertFalse(list(self.root.glob(".published-runtime-*")))

    def replace(self, name, body):
        self.cache.chmod(0o700)
        path = self.cache / name
        path.chmod(0o600)
        path.write_bytes(body)
        path.chmod(0o400)
        self.cache.chmod(0o500)

    def test_all_six_pinned_inputs_are_prepared_once_and_independently_copied(self):
        self.prepare()
        self.assertEqual(self.requests, [(runtime.RELEASE + n, s)
                                        for n, (s, _) in runtime.expected_files(self.pins).items()])
        self.assertEqual(set(p.name for p in self.cache.iterdir()), set(self.bodies) | {runtime.MARKER})
        first, second = self.root / "first", self.root / "second"
        for destination in (first, second):
            runtime.materialize(str(self.cache), str(destination), pins=self.pins)
            self.assertEqual(runtime.read_inputs(str(destination), self.pins, prepared=False), self.bodies)
            for name, body in self.bodies.items():
                self.assertEqual((destination / name).read_bytes(), body)
                self.assertEqual((destination / name).stat().st_nlink, 1)
                self.assertNotEqual((destination / name).stat().st_ino, (self.cache / name).stat().st_ino)
        for name in self.bodies:
            self.assertNotEqual((first / name).stat().st_ino, (second / name).stat().st_ino)
        (first / "private-state").write_text("invocation one only")
        self.assertFalse((second / "private-state").exists())
        self.assertFalse((self.cache / "private-state").exists())

    def test_transfer_failure_never_publishes_a_partial_cache(self):
        def fail(url, size):
            if len(self.requests) == 3:
                raise runtime.InputError("controlled transport failure")
            return self.fetch(url, size)
        with self.assertRaises(runtime.InputError):
            runtime.prepare(str(self.cache), transport=fail, pins=self.pins)
        self.assertEqual(len(self.requests), 3)
        self.assertFalse(self.cache.exists())
        self.assertEqual(list(self.root.iterdir()), [])

    def test_wrong_archive_checksum_record_size_or_upstream_binding_is_refused(self):
        archive = self.pins[0][0]
        for selected, mutation in ((archive, lambda b: bytes([b[0] ^ 1]) + b[1:]),
                                   (archive + ".sha256", lambda b: b[:-1]),
                                   (archive + ".sha256", lambda b: b"0" + b[1:])):
            with self.subTest(selected=selected, mutation=mutation):
                def altered(url, size):
                    body = self.fetch(url, size)
                    return mutation(body) if url == runtime.RELEASE + selected else body
                with self.assertRaises(runtime.InputError):
                    runtime.prepare(str(self.cache), transport=altered, pins=self.pins)
                self.assertFalse(self.cache.exists())
        bodies = dict(self.bodies)
        bodies[archive + ".sha256"] = b"0" * 64 + b" *" + archive.encode() + b"\n"
        pins = list(self.pins)
        name, size, digest, check_size, _ = pins[0]
        pins[0] = (name, size, digest, check_size, hashlib.sha256(bodies[archive + ".sha256"]).hexdigest())
        with self.assertRaisesRegex(runtime.InputError, "disagreement"):
            runtime.verify_bodies(bodies, pins)

    def test_existing_or_concurrently_created_foreign_destination_is_not_replaced(self):
        self.cache.mkdir()
        (self.cache / "foreign").write_text("preserve")
        with self.assertRaises(runtime.InputError):
            self.prepare()
        self.assertEqual(self.requests, [])
        (self.cache / "foreign").unlink()
        self.cache.rmdir()
        def collision(url, size):
            if not self.cache.exists():
                self.cache.mkdir()
                (self.cache / "foreign").write_text("preserve")
            return self.fetch(url, size)
        with self.assertRaises(OSError):
            runtime.prepare(str(self.cache), transport=collision, pins=self.pins)
        self.assertEqual((self.cache / "foreign").read_text(), "preserve")
        self.assertFalse(list(self.root.glob(".published-runtime-*")))

    def test_missing_cache_or_input_is_refused_without_fallback(self):
        self.refuse_cache()
        self.prepare()
        self.cache.chmod(0o700)
        (self.cache / self.pins[0][0]).unlink()
        self.refuse_cache()

    def test_corrupt_archive_and_checksum_are_each_refused(self):
        self.prepare()
        for name in (self.pins[0][0], self.pins[0][0] + ".sha256"):
            original = self.bodies[name]
            self.replace(name, bytes([original[0] ^ 1]) + original[1:])
            self.refuse_cache()
            self.replace(name, original)

    def test_foreign_receipt_or_extra_input_is_refused(self):
        self.prepare()
        marker = runtime.receipt(self.pins)
        self.replace(runtime.MARKER, b"x" * len(marker))
        self.refuse_cache()
        self.replace(runtime.MARKER, marker)
        self.cache.chmod(0o700)
        (self.cache / "unexpected").write_text("foreign")
        self.refuse_cache()

    def test_symbolic_link_root_ancestor_and_input_are_refused(self):
        self.prepare()
        alias = self.root / "alias"
        alias.symlink_to(self.root, target_is_directory=True)
        with self.assertRaises(OSError):
            runtime.materialize(str(alias / "prepared"), str(self.root / "private"), pins=self.pins)
        actual = self.root / "actual"
        self.cache.rename(actual)
        self.cache.symlink_to(actual, target_is_directory=True)
        self.refuse_cache()
        self.cache.unlink()
        actual.rename(self.cache)
        self.cache.chmod(0o700)
        name = self.pins[0][0]
        (self.cache / name).rename(self.root / name)
        (self.cache / name).symlink_to(self.root / name)
        self.refuse_cache()

    def test_hard_link_fifo_writable_and_foreign_owner_inputs_are_refused(self):
        self.prepare()
        name = self.pins[0][0]
        source = self.cache / name
        os.link(source, self.root / "hard-link")
        self.refuse_cache()
        (self.root / "hard-link").unlink()
        source.chmod(0o420)
        self.refuse_cache()
        source.chmod(0o400)
        actual_stat = runtime.os.fstat
        def foreign_owner(fd):
            info = actual_stat(fd)
            fields = list(info)
            fields[4] = os.getuid() + 1
            return os.stat_result(fields)
        with patch.object(runtime.os, "fstat", side_effect=foreign_owner):
            self.refuse_cache()
        self.cache.chmod(0o700)
        source.unlink()
        os.mkfifo(source, 0o400)
        self.refuse_cache()

    def test_transport_retains_exact_90_second_bound_and_has_no_retry(self):
        with patch.object(runtime.subprocess, "run") as run:
            run.return_value = subprocess.CompletedProcess([], 0, b"bounded", b"")
            self.assertEqual(runtime.fetch(runtime.RELEASE + "control", 7), b"bounded")
            args, kwargs = run.call_args
            self.assertEqual(kwargs["timeout"], 90)
            self.assertEqual(args[0][args[0].index("--max-time") + 1], "90")
            self.assertNotIn("--retry", args[0])
            run.return_value = subprocess.CompletedProcess([], 23, b"", b"controlled transport diagnostic")
            with self.assertRaises(runtime.InputError):
                runtime.fetch(runtime.RELEASE + "control", 7)


if __name__ == "__main__":
    unittest.main()
