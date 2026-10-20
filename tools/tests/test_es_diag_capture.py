"""Disposable synthetic archives and Git objects; never live qualification proof."""
import io
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import tempfile
import types
import unittest

import test_dapr_broker_qualification as fixtures

ROOT = Path(__file__).resolve().parents[2]
RETAINED = "_bmad-output/implementation-artifacts/evidence/story-3-21/verification-2026-10-20/packet"
BASELINE_IDENTITIES = RETAINED + "/execution-identities.json"
VERIFIER_SHA256 = "7357c189c12ce284196b84ceba47a435f21003d898f65ef6a9b272030fd80afa"


class EsDiagCaptureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workspace = tempfile.TemporaryDirectory(prefix="synthetic-es-diag-tests-")
        cls.repository = Path(cls.workspace.name) / "repository"
        cls.repository.mkdir()
        subprocess.run(["git", "init", "--quiet", str(cls.repository)], check=True, capture_output=True)
        (cls.repository / "tools").mkdir()
        cls.adapter = cls.repository / "tools/validate-es-diag-capture.py"
        shutil.copyfile(ROOT / "tools/validate-es-diag-capture.py", cls.adapter)
        verifier = (ROOT / RETAINED / "inputs/tools/dapr_broker_qualification.py").read_bytes()
        if hashlib.sha256(verifier).hexdigest() != VERIFIER_SHA256:
            raise ValueError("retained baseline verifier digest mismatch")
        cls.trusted_verifier = types.ModuleType("synthetic_fixture_trusted_verifier")
        exec(compile(verifier, "digest-bound-synthetic-fixture-verifier", "exec"), cls.trusted_verifier.__dict__)
        cls.control_plane = json.loads((ROOT / BASELINE_IDENTITIES).read_bytes())["controlPlane"]
        cls.baseline = cls.baseline_object(verifier)
        cls.mismatched = cls.baseline_object(b"raise RuntimeError('untrusted verifier must never execute')\n")
        # A mutable sibling import would fail: the isolated adapter must use Git.
        (cls.repository / "tools/dapr_broker_qualification.py").write_text("raise RuntimeError('mutable sibling imported')\n")

    @classmethod
    def tearDownClass(cls):
        cls.workspace.cleanup()

    @classmethod
    def git(cls, *arguments, data):
        return subprocess.run(["git", "-C", str(cls.repository), *arguments], input=data,
                              check=True, capture_output=True).stdout.decode().strip()

    @classmethod
    def baseline_object(cls, verifier):
        # Plumbing creates disposable fixture objects only, without an index,
        # branch, hooks, or mutations to the real repository's Git history.
        entries = {}
        files = {"tools/dapr_broker_qualification.py": verifier,
                 "deploy/dapr/qualification/runtime-lock.json":
                     (ROOT / RETAINED / "inputs/deploy/dapr/qualification/runtime-lock.json").read_bytes(),
                 BASELINE_IDENTITIES: (ROOT / BASELINE_IDENTITIES).read_bytes()}
        for path, data in files.items():
            node = entries
            *parents, name = path.split("/")
            for part in parents:
                node = node.setdefault(part, {})
            node[name] = cls.git("hash-object", "-w", "--stdin", data=data)

        def tree_object(node):
            lines = []
            for name, value in sorted(node.items()):
                kind, identity = ("040000 tree", tree_object(value)) if isinstance(value, dict) else ("100644 blob", value)
                lines.append(f"{kind} {identity}\t{name}\n")
            return cls.git("mktree", data="".join(lines).encode())

        tree = tree_object(entries)
        commit = (f"tree {tree}\nauthor Synthetic Fixture <synthetic@example.invalid> 0 +0000\n"
                  "committer Synthetic Fixture <synthetic@example.invalid> 0 +0000\n\n"
                  "test: synthetic verifier fixture\n").encode()
        return cls.git("hash-object", "-t", "commit", "-w", "--stdin", data=commit)

    def setUp(self):
        self.fixture = fixtures.BrokerQualificationTests(methodName="runTest")
        self.fixture.setUp()
        self.directory = self.fixture.directory
        self.archive = Path(self.workspace.name) / "packet.tar.gz"
        identities = json.loads((self.directory / "execution-identities.json").read_text())
        identities["controlPlane"] = self.control_plane
        fixtures.write_json(self.directory / "execution-identities.json", identities)
        # An honest rejected binding vector is a complete capture, not a
        # technical PASS. Keep the HTTP receipt and derived observation aligned.
        self.fixture.change("rabbitmq/missing-bindings/observation.json", lambda row: row.update(unboundPublishStatus=204))
        for command in self.fixture.commands:
            if command.get("input", {}).get("id") == "rabbitmq-missing-bindings-unbound":
                command["httpStatus"] = 204
            if command.get("argv", [""])[0] == "HTTP" and "/publish/" in command["argv"][-1]:
                command["exitCode"] = 0 if command["httpStatus"] < 400 else 1
        snapshot = json.loads((self.directory / "workspace-before.json").read_bytes())
        for _ in range(2):
            self.fixture.commands.append({"argv": ["docker", "ps", "-aq", "--no-trunc"],
                                          "exitCode": 0, "output": "\n".join(snapshot),
                                          "startedUtc": "2026-10-20T00:00:00+00:00", "endedUtc": "2026-10-20T00:00:01+00:00"})
            self.fixture.commands.append({**self.fixture.commands[1], "output": json.dumps(snapshot, sort_keys=True)})
        fixtures.write_json(self.directory / "commands.json", self.fixture.commands)
        self.fixture.packet["brokers"]["rabbitmq"]["missing-bindings"]["result"] = "FAIL"
        self.fixture.seal()

    def tearDown(self):
        self.fixture.tearDown()
        self.archive.unlink(missing_ok=True)

    def pack(self, extras=()):
        with tarfile.open(self.archive, "w:gz", format=tarfile.USTAR_FORMAT) as archive:
            for path in sorted(self.directory.rglob("*")):
                if path.is_file():
                    archive.add(path, arcname=path.relative_to(self.directory).as_posix(), recursive=False)
            for member, data in extras:
                archive.addfile(member, io.BytesIO(data) if data is not None else None)

    def run_cli(self, *, baseline=None, evidence=None):
        optimization = ["-" + "O" * sys.flags.optimize] if sys.flags.optimize else []
        return subprocess.run([sys.executable, *optimization, "-I", "-S", "-B", str(self.adapter),
                               str(evidence or self.archive), "--baseline", baseline or self.baseline],
                              cwd=self.workspace.name, capture_output=True, text=True, timeout=20)

    def reject(self, text):
        result = self.run_cli()
        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        output = json.loads(result.stdout)
        self.assertEqual("FAIL", output["captureIntegrity"])
        self.assertEqual("FAIL", output["productionEligibility"])
        self.assertIn(text, output["error"])

    def save_commands(self):
        fixtures.write_json(self.directory / "commands.json", self.fixture.commands)
        self.fixture.seal()

    def omit_inventory_receipts(self, *, enumeration=True, inspect=True):
        for command in self.fixture.commands:
            if ((enumeration and command["argv"][:2] == ["docker", "ps"])
                    or (inspect and command["argv"][:2] == ["docker", "inspect"])):
                command["argv"] = ["synthetic-omitted-inventory-receipt"]
        self.save_commands()

    def testCompleteRejectedCapturePassesWithDeterministicNonAuthorizingOutput(self):
        before = {p.relative_to(self.directory).as_posix(): p.read_bytes() for p in self.directory.rglob("*") if p.is_file()}
        self.pack()
        first = self.run_cli()
        self.assertEqual(0, first.returncode, first.stdout + first.stderr)
        result = json.loads(first.stdout)
        self.assertEqual("PASS", result["captureIntegrity"])
        self.assertEqual("FAIL", result["technicalQualification"])
        self.assertEqual(["rabbitmq/missing-bindings"], result["failedScenarios"])
        self.assertEqual("FAIL", result["productionEligibility"])
        self.assertEqual(24, result["executedObservations"])
        self.assertIsNone(result["selectedBroker"])
        self.assertEqual(first.stdout, self.run_cli().stdout)
        after = {p.relative_to(self.directory).as_posix(): p.read_bytes() for p in self.directory.rglob("*") if p.is_file()}
        self.assertEqual(before, after)
        self.assertFalse(list(self.repository.rglob("__pycache__")))

    def testMissingVectorFails(self):
        del self.fixture.packet["brokers"]["kafka"]["missing-bindings"]
        self.fixture.seal()
        self.pack()
        self.reject("exactly 12 executed vectors")

    def testBlockedRejectedVectorFails(self):
        self.fixture.change("rabbitmq/missing-bindings/observation.json", lambda row: row.update(blockedBy="synthetic blocked execution"))
        self.pack()
        self.reject("blocked vector")

    def testRejectedVectorsStillRequireReadbackAndDiagnosticsKinds(self):
        for name, kind in (("application-readback.json", "HTTP application readback"),
                           ("broker-diagnostics.json", "broker diagnostics")):
            with self.subTest(name=name):
                path = "rabbitmq/missing-bindings/" + name
                original = (self.directory / path).read_bytes()
                self.fixture.change(path, lambda row: row.update(kind="synthetic-nonexecution"))
                self.pack()
                self.reject(kind)
                (self.directory / path).write_bytes(original)
                self.fixture.seal()

    def testSuccessfulPreflightAndBothControlPlaneIdentitiesAreMandatory(self):
        path = self.directory / "execution-identities.json"
        original = path.read_bytes()
        for mutation, expected in ((lambda row: row.update(preflightPassed=False), "successful preflight"),
                                   (lambda row: row["controlPlane"].pop("scheduler"), "both control-plane")):
            with self.subTest(expected=expected):
                path.write_bytes(original)
                self.fixture.change("execution-identities.json", mutation)
                self.pack()
                self.reject(expected)

    def testBothLoadedSidecarRolesAreMandatory(self):
        (self.directory / "kafka/metadata/qualification-publisher.json").unlink()
        self.fixture.seal()
        self.pack()
        self.reject("both loaded sidecar roles")

    def testResealedMissingSubscriptionSourceCoverageFails(self):
        name = "deploy/dapr/qualification/subscriptions.yaml"
        self.fixture.change("inputs/deploy/dapr/qualification/runtime-lock.json", lambda row: row["inputs"].pop(name))
        (self.directory / "inputs" / name).unlink()
        self.fixture.seal()
        self.assertEqual("PASS", self.trusted_verifier.validate_packet(self.directory, allow_failures=True)["integrity"])
        self.pack()
        self.reject("baseline source/component/subscription input coverage")

    def testChangedDiagnosticInputsAndAdditiveSnapshotsRemainPermitted(self):
        lock = self.directory / "inputs/deploy/dapr/qualification/runtime-lock.json"
        profile = json.loads(lock.read_bytes())
        changed = "tools/dapr_broker_probe.py"
        source = self.directory / "inputs" / changed
        source.write_bytes(source.read_bytes() + b"\n# Clearly synthetic diagnostic fixture update.\n")
        profile["inputs"][changed] = fixtures.digest(source)
        added = "synthetic-diagnostic-snapshot.txt"
        source = self.directory / "inputs" / added
        source.write_text("synthetic additive snapshot\n")
        profile["inputs"][added] = fixtures.digest(source)
        fixtures.write_json(lock, profile)
        self.fixture.seal()
        self.pack()
        result = self.run_cli()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def testDistinctFilesCannotAliasAnotherBrokerVector(self):
        for field in ("observation", "applicationReadback", "brokerDiagnostics"):
            source = self.fixture.packet["brokers"]["rabbitmq"]["stable-id-deduplication"][field]
            target = self.fixture.packet["brokers"]["kafka"]["stable-id-deduplication"][field]
            value = json.loads((self.directory / source).read_bytes())
            if field == "observation":
                value["broker"] = "kafka"
            fixtures.write_json(self.directory / target, value)
        self.fixture.seal()
        self.assertEqual("PASS", self.trusted_verifier.validate_packet(self.directory, allow_failures=True)["integrity"])
        self.pack()
        self.reject("primary vector message identities must be globally distinct")

    def testDistinctMessagesCannotShareDiagnosticReceipt(self):
        source = self.fixture.packet["brokers"]["rabbitmq"]["stable-id-deduplication"]["brokerDiagnostics"]
        target = self.fixture.packet["brokers"]["kafka"]["stable-id-deduplication"]["brokerDiagnostics"]
        (self.directory / target).write_bytes((self.directory / source).read_bytes())
        self.fixture.seal()
        self.assertEqual("PASS", self.trusted_verifier.validate_packet(self.directory, allow_failures=True)["integrity"])
        self.pack()
        self.reject("readback/diagnostic receipts must be independent")

    def testPublishMethodAndExitCodeMustMatchHttpExecution(self):
        receipt = next(row for row in self.fixture.commands
                       if row.get("input", {}).get("id") == "rabbitmq-missing-bindings")
        original = dict(receipt)
        for method, exit_code in (("GET", 17), ("GET", 0), ("POST", 17)):
            with self.subTest(method=method, exit_code=exit_code):
                receipt.update(original)
                receipt["argv"] = ["HTTP", method, receipt["argv"][-1]]
                receipt["exitCode"] = exit_code
                self.save_commands()
                self.pack()
                self.reject("publication requires POST with consistent HTTP status/exit")

    def testLoadedMetadataRequiresSuccessfulGetExecution(self):
        receipt = next(row for row in self.fixture.commands
                       if row.get("argv", [""])[0] == "HTTP"
                       and row["argv"][-1].endswith("/v1.0/metadata")
                       and json.loads(row["output"]).get("id") == "qualification-publisher")
        original = dict(receipt)
        for method, exit_code in (("POST", 31), ("POST", 0), ("GET", 31)):
            with self.subTest(method=method, exit_code=exit_code):
                receipt.update(original)
                receipt["argv"] = ["HTTP", method, receipt["argv"][-1]]
                receipt["exitCode"] = exit_code
                self.save_commands()
                self.pack()
                self.reject("loaded metadata requires successful HTTP GET")

    def testReadbackRequiresSuccessfulGetExecution(self):
        path = self.fixture.packet["brokers"]["rabbitmq"]["missing-bindings"]["applicationReadback"]
        index = json.loads((self.directory / path).read_bytes())["commandReceipt"]
        receipt = self.fixture.commands[index]
        original = dict(receipt)
        for method, exit_code in (("GET", 17), ("POST", 0)):
            with self.subTest(method=method, exit_code=exit_code):
                receipt.update(original)
                receipt["argv"] = ["HTTP", method, receipt["argv"][-1]]
                receipt["exitCode"] = exit_code
                self.save_commands()
                self.pack()
                self.reject("readback requires a successful HTTP GET")

    def testControlPlaneBinaryIdentityCannotDrift(self):
        self.fixture.change("execution-identities.json",
                            lambda row: row["controlPlane"]["scheduler"].update(binarySha256="0" * 64))
        self.assertEqual("PASS", self.trusted_verifier.validate_packet(self.directory, allow_failures=True)["integrity"])
        self.pack()
        self.reject("immutable control-plane identity differs from baseline")

    def testEmptyInventoriesNeedTwoSuccessfulCompleteEnumerations(self):
        for name in ("workspace-before.json", "workspace-after.json"):
            fixtures.write_json(self.directory / name, {})
        self.omit_inventory_receipts()
        self.pack()
        self.reject("lacks complete successful Docker inventory receipts")
        for _ in range(2):
            self.fixture.commands.append({"argv": ["docker", "ps", "-aq", "--no-trunc"],
                                          "exitCode": 0, "output": "", "startedUtc": "2026-10-20T00:00:00+00:00",
                                          "endedUtc": "2026-10-20T00:00:01+00:00"})
        self.save_commands()
        self.pack()
        result = self.run_cli()
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def testNonemptyInventoryRequiresMatchingSuccessfulInspect(self):
        for command in self.fixture.commands:
            if command["argv"][:2] == ["docker", "inspect"]:
                command["exitCode"] = 17
        self.save_commands()
        self.assertEqual("PASS", self.trusted_verifier.validate_packet(self.directory, allow_failures=True)["integrity"])
        self.pack()
        self.reject("lacks complete successful Docker inventory receipts")

    def testFailedOrFilteredEnumerationCannotSupportPreservation(self):
        enumerations = [row for row in self.fixture.commands if row["argv"] == ["docker", "ps", "-aq", "--no-trunc"]]
        for altered in ("failed", "filtered"):
            with self.subTest(altered=altered):
                for row in enumerations:
                    row["exitCode"] = 1 if altered == "failed" else 0
                    row["argv"] = ["docker", "ps", "-aq", "--no-trunc"] + (["--filter", "label=synthetic"] if altered == "filtered" else [])
                self.save_commands()
                self.pack()
                self.reject("lacks complete successful Docker inventory receipts")

    def testDuplicateCleanupKeysFailBeforeDelegatedVerifier(self):
        (self.directory / "cleanup.json").write_text(
            '{"ownedResourcesRemaining":[],"unrelatedContainersUnchanged":false,"unrelatedContainersUnchanged":true}')
        self.fixture.seal()
        self.assertEqual("PASS", self.trusted_verifier.validate_packet(self.directory, allow_failures=True)["integrity"])
        self.pack()
        self.reject("JSON: duplicate object key")

    def testRehashedSameVersionBinarySubstitutionFails(self):
        self.fixture.change("inputs/deploy/dapr/qualification/runtime-lock.json",
                            lambda row: row["runtime"].update(binarySha256="0" * 64))
        self.fixture.change("execution-identities.json", lambda row: row.update(daprdSha256="0" * 64))
        self.assertEqual("PASS", self.trusted_verifier.validate_packet(self.directory, allow_failures=True)["integrity"])
        self.pack()
        self.reject("immutable candidate runtime differs from baseline")

    def testRehashedSameVersionOciSubstitutionFails(self):
        profile_path = self.directory / "inputs/deploy/dapr/qualification/runtime-lock.json"
        profile = json.loads(profile_path.read_text())
        image = profile["images"]["rabbitmq"]
        manifest = self.directory / "inputs" / image["manifest"]["path"]
        content = json.loads(manifest.read_text())
        content["syntheticFixtureAnnotation"] = "same-version substituted manifest"
        fixtures.write_json(manifest, content)
        substituted = fixtures.digest(manifest)
        image["reference"] = image["reference"].split("@sha256:")[0] + "@sha256:" + substituted
        image["manifest"]["sha256"] = substituted
        fixtures.write_json(profile_path, profile)
        self.fixture.seal()
        self.assertEqual("PASS", self.trusted_verifier.validate_packet(self.directory, allow_failures=True)["integrity"])
        self.pack()
        self.reject("immutable candidate images differs from baseline")

    def testPreservationCannotBeSelfDeclaredAfterContainerChange(self):
        self.fixture.change("workspace-after.json", lambda row: row.update({"unrelated-container": "changed-spec"}))
        self.pack()
        self.reject("unrelated container specifications changed")

    def testInventoryPollutionAndHashCorruptionFail(self):
        extra = tarfile.TarInfo("unlisted.log")
        extra.size = 1
        self.pack([(extra, b"x")])
        self.reject("inventory is not exactly")
        (self.directory / "cleanup.json").write_text("{}")
        self.pack()
        self.reject("retained-byte hash mismatch")

    def testUnsafeMemberPathsFail(self):
        for name in ("../escape", "/absolute", "C:/absolute", "a/../escape", "a//b", "a/./b", "a\\b", "a\nb", "a\x7fb"):
            with self.subTest(name=name):
                member = tarfile.TarInfo(name)
                self.pack([(member, b"")])
                self.reject("archive:")

    def testDuplicateLinksDevicesAndDirectoriesFail(self):
        for name, kind in (("packet.json", tarfile.REGTYPE), ("link", tarfile.SYMTYPE),
                           ("hardlink", tarfile.LNKTYPE), ("device", tarfile.CHRTYPE),
                           ("directory", tarfile.DIRTYPE), ("fifo", tarfile.FIFOTYPE)):
            with self.subTest(kind=kind):
                member = tarfile.TarInfo(name)
                member.type = kind
                member.linkname = "packet.json" if kind in (tarfile.SYMTYPE, tarfile.LNKTYPE) else ""
                self.pack([(member, b"")])
                self.reject("duplicate member" if name == "packet.json" else "only regular files")

    def testExcessiveMemberSizeAndCountFail(self):
        member = tarfile.TarInfo("huge")
        member.size = 64 * 1024 * 1024 + 1
        with gzip.open(self.archive, "wb") as archive:
            archive.write(member.tobuf(format=tarfile.USTAR_FORMAT))
        self.reject("excessive member size")
        self.pack([(tarfile.TarInfo("synthetic-extra-" + str(index)), b"") for index in range(4097)])
        self.reject("excessive member count")

    def testTrailingArchiveCannotHideASecondInventory(self):
        self.pack()
        with self.archive.open("ab") as output:
            output.write(gzip.compress(tarfile.TarInfo("hidden-extra").tobuf()))
        self.reject("trailing data")

    def testInvalidNonzeroFinalTarHeaderIsNotSilentlySwallowed(self):
        self.pack()
        raw = gzip.decompress(self.archive.read_bytes())
        with tarfile.open(fileobj=io.BytesIO(raw), mode="r:") as archive:
            last = archive.getmembers()[-1]
        end = last.offset_data + ((last.size + 511) // 512) * 512
        malformed = raw[:end] + b"invalid-final-tar-header".ljust(512, b"x") + b"\0" * 512
        self.archive.write_bytes(gzip.compress(malformed))
        self.reject("nonzero tar termination")

    def testTruncatedTarTerminationFails(self):
        self.pack()
        raw = gzip.decompress(self.archive.read_bytes())
        with tarfile.open(fileobj=io.BytesIO(raw), mode="r:") as archive:
            last = archive.getmembers()[-1]
        end = last.offset_data + ((last.size + 511) // 512) * 512
        self.archive.write_bytes(gzip.compress(raw[:end] + b"\0" * 512))
        self.reject("truncated tar termination")

    def testNonzeroLastMemberPaddingFails(self):
        self.pack()
        raw = bytearray(gzip.decompress(self.archive.read_bytes()))
        with tarfile.open(fileobj=io.BytesIO(raw), mode="r:") as archive:
            last = archive.getmembers()[-1]
        self.assertNotEqual(0, last.size % 512)
        raw[last.offset_data + last.size] = 1
        self.archive.write_bytes(gzip.compress(raw))
        self.reject("nonzero tar termination")

    def testCorruptAppendedGzipProducesDeterministicJsonFailure(self):
        self.pack()
        malformed = bytearray(gzip.compress(b"synthetic appended gzip"))
        malformed[10] = 7  # Invalid DEFLATE block type, after a valid gzip header.
        with self.archive.open("ab") as output:
            output.write(malformed)
        self.reject("malformed gzip stream")
        self.assertEqual(self.run_cli().stdout, self.run_cli().stdout)

    def testNonRegularArchiveAndShortBaselineFail(self):
        self.pack()
        result = self.run_cli(baseline=self.baseline[:12])
        self.assertEqual(1, result.returncode)
        self.assertIn("full lowercase 40-character commit", result.stdout)
        link = Path(self.workspace.name) / "synthetic-link.tar.gz"
        link.symlink_to(self.archive)
        try:
            self.assertEqual(1, self.run_cli(evidence=link).returncode)
        finally:
            link.unlink()

    def testMismatchedBaselineVerifierIsRejectedBeforeExecution(self):
        self.pack()
        result = self.run_cli(baseline=self.mismatched)
        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("verifier digest mismatch", result.stdout)
        self.assertNotIn("untrusted verifier must never execute", result.stdout + result.stderr)


if __name__ == "__main__":
    unittest.main()
