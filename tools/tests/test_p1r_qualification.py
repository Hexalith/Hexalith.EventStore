"""Contract tamper checks and actual Linux process end-state controls."""
import contextlib
import copy
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import p1r_qualification as p


class PacketTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.scratch = tempfile.TemporaryDirectory(prefix="p1r-contract-tests-")
        cls.prepare = Path(cls.scratch.name) / "prepare"
        cls.run_packet = Path(cls.scratch.name) / "run"
        # Contract mutations compare against a controlled snapshot rather than
        # unrelated concurrent workspace edits. Actual Git/index binding has its
        # own disposable-checkout regression; all process operations remain real.
        cls.baseline_binding = p.source_binding()
        cls.source_observer = mock.patch.object(p, "source_binding", side_effect=lambda root=p.ROOT: copy.deepcopy(cls.baseline_binding))
        cls.source_observer.start()
        p.create_packet(cls.prepare, "prepare")
        p.create_packet(cls.run_packet, "run")

    @classmethod
    def tearDownClass(cls):
        cls.source_observer.stop()
        cls.scratch.cleanup()

    @contextlib.contextmanager
    def changed(self, directory, name, mutate, reseal=True):
        path = directory / name
        original = path.read_bytes()
        index = (directory / "SHA256SUMS").read_bytes()
        value = json.loads(original)
        mutate(value)
        p.write_json(path, value)
        if reseal:
            p.seal(directory)
        try:
            yield
        finally:
            path.write_bytes(original)
            (directory / "SHA256SUMS").write_bytes(index)

    def testPreparationCannotQualifyExistingSourceEvidence(self):
        result = p.validate_packet(self.prepare)
        self.assertTrue(result["valid"])
        self.assertFalse(result["qualified"])
        self.assertFalse(result["p1r_usable"])
        packet = p.read_json(self.prepare / "packet.json")
        self.assertEqual(len(packet["scenarios"]), 17)
        self.assertEqual(len(packet["families"]), 7)
        self.assertEqual(len(packet["additions"]), 2)
        self.assertTrue(all(r["execution"] == "unavailable" and r["assertions"] is None for r in packet["scenarios"]))

    def testRealControlsRetainCountersAndActualProcessEndState(self):
        result = p.validate_packet(self.run_packet)
        self.assertEqual(result["process_assertions"], 38)
        packet = p.read_json(self.run_packet / "packet.json")
        self.assertEqual(packet["sentinel"]["before"], packet["sentinel"]["after_controls"])
        self.assertTrue(packet["sentinel"]["released"])
        for row in packet["process_controls"]:
            with self.subTest(control=row["id"]):
                receipt = p.read_json(self.run_packet / row["receipt"])
                self.assertEqual(receipt["execution"], "passed")
                self.assertGreater(receipt["assertions"]["attempted"], 0)
                for owned in receipt["owned_processes"]:
                    current = p.process_state(owned["pid"])
                    self.assertTrue(current is None or current["start_ticks"] != owned["start_ticks"])
                self.assertTrue(all(c["remaining"] == [] for c in receipt["cleanup"]))
                if row["id"] in ("descendants", "repeated-cleanup"):
                    self.assertGreaterEqual(len(receipt["owned_processes"]), 2)

    def testExistingOutputRefusedWithoutChangingBytes(self):
        original = (self.prepare / "packet.json").read_bytes()
        with self.assertRaisesRegex(p.InvalidPacket, "already exists"):
            p.create_packet(self.prepare, "prepare")
        self.assertEqual((self.prepare / "packet.json").read_bytes(), original)

    def testMissingAndDuplicateRequiredCasesRefused(self):
        for mutation in (lambda v: v["scenarios"].pop(), lambda v: v["scenarios"].append(copy.deepcopy(v["scenarios"][0]))):
            with self.changed(self.prepare, "packet.json", mutation):
                with self.assertRaisesRegex(p.InvalidPacket, "required inventory"):
                    p.validate_packet(self.prepare)

    def testInventedQualificationRefusedEvenWithNewChecksums(self):
        for key in ("qualified", "p1r_usable"):
            with self.changed(self.prepare, "packet.json", lambda v: v.__setitem__(key, True)):
                with self.assertRaisesRegex(p.InvalidPacket, "invented qualification"):
                    p.validate_packet(self.prepare)

    def testRequiredFailedSkippedUnmeasuredIncompatibleLanesCannotPass(self):
        for field, value in (("execution", "failed"), ("execution", "skipped"), ("execution", "passed"),
                             ("compatibility", "incompatible"), ("assertions", {"attempted": 0, "passed": 0, "failed": 0})):
            with self.changed(self.prepare, "packet.json", lambda v: v["scenarios"][0].__setitem__(field, value)):
                with self.assertRaisesRegex(p.InvalidPacket, "required qualification lane"):
                    p.validate_packet(self.prepare)

    def testUnmeasuredZeroAndInventedCountsRefused(self):
        for counter in (None, {"attempted": 0, "passed": 0, "failed": 0}, {"attempted": 99, "passed": 99, "failed": 0}):
            with self.changed(self.prepare, "packet.json", lambda v: v["preparation"].__setitem__("assertions", counter)):
                with self.assertRaises(p.InvalidPacket):
                    p.validate_packet(self.prepare)

    def testOmittedExecutedAssertionsRefused(self):
        row = p.read_json(self.run_packet / "packet.json")["process_controls"][1]
        def omit(value):
            value["checks"].pop()
            value["assertions"] = p.checks_counter(value["checks"])
        with self.changed(self.run_packet, row["receipt"], omit):
            with self.assertRaisesRegex(p.InvalidPacket, "assertions omitted"):
                p.validate_packet(self.run_packet)

    def testTamperedFileHashAndSourceInputRefused(self):
        with self.changed(self.prepare, "packet.json", lambda v: v.__setitem__("qualified", True), reseal=False):
            with self.assertRaisesRegex(p.InvalidPacket, "hash mismatch"):
                p.validate_packet(self.prepare)

    def testResealedSourceHashesAndDuplicateInputsRefused(self):
        binding_path = self.prepare / "source-binding.json"
        packet_path = self.prepare / "packet.json"
        original_binding = binding_path.read_bytes()
        original_packet = packet_path.read_bytes()
        original_index = (self.prepare / "SHA256SUMS").read_bytes()
        for mutation in (lambda v: v["main"]["files"][0].__setitem__("sha256", "0" * 64),
                         lambda v: v["main"]["files"].append(copy.deepcopy(v["main"]["files"][0])),
                         lambda v: v["retained_inputs"].pop()):
            try:
                binding = json.loads(original_binding)
                mutation(binding)
                p.write_json(binding_path, binding)
                packet = json.loads(original_packet)
                packet["source_binding_sha256"] = p.digest(binding_path.read_bytes())
                p.write_json(packet_path, packet)
                p.seal(self.prepare)
                with self.assertRaisesRegex(p.InvalidPacket, "inputs changed"):
                    p.validate_packet(self.prepare)
            finally:
                binding_path.write_bytes(original_binding)
                packet_path.write_bytes(original_packet)
                (self.prepare / "SHA256SUMS").write_bytes(original_index)

    def testMissingRequiredEvidenceRefusesExecutionAndRetainsFailure(self):
        out = Path(self.scratch.name) / "missing-required-evidence"
        with mock.patch.object(p, "source_binding", side_effect=p.InvalidPacket("retained source evidence hash mismatch")), \
                mock.patch.object(p, "execute_control") as execute:
            packet = p.create_packet(out, "run")
        execute.assert_not_called()
        self.assertEqual(packet["preparation"]["execution"], "failed")
        self.assertEqual(packet["preparation"]["assertions"]["attempted"], 0)
        self.assertFalse(packet["qualified"])
        self.assertTrue((out / "packet.json").is_file())
        self.assertTrue((out / "SHA256SUMS").is_file())
        with self.assertRaises(p.InvalidPacket):
            p.validate_packet(out)
        with self.changed(self.prepare, "source-binding.json", lambda v: v["main"]["files"][0].__setitem__("sha256", "0" * 64)):
            with self.assertRaisesRegex(p.InvalidPacket, "binding hash mismatch"):
                p.validate_packet(self.prepare)

    def testDuplicateJsonAndChecksumInputsRefused(self):
        path = self.prepare / "packet.json"
        original = path.read_bytes()
        try:
            path.write_bytes(b'{"schema": "a", "schema": "b"}')
            p.seal(self.prepare)
            with self.assertRaisesRegex(p.InvalidPacket, "duplicate JSON"):
                p.validate_packet(self.prepare)
        finally:
            path.write_bytes(original)
            p.seal(self.prepare)
        index = self.prepare / "SHA256SUMS"
        original = index.read_bytes()
        try:
            index.write_bytes(original + original.splitlines()[0] + b"\n")
            with self.assertRaisesRegex(p.InvalidPacket, "duplicate packet input"):
                p.validate_packet(self.prepare)
        finally:
            index.write_bytes(original)

    def testMissingReceiptAndExtraInputRefused(self):
        row = p.read_json(self.run_packet / "packet.json")["process_controls"][0]
        path = self.run_packet / row["receipt"]
        original = path.read_bytes()
        try:
            path.unlink()
            with self.assertRaisesRegex(p.InvalidPacket, "omitted"):
                p.validate_packet(self.run_packet)
        finally:
            path.write_bytes(original)
        extra = self.prepare / "extra.txt"
        try:
            extra.write_text("extra")
            with self.assertRaisesRegex(p.InvalidPacket, "unexpected"):
                p.validate_packet(self.prepare)
        finally:
            extra.unlink()

    def testReadOnlyValidationDoesNotModifyPacket(self):
        before = {x.relative_to(self.run_packet): x.read_bytes() for x in self.run_packet.rglob("*") if x.is_file()}
        p.validate_packet(self.run_packet)
        after = {x.relative_to(self.run_packet): x.read_bytes() for x in self.run_packet.rglob("*") if x.is_file()}
        self.assertEqual(before, after)

    def testResealedOwnershipAndCleanupOmissionsRefused(self):
        rows = p.read_json(self.run_packet / "packet.json")["process_controls"]
        mutations = (
            ("success", lambda v: v.__setitem__("owned_processes", [])),
            ("success", lambda v: v.__setitem__("root_process", None)),
            ("timeout", lambda v: v["cleanup"][0].__setitem__("before", [])),
            ("cancellation", lambda v: v["cleanup"][0].__setitem__("before", [])),
            ("descendants", lambda v: v["cleanup"][0]["before"].pop()),
            ("timeout", lambda v: v["cleanup"][0]["before"].append(copy.deepcopy(v["cleanup"][0]["before"][0]))),
            ("timeout", lambda v: v["cleanup"][0]["before"][0].__setitem__("start_ticks", 1)),
            ("timeout", lambda v: v["cleanup"].__setitem__(0, None)),
            ("timeout", lambda v: v.__setitem__("owned_processes", [None])),
        )
        for control, mutate in mutations:
            receipt = next(row["receipt"] for row in rows if row["id"] == control)
            with self.subTest(control=control), self.changed(self.run_packet, receipt, mutate):
                with self.assertRaises(p.InvalidPacket):
                    p.validate_packet(self.run_packet)

    def testResealedTypedAndConsistentOutcomesRequired(self):
        rows = p.read_json(self.run_packet / "packet.json")["process_controls"]
        mutations = (
            ("timeout", "exit_code", "-15"), ("success", "exit_code", False),
            ("success", "launch_error", {"type": "FileNotFoundError", "errno": 2}),
            ("cancellation", "timed_out", True), ("timeout", "cancelled", True),
            ("startup-failure", "cancelled", True), ("startup-failure", "timed_out", True),
            ("startup-failure", "launch_error", {"type": "PermissionError", "errno": 13}),
            ("startup-failure", "launch_error", {"type": "FileNotFoundError", "errno": 2.0}),
        )
        for control, field, value in mutations:
            receipt = next(row["receipt"] for row in rows if row["id"] == control)
            with self.subTest(control=control, field=field), self.changed(self.run_packet, receipt, lambda v: v.__setitem__(field, value)):
                with self.assertRaises(p.InvalidPacket):
                    p.validate_packet(self.run_packet)

    def testResealedReceiptIntervalsMustBeInsideInvocationAndOrdered(self):
        rows = p.read_json(self.run_packet / "packet.json")["process_controls"]
        first = p.read_json(self.run_packet / rows[0]["receipt"])
        for control, mutation in (
            ("timeout", lambda v: v.update(started_utc="1800-01-01T00:00:00+00:00", finished_utc="1800-01-01T00:00:01+00:00")),
            ("success", lambda v: v.update(started_utc=first["started_utc"], finished_utc=first["finished_utc"])),
        ):
            receipt = next(row["receipt"] for row in rows if row["id"] == control)
            with self.changed(self.run_packet, receipt, mutation):
                with self.assertRaisesRegex(p.InvalidPacket, "interval"):
                    p.validate_packet(self.run_packet)

    def testResealedSentinelNullSuspensionAndReleaseClaimsRefused(self):
        for mutation in (
            lambda v: v["sentinel"].update(before=None, after_controls=None),
            lambda v: v["sentinel"]["after_controls"].__setitem__("running", False),
            lambda v: v["sentinel"]["after_controls"].__setitem__("start_ticks", 1),
            lambda v: v["sentinel"].__setitem__("after_release", copy.deepcopy(v["sentinel"]["before"])),
        ):
            with self.changed(self.run_packet, "packet.json", mutation):
                with self.assertRaises(p.InvalidPacket):
                    p.validate_packet(self.run_packet)
        sentinel = p.read_json(self.run_packet / "packet.json")["sentinel"]["identity"]
        real_state = p.process_state
        with mock.patch.object(p, "process_state", side_effect=lambda pid: {**sentinel, "state": "S", "ppid": os.getpid()} if pid == sentinel["pid"] else real_state(pid)):
            with self.assertRaisesRegex(p.InvalidPacket, "unreleased"):
                p.validate_packet(self.run_packet)

    def testNestedChecksumNamedInputCannotDisappear(self):
        nested = self.prepare / "nested" / "SHA256SUMS"
        original = (self.prepare / "SHA256SUMS").read_bytes()
        nested.parent.mkdir()
        try:
            nested.write_text("unexpected nested input\n")
            with self.assertRaisesRegex(p.InvalidPacket, "unexpected"):
                p.validate_packet(self.prepare)
            p.seal(self.prepare)
            self.assertIn("nested/SHA256SUMS", (self.prepare / "SHA256SUMS").read_text())
            with self.assertRaisesRegex(p.InvalidPacket, "extra receipt"):
                p.validate_packet(self.prepare)
        finally:
            nested.unlink()
            nested.parent.rmdir()
            (self.prepare / "SHA256SUMS").write_bytes(original)

    def testFailedInitialSentinelObservationIsSealedAndReleased(self):
        out = Path(self.scratch.name) / "sentinel-observation-failed"
        real_state = p.sentinel_state
        observed = []
        def unavailable_once(pid):
            observed.append(pid)
            return None if len(observed) == 1 else real_state(pid)
        with mock.patch.object(p, "sentinel_state", side_effect=unavailable_once):
            packet = p.create_packet(out, "run")
        self.assertTrue(packet["errors"])
        self.assertIsNone(packet["sentinel"]["before"])
        self.assertTrue(packet["sentinel"]["released"])
        self.assertIsNone(p.process_state(observed[0]))
        self.assertTrue((out / "SHA256SUMS").is_file())
        with self.assertRaisesRegex(p.InvalidPacket, "incomplete"):
            p.validate_packet(out)

    def testMalformedChecksAreSafeLibraryRefusals(self):
        for mutation in (lambda v: v["preparation"].__setitem__("checks", [None]),
                         lambda v: v.__setitem__("preparation", None),
                         lambda v: v["scenarios"].append(None)):
            with self.changed(self.prepare, "packet.json", mutation):
                with self.assertRaises(p.InvalidPacket):
                    p.validate_packet(self.prepare)

    def testMalformedJsonRootsAndChecksAreStructuredCliRefusals(self):
        path = self.prepare / "packet.json"
        original = path.read_bytes()
        index = (self.prepare / "SHA256SUMS").read_bytes()
        values = [[], {**json.loads(original), "preparation": {"checks": [None], "assertions": {"attempted": 1, "passed": 1, "failed": 0}, "execution": "passed"}}]
        try:
            for value in values:
                p.write_json(path, value)
                p.seal(self.prepare)
                result = subprocess.run([sys.executable, str(p.ROOT / "tools/p1r-qualification.py"), "validate", str(self.prepare)],
                                        capture_output=True, timeout=20)
                self.assertEqual(result.returncode, 2)
                self.assertNotIn(b"Traceback", result.stderr)
                refusal = json.loads(result.stderr)
                self.assertFalse(refusal["valid"])
                self.assertFalse(refusal["qualified"])
        finally:
            path.write_bytes(original)
            (self.prepare / "SHA256SUMS").write_bytes(index)


class SourceAndProcessTests(unittest.TestCase):
    def testPidfdErrorsAreSkippedOnlyForDisappearedOrReusedOwnedIdentity(self):
        owned={'pid':12345,'start_ticks':10,'pgid':12345,'session':12345}
        live={**owned,'ppid':1,'state':'S'}
        reused={**live,'start_ticks':20}
        for observed in (None,reused):
            with self.subTest(preliminary=observed),mock.patch.object(p,'process_state',return_value=observed), \
                    mock.patch.object(p.os,'pidfd_open') as opened:
                p.signal_owned(owned,signal.SIGTERM)
                opened.assert_not_called()
            with self.subTest(raced=observed),mock.patch.object(p,'process_state',side_effect=[live,observed]), \
                    mock.patch.object(p.os,'pidfd_open',side_effect=OSError(22,'bounded invalid pidfd')), \
                    mock.patch.object(p.signal,'pidfd_send_signal') as sent:
                p.signal_owned(owned,signal.SIGTERM)
                sent.assert_not_called()
        for error in (OSError(22,'matching live pidfd failure'),PermissionError(13,'denied'),OSError(5,'read failure')):
            with self.subTest(error=error.errno),mock.patch.object(p,'process_state',return_value=live), \
                    mock.patch.object(p.os,'pidfd_open',side_effect=error):
                with self.assertRaises(type(error)):
                    p.signal_owned(owned,signal.SIGTERM)
        with mock.patch.object(p,'process_state',side_effect=[live,reused]), \
                mock.patch.object(p.os,'pidfd_open',return_value=42),mock.patch.object(p.os,'close') as closed, \
                mock.patch.object(p.signal,'pidfd_send_signal') as sent:
            p.signal_owned(owned,signal.SIGTERM)
            sent.assert_not_called()
            closed.assert_called_once_with(42)

    def testDisappearingProcStatIsAbsentButOtherReadErrorsRemainFailures(self):
        for error in (FileNotFoundError(2,'gone'),ProcessLookupError(3,'gone')):
            with self.subTest(type=type(error).__name__),mock.patch.object(Path,'read_text',side_effect=error):
                self.assertIsNone(p.process_state(12345))
                with mock.patch.object(Path,'iterdir',return_value=[Path('/proc/12345')]):
                    self.assertEqual(p.process_inventory(),[])
        for error in (PermissionError(13,'denied'),OSError(5,'read failure')):
            with self.subTest(type=type(error).__name__),mock.patch.object(Path,'read_text',side_effect=error):
                with self.assertRaises(type(error)):
                    p.process_state(12345)

    def disposable_workspace(self, scratch, nested=False):
        workspace = Path(scratch) / "workspace"
        real_workspace = p.ROOT
        subprocess.run(["git", "clone", "--shared", "--no-checkout", "-q", str(real_workspace), str(workspace)], check=True)
        subprocess.run(["git", "read-tree", "HEAD"], cwd=workspace, check=True)
        owner = workspace / "references/Hexalith.Tenants"
        owner.parent.mkdir(exist_ok=True)
        subprocess.run(["git", "clone", "--shared", "--no-checkout", "-q", str(p.ROOT), str(owner)], check=True)
        subprocess.run(["git", "read-tree", "HEAD"], cwd=owner, check=True)
        # The superproject recognition comes from real absorbed gitdirs; no
        # monkeypatch supplies the trust-boundary observation being tested.
        modules = '[submodule "EventStore"]\n path = references/Hexalith.Tenants\n url = local-owner\n[submodule "Builds"]\n path = references/Hexalith.Builds\n url = local-builds\n'
        (workspace / ".gitmodules").write_text(modules)
        subprocess.run(["git", "submodule", "absorbgitdirs", "references/Hexalith.Tenants"], cwd=workspace, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        (owner / ".gitmodules").write_bytes(p.git(p.ROOT, "show", "HEAD:.gitmodules"))
        (owner / "README.md").write_bytes(p.git(p.ROOT, "show", "HEAD:README.md"))
        builds_source = p.ROOT / "references/Hexalith.Builds"
        if not (builds_source / ".git").exists():
            builds_source = p.ROOT.parent / "Hexalith.Builds"
        builds = (owner if nested else workspace) / "references/Hexalith.Builds"
        builds.parent.mkdir(exist_ok=True)
        subprocess.run(["git", "clone", "--shared", "--no-checkout", "-q", str(builds_source), str(builds)], check=True)
        subprocess.run(["git", "read-tree", "HEAD"], cwd=builds, check=True)
        (builds / "README.md").write_bytes(p.git(builds_source, "show", "HEAD:README.md"))
        if not nested:
            subprocess.run(["git", "submodule", "absorbgitdirs", "references/Hexalith.Builds"], cwd=workspace, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        return workspace, owner, builds

    def testWorkspaceRootBuildsIsObservedWithoutNestedInitialization(self):
        with tempfile.TemporaryDirectory() as scratch:
            workspace, owner, builds = self.disposable_workspace(scratch)
            with mock.patch.object(p, "relevant", side_effect=lambda name: name == "README.md"), mock.patch.object(p, "retained_inputs", return_value=[]):
                binding = p.source_binding(owner)
            observed = binding["workspace_builds"]
            self.assertEqual(Path(observed["workspace"]), workspace)
            self.assertEqual(Path(observed["path"]), builds)
            self.assertEqual(observed["binding"]["head"], p.git(builds, "rev-parse", "HEAD").decode().strip())
            self.assertFalse((owner / "references/Hexalith.Builds/.git").exists())

    def testNestedBuildsObservationKeepsOwningRepositoryRoute(self):
        with tempfile.TemporaryDirectory() as scratch:
            _, owner, builds = self.disposable_workspace(scratch, nested=True)
            with mock.patch.object(p, "relevant", side_effect=lambda name: name == "README.md"), mock.patch.object(p, "retained_inputs", return_value=[]):
                binding = p.source_binding(owner)
            self.assertNotIn("workspace_builds", binding)
            observed = next(item for item in binding["dependencies"] if item["path"] == "references/Hexalith.Builds")
            self.assertTrue(observed["initialized"])
            self.assertEqual(observed["binding"]["head"], p.git(builds, "rev-parse", "HEAD").decode().strip())

    def testWorkspaceBuildsSubstitutionAndUndeclaredFallbackAreRefused(self):
        with tempfile.TemporaryDirectory() as scratch:
            workspace, owner, builds = self.disposable_workspace(scratch)
            real_git, real_declarations = p.git, p.declared_dependencies
            def substituted(root, *arguments):
                if root == builds and arguments == ("rev-parse", "--show-toplevel"):
                    return b"/unrelated/Builds\n"
                return real_git(root, *arguments)
            def undeclared(root):
                rows = real_declarations(root)
                return rows if root == owner else [row for row in rows if row != "references/Hexalith.Builds"]
            with mock.patch.object(p, "relevant", side_effect=lambda name: name == "README.md"), mock.patch.object(p, "retained_inputs", return_value=[]):
                with mock.patch.object(p, "git", side_effect=substituted):
                    with self.assertRaisesRegex(p.InvalidPacket, "repository substituted"):
                        p.source_binding(owner)
                with mock.patch.object(p, "declared_dependencies", side_effect=undeclared):
                    with self.assertRaisesRegex(p.InvalidPacket, "not root declared"):
                        p.source_binding(owner)

    def testDirtyUntrackedAndStagedNewSourcesHaveIndependentBindings(self):
        with tempfile.TemporaryDirectory(prefix="p1r-source-test-") as scratch:
            root = Path(scratch)
            # Reuse existing objects in a disposable checkout; no test invents
            # a commit message or mutates the user's repository/history.
            subprocess.run(["git", "clone", "--shared", "--no-checkout", "-q", str(p.ROOT), scratch], check=True)
            subprocess.run(["git", "read-tree", "HEAD"], cwd=root, check=True)
            (root / "src").mkdir()
            tracked = root / ".editorconfig"
            tracked.write_bytes(p.git(root, "show", "HEAD:.editorconfig"))
            with mock.patch.object(p, "relevant", side_effect=lambda name: name in (".editorconfig", "src/New.cs")):
                baseline = p.repository_binding(root)
                tracked.write_text("dirty\n")
                (root / "src" / "New.cs").write_text("untracked\n")
                changed = p.repository_binding(root)
                self.assertEqual(baseline["head"], changed["head"])
                self.assertNotEqual(baseline["diff_sha256"], changed["diff_sha256"])
                self.assertEqual(changed["files"][0]["sha256"], p.digest(b"dirty\n"))
                self.assertIsNone(changed["files"][1]["git_blob"])
                self.assertIsNone(changed["files"][1]["index_blob"])
                subprocess.run(["git", "add", "--", "src/New.cs"], cwd=root, check=True)
                staged = p.repository_binding(root)
                self.assertEqual([item["path"] for item in staged["files"]], [".editorconfig", "src/New.cs"])
                self.assertIsNotNone(staged["files"][1]["index_blob"])
                self.assertIsNone(staged["files"][1]["git_blob"])
                self.assertNotEqual(changed["diff_sha256"], staged["diff_sha256"])
                (root / "src" / "New.cs").write_text("staged with subsequent working edits\n")
                unstaged = p.repository_binding(root)
                self.assertEqual(unstaged["files"][1]["index_blob"], staged["files"][1]["index_blob"])
                self.assertEqual(unstaged["files"][1]["sha256"], p.digest(b"staged with subsequent working edits\n"))
                self.assertNotEqual(unstaged["diff_sha256"], staged["diff_sha256"])
                tracked.unlink()
                with self.assertRaisesRegex(p.InvalidPacket, "missing"):
                    p.repository_binding(root)

    def testExternalSigintPreservesReceiptAndCleansActualDescendants(self):
        with tempfile.TemporaryDirectory(prefix="p1r-sigint-test-") as scratch:
            out = Path(scratch) / "packet"
            process = subprocess.Popen([sys.executable, str(p.ROOT / "tools/p1r-qualification.py"), "run", "--out", str(out)],
                                       stdout=subprocess.PIPE, stderr=subprocess.PIPE)
            try:
                deadline = time.monotonic() + 20
                interrupted = False
                while process.poll() is None and time.monotonic() < deadline:
                    for path in (out / "receipts").glob("*.json"):
                        value = p.read_json(path)
                        if value["control"] == "descendants" and value["finished_utc"] is None:
                            time.sleep(.1)
                            process.send_signal(signal.SIGINT)
                            interrupted = True
                            break
                    if interrupted:
                        break
                    time.sleep(.02)
                stdout, stderr = process.communicate(timeout=20)
                self.assertTrue(interrupted)
                self.assertEqual(process.returncode, 2, (stdout, stderr))
                packet = p.read_json(out / "packet.json")
                row = next(r for r in packet["process_controls"] if r["id"] == "descendants")
                receipt = p.read_json(out / row["receipt"])
                self.assertTrue(receipt["cancelled"])
                self.assertEqual(receipt["execution"], "failed")
                self.assertGreaterEqual(len(receipt["owned_processes"]), 2)
                self.assertTrue(all(p.process_state(owned["pid"]) is None for owned in receipt["owned_processes"]))
                self.assertTrue(all(c["remaining"] == [] for c in receipt["cleanup"]))
                self.assertEqual(packet["sentinel"]["before"], packet["sentinel"]["after_controls"])
                self.assertTrue(packet["sentinel"]["released"])
                with self.assertRaisesRegex(p.InvalidPacket, "failed or incomplete"):
                    p.validate_packet(out)
            finally:
                if process.poll() is None:
                    process.kill()
                    process.wait()

    def testCleanupAttemptsEveryIdentityAfterOneFailure(self):
        p.subreaper()
        owner = p.OwnedProcesses()
        python = str(Path(sys.executable).resolve())
        owner.attach(subprocess.Popen(p.control_command("descendants", python), stdout=subprocess.DEVNULL,
                                      env=owner.environment(), start_new_session=True))
        try:
            deadline = time.monotonic() + 2
            while len(owner.owned) < 2 and time.monotonic() < deadline:
                owner.discover()
                time.sleep(.01)
            self.assertGreaterEqual(len(owner.owned), 2)
            calls = []
            real_signal = p.signal_owned
            def fail_once(value, sig):
                calls.append(value["pid"])
                if len(calls) == 1:
                    raise PermissionError("test denied one attempt")
                return real_signal(value, sig)
            with mock.patch.object(p, "signal_owned", side_effect=fail_once):
                cleanup = owner.cleanup()
            self.assertTrue(cleanup["errors"])
            self.assertEqual(cleanup["remaining"], [])
            self.assertTrue(set(owner.owned).issubset(calls))
            self.assertEqual(owner.cleanup()["remaining"], [])
        finally:
            owner.cleanup()

    def testPrivateRegistrationFindsDetachedChildAfterEarlyParentExit(self):
        p.subreaper()
        owner = p.OwnedProcesses()
        script = ("import subprocess,sys; p=subprocess.Popen([sys.executable,'-I','-c','import time; time.sleep(30)'],"
                  "stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True); print(p.pid,flush=True)")
        process = subprocess.Popen([sys.executable, "-I", "-c", script], env=owner.environment(),
                                   stdout=subprocess.PIPE, start_new_session=True)
        owner.attach(process)
        try:
            output, _ = process.communicate(timeout=3)
            child = int(output)
            self.assertEqual(process.returncode, 0)
            self.assertIsNotNone(p.process_state(child))
            owner.discover()  # First discovery happens after the parent has gone.
            self.assertIn(child, owner.owned)
            self.assertNotEqual(owner.owned[child]["session"], owner.root["session"])
            self.assertEqual(owner.cleanup()["remaining"], [])
            self.assertIsNone(p.process_state(child))
            self.assertEqual(owner.cleanup()["remaining"], [])
        finally:
            owner.cleanup()
            if process.stdout is not None:
                process.stdout.close()

    def testStaleParentAndSessionIdsNeverAdoptUnrelatedProcess(self):
        owner = p.OwnedProcesses()
        stale = {"pid": 900000, "start_ticks": 10, "pgid": 900000, "session": 900000}
        owner.root = stale
        owner.owned[stale["pid"]] = stale
        reused = {**stale, "start_ticks": 20, "ppid": 1, "state": "S"}
        unrelated = {"pid": 900001, "start_ticks": 30, "pgid": 900000, "session": 900000, "ppid": 900000, "state": "S"}
        with mock.patch.object(p, "process_inventory", return_value=[reused, unrelated]), \
                mock.patch.object(Path, "read_bytes", return_value=b"UNRELATED=1\0"):
            owner.discover()
        self.assertEqual(owner.owned, {stale["pid"]: stale})

    def testRealSuspendedSentinelIsNotRunning(self):
        process = subprocess.Popen([sys.executable, "-I", "-c", "import time; time.sleep(30)"], start_new_session=True)
        try:
            process.send_signal(signal.SIGSTOP)
            deadline = time.monotonic() + 2
            while p.process_state(process.pid)["state"] not in ("T", "t") and time.monotonic() < deadline:
                time.sleep(.01)
            self.assertFalse(p.sentinel_state(process.pid)["running"])
            process.send_signal(signal.SIGCONT)
        finally:
            process.kill()
            process.wait(timeout=2)
        self.assertIsNone(p.process_state(process.pid))

    def testSecondSigintDuringTimerJoinCannotBypassCleanup(self):
        p.subreaper()
        with tempfile.TemporaryDirectory(prefix="p1r-second-sigint-") as scratch:
            directory = Path(scratch)
            (directory / "receipts").mkdir()
            sentinel_process = subprocess.Popen([sys.executable, "-I", "-c", "import time; time.sleep(30)"], start_new_session=True)
            sentinel = p.sentinel_state(sentinel_process.pid)
            real_join = p.threading.Timer.join
            signals = []
            def second_sigint(timer, *args, **kwargs):
                signals.append(signal.getsignal(signal.SIGINT))
                os.kill(os.getpid(), signal.SIGINT)
                return real_join(timer, *args, **kwargs)
            try:
                with mock.patch.object(p.threading.Timer, "join", second_sigint):
                    result = p.execute_control(directory, "0" * 32, "cancellation", sentinel, str(Path(sys.executable).resolve()))
                receipt = p.read_json(directory / result["receipt"])
                self.assertEqual(signals, [signal.SIG_IGN])
                self.assertEqual(receipt["execution"], "passed")
                self.assertTrue(receipt["cancelled"])
                self.assertEqual(len(receipt["cleanup"]), 2)
                self.assertTrue(all(c["remaining"] == [] for c in receipt["cleanup"]))
                self.assertTrue(all(p.process_state(value["pid"]) is None for value in receipt["owned_processes"]))
                self.assertEqual(p.sentinel_state(sentinel_process.pid), sentinel)
            finally:
                sentinel_process.terminate()
                sentinel_process.wait(timeout=2)


if __name__ == "__main__":
    unittest.main()
