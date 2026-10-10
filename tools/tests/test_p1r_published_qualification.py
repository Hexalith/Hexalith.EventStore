"""Contract tests for the 6.1-P1R published qualification harness.

Every package, lane, restore, cleanup, owner-input and decision document built
here is a clearly marked synthetic tooling fixture (``hexalith-p1r-synthetic-fixture``).
They verify contracts only: synthetic evidence and local process controls must
never satisfy a published or operational lane. Source binding is replaced by a
marked synthetic snapshot so packet contracts do not depend on unrelated
concurrent workspace edits; the process controls themselves remain real.
"""
import ast
import base64
import contextlib
import copy
import hashlib
import json
import os
from pathlib import Path
import shutil
import signal
import struct
import subprocess
import sys
import tempfile
import time
import unittest
from unittest import mock
import uuid
import warnings
import zipfile

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
import p1r_qualification as preparation  # noqa: E402
import p1r_published_qualification as q  # noqa: E402
import p1r_qualification_runtime as runtime  # noqa: E402

PYTHON = str(Path(sys.executable).resolve())
SYNTHETIC_BINDING = {"repository": str(q.ROOT), "fixture": q.SYNTHETIC,
                     "python": {"path": PYTHON, "sha256": "0" * 64, "version": list(sys.version_info[:3])}}
VERSION = "0.0.1-p1r.synthetic"
FEED = "https://synthetic.invalid/v3/index.json"
COMMIT = "3" * 40
PROFILE = {"runtime": "synthetic-runtime", "runtime_version": "0.0.0", "backend": "synthetic-backend",
           "backend_image": "synthetic/backend@sha256:" + "4" * 64, "selected_by": "synthetic-fixture",
           "reference": "synthetic tooling fixture"}
PACKAGE_IDS = ("Hexalith.EventStore.Contracts", "Hexalith.EventStore.Server")
DRIVER = r"""
import copy, importlib.util, json, sys
from pathlib import Path
from unittest import mock
tools = Path(sys.argv[1])
sys.path.insert(0, str(tools))
import p1r_qualification as preparation
binding = json.loads(sys.argv[2])
spec = importlib.util.spec_from_file_location("p1r_published_cli", tools / "p1r-published-qualification.py")
cli = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cli)
with mock.patch.object(preparation, "source_binding", side_effect=lambda root=None: copy.deepcopy(binding)):
    raise SystemExit(cli.main(sys.argv[3:]))
"""
_patcher = None


def setUpModule():
    global _patcher
    _patcher = mock.patch.object(preparation, "source_binding", side_effect=lambda root=preparation.ROOT: copy.deepcopy(SYNTHETIC_BINDING))
    _patcher.start()


def tearDownModule():
    _patcher.stop()


def sha(data):
    return hashlib.sha256(data).hexdigest()


class PostgresqlComponentTests(unittest.TestCase):
    def testNonStringRenderedHashIsInvalidPacket(self):
        content = (q.ROOT / "deploy/dapr/statestore-postgresql.yaml").read_text()
        file = {"content": content, "credential_redacted": True, "rendered_sha256": 7}
        with self.assertRaisesRegex(q.InvalidPacket, "PostgreSQL component template"):
            q.validate_postgresql_component(file)


def at(second):
    return f"2026-10-06T00:{second // 60:02d}:{second % 60:02d}+00:00"


def content_hash(identifier):
    return base64.b64encode(hashlib.sha512(b"synthetic-content:" + identifier.encode()).digest()).decode()


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n")


def synthetic_archive(path, identifier, version, marker=True, signature=True, commit=COMMIT, description="Synthetic tooling fixture",
                      nuspec_version=None, dll=True, duplicate=False, compression=zipfile.ZIP_STORED):
    nuspec = (f'<?xml version="1.0" encoding="utf-8"?>\n<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">'
              f"<metadata><id>{identifier}</id><version>{nuspec_version or version}</version><authors>Synthetic</authors>"
              f"<description>{description}</description><tags>{q.SYNTHETIC if marker else 'fixture'}</tags>"
              f'<repository type="git" url="https://synthetic.invalid/eventstore.git" commit="{commit}" />'
              "</metadata></package>")
    library = (f"lib/net10.0/{identifier}.dll", b"synthetic-dll:" + identifier.encode())
    entries = [(identifier + ".nuspec", nuspec.encode())] + ([library] if dll else []) + ([library] if duplicate else [])
    if signature:
        entries.append((".signature.p7s", b"synthetic-signature-bytes"))
    if marker:
        entries.append((q.MARKER_ENTRY, b"synthetic tooling fixture; never a published package\n"))
    path.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(path, "w") as archive, warnings.catch_warnings():
        warnings.simplefilter("ignore")  # A duplicate entry name is the deliberate defect.
        for name, data in entries:
            info = zipfile.ZipInfo(name, date_time=(2026, 1, 1, 0, 0, 0))
            info.compress_type = compression
            archive.writestr(info, data)
    return path.read_bytes()


def synthetic_evidence(root, role="candidate", version=VERSION, marker=True):
    """Build an isolated Release consumer layout around synthetic archives; returns (evidence, selection)."""
    packages_root = root / "packages"
    output = root / "consumer/Host/bin/Release/net10.0"
    output.mkdir(parents=True)
    selected, libraries, locked, loaded = [], {}, {}, []
    for identifier in PACKAGE_IDS:
        lower = identifier.lower()
        base = packages_root / lower / version.lower()
        data = synthetic_archive(base / f"{lower}.{version.lower()}.nupkg", identifier, version, marker=marker)
        write(base / ".nupkg.metadata", {"version": 2, "contentHash": content_hash(identifier), "source": FEED})
        dll = b"synthetic-dll:" + identifier.encode()
        (output / f"{identifier}.dll").write_bytes(dll)
        selected.append({"id": identifier, "archive_sha256": sha(data), "content_hash": content_hash(identifier),
                         "repository_commit": COMMIT})
        libraries[f"{identifier}/{version}"] = {"type": "package", "sha512": content_hash(identifier), "path": f"{lower}/{version}"}
        locked[identifier] = {"type": "Direct", "requested": f"[{version}, )", "resolved": version, "contentHash": content_hash(identifier)}
        loaded.append({"name": identifier, "version": "0.0.1.0", "path": str(output / f"{identifier}.dll"), "sha256": sha(dll)})
    write(root / "consumer/Host/obj/project.assets.json",
          {"version": 3, "libraries": libraries, "project": {"restore": {"packagesPath": str(packages_root) + "/",
                                                                          "projectStyle": "PackageReference"}}})
    write(root / "consumer/Host/packages.lock.json", {"version": 2, "dependencies": {"net10.0": locked}})
    write(root / "loaded/Host.json", {"assemblies": loaded, "fixture": q.SYNTHETIC})
    write(root / "evidence.json", {"schema": q.EVIDENCE_SCHEMA, "role": role, "fixture": q.SYNTHETIC if marker else None,
                                   "packages_root": "packages", "configuration": "Release",
                                   "projects": [{"name": "Host", "assets": "consumer/Host/obj/project.assets.json",
                                                 "lock": "consumer/Host/packages.lock.json", "loaded": "loaded/Host.json",
                                                 "output": "consumer/Host/bin/Release/net10.0"}]})
    selection = {"version": version, "tag": "v" + version, "tag_commit": "1" * 40,
                 "builds": {"version": "0.0.1", "commit": "2" * 40}, "feed": FEED, "packages": selected}
    return root, selection


def synthetic_inputs(candidate, rollback=None, profile=True, instrumentation=True, additions=q.ADDITIONS, fixture=q.SYNTHETIC):
    return {"schema": q.INPUTS_SCHEMA, "fixture": fixture,
            "authority": {"owner": "synthetic-fixture-owner", "reference": "synthetic tooling fixture; grants no authority",
                          "date": "2026-10-06"},
            "candidate": candidate, "rollback": rollback, "operational_profile": copy.deepcopy(PROFILE) if profile else None,
            "assertion_instrumentation": {"mechanism": "synthetic-counter", "accepted_by": "test-owner",
                                          "reference": "synthetic tooling fixture"} if instrumentation else None,
            "selected_additions": list(additions)}


def synthetic_decisions(inputs_sha256, decision="approved", conformance="conformant", fixture=q.SYNTHETIC):
    rows = [{"role": role, "decision": decision, "owner": None if decision == "pending" else "synthetic-fixture-owner",
             "date": None if decision == "pending" else "2026-10-06",
             "reference": None if decision == "pending" else "synthetic tooling fixture"} for role in q.DECISION_ROLES]
    return {"schema": q.DECISIONS_SCHEMA, "fixture": fixture, "inputs_sha256": inputs_sha256, "decisions": rows,
            "conformance": {"status": conformance, "baseline": None if conformance == "pending" else "5" * 40,
                            "reference": None if conformance == "pending" else "synthetic tooling fixture"}}


def synthetic_verifier(archive, output, cwd):
    """Injected test double: its output mimics success, so only the scope rule keeps it tooling-only."""
    output.write_bytes(b"synthetic test double; " + q.REPOSITORY_SIGNATURE.encode() + b"\n")
    return {"verifier": "synthetic-test-double", "argv": ["synthetic-verifier", str(archive)], "cwd": str(cwd),
            "started_utc": q.stamp(), "finished_utc": q.stamp(), "exit_code": 0}


def counter(checks):
    return preparation.checks_counter(checks)


def lane_case(identifier="case-1", operation="supported", expected="effect", outcome="effect", disposition="compatible",
              before="a", after="b", checks=None):
    checks = [{"id": "synthetic-check", "passed": True}] if checks is None else checks
    return {"id": identifier, "operation": operation, "expected": expected, "outcome": outcome, "disposition": disposition,
            "inventory": {"before_sha256": sha(before.encode()), "after_sha256": sha(after.encode())},
            "checks": checks, "assertions": counter(checks)}


def lane_receipt(lane="metadata-write", cases=None, scope="tooling-synthetic", execution="passed", compatibility="unverified"):
    cases = [lane_case()] if cases is None else cases
    total = {key: sum(case["assertions"][key] for case in cases) for key in ("attempted", "passed", "failed")}
    return {"schema": q.LANE_RECEIPT_SCHEMA, "id": uuid.uuid4().hex, "lane": lane, "scope": scope,
            "fixture": {"id": "synthetic-lane-" + lane, "synthetic": scope == "tooling-synthetic"},
            "inputs_sha256": None, "instrumentation": None, "profile": None,
            "argv": ["synthetic-executor", lane], "cwd": "/synthetic", "started_utc": at(0), "finished_utc": at(1),
            "exit_code": 0, "output_sha256": "5" * 64,
            "identities": {"configuration": "Release", "packages": [], "loaded_assemblies": []},
            "cases": cases, "assertions": total, "execution": execution, "compatibility": compatibility}


def cleanup_receipt(scope="tooling-synthetic"):
    invocation = uuid.uuid4().hex
    label = "hexalith.p1r.invocation=" + invocation
    owned = [{"kind": "container", "id": "synthetic-container", "label": label},
             {"kind": "database", "id": "synthetic-database", "label": label}]
    shared = {"synthetic-shared": {"image": "synthetic/shared", "running": True, "started": "2026-10-06T00:00:00Z"}}
    checks = [{"id": name, "passed": True} for name in runtime.CLEANUP_CHECKS]
    return {"schema": runtime.CLEANUP_SCHEMA, "id": uuid.uuid4().hex, "invocation": invocation, "scope": scope,
            "fixture": {"id": "synthetic-cleanup", "synthetic": scope == "tooling-synthetic"}, "inputs_sha256": None,
            "profile": None, "owned": owned,
            "attempts": [{"started_utc": at(40), "finished_utc": at(41), "targeted": [r["id"] for r in owned],
                          "removed": [r["id"] for r in owned], "remaining": [], "errors": []},
                         {"started_utc": at(42), "finished_utc": at(43), "targeted": [], "removed": [], "remaining": [],
                          "errors": []}],
            "shared": {"before": copy.deepcopy(shared), "after": copy.deepcopy(shared), "complete": True},
            "checks": checks, "assertions": counter(checks), "execution": "passed", "compatibility": "unverified"}


def inventory_rows(head, extra_event=None, metadata_floor=5, primary_hashes=None):
    rows = [{"key": "tenant-a:counter:fixture:metadata", "tenant": "tenant-a", "kind": "metadata",
             "sha256": sha(f"metadata-{head}-{metadata_floor}".encode()), "sequence": head, "floor": metadata_floor},
            {"key": "tenant-a:counter:fixture:snapshot", "tenant": "tenant-a", "kind": "snapshot", "sha256": sha(b"snapshot-9"),
             "sequence": 9, "floor": None},
            {"key": "tenant-a:command-status:synthetic", "tenant": "tenant-a", "kind": "bookkeeping",
             "sha256": sha(f"status-{head}".encode()), "sequence": None, "floor": None},
            {"key": "tenant-b:counter:fixture:metadata", "tenant": "tenant-b", "kind": "metadata", "sha256": sha(b"b-metadata"),
             "sequence": 3, "floor": None}]
    for number in range(5, head + 1):
        value = (primary_hashes or {}).get(number, sha(f"event-{number}".encode()))
        rows.append({"key": f"tenant-a:counter:fixture:events:{number:03d}", "tenant": "tenant-a", "kind": "event",
                     "sha256": value, "sequence": number, "floor": None})
    for number in range(1, 4):
        rows.append({"key": f"tenant-b:counter:fixture:events:{number:03d}", "tenant": "tenant-b", "kind": "event",
                     "sha256": sha(f"b-event-{number}".encode()), "sequence": number, "floor": None})
    return sorted(rows, key=lambda row: row["key"])


def inventory(command, rows):
    return {"command": command, "rows": rows, "sha256": q.digest(q.preparation.canonical(rows))}


def restore_receipt(role="candidate", scope="tooling-synthetic"):
    source_db, restored_db, backup = sha(b"source-db"), sha(b"restored-db"), sha(b"synthetic-dump")
    source = inventory_rows(12)
    appended = inventory_rows(13)
    writer = {"pid": 4101, "start_ticks": 11, "pgid": 4101, "session": 4101}
    restarted = {"pid": 4102, "start_ticks": 22, "pgid": 4102, "session": 4102}
    def command(identifier, step, database=None, output=None, input_sha256=None, input_bytes=None, processes=None):
        return {"id": identifier, "step": step, "argv": ["synthetic-" + step], "cwd": "/synthetic", "started_utc": at(identifier),
                "finished_utc": at(identifier), "exit_code": 0, "output_sha256": output or sha(step.encode()),
                "database": database, "input_sha256": input_sha256, "input_bytes": input_bytes, "processes": processes}
    inventories = {"source": inventory(3, source), "restored": inventory(7, copy.deepcopy(source)),
                   "appended": inventory(10, appended), "restarted": inventory(14, copy.deepcopy(appended))}
    commands = [command(1, "create-database", source_db), command(2, "seed", source_db),
                command(3, "inventory", source_db, inventories["source"]["sha256"]),
                command(4, "backup", source_db, backup), command(5, "create-database", restored_db),
                command(6, "restore", restored_db, input_sha256=backup, input_bytes=4096),
                command(7, "inventory", restored_db, inventories["restored"]["sha256"]),
                command(8, "start-writer", restored_db, processes=[writer]), command(9, "append", restored_db),
                command(10, "inventory", restored_db, inventories["appended"]["sha256"]),
                command(11, "stop-writer", restored_db), command(12, "restart", restored_db, processes=[restarted]),
                command(13, "replay", restored_db), command(14, "inventory", restored_db, inventories["restarted"]["sha256"])]
    checks = [{"id": name, "passed": True} for name in runtime.RESTORE_CHECKS]
    return {"schema": runtime.RESTORE_SCHEMA, "id": uuid.uuid4().hex, "role": role, "scope": scope,
            "fixture": {"id": "synthetic-restore", "synthetic": scope == "tooling-synthetic"}, "inputs_sha256": None,
            "profile": None, "packages": {}, "tenants": {"primary": "tenant-a", "secondary": "tenant-b"},
            "commands": commands, "databases": {"source": source_db, "restored": restored_db},
            "backup": {"command": 4, "sha256": backup, "bytes": 4096}, "inventories": inventories,
            "observations": {"state_before_append": 12, "appended_sequence": 13, "state_after_restart": 13},
            "cleanup": cleanup_receipt(scope), "checks": checks, "assertions": counter(checks), "execution": "passed",
            "compatibility": "unverified"}


def rehash(receipt, *names):
    """Keep inventory/command bindings consistent after a deliberate data mutation (honest observation)."""
    for name in names:
        value = receipt["inventories"][name]
        value["sha256"] = q.digest(q.preparation.canonical(value["rows"]))
        next(c for c in receipt["commands"] if c["id"] == value["command"])["output_sha256"] = value["sha256"]


def mark_failed(receipt, failed):
    for check in receipt["checks"]:
        check["passed"] = check["id"] not in failed
    receipt["assertions"] = counter(receipt["checks"])
    receipt["execution"] = "failed"


CANDIDATE_ARCHIVES = {identifier: sha(b"verified-candidate:" + identifier.encode()) for identifier in PACKAGE_IDS}
ROLLBACK_ARCHIVES = {identifier: sha(b"verified-rollback:" + identifier.encode()) for identifier in PACKAGE_IDS}
CANDIDATE_DLL, ROLLBACK_DLL = sha(b"verified-candidate-dll"), sha(b"verified-rollback-dll")


def selection_stub(version, archives):
    return {"version": version, "tag": "v" + version, "tag_commit": "1" * 40, "builds": {"version": "0.0.1", "commit": "2" * 40},
            "feed": FEED, "packages": [{"id": identifier, "archive_sha256": archives[identifier],
                                        "content_hash": content_hash(identifier), "repository_commit": COMMIT}
                                       for identifier in PACKAGE_IDS]}


def verified_context(rollback=True):
    """In-memory owner-selected context with verified candidate (and rollback) archive/DLL identities."""
    value = synthetic_inputs(selection_stub(VERSION, CANDIDATE_ARCHIVES),
                             rollback=selection_stub("0.0.0-p1r.synthetic", ROLLBACK_ARCHIVES) if rollback else None)
    value["fixture"] = None
    verified = {"candidate": {"archives": dict(CANDIDATE_ARCHIVES), "dlls": {CANDIDATE_DLL}}}
    if rollback:
        verified["rollback"] = {"archives": dict(ROLLBACK_ARCHIVES), "dlls": {ROLLBACK_DLL}}
    return {"inputs": {"scope": q.validate_inputs(value), "sha256": "6" * 64, "value": value}, "verified": verified}


def run_cli(*arguments, timeout=60):
    return subprocess.run([sys.executable, "-c", DRIVER, str(TOOLS), json.dumps(SYNTHETIC_BINDING), *map(str, arguments)],
                          capture_output=True, timeout=timeout)


class PacketFixture(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.scratch = tempfile.TemporaryDirectory(prefix="p1r-published-tests-")
        cls.root = Path(cls.scratch.name)
        cls.evidence, cls.selection = synthetic_evidence(cls.root / "synthetic-candidate-evidence")
        cls.rollback_evidence, cls.rollback = synthetic_evidence(cls.root / "synthetic-rollback-evidence", role="rollback",
                                                                 version="0.0.0-p1r.synthetic")
        cls.inputs_value = synthetic_inputs(cls.selection, rollback=cls.rollback)
        cls.inputs = cls.root / "synthetic-inputs.json"
        write(cls.inputs, cls.inputs_value)
        cls.decisions = cls.root / "synthetic-decisions.json"
        write(cls.decisions, synthetic_decisions(sha(cls.inputs.read_bytes())))
        cls.receipt_paths = []
        for value in (lane_receipt("metadata-write"), lane_receipt("reminder-recovery"), restore_receipt("candidate"),
                      restore_receipt("rollback"), cleanup_receipt()):
            path = cls.root / ("synthetic-receipt-" + value["id"] + ".json")
            write(path, value)
            cls.receipt_paths.append(path)
        cls.none = cls.root / "prepare-without-inputs"
        q.create_packet(cls.none)
        cls.full = cls.root / "prepare-synthetic"
        cls.full_packet = q.create_packet(cls.full, inputs=cls.inputs, decisions=cls.decisions, candidate_evidence=cls.evidence,
                                          rollback_evidence=cls.rollback_evidence, receipts=cls.receipt_paths,
                                          verifier=synthetic_verifier)

    @classmethod
    def tearDownClass(cls):
        cls.scratch.cleanup()

    @contextlib.contextmanager
    def changed(self, directory, name, mutate, reseal=True):
        path = directory / name
        original = path.read_bytes()
        index = (directory / "SHA256SUMS").read_bytes()
        value = json.loads(original)
        mutate(value)
        q.write_json(path, value)
        if reseal:
            preparation.seal(directory)
        try:
            yield
        finally:
            path.write_bytes(original)
            (directory / "SHA256SUMS").write_bytes(index)

    def receipt_name(self, group, lane):
        packet = q.parse_json((self.full / "packet.json").read_bytes())
        return next(row for row in packet["lanes"][group] if row["id"] == lane)["receipts"][0]


class InventoryAndInputTests(PacketFixture):
    def testCanonicalInventoryMatchesTheHistoricalContract(self):
        tree = ast.parse((q.ROOT / preparation.VERIFIER).read_bytes())
        historical = next(ast.literal_eval(node.value) for node in tree.body if isinstance(node, ast.Assign)
                          and any(getattr(target, "id", None) == "SCENARIOS" for target in node.targets))
        self.assertEqual(q.SCENARIOS, historical)
        self.assertEqual(len(q.SCENARIOS), 17)
        self.assertEqual(len(q.FAMILIES), 7)
        self.assertTrue(set(q.FAMILIES) <= set(q.SCENARIOS))
        self.assertEqual(q.ADDITIONS, ("reminder-recovery", "logical-event-evolution"))

    def testMissingOwnerInputsRefuseEveryDependentLane(self):
        result = q.validate_packet(self.none)
        self.assertTrue(result["valid"])
        for key in ("technically_qualified", "decisions_complete", "qualified", "capable_rollback_qualified", "p1r_usable"):
            self.assertFalse(result[key])
        self.assertIn(q.R_INPUTS, result["refusals"])
        self.assertEqual(result["recovery"], q.FREEZE)
        packet = q.parse_json((self.none / "packet.json").read_bytes())
        self.assertIsNone(packet["inputs"])
        self.assertEqual(packet["downstream_gates"], q.DOWNSTREAM_GATES)
        for group, names in q.INVENTORIES.items():
            self.assertEqual([row["id"] for row in packet["lanes"][group]], list(names))
            for row in packet["lanes"][group]:
                self.assertEqual((row["execution"], row["compatibility"], row["assertions"], row["reason"]),
                                 ("unavailable", "unverified", None, q.R_INPUTS))
        self.assertEqual(len(result["unsatisfied"]), packet["evaluation"]["required_lanes"])

    def testDependentExecutionWithoutInputsIsRefusedNotDefaulted(self):
        for index, arguments in enumerate(({"candidate_evidence": self.evidence}, {"rollback_evidence": self.rollback_evidence},
                                           {"decisions": self.decisions}, {"receipts": self.receipt_paths[:1]})):
            out = self.root / f"refused-dependent-{index}"
            packet = q.create_packet(out, **arguments, verifier=synthetic_verifier)
            self.assertEqual(packet["errors"], ["dependent execution refused: " + q.R_INPUTS])
            self.assertIsNone(packet["inputs"])
            self.assertFalse((out / "source-binding.json").exists())
            self.assertTrue(all(row["execution"] == "unavailable" for rows in packet["lanes"].values() for row in rows))
            self.assertFalse(packet["evaluation"]["qualified"])
            with self.assertRaisesRegex(q.InvalidPacket, "refused or incomplete"):
                q.validate_packet(out)

    def testMalformedOrIncompleteOwnerInputsRefused(self):
        mutations = (
            ("unknown member", lambda v: v.__setitem__("selected_tuple", "latest")),
            ("missing member", lambda v: v.pop("operational_profile")),
            ("default candidate", lambda v: v.__setitem__("candidate", None)),
            ("version drift", lambda v: v["candidate"].__setitem__("tag", "v9.9.9")),
            ("short commit", lambda v: v["candidate"].__setitem__("tag_commit", "abc")),
            ("duplicate package", lambda v: v["candidate"]["packages"].append(copy.deepcopy(v["candidate"]["packages"][0]))),
            ("foreign package", lambda v: v["candidate"]["packages"][0].__setitem__("id", "Contoso.Server")),
            ("plain feed", lambda v: v["candidate"].__setitem__("feed", "http://synthetic.invalid/v3/index.json")),
            ("unknown addition", lambda v: v.__setitem__("selected_additions", ["guessed-addition"])),
            ("reordered additions", lambda v: v.__setitem__("selected_additions", list(reversed(q.ADDITIONS)))),
            ("unaccepted instrumentation", lambda v: v["assertion_instrumentation"].__setitem__("accepted_by", "developer")),
            ("same rollback", lambda v: v.__setitem__("rollback", copy.deepcopy(v["candidate"]))),
            ("unknown marker", lambda v: v.__setitem__("fixture", "looks-real")),
        )
        for name, mutate in mutations:
            with self.subTest(name):
                value = copy.deepcopy(self.inputs_value)
                mutate(value)
                with self.assertRaises(q.InvalidPacket):
                    q.validate_inputs(value)
        path = self.root / "duplicate-member-inputs.json"
        path.write_text('{"schema": "a", "schema": "b"}')
        out = self.root / "refused-malformed-inputs"
        packet = q.create_packet(out, inputs=path)
        self.assertEqual(packet["errors"], ["duplicate JSON member"])
        with self.assertRaises(q.InvalidPacket):
            q.validate_packet(out)

    def testDecisionRolesAndFixtureMarkedApprovalsAreIncomplete(self):
        base = synthetic_decisions("6" * 64)
        for name, mutate in (("missing role", lambda v: v["decisions"].pop()),
                             ("duplicate role", lambda v: v["decisions"].__setitem__(1, copy.deepcopy(v["decisions"][0]))),
                             ("reordered roles", lambda v: v["decisions"].reverse())):
            with self.subTest(name):
                value = copy.deepcopy(base)
                mutate(value)
                with self.assertRaisesRegex(q.InvalidPacket, "missing, duplicate or reordered owner decision"):
                    q.validate_decisions(value)
        # In-memory owner-selected inputs: approved, conformant but fixture-marked decisions grant nothing.
        value = copy.deepcopy(self.inputs_value)
        value["fixture"] = None
        inputs = {"scope": q.validate_inputs(value), "sha256": "6" * 64, "value": value}
        marked = {"scope": q.validate_decisions(base), "value": base}
        self.assertEqual(marked["scope"], "tooling-synthetic")
        self.assertFalse(q.decisions_complete(inputs, marked))
        self.assertIn("synthetic tooling decisions grant no acceptance", q.refusals(inputs, marked))
        unmarked = dict(copy.deepcopy(base), fixture=None)
        self.assertTrue(q.decisions_complete(inputs, {"scope": q.validate_decisions(unmarked), "value": unmarked}))

    def testRollbackEvidenceWithoutSelectedRollbackRefused(self):
        path = self.root / "synthetic-inputs-no-rollback.json"
        write(path, synthetic_inputs(self.selection))
        out = self.root / "refused-rollback"
        packet = q.create_packet(out, inputs=path, rollback_evidence=self.rollback_evidence, verifier=synthetic_verifier)
        self.assertEqual(packet["errors"], ["rollback execution refused: " + q.R_NO_ROLLBACK])
        self.assertEqual(packet["lanes"]["packages"][1]["reason"], q.R_NO_ROLLBACK)
        self.assertIn(q.R_NO_ROLLBACK, packet["refusals"])

    def testExistingOutputRefusedWithoutChangingBytes(self):
        before = {p: p.read_bytes() for p in self.none.rglob("*") if p.is_file()}
        with self.assertRaisesRegex(q.InvalidPacket, "already exists"):
            q.create_packet(self.none)
        self.assertEqual(before, {p: p.read_bytes() for p in self.none.rglob("*") if p.is_file()})
        link = self.root / "output-link"
        link.symlink_to(self.root / "missing-target")
        with self.assertRaisesRegex(q.InvalidPacket, "already exists"):
            q.create_packet(link)

    def testMissingDuplicateOrReorderedInventoryRejected(self):
        mutations = [lambda v, g=group: v["lanes"][g].pop() for group in q.INVENTORIES]
        mutations += [lambda v: v["lanes"]["scenarios"].append(copy.deepcopy(v["lanes"]["scenarios"][0])),
                      lambda v: v["lanes"]["additions"].reverse(),
                      lambda v: v["lanes"].pop("recovery")]
        for index, mutate in enumerate(mutations):
            with self.subTest(index=index), self.changed(self.full, "packet.json", mutate):
                with self.assertRaises(q.InvalidPacket):
                    q.validate_packet(self.full)

    def testFamiliesMustMirrorTheirScenarios(self):
        def diverge(value):
            row = value["lanes"]["families"][1]
            row.update(execution="unavailable", compatibility="unverified", scope=None, assertions=None, receipts=[],
                       reason=q.R_NOT_EXECUTED)
        with self.changed(self.full, "packet.json", diverge):
            with self.assertRaisesRegex(q.InvalidPacket, "family dispositions"):
                q.validate_packet(self.full)

    def testInventedQualificationRefusedEvenWhenResealed(self):
        for key in ("technically_qualified", "decisions_complete", "qualified", "capable_rollback_qualified"):
            with self.subTest(key), self.changed(self.full, "packet.json", lambda v, k=key: v["evaluation"].__setitem__(k, True)):
                with self.assertRaisesRegex(q.InvalidPacket, "invented or stale"):
                    q.validate_packet(self.full)
        with self.changed(self.full, "packet.json", lambda v: v["evaluation"].__setitem__("p1r_usable", True)):
            with self.assertRaisesRegex(q.InvalidPacket, "invented qualification"):
                q.validate_packet(self.full)
        with self.changed(self.full, "packet.json", lambda v: v["evaluation"].__setitem__("recovery", "writable-rollback")):
            with self.assertRaises(q.InvalidPacket):
                q.validate_packet(self.full)

    def testUnboundPassingLaneClaimsRefused(self):
        def claim(value):
            row = value["lanes"]["scenarios"][0]
            row.update(execution="passed", compatibility="compatible", scope="operational",
                       assertions={"attempted": 3, "passed": 3, "failed": 0}, reason=None)
        with self.changed(self.full, "packet.json", claim):
            with self.assertRaisesRegex(q.InvalidPacket, "unbound evidence"):
                q.validate_packet(self.full)

    def testDownstreamGatesAndRefusalsCannotChange(self):
        for mutate in (lambda v: v["downstream_gates"].__setitem__("independent-readiness", "READY"),
                       lambda v: v["downstream_gates"].pop("story-8.11"),
                       lambda v: v["refusals"].clear()):
            with self.changed(self.full, "packet.json", mutate):
                with self.assertRaises(q.InvalidPacket):
                    q.validate_packet(self.full)


class PackageEvidenceTests(PacketFixture):
    def testSyntheticPackagesPassChecksButRetainToolingOnlyScope(self):
        result = q.validate_packet(self.full)
        self.assertTrue(result["valid"])
        self.assertFalse(result["technically_qualified"])
        self.assertFalse(result["qualified"])
        self.assertFalse(result["p1r_usable"])
        self.assertEqual(result["evidence_scopes"], ["tooling-synthetic"])
        packet = q.parse_json((self.full / "packet.json").read_bytes())
        for row in packet["lanes"]["packages"]:
            self.assertEqual((row["execution"], row["compatibility"], row["scope"]), ("passed", "unverified", "tooling-synthetic"))
            self.assertGreater(row["assertions"]["attempted"], 20)
            self.assertEqual(row["assertions"]["failed"], 0)
        self.assertIn("packages/candidate-packages", result["unsatisfied"])
        self.assertIn("packages/rollback-packages", result["unsatisfied"])
        self.assertEqual(result["package_assertions"], sum(row["assertions"]["attempted"] for row in packet["lanes"]["packages"]))

    def run_lane(self, name, mutate=None, inputs=None, selection=None, verifier=synthetic_verifier):
        evidence, generated = synthetic_evidence(self.root / ("synthetic-evidence-" + name))
        selection = copy.deepcopy(selection or generated)
        if mutate is not None:
            mutate(evidence, selection)
        path = self.root / f"synthetic-inputs-{name}.json"
        write(path, inputs(selection) if inputs else synthetic_inputs(selection))
        out = self.root / ("lane-" + name)
        packet = q.create_packet(out, inputs=path, candidate_evidence=evidence, verifier=verifier)
        return out, packet

    def testRemovingInputMarkerCannotPromoteSyntheticArchives(self):
        out, packet = self.run_lane("unmarked-inputs", inputs=lambda s: synthetic_inputs(s, fixture=None))
        row = packet["lanes"]["packages"][0]
        self.assertEqual((row["execution"], row["scope"], row["compatibility"]), ("passed", "tooling-synthetic", "unverified"))
        result = q.validate_packet(out)
        self.assertFalse(result["technically_qualified"])

        def strip(evidence, selection):
            manifest = json.loads((evidence / "evidence.json").read_text())
            manifest["fixture"] = None
            write(evidence / "evidence.json", manifest)
            for package in selection["packages"]:
                lower = package["id"].lower()
                archive = evidence / "packages" / lower / VERSION / f"{lower}.{VERSION}.nupkg"
                package["archive_sha256"] = sha(synthetic_archive(archive, package["id"], VERSION, marker=False))
        out, packet = self.run_lane("unmarked-archives", strip, inputs=lambda s: synthetic_inputs(s, fixture=None))
        row = packet["lanes"]["packages"][0]
        # Only the injected verifier now distinguishes the fixture; it alone keeps tooling scope.
        self.assertEqual((row["execution"], row["scope"], row["compatibility"]), ("passed", "tooling-synthetic", "unverified"))
        self.assertFalse(q.validate_packet(out)["technically_qualified"])

        def relabelled(archive, output, cwd):
            return {**synthetic_verifier(archive, output, cwd), "verifier": q.VERIFIER}
        # A double that labels itself as the actual verifier is still labelled by the harness, not by itself.
        out, packet = self.run_lane("relabelled-verifier", strip, inputs=lambda s: synthetic_inputs(s, fixture=None),
                                    verifier=relabelled)
        row = packet["lanes"]["packages"][0]
        self.assertEqual((row["execution"], row["scope"], row["compatibility"]), ("passed", "tooling-synthetic", "unverified"))
        receipt = q.parse_json((out / row["receipts"][0]).read_bytes())
        self.assertEqual({p["signature"]["verifier"] for p in receipt["observation"]["packages"]}, {"injected-verifier"})
        self.assertFalse(q.validate_packet(out)["technically_qualified"])

    def testSubstitutionsAndProvenanceDriftAreRetainedFailures(self):
        server = "Hexalith.EventStore.Server"
        archive = Path("packages/hexalith.eventstore.server") / VERSION / f"hexalith.eventstore.server.{VERSION}.nupkg"

        def edit(relative, change):
            def mutate(evidence, selection):
                path = evidence / relative
                value = json.loads(path.read_text())
                change(value, evidence, selection)
                write(path, value)
            return mutate

        def swap(evidence, selection):
            (evidence / archive).write_bytes(synthetic_archive(self.root / "swap.nupkg", server, VERSION, commit="9" * 40))

        def symlink(evidence, selection):
            target = evidence / "elsewhere.nupkg"
            (evidence / archive).rename(target)
            (evidence / archive).symlink_to(target)

        def missing(evidence, selection):
            (evidence / archive).unlink()

        def unsigned(evidence, selection):
            data = synthetic_archive(evidence / archive, server, VERSION, signature=False)
            next(p for p in selection["packages"] if p["id"] == server)["archive_sha256"] = sha(data)

        def project_reference(value, evidence, selection):
            value["libraries"][f"{server}/{VERSION}"]["type"] = "project"

        def version_drift(value, evidence, selection):
            library = value["libraries"].pop(f"{server}/{VERSION}")
            value["libraries"][f"{server}/9.9.9"] = library

        def shared_cache(value, evidence, selection):
            value["project"]["restore"]["packagesPath"] = str(Path.home() / ".nuget/packages") + "/"

        def debug_output(evidence, selection):
            manifest = json.loads((evidence / "evidence.json").read_text())
            manifest["projects"][0]["output"] = "consumer/Host/bin/Debug/net10.0"
            write(evidence / "evidence.json", manifest)

        def foreign_loaded(value, evidence, selection):
            other = evidence / "consumer/Host/bin/Release/net10.0/other.dll"
            other.write_bytes(b"locally built source output")
            value["assemblies"][1].update(path=str(other), sha256=sha(other.read_bytes()))

        def outside_loaded(value, evidence, selection):
            value["assemblies"][1]["path"] = str(q.ROOT / "src/Hexalith.EventStore.Server/bin/Debug/net10.0/Hexalith.EventStore.Server.dll")

        def lock_drift(value, evidence, selection):
            value["dependencies"]["net10.0"][server]["contentHash"] = content_hash("drift")

        def rewrite(**options):
            def mutate(evidence, selection):
                data = synthetic_archive(evidence / archive, server, VERSION, **options)
                next(p for p in selection["packages"] if p["id"] == server)["archive_sha256"] = sha(data)
            return mutate

        def corrupt_deflate(evidence, selection):
            path = evidence / archive
            synthetic_archive(path, server, VERSION, compression=zipfile.ZIP_DEFLATED)
            with zipfile.ZipFile(path) as opened:
                offset = opened.getinfo(f"lib/net10.0/{server}.dll").header_offset
            data = bytearray(path.read_bytes())
            name_length, extra_length = struct.unpack("<HH", data[offset + 26:offset + 30])
            start = offset + 30 + name_length + extra_length
            data[start:start + 2] = b"\xff\xff"  # Reserved deflate block type: zlib.error on read.
            path.write_bytes(bytes(data))
            next(p for p in selection["packages"] if p["id"] == server)["archive_sha256"] = sha(bytes(data))

        def author_only(archive, output, cwd):
            # Exits 0, but the output never shows a repository signature.
            output.write_bytes(b"synthetic test double; Signature type: Author\n")
            return {"verifier": "synthetic-test-double", "argv": ["synthetic-verifier", str(archive)], "cwd": str(cwd),
                    "started_utc": q.stamp(), "finished_utc": q.stamp(), "exit_code": 0}

        def extra_library(value, evidence, selection):
            value["libraries"]["Synthetic.Unlocked/1.0.0"] = {"type": "package", "sha512": content_hash("unlocked")}

        def graph_without(*identifiers):
            def mutate(evidence, selection):
                for name, key in (("consumer/Host/obj/project.assets.json", "libraries"),
                                  ("consumer/Host/packages.lock.json", "dependencies")):
                    value = json.loads((evidence / name).read_text())
                    for identifier in identifiers:
                        if key == "libraries":
                            value[key].pop(f"{identifier}/{VERSION}")
                        else:
                            value[key]["net10.0"].pop(identifier)
                    write(evidence / name, value)
            return mutate

        def content_sha512(evidence, selection):
            assets = json.loads((evidence / "consumer/Host/obj/project.assets.json").read_text())
            lock = json.loads((evidence / "consumer/Host/packages.lock.json").read_text())
            assets["libraries"][f"{server}/{VERSION}"]["sha512"] = content_hash("drift")
            lock["dependencies"]["net10.0"][server]["contentHash"] = content_hash("drift")
            write(evidence / "consumer/Host/obj/project.assets.json", assets)
            write(evidence / "consumer/Host/packages.lock.json", lock)

        def obj_loaded(value, evidence, selection):
            target = evidence / f"consumer/Host/obj/{server}.dll"
            target.write_bytes((evidence / f"consumer/Host/bin/Release/net10.0/{server}.dll").read_bytes())
            next(a for a in value["assemblies"] if a["name"] == server)["path"] = str(target)

        def unconsumed(evidence, selection):
            client = "Hexalith.EventStore.Client"
            base = evidence / "packages" / client.lower() / VERSION
            data = synthetic_archive(base / f"{client.lower()}.{VERSION}.nupkg", client, VERSION)
            write(base / ".nupkg.metadata", {"version": 2, "contentHash": content_hash(client), "source": FEED})
            selection["packages"].append({"id": client, "archive_sha256": sha(data), "content_hash": content_hash(client),
                                          "repository_commit": COMMIT})

        cases = (
            ("swapped-archive", swap, server + ":archive-sha256"),
            ("symlinked-archive", symlink, server + ":archive-regular"),
            ("missing-archive", missing, server + ":archive-regular"),
            ("unsigned-archive", unsigned, server + ":signature-entry"),
            ("content-hash-drift", edit(f"packages/hexalith.eventstore.server/{VERSION}/.nupkg.metadata",
                                        lambda v, e, s: v.__setitem__("contentHash", content_hash("drift"))), server + ":content-hash"),
            ("feed-drift", edit(f"packages/hexalith.eventstore.server/{VERSION}/.nupkg.metadata",
                                lambda v, e, s: v.__setitem__("source", "https://other.invalid/v3/index.json")), server + ":restore-source"),
            ("repository-drift", lambda e, s: next(p for p in s["packages"] if p["id"] == server).__setitem__("repository_commit", "8" * 40),
             server + ":repository-commit"),
            ("source-dependency", edit("consumer/Host/obj/project.assets.json", project_reference), "Host:no-eventstore-source-dependency"),
            ("version-drift", edit("consumer/Host/obj/project.assets.json", version_drift), "Host:selected-versions-and-content"),
            ("shared-package-cache", edit("consumer/Host/obj/project.assets.json", shared_cache), "Host:isolated-package-folder"),
            ("debug-output", debug_output, "Host:release-configuration"),
            ("foreign-loaded-binary", edit("loaded/Host.json", foreign_loaded), "Host:loaded-verified-archive-bytes"),
            ("source-loaded-binary", edit("loaded/Host.json", outside_loaded), "Host:loaded-release-isolated-paths"),
            ("lock-drift", edit("consumer/Host/packages.lock.json", lock_drift), "Host:lock-agrees-with-restore-graph"),
            ("nuspec-source-leak", rewrite(description="src/Hexalith.EventStore.Server/Leak.cs"), server + ":nuspec-contract"),
            ("nuspec-identity", rewrite(nuspec_version="9.9.9"), server + ":nuspec-identity"),
            ("duplicate-entries", rewrite(duplicate=True), server + ":unique-archive-entries"),
            ("no-assemblies", rewrite(dll=False), server + ":assemblies-present"),
            ("corrupt-deflate", corrupt_deflate, server + ":nuspec-contract"),
            ("author-only-signature", None, server + ":repository-signature-verified", author_only),
            ("unlocked-library", edit("consumer/Host/obj/project.assets.json", extra_library), "Host:lock-covers-restore-graph"),
            ("no-eventstore-graph", graph_without(*PACKAGE_IDS), "Host:eventstore-packages-consumed"),
            ("content-sha512-drift", content_sha512, "Host:selected-versions-and-content"),
            ("no-loaded-eventstore", edit("loaded/Host.json", lambda v, e, s: v.__setitem__("assemblies", [])),
             "Host:loaded-eventstore-assemblies"),
            ("graph-lacks-loaded", graph_without(server), "Host:loaded-from-selected-graph"),
            ("obj-loaded-binary", edit("loaded/Host.json", obj_loaded), "Host:loaded-release-isolated-paths"),
            ("unconsumed-selection", unconsumed, "selected-packages-consumed"),
        )
        for name, mutate, failing, *verifier in cases:
            with self.subTest(name):
                out, packet = self.run_lane(name, mutate, verifier=verifier[0] if verifier else synthetic_verifier)
                self.assertEqual(packet["errors"], [])
                row = packet["lanes"]["packages"][0]
                self.assertEqual((row["execution"], row["compatibility"]), ("failed", "unverified"))
                self.assertGreater(row["assertions"]["failed"], 0)
                receipt = q.parse_json((out / row["receipts"][0]).read_bytes())
                self.assertIn({"id": failing, "passed": False}, receipt["checks"])
                result = q.validate_packet(out)
                self.assertIn("packages/candidate-packages", result["unsatisfied"])
                self.assertFalse(result["technically_qualified"])

    def testUnreadableEvidenceManifestRefusesTheInvocation(self):
        out, packet = self.run_lane("missing-manifest", lambda evidence, selection: (evidence / "evidence.json").unlink())
        self.assertEqual(packet["errors"], ["missing or substituted input"])
        self.assertEqual(packet["lanes"]["packages"][0]["execution"], "unavailable")
        with self.assertRaisesRegex(q.InvalidPacket, "refused or incomplete"):
            q.validate_packet(out)

    def testTamperedPackageReceiptAndRetainedCopiesRefused(self):
        name = self.receipt_name("packages", "candidate-packages")
        mutations = (
            lambda v: v["checks"][0].__setitem__("passed", False),
            lambda v: v.__setitem__("assertions", {"attempted": 99, "passed": 99, "failed": 0}),
            lambda v: v["observation"]["packages"][0].__setitem__("sha256", "0" * 64),
            lambda v: v["observation"]["packages"].pop(),
            lambda v: v["observation"]["projects"].pop(),
            lambda v: v.__setitem__("scope", "published-package"),
            lambda v: v.__setitem__("compatibility", "compatible"),
            lambda v: v["observation"]["packages"][0]["signature"]["output"].__setitem__("sha256", "0" * 64),
            lambda v: v["observation"].__setitem__("projects", None),
        )
        for index, mutate in enumerate(mutations):
            with self.subTest(index=index), self.changed(self.full, name, mutate):
                with self.assertRaises(q.InvalidPacket):
                    q.validate_packet(self.full)
        receipt = q.parse_json((self.full / name).read_bytes())
        # A fully consistent forgery that drops the only consumer project (receipt, row and seal all
        # updated) is still caught by the retained evidence manifest copy.
        forged = self.root / "forged-dropped-project"
        shutil.copytree(self.full, forged)
        value = q.parse_json((forged / name).read_bytes())
        dropped = value["observation"]["projects"].pop()["name"]
        value["checks"] = [c for c in value["checks"] if not c["id"].startswith(dropped + ":")]
        value["checks"][-1]["passed"] = False
        value.update(assertions=counter(value["checks"]), execution="failed")
        q.write_json(forged / name, value)
        packet = q.parse_json((forged / "packet.json").read_bytes())
        packet["lanes"]["packages"][0].update(execution="failed", assertions=value["assertions"])
        q.write_json(forged / "packet.json", packet)
        preparation.seal(forged)
        with self.assertRaisesRegex(q.InvalidPacket, "retained evidence manifest"):
            q.validate_packet(forged)
        copy_name = receipt["observation"]["projects"][0]["assets"]["path"]
        with self.changed(self.full, copy_name, lambda v: v["libraries"].clear()):
            with self.assertRaisesRegex(q.InvalidPacket, "retained consumer evidence hash mismatch"):
                q.validate_packet(self.full)

    def testPackageScopeSignalsAndPublishedBranch(self):
        # In-memory relabelling of a retained synthetic observation exercises the published-package branch;
        # packet creation itself can never produce this scope from synthetic fixtures.
        name = self.receipt_name("packages", "candidate-packages")
        clean = q.parse_json((self.full / name).read_bytes())
        observation = clean["observation"]
        observation["fixture"] = None
        for package in observation["packages"]:
            package["entries"]["synthetic_marker"] = package["nuspec"]["synthetic_marker"] = False
            package["signature"]["verifier"] = q.VERIFIER
        value = copy.deepcopy(self.inputs_value)
        value["fixture"] = None
        owner = {"scope": "owner-selected", "sha256": "6" * 64, "value": value}
        self.assertEqual(q.package_scope(observation, owner), "published-package")
        signals = (
            ("synthetic inputs", lambda o, i: i.__setitem__("scope", "tooling-synthetic")),
            ("evidence fixture", lambda o, i: o.__setitem__("fixture", q.SYNTHETIC)),
            ("archive marker entry", lambda o, i: o["packages"][0]["entries"].__setitem__("synthetic_marker", True)),
            ("nuspec marker tag", lambda o, i: o["packages"][1]["nuspec"].__setitem__("synthetic_marker", True)),
            ("injected verifier", lambda o, i: o["packages"][0]["signature"].__setitem__("verifier", "injected-verifier")),
        )
        for label, mutate in signals:
            with self.subTest(label):
                single, inputs = copy.deepcopy(observation), copy.deepcopy(owner)
                mutate(single, inputs)
                self.assertEqual(q.package_scope(single, inputs), "tooling-synthetic")
        missing = copy.deepcopy(observation)
        missing["packages"][0]["signature"] = None
        self.assertEqual(q.package_scope(missing, owner), "published-package")  # A missing archive is a failure, not a fixture.
        _, outcome = q.package_outcome(self.full, clean, value["candidate"], owner)
        self.assertEqual((outcome["execution"], outcome["compatibility"], outcome["scope"]),
                         ("passed", "compatible", "published-package"))
        failing = copy.deepcopy(clean)
        failing["observation"]["packages"][0]["sha256"] = "0" * 64
        _, outcome = q.package_outcome(self.full, failing, value["candidate"], owner)
        self.assertEqual((outcome["execution"], outcome["compatibility"], outcome["scope"]),
                         ("failed", "incompatible", "published-package"))

    def testValidateOnlyGuardsRefuseResealedForgeries(self):
        forged = self.root / "forged-without-inputs"
        shutil.copytree(self.full, forged)
        packet = q.parse_json((forged / "packet.json").read_bytes())
        packet.update(inputs=None, decisions=None, refusals=q.refusals(None, None))
        q.write_json(forged / "packet.json", packet)
        (forged / "inputs.json").unlink()
        (forged / "decisions.json").unlink()
        preparation.seal(forged)
        with self.assertRaisesRegex(q.InvalidPacket, "dependent evidence lacks owner execution inputs"):
            q.validate_packet(forged)
        name = self.receipt_name("packages", "candidate-packages")
        with self.changed(self.full, name, lambda v: v.__setitem__("started_utc", "1800-01-01T00:00:00+00:00")):
            with self.assertRaisesRegex(q.InvalidPacket, "package receipt interval outside invocation"):
                q.validate_packet(self.full)
        escaped = self.root / "forged-escaped-copy"
        shutil.copytree(self.full, escaped)
        receipt = q.parse_json((escaped / name).read_bytes())
        assets = receipt["observation"]["projects"][0]["assets"]
        shutil.copyfile(escaped / assets["path"], escaped / "receipts/escaped-assets.json")
        assets["path"] = "receipts/escaped-assets.json"
        q.write_json(escaped / name, receipt)
        preparation.seal(escaped)
        with self.assertRaisesRegex(q.InvalidPacket, "retained package evidence escapes its receipt"):
            q.validate_packet(escaped)

    @unittest.skipUnless(shutil.which("dotnet"), "actual NuGet signature verifier unavailable")
    def testActualSignatureVerifierRefusesSyntheticSignature(self):
        directory = self.root / "actual-verifier"
        directory.mkdir()
        archive = directory / "hexalith.eventstore.server.nupkg"
        synthetic_archive(archive, "Hexalith.EventStore.Server", VERSION)
        result = q.default_verifier(archive, directory / "verify.txt", directory)
        self.assertEqual(result["verifier"], q.VERIFIER)
        self.assertNotEqual(result["exit_code"], 0)
        self.assertNotIn(q.REPOSITORY_SIGNATURE, (directory / "verify.txt").read_text(errors="replace"))


class LaneReceiptTests(PacketFixture):
    def testSyntheticLaneReceiptsCannotSatisfyPublishedLanes(self):
        packet = q.parse_json((self.full / "packet.json").read_bytes())
        row = next(r for r in packet["lanes"]["scenarios"] if r["id"] == "metadata-write")
        self.assertEqual((row["execution"], row["compatibility"], row["scope"]), ("passed", "unverified", "tooling-synthetic"))
        self.assertEqual(next(r for r in packet["lanes"]["families"] if r["id"] == "metadata-write"), row)
        self.assertIn("scenarios/metadata-write", packet["evaluation"]["unsatisfied"])
        self.assertIn("families/metadata-write", packet["evaluation"]["unsatisfied"])
        addition = packet["lanes"]["additions"][0]
        self.assertEqual((addition["execution"], addition["scope"]), ("passed", "tooling-synthetic"))
        self.assertIn("additions/reminder-recovery", packet["evaluation"]["unsatisfied"])

    def testUnsupportedOperationRetainsHonestRefusal(self):
        unsupported = lane_case("old-dispatcher-fenced", "unsupported", "refusal", "refusal", "incompatible", "same", "same")
        outcome = q.lane_outcome(lane_receipt("mixed-api", [unsupported]))
        self.assertEqual((outcome["execution"], outcome["compatibility"]), ("passed", "unverified"))
        # The same contract at published scope: a passing honest refusal is still never compatible.
        published = lane_receipt("mixed-api", [copy.deepcopy(unsupported)], scope="published-package", compatibility="incompatible")
        self.assertEqual(q.lane_outcome(published)["compatibility"], "incompatible")
        published["compatibility"] = "compatible"
        with self.assertRaisesRegex(q.InvalidPacket, "compatibility differs"):
            q.lane_outcome(published)
        claims = (
            ("claimed compatible case", lane_case("x", "unsupported", "refusal", "refusal", "compatible", "same", "same"), "claimed compatibility"),
            ("expected effect", lane_case("x", "unsupported", "effect", "effect", "incompatible"), "cannot expect an effect"),
        )
        for name, case, message in claims:
            with self.subTest(name), self.assertRaisesRegex(q.InvalidPacket, message):
                q.lane_outcome(lane_receipt("mixed-api", [case]))
        for name, case in (("effect after refusal expected", lane_case("x", "unsupported", "refusal", "effect", "incompatible")),
                           ("refusal changed inventory", lane_case("x", "unsupported", "refusal", "refusal", "incompatible", "a", "b"))):
            with self.subTest(name):
                receipt = lane_receipt("mixed-api", [case])
                with self.assertRaisesRegex(q.InvalidPacket, "claimed lane outcome"):
                    q.lane_outcome(receipt)
                receipt["execution"] = "failed"
                self.assertEqual(q.lane_outcome(receipt)["execution"], "failed")

    def testZeroSkippedAndUnmeasuredCountsRemainNonpassing(self):
        receipt = lane_receipt()
        receipt["cases"][0].update(checks=[], assertions=None)
        receipt["assertions"] = None
        with self.assertRaisesRegex(q.InvalidPacket, "claimed lane outcome"):
            q.lane_outcome(receipt)
        receipt["execution"] = "failed"
        self.assertEqual(q.lane_outcome(receipt)["assertions"], None)
        for name, mutate, message in (
            ("skipped", lambda v: v.__setitem__("execution", "skipped"), "claimed lane outcome"),
            ("zero counter", lambda v: v["cases"][0].update(checks=[], assertions={"attempted": 0, "passed": 0, "failed": 0}),
             "zero or inconsistent"),
            ("invented counter", lambda v: v.__setitem__("assertions", {"attempted": 9, "passed": 9, "failed": 0}), "counter differs"),
            ("no cases", lambda v: v.__setitem__("cases", []), "no executed case"),
            ("nonzero exit", lambda v: v.__setitem__("exit_code", 1), "claimed lane outcome"),
        ):
            with self.subTest(name):
                value = lane_receipt()
                mutate(value)
                with self.assertRaisesRegex(q.InvalidPacket, message):
                    q.lane_outcome(value)

    def testPublishedOrOperationalClaimsNeedOwnerSelectedVerifiedBindings(self):
        context = {"inputs": {"scope": "tooling-synthetic", "sha256": "6" * 64, "value": self.inputs_value}, "verified": {}}
        receipt = lane_receipt(scope="published-package", compatibility="compatible")
        with self.assertRaisesRegex(q.InvalidPacket, "lacks owner-selected inputs"):
            q.receipt_outcome(receipt, context)
        owner = {"inputs": {"scope": "owner-selected", "sha256": "6" * 64, "value": self.inputs_value}, "verified": {}}
        receipt["inputs_sha256"], receipt["instrumentation"] = "6" * 64, "synthetic-counter"
        with self.assertRaisesRegex(q.InvalidPacket, "verified published package binding"):
            q.receipt_outcome(receipt, owner)
        restore = restore_receipt(scope="operational")
        restore["cleanup"]["compatibility"] = "compatible"
        restore["compatibility"] = "compatible"
        with self.assertRaisesRegex(q.InvalidPacket, "operational evidence lacks owner-selected inputs"):
            q.receipt_outcome(restore, context)
        no_profile = copy.deepcopy(owner)
        no_profile["inputs"]["value"] = synthetic_inputs(self.selection, profile=False)
        restore["inputs_sha256"] = restore["cleanup"]["inputs_sha256"] = "6" * 64
        with self.assertRaisesRegex(q.InvalidPacket, "operational profile not selected"):
            q.receipt_outcome(restore, no_profile)

    @staticmethod
    def bound_lane(scope="published-package", packages=None, loaded=None):
        """A lane receipt bound to the in-memory verified identities of verified_context()."""
        receipt = lane_receipt(scope=scope, compatibility="compatible")
        receipt.update(inputs_sha256="6" * 64, instrumentation="synthetic-counter",
                       profile=copy.deepcopy(PROFILE) if scope == "operational" else None)
        receipt["identities"]["packages"] = [{"id": k, "sha256": v} for k, v in (packages or CANDIDATE_ARCHIVES).items()]
        receipt["identities"]["loaded_assemblies"] = [{"name": "Hexalith.EventStore.Server", "version": "0.0.1.0",
                                                       "path": "/synthetic/Hexalith.EventStore.Server.dll", "sha256": value}
                                                      for value in (loaded or [CANDIDATE_DLL])]
        return receipt

    @staticmethod
    def bound_restore(role="candidate", archives=None):
        receipt = restore_receipt(role=role, scope="operational")
        receipt["inputs_sha256"] = receipt["cleanup"]["inputs_sha256"] = "6" * 64
        receipt["profile"] = copy.deepcopy(PROFILE)
        receipt["cleanup"]["profile"] = copy.deepcopy(PROFILE)
        receipt["packages"] = dict(archives or (CANDIDATE_ARCHIVES if role == "candidate" else ROLLBACK_ARCHIVES))
        receipt["cleanup"]["compatibility"] = receipt["compatibility"] = "compatible"
        return receipt

    def testVerifiedBindingsAcceptMatchesAndRefuseEachDrift(self):
        context = verified_context()
        for scope in ("published-package", "operational"):
            group, lane, outcome = q.receipt_outcome(self.bound_lane(scope), context)
            self.assertEqual((group, lane, outcome["compatibility"], outcome["scope"]), ("scenarios", "metadata-write", "compatible", scope))
        # Cross-version: verified rollback archives may appear alongside the complete candidate binding.
        both = self.bound_lane(packages=None, loaded=[CANDIDATE_DLL, ROLLBACK_DLL])
        both["identities"]["packages"] += [{"id": k, "sha256": v} for k, v in ROLLBACK_ARCHIVES.items()]
        self.assertEqual(q.receipt_outcome(both, context)[2]["compatibility"], "compatible")
        refusals = (
            ("rollback-only archives", self.bound_lane(packages=ROLLBACK_ARCHIVES, loaded=[ROLLBACK_DLL]), "verified published package binding"),
            ("rollback-only DLLs", self.bound_lane(loaded=[ROLLBACK_DLL]), "loaded assemblies differ"),
            ("partial candidate", self.bound_lane(packages={PACKAGE_IDS[0]: CANDIDATE_ARCHIVES[PACKAGE_IDS[0]]}), "verified published package binding"),
            ("unverified archive", self.bound_lane(packages={**CANDIDATE_ARCHIVES, PACKAGE_IDS[0]: "e" * 64}), "verified published package binding"),
            ("unverified DLL", self.bound_lane(loaded=[CANDIDATE_DLL, "e" * 64]), "loaded assemblies differ"),
        )
        for name, receipt, message in refusals:
            with self.subTest(name), self.assertRaisesRegex(q.InvalidPacket, message):
                q.receipt_outcome(receipt, context)
        mutations = (
            ("debug configuration", lambda r: r["identities"].__setitem__("configuration", "Debug"), "verified published package binding"),
            ("instrumentation mismatch", lambda r: r.__setitem__("instrumentation", "another-counter"), "accepted instrumentation"),
        )
        for name, mutate, message in mutations:
            with self.subTest(name):
                receipt = self.bound_lane()
                mutate(receipt)
                with self.assertRaisesRegex(q.InvalidPacket, message):
                    q.receipt_outcome(receipt, context)
        operational = self.bound_lane("operational")
        operational["profile"]["runtime_version"] = "9.9.9"
        with self.assertRaisesRegex(q.InvalidPacket, "selected profile"):
            q.receipt_outcome(operational, context)
        with self.assertRaisesRegex(q.InvalidPacket, "verified published package binding"):
            q.receipt_outcome(self.bound_lane(), dict(context, verified={"rollback": context["verified"]["rollback"]}))

        for role in q.ROLES:
            group, lane, outcome = q.receipt_outcome(self.bound_restore(role), context)
            self.assertEqual((group, lane, outcome["compatibility"], outcome["scope"]), ("recovery", role + "-restore", "compatible", "operational"))
        drifted = self.bound_restore()
        drifted["profile"]["runtime_version"] = drifted["cleanup"]["profile"]["runtime_version"] = "9.9.9"
        with self.assertRaisesRegex(q.InvalidPacket, "selected profile"):
            q.receipt_outcome(drifted, context)
        with self.assertRaisesRegex(q.InvalidPacket, "restore writer lacks verified published package binding"):
            q.receipt_outcome(self.bound_restore("candidate", ROLLBACK_ARCHIVES), context)
        with self.assertRaisesRegex(q.InvalidPacket, "restore writer lacks verified published package binding"):
            q.receipt_outcome(self.bound_restore("candidate", {PACKAGE_IDS[0]: "e" * 64}), context)

    def testMalformedImportedReceiptIsRetainedAsRefusal(self):
        cases = (("unhashable backup command", lambda v: v["backup"].__setitem__("command", [4])),
                 ("list-valued cleanup ids", lambda v: v["cleanup"]["attempts"][0].__setitem__("targeted", [["synthetic-container"]])))
        for index, (name, mutate) in enumerate(cases):
            with self.subTest(name):
                value = restore_receipt()
                mutate(value)
                path = self.root / f"synthetic-malformed-receipt-{index}.json"
                write(path, value)
                out = self.root / f"malformed-receipt-{index}"
                packet = q.create_packet(out, inputs=self.inputs, receipts=[path])
                self.assertEqual(packet["errors"], ["TypeError"])
                self.assertTrue((out / "SHA256SUMS").is_file())
                self.assertEqual(packet["lanes"]["recovery"][0]["execution"], "unavailable")
                with self.assertRaisesRegex(q.InvalidPacket, "refused or incomplete"):
                    q.validate_packet(out)

    def testImportRefusesUnselectedAdditionsDuplicatesAndUnknownContracts(self):
        path = self.root / "synthetic-inputs-without-additions.json"
        write(path, synthetic_inputs(self.selection, additions=()))
        packet = q.create_packet(self.root / "unselected-addition", inputs=path, receipts=[self.receipt_paths[1]])
        self.assertEqual(packet["errors"], [q.R_NOT_SELECTED])
        duplicate = q.create_packet(self.root / "duplicate-receipt", inputs=self.inputs,
                                    receipts=[self.receipt_paths[0], self.receipt_paths[0]])
        self.assertEqual(duplicate["errors"], ["duplicate receipt identity"])
        other = self.root / "synthetic-second-metadata-write.json"
        write(other, lane_receipt("metadata-write"))
        twice = q.create_packet(self.root / "duplicate-lane", inputs=self.inputs, receipts=[self.receipt_paths[0], other])
        self.assertEqual(twice["errors"], ["duplicate lane evidence"])
        unknown = self.root / "synthetic-unknown.json"
        write(unknown, {"schema": "hexalith.p1r.qualified.v1", "id": "0" * 32})
        refused = q.create_packet(self.root / "unknown-contract", inputs=self.inputs, receipts=[unknown])
        self.assertEqual(refused["errors"], ["unsupported receipt contract"])

    def testTamperedImportedReceiptRefused(self):
        name = self.receipt_name("scenarios", "metadata-write")
        for mutate in (lambda v: v.__setitem__("compatibility", "compatible"),
                       lambda v: v.__setitem__("scope", "operational"),
                       lambda v: v["cases"][0].__setitem__("outcome", "refusal"),
                       lambda v: v.__setitem__("lane", "query-wire")):
            with self.changed(self.full, name, mutate):
                with self.assertRaises(q.InvalidPacket):
                    q.validate_packet(self.full)


class RuntimeContractTests(PacketFixture):
    def testSyntheticRestoreSatisfiesContractButNeverOperationalLane(self):
        outcome = runtime.restore_outcome(restore_receipt())
        self.assertEqual((outcome["execution"], outcome["compatibility"], outcome["scope"]),
                         ("passed", "unverified", "tooling-synthetic"))
        self.assertEqual(outcome["assertions"]["attempted"], len(runtime.RESTORE_CHECKS))
        self.assertEqual(outcome["bookkeeping_rows"], 4)
        packet = q.parse_json((self.full / "packet.json").read_bytes())
        for row in packet["lanes"]["recovery"]:
            self.assertEqual((row["execution"], row["scope"], row["compatibility"]), ("passed", "tooling-synthetic", "unverified"))
            self.assertIn("recovery/" + row["id"], packet["evaluation"]["unsatisfied"])

    def testRestoreInvariantViolationsAreRetainedFailures(self):
        def later(receipt, select, field, value):
            for name in ("appended", "restarted"):
                next(r for r in receipt["inventories"][name]["rows"] if select(r))[field] = value
            rehash(receipt, "appended", "restarted")

        def event_hash(receipt):
            later(receipt, lambda r: r["key"].endswith("events:007"), "sha256", "e" * 64)

        def source_shape(receipt):
            # The backup itself lacks the retained shape (snapshot 8); restored stays an exact copy of it.
            for name in ("source", "restored"):
                next(r for r in receipt["inventories"][name]["rows"] if r["kind"] == "snapshot")["sequence"] = 8
            rehash(receipt, "source", "restored")

        def floor_lost(receipt):
            later(receipt, lambda r: r["kind"] == "metadata" and r["tenant"] == "tenant-a", "floor", None)

        def second_tenant(receipt):
            next(r for r in receipt["inventories"]["restarted"]["rows"] if r["tenant"] == "tenant-b")["sha256"] = "f" * 64
            rehash(receipt, "restarted")

        def snapshot(receipt):
            later(receipt, lambda r: r["kind"] == "snapshot", "sha256", "d" * 64)

        def restored_differs(receipt):
            receipt["inventories"]["restored"]["rows"].pop()
            rehash(receipt, "restored")

        def stale_restart(receipt):
            receipt["commands"][11]["processes"] = copy.deepcopy(receipt["commands"][7]["processes"])

        def reused_database(receipt):
            receipt["commands"][4]["database"] = receipt["databases"]["source"]

        def no_append(receipt):
            receipt["observations"]["appended_sequence"] = 12

        def cleanup_failed(receipt):
            receipt["cleanup"]["attempts"][-1]["remaining"] = ["synthetic-container"]
            mark = [c for c in receipt["cleanup"]["checks"]]
            for check in mark:
                check["passed"] = check["id"] != "no-owned-remaining"
            receipt["cleanup"]["assertions"] = counter(mark)
            receipt["cleanup"]["execution"] = "failed"

        cases = (
            ("prior event rewritten", event_hash, {"prior-event-hashes-preserved"}),
            ("retained floor lost", floor_lost, {"retained-floor-preserved"}),
            ("second tenant changed", second_tenant, {"second-tenant-preserved"}),
            ("snapshot changed", snapshot, {"snapshot-preserved"}),
            ("restore not exact", restored_differs, {"restored-equals-backup-source", "second-tenant-preserved"}),
            ("restart reused writer", stale_restart, {"fresh-restart"}),
            ("database not fresh", reused_database, {"fresh-restored-database"}),
            ("append not observed", no_append, {"appended-thirteen"}),
            ("owned cleanup incomplete", cleanup_failed, {"owned-cleanup-complete"}),
            ("source lacks retained shape", source_shape, {"retained-source-shape"}),
            ("head twelve not reconstructed", lambda r: r["observations"].__setitem__("state_before_append", 11),
             {"reconstructed-head-twelve"}),
            ("thirteen not reconstructed", lambda r: r["observations"].__setitem__("state_after_restart", 12),
             {"restart-reconstructs-thirteen"}),
            ("writer never stopped", lambda r: r["commands"][10].__setitem__("exit_code", 1), {"fresh-restart"}),
            ("writer stopped elsewhere", lambda r: r["commands"][10].__setitem__("database", r["databases"]["source"]),
             {"fresh-restart"}),
            ("no replay after restart", lambda r: r["commands"][12].__setitem__("exit_code", 1), {"restart-reconstructs-thirteen"}),
            ("replay elsewhere", lambda r: r["commands"][12].__setitem__("database", r["databases"]["source"]),
             {"restart-reconstructs-thirteen"}),
            ("restored database used before creation", lambda r: r["commands"][1].__setitem__("database", r["databases"]["restored"]),
             {"fresh-restored-database"}),
        )
        for name, mutate, failed in cases:
            with self.subTest(name):
                receipt = restore_receipt()
                mutate(receipt)
                with self.assertRaisesRegex(q.InvalidPacket, "claimed observations|claimed outcome"):
                    runtime.restore_outcome(receipt)
                mark_failed(receipt, failed)
                outcome = runtime.restore_outcome(receipt)
                self.assertEqual((outcome["execution"], outcome["compatibility"]), ("failed", "unverified"))
                operational = copy.deepcopy(receipt)
                operational["scope"] = operational["cleanup"]["scope"] = "operational"
                operational["fixture"]["synthetic"] = operational["cleanup"]["fixture"]["synthetic"] = False
                if operational["cleanup"]["execution"] == "passed":
                    operational["cleanup"]["compatibility"] = "compatible"
                else:
                    operational["cleanup"]["compatibility"] = "incompatible"
                operational["compatibility"] = "incompatible"
                self.assertEqual(runtime.restore_outcome(operational)["compatibility"], "incompatible")

    def testRestoreBindingTamperingRefused(self):
        def backup_hash(receipt):
            receipt["backup"]["sha256"] = "1" * 64

        def restore_input(receipt):
            receipt["commands"][5]["input_sha256"] = "2" * 64

        def inventory_hash(receipt):
            receipt["inventories"]["appended"]["rows"][0]["sha256"] = "3" * 64

        def wrong_database(receipt):
            receipt["inventories"]["restored"]["command"] = 3

        def missing_restore(receipt):
            receipt["commands"][5]["step"] = "seed"

        def unknown_step(receipt):
            receipt["commands"][1]["step"] = "guess"

        def duplicate_key(receipt):
            rows = receipt["inventories"]["source"]["rows"]
            rows.append(copy.deepcopy(rows[0]))

        def marker_scope(receipt):
            receipt["fixture"]["synthetic"] = False

        def foreign_cleanup(receipt):
            receipt["cleanup"]["owned"][0]["label"] = "hexalith.p1r.invocation=" + "0" * 32

        for mutate in (backup_hash, restore_input, inventory_hash, wrong_database, missing_restore, unknown_step,
                       duplicate_key, marker_scope, foreign_cleanup):
            with self.subTest(mutate.__name__):
                receipt = restore_receipt()
                mutate(receipt)
                with self.assertRaises(q.InvalidPacket):
                    runtime.restore_outcome(receipt)

        def cleanup_scope(receipt):
            receipt["cleanup"].update(scope="operational", compatibility="compatible")
            receipt["cleanup"]["fixture"]["synthetic"] = False

        def restored_before_restore(receipt):
            receipt["commands"][4].update(step="inventory", output_sha256=receipt["inventories"]["restored"]["sha256"])
            receipt["inventories"]["restored"]["command"] = 5

        messages = (
            ("embedded cleanup scope", cleanup_scope, "embedded cleanup differs"),
            ("embedded cleanup inputs", lambda r: r["cleanup"].__setitem__("inputs_sha256", "6" * 64), "embedded cleanup differs"),
            ("embedded cleanup profile", lambda r: r["cleanup"].__setitem__("profile", copy.deepcopy(PROFILE)), "embedded cleanup differs"),
            ("restore input bytes", lambda r: r["commands"][5].__setitem__("input_bytes", 4095), "restore input differs"),
            ("restored inventory before restore", restored_before_restore, "restored inventory precedes its restore"),
        )
        for name, mutate, message in messages:
            with self.subTest(name):
                receipt = restore_receipt()
                mutate(receipt)
                with self.assertRaisesRegex(q.InvalidPacket, message):
                    runtime.restore_outcome(receipt)

    def testCleanupFailuresRemainNonpassing(self):
        def remaining(receipt):
            receipt["attempts"][-1]["remaining"] = ["synthetic-container"]

        def errors(receipt):
            receipt["attempts"][0]["errors"] = ["PermissionError"]

        def single(receipt):
            receipt["attempts"].pop()

        def drift(receipt):
            receipt["shared"]["after"]["synthetic-shared"]["running"] = False

        def incomplete(receipt):
            receipt["shared"]["complete"] = False

        def partial_first(receipt):
            receipt["attempts"][0]["targeted"] = ["synthetic-container"]

        def abandoned(receipt):
            receipt["attempts"][0]["remaining"] = ["synthetic-database"]
            receipt["attempts"][1]["remaining"] = ["synthetic-database"]

        cases = ((remaining, {"no-owned-remaining"}), (errors, {"no-cleanup-errors"}), (single, {"repeated-cleanup"}),
                 (drift, {"shared-preserved"}), (incomplete, {"shared-discovery-complete"}),
                 (partial_first, {"first-attempt-targets-every-owned"}),
                 (abandoned, {"later-attempts-continue", "no-owned-remaining"}))
        for mutate, failed in cases:
            with self.subTest(mutate.__name__):
                receipt = cleanup_receipt()
                mutate(receipt)
                with self.assertRaisesRegex(q.InvalidPacket, "claimed observations"):
                    runtime.cleanup_outcome(receipt)
                mark_failed(receipt, failed)
                self.assertEqual(runtime.cleanup_outcome(receipt)["execution"], "failed")
        receipt = cleanup_receipt()
        receipt["shared"]["before"]["synthetic-container"] = {"image": "x", "running": True, "started": "t"}
        with self.assertRaisesRegex(q.InvalidPacket, "shared-resource"):
            runtime.cleanup_outcome(receipt)

    def testBackendExecutionRequiresSelectedProfile(self):
        with self.assertRaisesRegex(q.InvalidPacket, "operational profile not selected"):
            runtime.require_operational_profile(None)
        with self.assertRaisesRegex(q.InvalidPacket, "operational profile not selected"):
            runtime.require_operational_profile(synthetic_inputs(self.selection, profile=False))
        self.assertEqual(runtime.require_operational_profile(self.inputs_value), PROFILE)

    def testMocksCannotQualifyOperationalOrPublishedLanes(self):
        result = q.validate_packet(self.full)
        self.assertFalse(result["technically_qualified"])
        self.assertFalse(result["decisions_complete"])
        self.assertFalse(result["capable_rollback_qualified"])
        self.assertEqual(result["recovery"], q.FREEZE)
        self.assertIn("synthetic tooling inputs select no published tuple", result["refusals"])
        self.assertIn("synthetic tooling decisions grant no acceptance", result["refusals"])
        packet = q.parse_json((self.full / "packet.json").read_bytes())
        executed = [f"{group}/{row['id']}" for group in q.INVENTORIES for row in packet["lanes"][group] if row["receipts"]]
        self.assertTrue(set(executed) <= set(result["unsatisfied"]))
        self.assertEqual(len(executed), 8)


class EvaluationTests(unittest.TestCase):
    """Pure evaluation over in-memory rows; packet validation separately refuses rows without bound receipts."""

    def setUp(self):
        scratch = tempfile.TemporaryDirectory(prefix="p1r-evaluation-")
        self.addCleanup(scratch.cleanup)
        _, self.selection = synthetic_evidence(Path(scratch.name))

    def owner_selected(self, selection, **options):
        value = synthetic_inputs(selection, rollback=copy.deepcopy(selection) | {"version": "0.0.0", "tag": "v0.0.0"}, **options)
        value["fixture"] = None
        return {"scope": q.validate_inputs(value), "sha256": "6" * 64, "value": value}

    def satisfied_lanes(self, selection):
        lanes = q.initial_lanes(selection)
        for group, rows in lanes.items():
            for row in rows:
                row.update(execution="passed", compatibility="compatible", scope=q.ACCEPTED_SCOPES[group][0],
                           assertions={"attempted": 1, "passed": 1, "failed": 0}, receipts=["receipts/x.json"], reason=None)
        return lanes

    def decisions(self, **options):
        value = synthetic_decisions("6" * 64, **options)
        value["fixture"] = None
        return {"scope": q.validate_decisions(value), "value": value}

    def testIncompleteDecisionsKeepUsabilityFalse(self):
        selection = self.selection
        inputs = self.owner_selected(selection)
        lanes = self.satisfied_lanes(inputs["value"])
        for name, decisions in (("missing", None), ("pending", self.decisions(decision="pending")),
                                ("rejected", self.decisions(decision="rejected")),
                                ("nonconformant", self.decisions(conformance="nonconformant")),
                                ("conformance pending", self.decisions(conformance="pending"))):
            with self.subTest(name):
                evaluation = q.evaluate(lanes, inputs, decisions)
                self.assertTrue(evaluation["technically_qualified"])
                self.assertFalse(evaluation["decisions_complete"])
                self.assertFalse(evaluation["qualified"])
                self.assertFalse(evaluation["p1r_usable"])
                self.assertEqual(evaluation["recovery"], q.FREEZE)
        other = self.decisions()
        other["value"]["inputs_sha256"] = "7" * 64
        self.assertFalse(q.evaluate(lanes, inputs, other)["decisions_complete"])
        complete = q.evaluate(lanes, inputs, self.decisions())
        self.assertTrue(complete["qualified"])
        self.assertTrue(complete["capable_rollback_qualified"])
        # Even complete decisions leave usability and the recovery envelope to their own coordinated transition.
        self.assertFalse(complete["p1r_usable"])
        self.assertEqual(complete["recovery"], q.FREEZE)

    def testEveryRequiredLaneAndInstrumentationIsNeeded(self):
        selection = self.selection
        inputs = self.owner_selected(selection)
        for group, name in q.required_lanes(inputs["value"]):
            for change in ({"execution": "failed"}, {"compatibility": "incompatible"}, {"compatibility": "unverified"},
                           {"scope": "tooling-synthetic"}, {"scope": "local-process-control"}, {"assertions": None},
                           {"assertions": {"attempted": 0, "passed": 0, "failed": 0}}, {"receipts": []}):
                lanes = self.satisfied_lanes(inputs["value"])
                next(row for row in lanes[group] if row["id"] == name).update(change)
                evaluation = q.evaluate(lanes, inputs, None)
                self.assertFalse(evaluation["technically_qualified"], (group, name, change))
                self.assertIn(f"{group}/{name}", evaluation["unsatisfied"])
        uninstrumented = self.owner_selected(selection, instrumentation=False)
        self.assertFalse(q.evaluate(self.satisfied_lanes(uninstrumented["value"]), uninstrumented, None)["technically_qualified"])
        synthetic = dict(inputs, scope="tooling-synthetic")
        self.assertFalse(q.evaluate(self.satisfied_lanes(inputs["value"]), synthetic, None)["technically_qualified"])

    def testUnselectedAdditionsAndRollbackAreNotRequired(self):
        selection = self.selection
        value = synthetic_inputs(selection, additions=("logical-event-evolution",))
        value["fixture"] = None
        inputs = {"scope": q.validate_inputs(value), "sha256": "6" * 64, "value": value}
        required = q.required_lanes(value)
        self.assertNotIn(("additions", "reminder-recovery"), required)
        self.assertNotIn(("packages", "rollback-packages"), required)
        self.assertNotIn(("recovery", "rollback-restore"), required)
        lanes = self.satisfied_lanes(value)
        evaluation = q.evaluate(lanes, inputs, None)
        self.assertTrue(evaluation["technically_qualified"])
        self.assertFalse(evaluation["capable_rollback_qualified"])


class ProcessControlTests(PacketFixture):
    def testRealProcessControlsProveOnlyTheirLocalScope(self):
        out = self.root / "process-controls"
        packet = q.create_packet(out, process_controls=True)
        self.assertEqual(packet["errors"], [])
        result = q.validate_packet(out)
        self.assertEqual(result["process_scope"], "local-process-control")
        self.assertEqual(result["process_assertions"], 38)
        self.assertFalse(result["technically_qualified"])
        self.assertIn("scenarios/failure-cleanup", result["unsatisfied"])
        self.assertIn("recovery/container-cleanup", result["unsatisfied"])
        rows = packet["process_controls"]["rows"]
        self.assertEqual([row["id"] for row in rows], list(preparation.CONTROLS))
        for row in rows:
            receipt = preparation.read_json(out / row["receipt"])
            self.assertEqual((row["execution"], row["compatibility"]), ("passed", "unverified"))
            self.assertTrue(all(cleanup["remaining"] == [] for cleanup in receipt["cleanup"]))
            self.assertTrue(all(preparation.process_state(owned["pid"]) is None
                                or preparation.process_state(owned["pid"])["start_ticks"] != owned["start_ticks"]
                                for owned in receipt["owned_processes"]))
        self.assertTrue(packet["process_controls"]["sentinel"]["released"])
        with self.changed(out, "packet.json", lambda v: v["process_controls"].__setitem__("scope", "operational")):
            with self.assertRaisesRegex(q.InvalidPacket, "wider scope"):
                q.validate_packet(out)

    def testCleanupFailureIsRetainedAndEveryOwnedIdentityStillReleased(self):
        calls = []
        real = preparation.signal_owned

        def fail_once(value, sig):
            calls.append(value["pid"])
            if len(calls) == 1:
                raise PermissionError("synthetic denial of one cleanup attempt")
            return real(value, sig)

        out = self.root / "process-cleanup-failure"
        with mock.patch.object(preparation, "signal_owned", side_effect=fail_once):
            packet = q.create_packet(out, process_controls=True)
        failed = [row for row in packet["process_controls"]["rows"] if row["execution"] == "failed"]
        self.assertEqual(len(failed), 1)
        receipt = preparation.read_json(out / failed[0]["receipt"])
        self.assertEqual(receipt["cleanup"][0]["errors"], ["PermissionError"])
        self.assertEqual(receipt["cleanup"][1]["remaining"], [])
        for row in packet["process_controls"]["rows"]:
            for owned in preparation.read_json(out / row["receipt"])["owned_processes"]:
                current = preparation.process_state(owned["pid"])
                self.assertTrue(current is None or current["start_ticks"] != owned["start_ticks"])
        with self.assertRaisesRegex(q.InvalidPacket, "process control failed or incomplete"):
            q.validate_packet(out)

    def testInterruptedPackageObservationRetainsSealedRefusal(self):
        def interrupted(archive, output, cwd):
            raise KeyboardInterrupt

        out = self.root / "interrupted-package-lane"
        packet = q.create_packet(out, inputs=self.inputs, candidate_evidence=self.evidence, verifier=interrupted)
        self.assertEqual(packet["errors"], ["KeyboardInterrupt"])
        self.assertEqual(packet["lanes"]["packages"][0]["execution"], "unavailable")
        self.assertTrue((out / "SHA256SUMS").is_file())
        self.assertFalse(packet["evaluation"]["technically_qualified"])
        with self.assertRaisesRegex(q.InvalidPacket, "refused or incomplete"):
            q.validate_packet(out)

    def testExternalSigintRetainsReceiptsAndCleansActualDescendants(self):
        out = self.root / "cli-sigint"
        process = subprocess.Popen([sys.executable, "-c", DRIVER, str(TOOLS), json.dumps(SYNTHETIC_BINDING), "prepare",
                                    "--out", str(out), "--process-controls"], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        try:
            deadline = time.monotonic() + 30
            interrupted = False
            while process.poll() is None and time.monotonic() < deadline and not interrupted:
                for path in (out / "receipts").glob("*.json") if (out / "receipts").is_dir() else ():
                    try:
                        value = preparation.read_json(path)
                    except preparation.InvalidPacket:
                        continue
                    if value.get("control") == "descendants" and value["finished_utc"] is None:
                        time.sleep(.1)
                        process.send_signal(signal.SIGINT)
                        interrupted = True
                        break
                time.sleep(.02)
            stdout, stderr = process.communicate(timeout=30)
            self.assertTrue(interrupted)
            self.assertEqual(process.returncode, 2, (stdout, stderr))
            self.assertFalse(json.loads(stderr)["qualified"])
            packet = q.parse_json((out / "packet.json").read_bytes())
            row = next(r for r in packet["process_controls"]["rows"] if r["id"] == "descendants")
            receipt = preparation.read_json(out / row["receipt"])
            self.assertTrue(receipt["cancelled"])
            self.assertEqual(receipt["execution"], "failed")
            self.assertGreaterEqual(len(receipt["owned_processes"]), 2)
            self.assertTrue(all(preparation.process_state(owned["pid"]) is None for owned in receipt["owned_processes"]))
            self.assertTrue(all(cleanup["remaining"] == [] for cleanup in receipt["cleanup"]))
            self.assertTrue(packet["process_controls"]["sentinel"]["released"])
            with self.assertRaisesRegex(q.InvalidPacket, "failed or incomplete"):
                q.validate_packet(out)
        finally:
            if process.poll() is None:
                process.kill()
                process.wait()


class IntegrityAndCliTests(PacketFixture):
    def testUnresealedTamperingAndExtraOrMissingFilesRefused(self):
        with self.changed(self.full, "inputs.json", lambda v: v.__setitem__("fixture", None), reseal=False):
            with self.assertRaisesRegex(q.InvalidPacket, "hash mismatch"):
                q.validate_packet(self.full)
        with self.changed(self.full, "inputs.json", lambda v: v.__setitem__("fixture", None)):
            with self.assertRaisesRegex(q.InvalidPacket, "owner document hash mismatch"):
                q.validate_packet(self.full)
        extra = self.full / "receipts" / "extra.json"
        try:
            extra.write_text("{}")
            with self.assertRaisesRegex(q.InvalidPacket, "unexpected"):
                q.validate_packet(self.full)
            preparation.seal(self.full)
            with self.assertRaisesRegex(q.InvalidPacket, "unbound or extra"):
                q.validate_packet(self.full)
        finally:
            extra.unlink()
            preparation.seal(self.full)
        name = self.receipt_name("recovery", "container-cleanup")
        original = (self.full / name).read_bytes()
        try:
            (self.full / name).unlink()
            with self.assertRaisesRegex(q.InvalidPacket, "omitted"):
                q.validate_packet(self.full)
        finally:
            (self.full / name).write_bytes(original)
        self.assertTrue(q.validate_packet(self.full)["valid"])

    def testValidationIsReadOnly(self):
        before = {p: p.read_bytes() for p in self.full.rglob("*") if p.is_file()}
        q.validate_packet(self.full)
        self.assertEqual(before, {p: p.read_bytes() for p in self.full.rglob("*") if p.is_file()})

    def testSourceDriftRefusesValidation(self):
        drifted = copy.deepcopy(SYNTHETIC_BINDING)
        drifted["python"]["sha256"] = "1" * 64
        with mock.patch.object(preparation, "source_binding", return_value=drifted):
            with self.assertRaisesRegex(q.InvalidPacket, "inputs changed"):
                q.validate_packet(self.full)

    def testCliRefusesDependentExecutionAndReusedOutputs(self):
        out = self.root / "cli-refused"
        result = run_cli("prepare", "--out", out, "--candidate-evidence", self.evidence)
        self.assertEqual(result.returncode, 2, result)
        self.assertNotIn(b"Traceback", result.stderr)
        refusal = json.loads(result.stderr)
        self.assertEqual(refusal["reason"], "refused or incomplete invocation")
        self.assertFalse(refusal["qualified"])
        self.assertEqual(refusal["errors"], ["dependent execution refused: " + q.R_INPUTS])
        self.assertIn(q.R_INPUTS, refusal["refusals"])
        self.assertEqual(q.parse_json((out / "packet.json").read_bytes())["errors"], refusal["errors"])
        before = (out / "packet.json").read_bytes()
        again = run_cli("prepare", "--out", out)
        self.assertEqual(again.returncode, 2)
        self.assertEqual(json.loads(again.stderr)["reason"], "output path already exists")
        self.assertEqual((out / "packet.json").read_bytes(), before)
        malformed = restore_receipt()
        malformed["backup"]["command"] = [4]
        write(self.root / "synthetic-cli-malformed.json", malformed)
        rejected = run_cli("prepare", "--out", self.root / "cli-malformed", "--inputs", self.inputs,
                           "--receipt", self.root / "synthetic-cli-malformed.json")
        self.assertEqual(rejected.returncode, 2, rejected)
        self.assertNotIn(b"Traceback", rejected.stderr)
        self.assertEqual(json.loads(rejected.stderr)["errors"], ["TypeError"])
        nested = self.root / "cli-nested"
        nested.mkdir()
        (nested / "packet.json").write_text("[" * 200000 + "]" * 200000)
        preparation.seal(nested)
        deep = run_cli("validate", nested)
        self.assertEqual(deep.returncode, 2, deep)
        self.assertNotIn(b"Traceback", deep.stderr)
        self.assertEqual(json.loads(deep.stderr)["reason"], "RecursionError")

    def testCliPrepareAndValidateNeverReportQualification(self):
        out = self.root / "cli-prepare"
        # Above two actual verifier calls at their 180 s bound each.
        result = run_cli("prepare", "--out", out, "--inputs", self.inputs, "--candidate-evidence", self.evidence, timeout=420)
        # The CLI uses the actual NuGet verifier: synthetic signatures are retained as failed checks.
        self.assertEqual(result.returncode, 0, result)
        prepared = json.loads(result.stdout)
        self.assertFalse(prepared["qualified"])
        self.assertFalse(prepared["p1r_usable"])
        validated = run_cli("validate", out)
        self.assertEqual(validated.returncode, 0, validated)
        summary = json.loads(validated.stdout)
        self.assertTrue(summary["valid"])
        self.assertFalse(summary["technically_qualified"])
        self.assertFalse(summary["p1r_usable"])
        receipt = q.parse_json((out / q.parse_json((out / "packet.json").read_bytes())["lanes"]["packages"][0]["receipts"][0]).read_bytes())
        signature = [c for c in receipt["checks"] if c["id"].endswith(":repository-signature-verified")]
        self.assertTrue(signature and not any(c["passed"] for c in signature))


if __name__ == "__main__":
    unittest.main()
