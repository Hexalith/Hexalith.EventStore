"""Bounded external-proof controls: Python subprocesses only; no Docker or .NET."""
import copy
import datetime as dt
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import p1r_evidence_storage as storage
import p1r_published_executor as executor
from p1r_qualification import InvalidPacket, canonical, digest


class EvidenceStorageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.repository = self.root / "checkout"
        self.repository.mkdir()
        original_popen = subprocess.Popen
        def controlled_popen(argv, *args, **kwargs):
            if Path(str(argv[0])).name.lower() in ("docker", "dotnet"):
                raise AssertionError("synthetic storage controls forbid Docker and .NET")
            return original_popen(argv, *args, **kwargs)
        process_control = mock.patch.object(subprocess, "Popen", side_effect=controlled_popen)
        process_control.start()
        self.addCleanup(process_control.stop)
        self.context = storage.context("a" * 32, self.repository, "b" * 64, "c" * 64, "d" * 64)
        self.store = storage.Store(self.root / "external", self.context)

    def command(self, data=b"complete proof\n", identity=1):
        return self.store.command({"id": identity, "argv": ["python3", "control.py"], "cwd": str(self.root),
            "started_utc": "2026-10-21T00:00:00+00:00", "finished_utc": "2026-10-21T00:00:01+00:00", "exit_code": 0}, data)

    def testCompleteBytesAndReplacementDecodedHashesAreDistinct(self):
        data = b"first\xff\n" + b"x" * 8192 + b"\nactual witness after excerpt"
        row = self.command(data)
        self.assertEqual(storage.command_text(row, self.context), data.decode("utf-8", "replace"))
        ref = row["output_ref"]["artifact"]
        self.assertEqual(ref["sha256"], digest(data))
        self.assertNotEqual(ref["sha256"], ref["decoded_sha256"])
        self.assertNotIn("actual witness", row["output_excerpt"])
        self.assertIn("actual witness", storage.materialize([row], self.context)[0]["output"])

    def testMultibyteExcerptsNeverSplitACharacterOrExceed2048Bytes(self):
        for data in (("😀" * 2000).encode(), ("é" * 2000).encode(), b"a" * 2047 + "😀".encode(), b"\xff" * 4096):
            with self.subTest(prefix=data[:8]):
                row = self.command(data)
                self.assertLessEqual(len(row["output_excerpt"].encode()), 2048)
                self.assertEqual(row["output_excerpt"], storage.excerpt(data))
                self.assertEqual(storage.command_text(row, self.context), data.decode("utf-8", "replace"))

    def testInvalidArtifactsRefuseWithoutExcerptFallback(self):
        for defect in ("missing", "corrupt", "truncated", "wrong-size", "attempt", "source", "candidate", "command", "status", "time", "manifest", "symlink", "expired"):
            with self.subTest(defect=defect), tempfile.TemporaryDirectory() as directory:
                store = storage.Store(Path(directory) / "proof", self.context)
                row = store.command({"id": 1, "argv": ["python3"], "cwd": directory, "started_utc": "a", "finished_utc": "b", "exit_code": 0}, b"valid")
                ref = row["output_ref"]["artifact"]
                target = Path(ref["locator"])
                if defect == "missing": target.unlink()
                elif defect in ("corrupt", "truncated"):
                    target.chmod(0o600);target.write_bytes(b"other" if defect == "corrupt" else b"v")
                elif defect == "wrong-size": ref["bytes"] += 1
                elif defect in ("attempt", "source", "candidate"): ref["context"][defect] = "e" * (32 if defect == "attempt" else 64)
                elif defect == "command": row["argv"].append("substituted")
                elif defect == "status": row["exit_code"] = 2
                elif defect == "time": row["finished_utc"] = "later"
                elif defect == "manifest": Path(row["output_ref"]["manifest"]).unlink()
                elif defect == "symlink":
                    target.unlink();target.symlink_to(self.repository / "missing")
                elif defect == "expired": ref["retained_until"] = "2000-01-01T00:00:00+00:00"
                with self.assertRaises(InvalidPacket): storage.command_text(row, self.context)

    def testUnknownSchemaAndMixedFormsAreRejected(self):
        for change in ({"output_schema": "hexalith.p1r.command-output.v2"}, {"output": "inline"}, {"output_schema": None}, {"raw_output_base64": "YQ=="}):
            row = copy.deepcopy(self.command())
            row.update(change)
            with self.subTest(change=change), self.assertRaises(InvalidPacket): storage.command_text(row, self.context)
        with self.assertRaises(InvalidPacket): storage.command_text(self.command())
        with self.assertRaises(InvalidPacket): storage.materialize({"schema": "hexalith.p1r.json-reference.v9"}, self.context)

    def testSignatureReaderUsesCompleteVerifiedTextAndRejectsUnavailableProof(self):
        import p1r_published_qualification as qualification
        row = self.command(b" " * 4096 + qualification.REPOSITORY_SIGNATURE.encode())
        self.assertNotIn(qualification.REPOSITORY_SIGNATURE, row["output_excerpt"])
        self.assertTrue(qualification.signature_verified(self.root, row, self.context))
        Path(row["output_ref"]["artifact"]["locator"]).unlink()
        with self.assertRaises(InvalidPacket): qualification.signature_verified(self.root, row, self.context)

    def testLegacyHashRulesArePreserved(self):
        row = {"output": "legacy", "output_sha256": digest(b"legacy")}
        self.assertEqual(storage.command_text(row), "legacy")
        self.assertEqual(storage.materialize(row, self.context), row)
        row["output"] = "changed"
        with self.assertRaises(InvalidPacket): storage.command_text(row)

    def testExternalInventoriesPreserveStrictJsonAndRepresentationRules(self):
        for data, representation in ((b'{"field":1,"field":2}', "canonical-json"),
                (b'[NaN]', "canonical-json"), (b'[Infinity]', "canonical-json"),
                (b'[', "canonical-json"), (b'[]', "raw-bytes")):
            reference = {"schema": storage.JSON_SCHEMA,
                "reference": self.store.retain(data, {"kind": "inventory"}, representation)}
            with self.subTest(data=data, representation=representation), self.assertRaises(InvalidPacket):
                storage.materialize(reference, self.context)

    def testCheckoutAndSubstitutedDestinationsRefuseBeforeWriting(self):
        target = self.repository / "generated"
        with self.assertRaises(InvalidPacket): storage.Store(target, self.context)
        self.assertFalse(target.exists())
        linked = self.root / "linked"
        linked.symlink_to(self.repository, target_is_directory=True)
        with self.assertRaises(InvalidPacket): storage.Store(linked / "proof", self.context)
        self.assertFalse((self.repository / "proof").exists())
        moved = self.root / "retained"
        self.store.root.rename(moved)
        self.store.root.symlink_to(self.repository, target_is_directory=True)
        with self.assertRaises(InvalidPacket): self.command()
        self.assertFalse((self.repository / "objects").exists())

    def testDeduplicatesRepeatedCommandCleanupAndInventoryBytes(self):
        first, second = self.command(b"same", 1), self.command(b"same", 2)
        self.assertEqual(first["output_ref"]["artifact"]["locator"], second["output_ref"]["artifact"]["locator"])
        rows = [{"key": "a", "tenant": "tenant-a", "kind": "event", "sha256": "e" * 64, "sequence": 1, "floor": None}]
        frozen = storage.retain_inventories({"before": rows, "after": rows, "cleanup": [first, second]}, self.store)
        self.assertEqual(frozen["before"], frozen["after"])
        restored = storage.materialize(frozen, self.context)
        self.assertEqual(restored["before"], rows)
        self.assertEqual(restored["after"], rows)
        self.assertEqual(len(list((self.store.root / "objects").iterdir())), 2)

    def testDiagnosticExpiryHoldsAndLocalAcceptanceRefusal(self):
        ref = self.command()["output_ref"]
        future = storage.utc() + dt.timedelta(days=31)
        with self.assertRaises(InvalidPacket): storage.resolve(ref, self.context, ref["artifact"]["identity"], now=future)
        held = storage.Store(self.root / "held", self.context, hold=True).retain(b"required", {"kind": "hold"})
        self.assertEqual(storage.resolve(held, self.context, {"kind": "hold"}, now=future), b"required")
        with self.assertRaises(InvalidPacket): storage.Store(self.root / "acceptance", self.context, retention="acceptance")

    def testExternalMountRefusesCustomSocketAndAliasBeforeDocker(self):
        import socket
        import p1r_isolation as isolation
        inside = self.store.root / "controller.sock"
        with mock.patch.object(isolation, "ROOT", self.repository), mock.patch.object(subprocess, "run") as docker:
            with self.assertRaises(InvalidPacket):
                isolation.IsolatedRunner(self.store.root / "run", workspace=self.repository,
                    outer_host="unix://" + str(inside))
            docker.assert_not_called()
        endpoint = self.root / "outer.sock"
        listener = socket.socket(socket.AF_UNIX)
        self.addCleanup(listener.close)
        listener.bind(str(endpoint))
        os.link(endpoint, self.store.root / "alias.sock")
        with self.assertRaises(InvalidPacket):
            isolation.validate_external_root(self.store.root, self.repository, "unix://" + str(endpoint))

    def testSizeBudgetRefusesBeforePayloadRead(self):
        ref = self.command(b"123456789")["output_ref"]
        with mock.patch.object(storage, "bounded_file", side_effect=AssertionError("no I/O")):
            with self.assertRaises(InvalidPacket): storage.resolve(ref, self.context, ref["artifact"]["identity"], limit=1)

    def test5116CommandsAndLargeInventoryExportOneBoundedCompleteIndex(self):
        packet = self.root / "packet";packet.mkdir()
        data = ("😀" * 8192).encode()
        commands = [self.command(data, index + 1) for index in range(5116)]
        (packet / "commands.json").write_bytes(canonical(commands))
        inventory = [{"key": str(index), "tenant": "tenant-a", "kind": "event", "sha256": "f" * 64, "sequence": index + 1, "floor": None} for index in range(10000)]
        (packet / "inventory.json").write_bytes(canonical(self.store.inventory(inventory)))
        summary = storage.export_attempt(packet, self.repository / "summaries", self.store)
        committed = list((self.repository / "summaries").rglob("*"))
        self.assertEqual([path.name for path in committed], ["summary.json"])
        self.assertLessEqual(sum(path.stat().st_size for path in committed), storage.ATTEMPT_BYTES)
        self.assertTrue(all(len(row["output_excerpt"].encode()) <= 2048 for row in commands))
        index = json.loads(storage.resolve(summary["complete_index"], self.context, {"kind": "complete-index"}))
        self.assertEqual(len(index["files"]), 2)
        self.assertEqual(summary["commands"]["total"], 5116)
        self.assertEqual(len({row["output_ref"]["artifact"]["locator"] for row in commands}), 1)
        self.assertNotIn("files_index_parts", summary)

    def testExternalRestoreImportBindsFullParentIdentityBeforeParsing(self):
        import p1r_published_qualification as qualification
        from test_p1r_published_qualification import LaneReceiptTests, verified_context
        receipt, admitted = LaneReceiptTests.bound_restore(), verified_context()
        admitted["source_binding"] = {"repository": str(self.repository)}
        for value in (receipt, receipt["cleanup"]):
            value["fixture"] = {"id": "p1r-" + "a" * 32, "synthetic": False}
        enclosing = storage.receipt_context(receipt, admitted["source_binding"], admitted["inputs"]["value"])
        store = storage.Store(self.root / "restore", enclosing)
        external = storage.retain_inventories(receipt, store)
        self.assertEqual(qualification.receipt_outcome(external, admitted)[2]["execution"], "passed")
        for defect in ("attempt", "source", "candidate"):
            changed, parent = copy.deepcopy(external), copy.deepcopy(admitted)
            if defect == "attempt": changed["fixture"]["id"] = "p1r-" + "e" * 32
            elif defect == "source": parent["source_binding"]["repository"] = str(self.root / "other-checkout")
            else: parent["inputs"]["value"]["candidate"]["version"] = "0.0.2-p1r.synthetic"
            with self.subTest(defect=defect), mock.patch.object(storage.json, "loads", side_effect=AssertionError("parse before identity verification")):
                with self.assertRaises(InvalidPacket): qualification.receipt_outcome(changed, parent)

    def testGenuineExternalCaseReceiptUsesFullOutputBeyondExcerpt(self):
        import p1r_published_qualification as qualification
        from test_p1r_published_executor import ProposedQualifiedSnapshotWitnessTests
        witness = ProposedQualifiedSnapshotWitnessTests()
        witness.setUp()
        self.addCleanup(witness.doCleanups)
        receipt, admitted = witness.import_fixture(scope="operational")
        enclosing = storage.receipt_context(receipt, admitted["source_binding"], admitted["inputs"]["value"])
        store = storage.Store(self.root / "case-proof", enclosing)
        extended = False
        for case in receipt["execution_evidence"]["cases"]:
            for command in case["commands"]:
                if isinstance(command.get("output"), str):
                    data = command.pop("output").encode()
                    if not extended and command["argv"][:2] == ["HTTP", "GET"] and command["argv"][2].endswith("/ready"):
                        data = b" " * 4096 + data
                        extended = True
                    store.command(command, data)
        receipt["execution_evidence"] = storage.retain_inventories(receipt["execution_evidence"], store)
        receipt["output_sha256"] = digest(canonical(receipt["execution_evidence"]))
        self.assertTrue(extended)
        self.assertEqual(qualification.receipt_outcome(receipt, admitted)[2]["execution"], "passed")
        command = receipt["execution_evidence"]["cases"][0]["commands"][0]
        Path(command["output_ref"]["artifact"]["locator"]).unlink()
        with self.assertRaises(InvalidPacket): qualification.receipt_outcome(receipt, admitted)

    def testRealPythonSubprocessThroughExecutorRunUsesVerifiedExternalOutput(self):
        binding = {"repository": str(self.repository), "scope": "synthetic Python control"}
        with mock.patch.object(executor, "ROOT", self.repository), \
                mock.patch.object(executor.preparation, "source_binding", return_value=binding), \
                mock.patch.object(executor.Executor, "shared", return_value={}):
            worker = executor.Executor(self.root / "python-run")
            self.addCleanup(lambda: __import__("shutil").rmtree(worker.scratch, ignore_errors=True))
            data = worker.run([sys.executable, "-c", "import sys;sys.stdout.buffer.write(b'x'*4096+b'final proof')"])
            row = worker.commands[-1]
            self.assertNotIn("output", row)
            self.assertEqual(storage.command_text(row, worker.storage.context).encode(), data)
            self.assertTrue(data.endswith(b"final proof"))
            self.assertNotIn("final proof", row["output_excerpt"])
            self.assertEqual(row["exit_code"], 0)
            self.assertTrue(all(not attempt["remaining"] for attempt in row["cleanup"]))


class EvidenceAdmissionTests(unittest.TestCase):
    def testLastMatchIgnoresGeneratedArtifactsButAllowsSummariesAndSourceBaselines(self):
        repository = Path(__file__).resolve().parents[2]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            subprocess.run(["git", "init", "-q", root], check=True)
            (root / ".gitignore").write_bytes((repository / ".gitignore").read_bytes())
            prefix = "_bmad-output/implementation-artifacts/evidence/story-3-22/attempt/"
            ignored = ["run.log", "Logs/capture.log", "packages/fixture.nupkg", "report.json", "results.json", "result.trx", "capture.zip", "capture.tar.gz", "payload.part", "capture.webm", "trace.dat", "commands.json", "reports/result.json", "results/TestResults.xml", "screenshots/home.png", "playwright-report/index.html"]
            allowed = ["summary.json", "verification.json", "source-baseline.json", "snapshot-baseline.cs", "snapshots/source.cs"]
            for name, expected in [(name, True) for name in ignored] + [(name, False) for name in allowed]:
                result = subprocess.run(["git", "check-ignore", "--no-index", prefix + name], cwd=root, capture_output=True)
                self.assertEqual(result.returncode, 0 if expected else 1, name)


class ReviewedStorageTests(unittest.TestCase):
    """Focused regressions for reviewed storage publication and reader boundaries."""
    setUp = EvidenceStorageTests.setUp
    command = EvidenceStorageTests.command

    def testAtomicConcurrentIdenticalPublication(self):
        import concurrent.futures
        import threading
        barrier, real_link = threading.Barrier(2), os.link
        payload = b"synchronized proof" * 4096
        def link(source, target, **kwargs):
            if target == digest(payload):
                self.assertFalse((self.store.root / "objects" / target).exists())
                barrier.wait(timeout=5)
            return real_link(source, target, **kwargs)
        with mock.patch.object(storage.os, "link", side_effect=link), concurrent.futures.ThreadPoolExecutor(2) as pool:
            futures = [pool.submit(self.store.retain, payload, {"kind": "concurrent"}) for _ in range(2)]
            refs = [future.result(timeout=10) for future in futures]
        for ref in refs:
            self.assertEqual(storage.resolve(ref, self.context, {"kind": "concurrent"}), payload)
        self.assertEqual(len(list((self.store.root / "objects").iterdir())), 1)

    def testInterruptedPublicationDoesNotExposeFinalOrTemporaryBytes(self):
        with mock.patch.object(storage.os, "link", side_effect=KeyboardInterrupt):
            with self.assertRaises(KeyboardInterrupt): self.command(b"interrupted")
        self.assertEqual(list((self.store.root / "objects").iterdir()), [])

    def testWarmRetainRepairsDeletionAndRejectsSubstitution(self):
        ref = self.command(b"retained")["output_ref"]
        target = Path(ref["artifact"]["locator"])
        target.unlink()
        recreated = self.command(b"retained")["output_ref"]
        self.assertEqual(storage.resolve(recreated, self.context, ref["artifact"]["identity"]), b"retained")
        target.unlink();target.symlink_to(self.repository / "unowned")
        with self.assertRaises(InvalidPacket): self.command(b"retained")
        self.assertFalse((self.repository / "unowned").exists())

    def testPublicStorageDirectoriesBecomePrivateWithoutChangingTheirOwner(self):
        root = self.root / "public"
        root.mkdir(mode=0o755)
        for name in ("objects", "manifests"): (root / name).mkdir(mode=0o755)
        owner = root.stat().st_uid
        store = storage.Store(root, self.context)
        ref = store.retain(os.urandom(128), {"kind": "private"})
        for directory in (root, root / "objects", root / "manifests"):
            self.assertEqual(directory.stat().st_mode & 0o777, 0o700)
            self.assertEqual(directory.stat().st_uid, owner)
        self.assertEqual(len(storage.resolve(ref, self.context, {"kind": "private"})), 128)

    def testRootContainerAcceptsControllerOwnerAndRefusesUnrelatedOwner(self):
        from types import SimpleNamespace
        for owner, allowed in ((1234, True), (5678, False)):
            info = SimpleNamespace(st_uid=owner, st_mode=0o40700)
            with mock.patch.object(storage, "_open_directory", return_value=100), \
                    mock.patch.object(storage.os, "geteuid", return_value=0), \
                    mock.patch.object(storage.os, "fstat", return_value=info), \
                    mock.patch.object(storage.os, "close"):
                if allowed:
                    storage.private_directory(self.store.root, controller_uid=1234)
                else:
                    with self.assertRaisesRegex(InvalidPacket, "unsafe owner"):
                        storage.private_directory(self.store.root, controller_uid=1234)

    def testAllOutputMarkersUseTheStrictReader(self):
        row = {"output": "inline", "output_excerpt": "partial", "output_sha256": digest(b"inline")}
        with self.assertRaises(InvalidPacket): storage.materialize(row, self.context)

    def testBoundedExcerptMatchesFullReplacementDecodingAtMultibyteBoundaries(self):
        for tail in ("😀".encode(), "é".encode(), "€".encode(), b"\xf0\x9f\x80invalid", b"\xff"):
            for prefix in range(storage.EXCERPT_BYTES - 4, storage.EXCERPT_BYTES + 1):
                data = b"a" * prefix + tail + b"x" * 10000
                expected = data.decode("utf-8", "replace").encode()[:storage.EXCERPT_BYTES].decode("utf-8", "ignore")
                self.assertEqual(storage.excerpt(data), expected)

    def testRepeatedLargeReferencesReuseVerifiedBytesAndDecodedText(self):
        row = self.command(b"x" * (2 * 1024 * 1024))
        original = storage.bounded_file
        with mock.patch.object(storage, "bounded_file", wraps=original) as read:
            values = storage.materialize([row] * 20, self.context, io_limit=3 * 1024 * 1024,
                                         decoded_limit=3 * 1024 * 1024)
        self.assertEqual(read.call_count, 2)
        self.assertIs(values[0]["output"], values[-1]["output"])

    def testAggregateOverflowRefusesBeforeReadingOrParsingTheNextPayload(self):
        first, second = self.store.inventory(["a" * 128]), self.store.inventory(["b" * 128])
        size = first["reference"]["artifact"]["bytes"]
        with mock.patch.object(storage.json, "loads", wraps=json.loads) as parse:
            with self.assertRaisesRegex(InvalidPacket, "decoded budget"):
                storage.materialize([first, second], self.context, decoded_limit=size)
            self.assertEqual(parse.call_count, 1)
        with self.assertRaisesRegex(InvalidPacket, "I/O budget"):
            storage.materialize([first, second], self.context, io_limit=1)

    def testLongSupportedArgumentRoundTripsAndOversizedManifestWritesNothing(self):
        row = {"id": 1, "argv": [sys.executable, "a" * 70001], "cwd": str(self.root),
            "started_utc": "a", "finished_utc": "b", "exit_code": 0}
        self.store.command(row, b"valid")
        self.assertEqual(storage.command_text(row, self.context), "valid")
        count = len(list((self.store.root / "objects").iterdir()))
        with self.assertRaisesRegex(InvalidPacket, "manifest exceeds"):
            self.store.retain(b"new payload", {"argument": "a" * storage.MANIFEST_BYTES})
        self.assertEqual(len(list((self.store.root / "objects").iterdir())), count)

    def testSuppressedGeneratedOutputStoresOnlyItsHash(self):
        worker = executor.Executor.__new__(executor.Executor)
        worker.commands, worker.storage, worker.save = [], self.store, mock.Mock()
        data = os.urandom(64)
        row = worker.record([sys.executable], self.root, executor.stamp(), 0, data, retain=False)
        self.assertIsNone(row["output"])
        self.assertEqual(row["output_sha256"], digest(data))
        self.assertFalse(storage.OUTPUT_MARKERS.intersection(row))
        self.assertIsNone(storage.command_text(row, self.context))
        self.assertEqual(list((self.store.root / "objects").iterdir()), [])

    def testSummaryPreservesOuterCleanupLaneAndInitializationFailures(self):
        for defect in ("outer-cleanup", "lane", "initialization"):
            with self.subTest(defect=defect):
                packet, outer = self.root / (defect + "-packet"), self.root / (defect + "-outer")
                if defect != "initialization":
                    packet.mkdir();outer.mkdir()
                    (packet / "execution.json").write_text(json.dumps({"finished_utc": "done", "errors": []}))
                if defect == "outer-cleanup":
                    (outer / "isolation.json").write_text(json.dumps({"passed": False, "errors": [],
                        "cleanup": [{"errors": ["removal failed"], "remaining": ["owned"]}]}))
                elif defect == "lane":
                    (packet / "receipts").mkdir()
                    (packet / "receipts/lane.json").write_text(json.dumps({"execution": "failed",
                        "assertions": {"attempted": 1, "passed": 0, "failed": 1}}))
                summary = storage.export_attempt(packet, self.repository / defect, self.store, isolation=outer,
                    exit_code=2 if defect == "initialization" else 0, error="startup refused" if defect == "initialization" else None)
                self.assertEqual(summary["outcome"]["status"], "failed")
                if defect == "outer-cleanup": self.assertIn("isolation.json", summary["isolation_proof"])
                if defect == "lane": self.assertEqual(summary["outcome"]["receipts"]["assertions"]["failed"], 1)

    def testSummaryDestinationSubstitutionIsRefusedBeforeWriting(self):
        packet = self.root / "packet";packet.mkdir()
        destination, outsider = self.repository / "summary", self.root / "outsider"
        outsider.mkdir()
        real_mkdir = os.mkdir
        def substitute(path, *args, **kwargs):
            result = real_mkdir(path, *args, **kwargs)
            if path == destination.name and kwargs.get("dir_fd") is not None:
                destination.rename(self.repository / "moved")
                destination.symlink_to(outsider, target_is_directory=True)
            return result
        with mock.patch.object(storage.os, "mkdir", side_effect=substitute):
            with self.assertRaises(InvalidPacket): storage.export_attempt(packet, destination, self.store)
        self.assertFalse((outsider / "summary.json").exists())
